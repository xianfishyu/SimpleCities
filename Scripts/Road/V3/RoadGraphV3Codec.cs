using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleCities.Road.V3;

public sealed class RoadGraphV3Data
{
    [JsonPropertyName("formatFamily")]
    public string? FormatFamily { get; set; }

    [JsonPropertyName("payloadType")]
    public string? PayloadType { get; set; }

    [JsonPropertyName("schemaVersion")]
    public int? SchemaVersion { get; set; }

    [JsonPropertyName("nextID")]
    public int? NextID { get; set; }

    [JsonPropertyName("nodes")]
    public List<RoadGraphV3NodeData>? Nodes { get; set; }

    [JsonPropertyName("edges")]
    public List<RoadGraphV3EdgeData>? Edges { get; set; }
}

public sealed class RoadGraphV3NodeData
{
    [JsonPropertyName("id")]
    public int? ID { get; set; }

    [JsonPropertyName("x")]
    public float? X { get; set; }

    [JsonPropertyName("y")]
    public float? Y { get; set; }
}

public sealed class RoadGraphV3EdgeData
{
    [JsonPropertyName("id")]
    public int? ID { get; set; }

    [JsonPropertyName("nodeAID")]
    public int? NodeAID { get; set; }

    [JsonPropertyName("nodeBID")]
    public int? NodeBID { get; set; }

    [JsonPropertyName("roadType")]
    public string? RoadType { get; set; }

    [JsonPropertyName("geometry")]
    public List<RoadGeometryData>? Geometry { get; set; }
}

public sealed record RoadGraphV3CodecResult(
    bool Success,
    RoadCanonicalGraph? Graph,
    int? NextID,
    string? Error)
{
    public static RoadGraphV3CodecResult Failure(string error) =>
        new(false, null, null, error);
}

/// <summary>
/// V3 format v1 的最小 canonical graph codec。
/// 序列化 `RoadCanonicalGraph` 为 family/version 精确的 JSON；反序列化执行基础结构校验。
/// 当前不实现严格 token/lexeme reader，后续由 save-system 负责有界 I/O 与同句柄校验。
/// </summary>
public static class RoadGraphV3Codec
{
    public const string FormatFamily = "simple-cities-v3";
    public const string PayloadType = "road-network";
    public const int SchemaVersion = 1;

    public static string Serialize(RoadCanonicalGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        int maxID = -1;
        foreach (RoadCanonicalNode node in graph.Nodes)
            maxID = Math.Max(maxID, node.ID);
        foreach (RoadCanonicalEdge edge in graph.Edges)
            maxID = Math.Max(maxID, edge.ID);

        var data = new RoadGraphV3Data
        {
            FormatFamily = FormatFamily,
            PayloadType = PayloadType,
            SchemaVersion = SchemaVersion,
            NextID = maxID + 1,
            Nodes = graph.Nodes
                .OrderBy(node => node.ID)
                .Select(node => new RoadGraphV3NodeData
                {
                    ID = node.ID,
                    X = node.Position.X,
                    Y = node.Position.Y,
                })
                .ToList(),
            Edges = graph.Edges
                .OrderBy(edge => edge.ID)
                .Select(edge => new RoadGraphV3EdgeData
                {
                    ID = edge.ID,
                    NodeAID = edge.NodeAID,
                    NodeBID = edge.NodeBID,
                    RoadType = edge.MergeKey,
                    Geometry = edge.Geometry
                        .Select(RoadGeometrySerializer.ToData)
                        .ToList(),
                })
                .ToList(),
        };

        return JsonSerializer.Serialize(data);
    }

    public static RoadGraphV3CodecResult Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return RoadGraphV3CodecResult.Failure("EmptyPayload");

