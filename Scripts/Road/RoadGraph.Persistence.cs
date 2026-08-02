using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

public partial class RoadGraph
{
    private const int RoadGraphSchemaVersion = 1;

    private static readonly JsonSerializerOptions RoadGraphJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    public object CaptureState()
    {
        return new RoadGraphSaveData
        {
            SchemaVersion = RoadGraphSchemaVersion,
            NextID = _nextID,
            Nodes = _nodes.Values
                .OrderBy(node => node.ID)
                .Select(node => new NodeSaveData
                {
                    ID = node.ID,
                    X = node.Position.X,
                    Y = node.Position.Y,
                })
                .Cast<NodeSaveData?>()
                .ToList(),
            Edges = _edges.Values
                .OrderBy(edge => edge.ID)
                .Select(edge => new EdgeSaveData
                {
                    ID = edge.ID,
                    NodeAID = edge.NodeA,
                    NodeBID = edge.NodeB,
                    GroupID = edge.GroupID,
                    Geometry = edge.GeometrySegments
                        .Select(RoadGeometrySerializer.ToData)
                        .Cast<RoadGeometryData?>()
                        .ToList(),
                })
                .Cast<EdgeSaveData?>()
                .ToList(),
            Groups = _groups.Values
                .OrderBy(group => group.ID)
                .Select(group => new GroupSaveData
                {
                    ID = group.ID,
                    EdgeIDs = group.EdgeIDs.Order().Select(id => (int?)id).ToList(),
                })
                .Cast<GroupSaveData?>()
                .ToList(),
        };
    }

    public void RestoreState(string json)
    {
        RestoredGraphState restored = ParseAndValidateState(json);

        ClearGraph();
        foreach ((int id, GraphNode node) in restored.Nodes)
            _nodes.Add(id, node);
        foreach ((int id, GraphEdge edge) in restored.Edges)
            _edges.Add(id, edge);
        foreach ((int id, RoadGroup group) in restored.Groups)
            _groups.Add(id, group);
        _nextID = restored.NextID;

        RebuildNodeEdges();
        RebuildSpatialIndex();
        GraphCleared?.Invoke();
    }

    private static RestoredGraphState ParseAndValidateState(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new JsonException("RoadGraph save payload is empty.");

        RoadGraphSaveData data = JsonSerializer.Deserialize<RoadGraphSaveData>(json, RoadGraphJsonOptions)
            ?? throw new JsonException("RoadGraph save payload must be a JSON object.");

        if (HasExtraFields(data.ExtraFields))
            throw new JsonException("RoadGraph save payload contains unknown root fields.");
        if (data.SchemaVersion != RoadGraphSchemaVersion)
            throw new JsonException($"Unsupported RoadGraph schemaVersion '{data.SchemaVersion?.ToString() ?? "missing"}'.");
        if (data.NextID is null || data.NextID < 0)
            throw new JsonException("RoadGraph nextID must be a non-negative integer.");
        if (data.Nodes is null || data.Edges is null || data.Groups is null)
            throw new JsonException("RoadGraph nodes, edges and groups arrays are required.");

        var allIDs = new HashSet<int>();
        var nodes = new Dictionary<int, GraphNode>();
        foreach (NodeSaveData? nodeData in data.Nodes)
        {
            if (nodeData is null || HasExtraFields(nodeData.ExtraFields))
                throw new JsonException("RoadGraph nodes cannot be null or contain unknown fields.");
            int id = ReadEntityID(nodeData.ID, "Node", allIDs);
            if (nodeData.X is null || nodeData.Y is null ||
                !float.IsFinite(nodeData.X.Value) || !float.IsFinite(nodeData.Y.Value))
                throw new JsonException($"Node {id} must have finite x and y coordinates.");
            nodes.Add(id, new GraphNode(id, new Vector2(nodeData.X.Value, nodeData.Y.Value)));
        }

        var groups = new Dictionary<int, RoadGroup>();
        var savedGroupEdges = new Dictionary<int, HashSet<int>>();
        foreach (GroupSaveData? groupData in data.Groups)
        {
            if (groupData is null || HasExtraFields(groupData.ExtraFields))
                throw new JsonException("RoadGraph groups cannot be null or contain unknown fields.");
            int id = ReadEntityID(groupData.ID, "Group", allIDs);
            if (groupData.EdgeIDs is null || groupData.EdgeIDs.Count == 0)
                throw new JsonException($"Group {id} must contain at least one edge ID.");

            var edgeIDs = new HashSet<int>();
            foreach (int? edgeIDValue in groupData.EdgeIDs)
            {
                if (edgeIDValue is null || edgeIDValue < 0 || !edgeIDs.Add(edgeIDValue.Value))
                    throw new JsonException($"Group {id} contains an invalid or duplicate edge ID.");
            }

            groups.Add(id, new RoadGroup(id));
            savedGroupEdges.Add(id, edgeIDs);
        }

        var edges = new Dictionary<int, GraphEdge>();
        var actualGroupEdges = groups.Keys.ToDictionary(id => id, _ => new HashSet<int>());
        var referencedNodeIDs = new HashSet<int>();
        foreach (EdgeSaveData? edgeData in data.Edges)
        {
            if (edgeData is null || HasExtraFields(edgeData.ExtraFields))
                throw new JsonException("RoadGraph edges cannot be null or contain unknown fields.");
            int id = ReadEntityID(edgeData.ID, "Edge", allIDs);
            int nodeAID = ReadReferenceID(edgeData.NodeAID, $"Edge {id} nodeAID");
            int nodeBID = ReadReferenceID(edgeData.NodeBID, $"Edge {id} nodeBID");
            int groupID = ReadReferenceID(edgeData.GroupID, $"Edge {id} groupID");
            if (nodeAID == nodeBID)
                throw new JsonException($"Edge {id} cannot reference the same endpoint twice.");
            if (!nodes.TryGetValue(nodeAID, out GraphNode? nodeA) ||
                !nodes.TryGetValue(nodeBID, out GraphNode? nodeB))
                throw new JsonException($"Edge {id} references a missing endpoint node.");
            if (!groups.ContainsKey(groupID))
                throw new JsonException($"Edge {id} references missing Group {groupID}.");
            if (edgeData.Geometry is null || edgeData.Geometry.Count == 0)
                throw new JsonException($"Edge {id} must contain at least one geometry segment.");

            var geometry = new RoadGeometrySegment[edgeData.Geometry.Count];
            for (int index = 0; index < geometry.Length; index++)
            {
                RoadGeometryDeserializationResult result =
                    RoadGeometrySerializer.FromData(edgeData.Geometry[index]);
                if (!result.Success)
                    throw new JsonException($"Edge {id} geometry segment {index} is invalid: {result.Error}.");
                geometry[index] = result.Geometry!;
                if (index > 0 && geometry[index - 1].End != geometry[index].Start)
                    throw new JsonException($"Edge {id} geometry segments are not continuous.");
            }

            if (!ArePositionsApproximatelyEqual(geometry[0].Start, nodeA.Position) ||
                !ArePositionsApproximatelyEqual(geometry[^1].End, nodeB.Position))
                throw new JsonException($"Edge {id} geometry endpoints do not match its nodes.");

            GraphEdge edge;
            try
            {
                edge = new GraphEdge(id, nodeAID, nodeBID, geometry, groupID);
            }
            catch (ArgumentException exception)
            {
                throw new JsonException($"Edge {id} geometry is invalid.", exception);
            }

            edges.Add(id, edge);
            actualGroupEdges[groupID].Add(id);
            referencedNodeIDs.Add(nodeAID);
            referencedNodeIDs.Add(nodeBID);
        }

        foreach ((int groupID, HashSet<int> expectedEdgeIDs) in savedGroupEdges)
        {
            if (!expectedEdgeIDs.SetEquals(actualGroupEdges[groupID]))
                throw new JsonException($"Group {groupID} edge membership does not match Edge groupID values.");
            foreach (int edgeID in expectedEdgeIDs)
                groups[groupID].AddEdge(edgeID);
        }
        if (referencedNodeIDs.Count != nodes.Count)
            throw new JsonException("RoadGraph save payload contains isolated nodes.");

        int maxID = allIDs.Count == 0 ? -1 : allIDs.Max();
        if (data.NextID.Value <= maxID)
            throw new JsonException($"RoadGraph nextID must be greater than every entity ID ({maxID}).");

        return new RestoredGraphState(data.NextID.Value, nodes, edges, groups);
    }

