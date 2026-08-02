using Godot;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SimpleCities.Tests;

public sealed class RoadGraphPersistenceV2Tests
{
    [Fact]
    public void CaptureState_UsesOnlyV2NodeEdgeGroupSchema()
    {
        var graph = new RoadGraph();
        Assert.True(graph.AddRoad(Vector2.Zero, new Vector2(8f, 3f), [new Vector2(4f, 1f)]) >= 0);

        JsonObject root = CaptureRoot(graph);

        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.NotNull(root["nextID"]);
        Assert.NotNull(root["nodes"]);
        Assert.NotNull(root["edges"]);
        Assert.NotNull(root["groups"]);
        foreach (string legacyName in new[] { "version", "junctions", "segments", "roads" })
            Assert.Null(root[legacyName]);

        JsonObject edge = FirstObject(root, "edges");
        Assert.NotNull(edge["nodeAID"]);
        Assert.NotNull(edge["nodeBID"]);
        Assert.NotNull(edge["groupID"]);
        Assert.NotNull(edge["geometry"]);
        foreach (string excludedName in new[] { "type", "waypoints", "totalLength", "fromJunctionID", "roadID" })
            Assert.Null(edge[excludedName]);

        JsonObject group = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(root["groups"])));
        Assert.NotNull(group["edgeIDs"]);
        Assert.Null(group["type"]);
    }

    [Fact]
    public void RestoreState_RoundTripsEveryNativeGeometryKind()
    {
        RoadGeometrySegment[] geometry =
        [
            new LineRoadGeometrySegment(new Vector2(0f, 0f), new Vector2(2f, 0f)),
            new CubicBezierRoadGeometrySegment(
                new Vector2(10f, 0f), new Vector2(10.5f, 2f),
                new Vector2(11.5f, -1f), new Vector2(12f, 0f)),
            new CubicHermiteRoadGeometrySegment(
                new Vector2(20f, 0f), new Vector2(2f, 1f),
                new Vector2(22f, 1f), new Vector2(1f, -1f)),
            new CircularArcRoadGeometrySegment(new Vector2(31f, 0f), 1f, Mathf.Pi, Mathf.Pi / 2f),
            new ClothoidRoadGeometrySegment(new Vector2(40f, 0f), 0f, 0f, 0f, 2f),
            new RationalQuadraticRoadGeometrySegment(
                new Vector2(50f, 0f), 1f, new Vector2(51f, 1f), 0.75f,
                new Vector2(52f, 0f), 1f),
        ];
        JsonObject payload = CreateNativeGeometryPayload(geometry);
        var graph = new RoadGraph();

        graph.RestoreState(payload.ToJsonString());

        GraphEdge[] edges = graph.GetAllEdges().OrderBy(edge => edge.ID).ToArray();
        Assert.Collection(
            edges,
            edge => Assert.IsType<LineRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)),
            edge => Assert.IsType<CubicBezierRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)),
            edge => Assert.IsType<CubicHermiteRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)),
            edge => Assert.IsType<CircularArcRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)),
            edge => Assert.IsType<ClothoidRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)),
            edge => Assert.IsType<RationalQuadraticRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)));

        JsonObject captured = CaptureRoot(graph);
        var restoredAgain = new RoadGraph();
        restoredAgain.RestoreState(captured.ToJsonString());
        Assert.True(JsonNode.DeepEquals(captured, CaptureRoot(restoredAgain)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(2)]
    public void RestoreState_MissingOldOrFutureVersion_IsRejectedWithoutMutation(int? schemaVersion)
    {
        var graph = CreatePopulatedGraph();
        JsonObject invalid = CaptureRoot(graph);
        if (schemaVersion is null)
            invalid.Remove("schemaVersion");
        else
            invalid["schemaVersion"] = schemaVersion.Value;

        AssertRestoreRejectedWithoutMutation(graph, invalid);
    }

    [Fact]
    public void RestoreState_WrongVersionTypeAndLegacySchemaAreRejected()
    {
        var graph = CreatePopulatedGraph();
        JsonObject wrongType = CaptureRoot(graph);
        wrongType["schemaVersion"] = "1";
        AssertRestoreRejectedWithoutMutation(graph, wrongType);

        Assert.Throws<JsonException>(() => new RoadGraph().RestoreState(
            """{"version":2,"nextID":0,"junctions":[],"segments":[],"roads":[]}"""));
    }

    [Fact]
    public void RestoreState_RejectsUnknownFieldsAndNullCollections()
    {
        var graph = CreatePopulatedGraph();
        JsonObject unknownRoot = CaptureRoot(graph);
        unknownRoot["unexpected"] = true;
        AssertRestoreRejectedWithoutMutation(graph, unknownRoot);

        JsonObject unknownNode = CaptureRoot(graph);
        FirstObject(unknownNode, "nodes")["label"] = "legacy";
        AssertRestoreRejectedWithoutMutation(graph, unknownNode);

        JsonObject nullEdges = CaptureRoot(graph);
        nullEdges["edges"] = null;
        AssertRestoreRejectedWithoutMutation(graph, nullEdges);
    }

    [Fact]
    public void RestoreState_RejectsConflictingAndInvalidIDs()
    {
        var graph = CreatePopulatedGraph();
        JsonObject collision = CaptureRoot(graph);
        FirstObject(collision, "edges")["id"] = FirstObject(collision, "nodes")["id"]!.GetValue<int>();
        AssertRestoreRejectedWithoutMutation(graph, collision);

        JsonObject missingEndpoint = CaptureRoot(graph);
        FirstObject(missingEndpoint, "edges")["nodeAID"] = 999;
        AssertRestoreRejectedWithoutMutation(graph, missingEndpoint);

        JsonObject missingGroup = CaptureRoot(graph);
        FirstObject(missingGroup, "edges")["groupID"] = 999;
        AssertRestoreRejectedWithoutMutation(graph, missingGroup);

        JsonObject badNextID = CaptureRoot(graph);
        badNextID["nextID"] = FirstObject(badNextID, "edges")["id"]!.GetValue<int>();
        AssertRestoreRejectedWithoutMutation(graph, badNextID);
    }

    [Fact]
    public void RestoreState_RejectsInconsistentGroupMembership()
    {
        var graph = CreatePopulatedGraph();
        JsonObject mismatch = CaptureRoot(graph);
        JsonArray mismatchIDs = Assert.IsType<JsonArray>(FirstObject(mismatch, "groups")["edgeIDs"]);
        mismatchIDs[0] = 999;
        AssertRestoreRejectedWithoutMutation(graph, mismatch);

        JsonObject duplicate = CaptureRoot(graph);
        JsonArray duplicateIDs = Assert.IsType<JsonArray>(FirstObject(duplicate, "groups")["edgeIDs"]);
        duplicateIDs.Add(duplicateIDs[0]!.GetValue<int>());
        AssertRestoreRejectedWithoutMutation(graph, duplicate);
    }

    [Fact]
    public void RestoreState_RejectsInvalidGeometryAndEndpointContracts()
    {
        var graph = CreatePopulatedGraph();
        JsonObject unknownKind = CaptureRoot(graph);
        FirstGeometry(unknownKind)["kind"] = "futureCurve";
        AssertRestoreRejectedWithoutMutation(graph, unknownKind);

        JsonObject degenerate = CaptureRoot(graph);
        JsonObject geometry = FirstGeometry(degenerate);
        geometry["end"] = geometry["start"]!.DeepClone();
        AssertRestoreRejectedWithoutMutation(graph, degenerate);

        JsonObject endpointMismatch = CaptureRoot(graph);
        FirstGeometry(endpointMismatch)["start"] = new JsonObject { ["x"] = 500f, ["y"] = 500f };
        AssertRestoreRejectedWithoutMutation(graph, endpointMismatch);

        JsonObject discontinuous = CaptureRoot(graph);
        JsonArray segments = Assert.IsType<JsonArray>(FirstObject(discontinuous, "edges")["geometry"]);
        JsonObject second = Assert.IsType<JsonObject>(segments[1]);
        second["start"] = new JsonObject { ["x"] = 5f, ["y"] = 5f };
        AssertRestoreRejectedWithoutMutation(graph, discontinuous);
    }

    [Fact]
    public void RestoreState_RejectsNonFiniteCoordinatesAndIsolatedNodes()
    {
        var graph = CreatePopulatedGraph();
        string overflowCoordinate = CaptureRoot(graph).ToJsonString().Replace("\"x\":0", "\"x\":1e999", StringComparison.Ordinal);
        AssertRestoreRejectedWithoutMutation(graph, overflowCoordinate);

        JsonObject isolated = CaptureRoot(graph);
        int nextID = isolated["nextID"]!.GetValue<int>();
        Assert.IsType<JsonArray>(isolated["nodes"]).Add(new JsonObject
        {
            ["id"] = nextID,
            ["x"] = 100f,
            ["y"] = 100f,
        });
        isolated["nextID"] = nextID + 1;
        AssertRestoreRejectedWithoutMutation(graph, isolated);
    }

    [Fact]
    public void RestoreState_ValidPayloadCommitsOnceAndRebuildsDerivedState()
    {
        var source = new RoadGraph();
        Assert.True(source.AddRoad(Vector2.Zero, new Vector2(8f, 2f), []) >= 0);
        var restored = new RoadGraph();
        int clearedCount = 0;
        restored.GraphCleared += () => clearedCount++;

        restored.RestoreState(SaveJson.Serialize(source.CaptureState()));

        Assert.Equal(1, clearedCount);
        GraphEdge edge = Assert.Single(restored.GetAllEdges());
        Assert.Equal(1, restored.GetNode(edge.NodeA)!.EdgeCount);
        Assert.Equal(1, restored.GetNode(edge.NodeB)!.EdgeCount);
        Assert.Equal(edge.ID, restored.FindClosestEdge(new Vector2(4f, 1f), 0.1f)!.ID);
    }

    private static RoadGraph CreatePopulatedGraph()
    {
        var graph = new RoadGraph();
        Assert.True(graph.AddRoad(Vector2.Zero, new Vector2(8f, 2f), [new Vector2(4f, 1f)]) >= 0);
        return graph;
    }

    private static void AssertRestoreRejectedWithoutMutation(RoadGraph graph, JsonObject invalid)
    {
        AssertRestoreRejectedWithoutMutation(graph, invalid.ToJsonString());
    }

    private static void AssertRestoreRejectedWithoutMutation(RoadGraph graph, string invalidJson)
    {
        string before = SaveJson.Serialize(graph.CaptureState());
        int clearedCount = 0;
        graph.GraphCleared += () => clearedCount++;

        Assert.Throws<JsonException>(() => graph.RestoreState(invalidJson));

        Assert.Equal(0, clearedCount);
        Assert.Equal(before, SaveJson.Serialize(graph.CaptureState()));
    }

    private static JsonObject CreateNativeGeometryPayload(IReadOnlyList<RoadGeometrySegment> geometry)
    {
        var nodes = new JsonArray();
        var edges = new JsonArray();
        var edgeIDs = new JsonArray();
        int edgeIDBase = geometry.Count * 2;
        for (int index = 0; index < geometry.Count; index++)
        {
            int nodeAID = index * 2;
            int nodeBID = nodeAID + 1;
            int edgeID = edgeIDBase + index;
            nodes.Add(CreateNode(nodeAID, geometry[index].Start));
            nodes.Add(CreateNode(nodeBID, geometry[index].End));
            edges.Add(new JsonObject
            {
                ["id"] = edgeID,
                ["nodeAID"] = nodeAID,
                ["nodeBID"] = nodeBID,
                ["groupID"] = edgeIDBase + geometry.Count,
                ["geometry"] = new JsonArray(JsonNode.Parse(
                    SaveJson.Serialize(RoadGeometrySerializer.ToData(geometry[index])))),
            });
            edgeIDs.Add(edgeID);
        }

        int groupID = edgeIDBase + geometry.Count;
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["nextID"] = groupID + 1,
            ["nodes"] = nodes,
            ["edges"] = edges,
            ["groups"] = new JsonArray(new JsonObject { ["id"] = groupID, ["edgeIDs"] = edgeIDs }),
        };
    }

    private static JsonObject CreateNode(int id, Vector2 position) => new()
    {
        ["id"] = id,
        ["x"] = position.X,
        ["y"] = position.Y,
    };

    private static JsonObject CaptureRoot(RoadGraph graph) =>
        Assert.IsType<JsonObject>(JsonNode.Parse(SaveJson.Serialize(graph.CaptureState())));

    private static JsonObject FirstObject(JsonObject root, string arrayName) =>
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(root[arrayName])[0]);

    private static JsonObject FirstGeometry(JsonObject root) =>
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(FirstObject(root, "edges")["geometry"])[0]);
}