        try
        {
            RoadGraphV3Data? data = JsonSerializer.Deserialize<RoadGraphV3Data>(json);
            return FromData(data);
        }
        catch (JsonException)
        {
            return RoadGraphV3CodecResult.Failure("MalformedJson");
        }
    }

    private static RoadGraphV3CodecResult FromData(RoadGraphV3Data? data)
    {
        if (data is null)
            return RoadGraphV3CodecResult.Failure("MalformedJson");
        if (!string.Equals(data.FormatFamily, FormatFamily, StringComparison.Ordinal))
            return RoadGraphV3CodecResult.Failure("InvalidFormatFamily");
        if (!string.Equals(data.PayloadType, PayloadType, StringComparison.Ordinal))
            return RoadGraphV3CodecResult.Failure("InvalidPayloadType");
        if (data.SchemaVersion != SchemaVersion)
            return RoadGraphV3CodecResult.Failure("UnsupportedSchemaVersion");
        if (data.NextID is not int nextID || nextID < 0)
            return RoadGraphV3CodecResult.Failure("InvalidNextID");
        if (data.Nodes is null || data.Edges is null)
            return RoadGraphV3CodecResult.Failure("MissingNodesOrEdges");

        var nodes = new List<RoadCanonicalNode>();
        var seenNodeIDs = new HashSet<int>();
        int maxID = -1;
        foreach (RoadGraphV3NodeData nodeData in data.Nodes)
        {
            if (nodeData.ID is not int id || nodeData.X is not float x || nodeData.Y is not float y)
                return RoadGraphV3CodecResult.Failure("InvalidNode");
            if (!seenNodeIDs.Add(id))
                return RoadGraphV3CodecResult.Failure("DuplicateNodeID");
            if (!RoadNumericPolicy.IsWithinCoordinateRange(x, y))
                return RoadGraphV3CodecResult.Failure("NodeOutOfRange");

            maxID = Math.Max(maxID, id);
            nodes.Add(new RoadCanonicalNode(id, RoadNumericPolicy.NormalizeVector(new Vector2(x, y))));
        }

        var edges = new List<RoadCanonicalEdge>();
        var seenEdgeIDs = new HashSet<int>();
        foreach (RoadGraphV3EdgeData edgeData in data.Edges)
        {
            if (edgeData.ID is not int id || edgeData.NodeAID is not int nodeA || edgeData.NodeBID is not int nodeB)
                return RoadGraphV3CodecResult.Failure("InvalidEdge");
            if (!seenEdgeIDs.Add(id))
                return RoadGraphV3CodecResult.Failure("DuplicateEdgeID");
            if (!seenNodeIDs.Contains(nodeA) || !seenNodeIDs.Contains(nodeB))
                return RoadGraphV3CodecResult.Failure("MissingEndpoint");
            if (string.IsNullOrWhiteSpace(edgeData.RoadType) ||
                !RoadTypeNames.TryParseWireName(edgeData.RoadType, out RoadType roadType))
            {
                return RoadGraphV3CodecResult.Failure("InvalidRoadType");
            }

            if (edgeData.Geometry is null || edgeData.Geometry.Count == 0)
                return RoadGraphV3CodecResult.Failure("EmptyGeometry");

            var geometry = new List<RoadGeometrySegment>(edgeData.Geometry.Count);
            foreach (RoadGeometryData geometryData in edgeData.Geometry)
            {
                RoadGeometryDeserializationResult geometryResult = RoadGeometrySerializer.FromData(geometryData);
                if (!geometryResult.Success || geometryResult.Geometry is null)
                    return RoadGraphV3CodecResult.Failure("InvalidGeometry");
                geometry.Add(geometryResult.Geometry);
            }

            maxID = Math.Max(maxID, id);
            edges.Add(new RoadCanonicalEdge(
                id,
                nodeA,
                nodeB,
                geometry,
                RoadTypeNames.ToWireName(roadType)));
        }

        if (nextID <= maxID)
            return RoadGraphV3CodecResult.Failure("NextIDNotAboveMax");

        return new RoadGraphV3CodecResult(
            true,
            new RoadCanonicalGraph(nodes, edges),
            nextID,
            null);
    }
}