    private static int ReadEntityID(int? value, string entityName, HashSet<int> allIDs)
    {
        int id = ReadReferenceID(value, $"{entityName} ID");
        if (!allIDs.Add(id))
            throw new JsonException($"{entityName} ID {id} conflicts with another entity ID.");
        return id;
    }

    private static int ReadReferenceID(int? value, string fieldName)
    {
        if (value is null || value < 0)
            throw new JsonException($"{fieldName} must be a non-negative integer.");
        return value.Value;
    }

    private static bool HasExtraFields(Dictionary<string, JsonElement>? fields) => fields?.Count > 0;

    private sealed record RestoredGraphState(
        int NextID,
        Dictionary<int, GraphNode> Nodes,
        Dictionary<int, GraphEdge> Edges,
        Dictionary<int, RoadGroup> Groups);

    private sealed class RoadGraphSaveData
    {
        [JsonPropertyName("schemaVersion")]
        public int? SchemaVersion { get; set; }

        [JsonPropertyName("nextID")]
        public int? NextID { get; set; }

        [JsonPropertyName("nodes")]
        public List<NodeSaveData?>? Nodes { get; set; }

        [JsonPropertyName("edges")]
        public List<EdgeSaveData?>? Edges { get; set; }

        [JsonPropertyName("groups")]
        public List<GroupSaveData?>? Groups { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtraFields { get; set; }
    }

    private sealed class NodeSaveData
    {
        [JsonPropertyName("id")]
        public int? ID { get; set; }

        [JsonPropertyName("x")]
        public float? X { get; set; }

        [JsonPropertyName("y")]
        public float? Y { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtraFields { get; set; }
    }

    private sealed class EdgeSaveData
    {
        [JsonPropertyName("id")]
        public int? ID { get; set; }

        [JsonPropertyName("nodeAID")]
        public int? NodeAID { get; set; }

        [JsonPropertyName("nodeBID")]
        public int? NodeBID { get; set; }

        [JsonPropertyName("groupID")]
        public int? GroupID { get; set; }

        [JsonPropertyName("geometry")]
        public List<RoadGeometryData?>? Geometry { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtraFields { get; set; }
    }

    private sealed class GroupSaveData
    {
        [JsonPropertyName("id")]
        public int? ID { get; set; }

        [JsonPropertyName("edgeIDs")]
        public List<int?>? EdgeIDs { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtraFields { get; set; }
    }
}
