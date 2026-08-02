using Godot;
using System.Text.Json.Nodes;

namespace SimpleCities.Tests;

public sealed class RoadGraphNativeEdgeSubdivisionTests
{
    public static TheoryData<RoadGeometrySegment> NativeGeometryCases => new()
    {
        new LineRoadGeometrySegment(Vector2.Zero, new Vector2(12f, 2f)),
        new CubicBezierRoadGeometrySegment(
            new Vector2(20f, 0f), new Vector2(20f, 8f),
            new Vector2(32f, 8f), new Vector2(32f, 0f)),
        new CubicHermiteRoadGeometrySegment(
            new Vector2(40f, 0f), new Vector2(4f, 10f),
            new Vector2(52f, 1f), new Vector2(5f, -8f)),
        new CircularArcRoadGeometrySegment(new Vector2(66f, 0f), 6f, Mathf.Pi, Mathf.Pi),
        new ClothoidRoadGeometrySegment(new Vector2(80f, 0f), 0.2f, 0f, 0.12f, 12f),
        new RationalQuadraticRoadGeometrySegment(
            new Vector2(100f, 0f), 1f, new Vector2(106f, 10f), 0.7f,
            new Vector2(112f, 1f), 1.1f),
    };

    [Theory]
    [MemberData(nameof(NativeGeometryCases))]
    public void SplitEdgeAtGeometryParameters_PreservesEveryNativeGeometryType(
        RoadGeometrySegment geometry)
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult submitted = graph.SubmitPath(new RoadPath([geometry]));
        int originalEdgeID = Assert.Single(submitted.Changes.CreatedEdgeIDs);
        GraphEdge original = Assert.IsType<GraphEdge>(graph.GetEdge(originalEdgeID));
        int originalNodeA = original.NodeA;
        int originalNodeB = original.NodeB;

        bool split = graph.SplitEdgeAtGeometryParameters(
            originalEdgeID,
            [new EdgeGeometrySplitPoint(0, 0.4f)]);

        Assert.True(split);
        Assert.Null(graph.GetEdge(originalEdgeID));
        GraphEdge[] replacements = graph.GetAllEdges().OrderBy(edge => edge.ID).ToArray();
        Assert.Equal(2, replacements.Length);
        Assert.All(replacements, edge => Assert.IsType(geometry.GetType(), Assert.Single(edge.GeometrySegments)));
        Assert.Contains(replacements, edge => edge.NodeA == originalNodeA || edge.NodeB == originalNodeA);
        Assert.Contains(replacements, edge => edge.NodeA == originalNodeB || edge.NodeB == originalNodeB);
        GraphNode splitNode = Assert.Single(graph.GetAllNodes(), node => node.EdgeCount == 2);
        Assert.Equal(geometry.GetPosition(0.4f), splitNode.Position);
    }

    [Fact]
    public void SplitEdgeAtGeometryParameters_CoalescesUnorderedParametersAndUpdatesGraphState()
    {
        var geometry = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(0f, 12f), new Vector2(16f, 12f), new Vector2(16f, 0f));
        var graph = new RoadGraph();
        RoadPathSubmissionResult submitted = graph.SubmitPath(new RoadPath([geometry]));
        int originalEdgeID = Assert.Single(submitted.Changes.CreatedEdgeIDs);
        int groupID = Assert.IsType<GraphEdge>(graph.GetEdge(originalEdgeID)).GroupID;
        int removedEvents = 0;
        var addedEdgeIDs = new List<int>();
        graph.EdgeRemoved += edge =>
        {
            Assert.Equal(originalEdgeID, edge.ID);
            removedEvents++;
        };
        graph.EdgeAdded += edge => addedEdgeIDs.Add(edge.ID);

        bool split = graph.SplitEdgeAtGeometryParameters(
            originalEdgeID,
            [
                new EdgeGeometrySplitPoint(0, 0.75f),
                new EdgeGeometrySplitPoint(0, 0f),
                new EdgeGeometrySplitPoint(0, 0.25f),
                new EdgeGeometrySplitPoint(0, 0.500001f),
                new EdgeGeometrySplitPoint(0, 0.5f),
                new EdgeGeometrySplitPoint(0, 1f),
            ]);

        Assert.True(split);
        Assert.Equal(1, removedEvents);
        Assert.Equal(4, addedEdgeIDs.Count);
        Assert.Equal(4, graph.GetAllEdges().Count());
        Assert.Equal(5, graph.GetAllNodes().Count());
        RoadGroup group = Assert.IsType<RoadGroup>(graph.GetGroup(groupID));
        Assert.Equal(4, group.EdgeCount);
        Assert.All(graph.GetAllEdges(), edge => Assert.Equal(groupID, edge.GroupID));
        Assert.All(graph.GetAllEdges(), edge =>
        {
            Assert.Contains(
                Assert.IsType<GraphNode>(graph.GetNode(edge.NodeA)).Edges,
                reference => reference.EdgeID == edge.ID);
            Assert.Contains(
                Assert.IsType<GraphNode>(graph.GetNode(edge.NodeB)).Edges,
                reference => reference.EdgeID == edge.ID);
        });
        Assert.DoesNotContain(originalEdgeID, addedEdgeIDs);
        GraphEdge closest = Assert.IsType<GraphEdge>(graph.FindClosestEdge(geometry.GetPosition(0.6f), 0.001f));
        Assert.Contains(closest.ID, addedEdgeIDs);
    }

    [Fact]
    public void SplitEdgeAtGeometryParameters_HandlesMultipleSegmentsAndSharedBoundaryOnce()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f));
        var cubic = new CubicBezierRoadGeometrySegment(
            line.End, new Vector2(12f, 5f), new Vector2(18f, 5f), new Vector2(20f, 0f));
        var graph = RestoreSingleEdge([line, cubic]);
        GraphEdge original = Assert.Single(graph.GetAllEdges());

        Assert.True(graph.SplitEdgeAtGeometryParameters(
            original.ID,
            [
                new EdgeGeometrySplitPoint(1, 0f),
                new EdgeGeometrySplitPoint(0, 1f),
            ]));

        GraphEdge[] replacements = graph.GetAllEdges().OrderBy(edge => edge.ID).ToArray();
        Assert.Equal(2, replacements.Length);
        Assert.IsType<LineRoadGeometrySegment>(Assert.Single(replacements[0].GeometrySegments));
        Assert.IsType<CubicBezierRoadGeometrySegment>(Assert.Single(replacements[1].GeometrySegments));
        Assert.Single(graph.GetAllNodes(), node => node.Position == line.End && node.EdgeCount == 2);

        string state = SaveJson.Serialize(graph.CaptureState());
        var restored = new RoadGraph();
        restored.RestoreState(state);
        Assert.Equal(state, SaveJson.Serialize(restored.CaptureState()));
    }

    [Fact]
    public void SplitEdgeAtGeometryParameters_SplitsDifferentNativeSegmentsInOneReplacement()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f));
        var cubic = new CubicBezierRoadGeometrySegment(
            line.End, new Vector2(12f, 5f), new Vector2(18f, 5f), new Vector2(20f, 0f));
        var graph = RestoreSingleEdge([line, cubic]);
        int originalEdgeID = Assert.Single(graph.GetAllEdges()).ID;

        Assert.True(graph.SplitEdgeAtGeometryParameters(
            originalEdgeID,
            [
                new EdgeGeometrySplitPoint(1, 0.5f),
                new EdgeGeometrySplitPoint(0, 0.5f),
            ]));

        GraphEdge[] replacements = graph.GetAllEdges().OrderBy(edge => edge.ID).ToArray();
        Assert.Equal(3, replacements.Length);
        Assert.IsType<LineRoadGeometrySegment>(Assert.Single(replacements[0].GeometrySegments));
        Assert.Collection(
            replacements[1].GeometrySegments,
            segment => Assert.IsType<LineRoadGeometrySegment>(segment),
            segment => Assert.IsType<CubicBezierRoadGeometrySegment>(segment));
        Assert.IsType<CubicBezierRoadGeometrySegment>(Assert.Single(replacements[2].GeometrySegments));
    }

    [Fact]
    public void SplitEdgeAtGeometryParameters_EndpointOnlyRequestHasNoSideEffects()
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult submitted = graph.SubmitPath(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
        ]));
        int edgeID = Assert.Single(submitted.Changes.CreatedEdgeIDs);
        string stateBefore = SaveJson.Serialize(graph.CaptureState());
        int addedEvents = 0;
        int removedEvents = 0;
        graph.EdgeAdded += _ => addedEvents++;
        graph.EdgeRemoved += _ => removedEvents++;

        bool split = graph.SplitEdgeAtGeometryParameters(
            edgeID,
            [
                new EdgeGeometrySplitPoint(0, 0f),
                new EdgeGeometrySplitPoint(0, 1f),
            ]);

        Assert.False(split);
        Assert.Equal(stateBefore, SaveJson.Serialize(graph.CaptureState()));
        Assert.Equal(0, addedEvents);
        Assert.Equal(0, removedEvents);
    }

    private static RoadGraph RestoreSingleEdge(IReadOnlyList<RoadGeometrySegment> geometry)
    {
        var geometryData = new JsonArray();
        foreach (RoadGeometrySegment segment in geometry)
            geometryData.Add(JsonNode.Parse(SaveJson.Serialize(RoadGeometrySerializer.ToData(segment))));

        var payload = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["nextID"] = 4,
            ["nodes"] = new JsonArray(
                CreateNode(0, geometry[0].Start),
                CreateNode(1, geometry[^1].End)),
            ["edges"] = new JsonArray(new JsonObject
            {
                ["id"] = 2,
                ["nodeAID"] = 0,
                ["nodeBID"] = 1,
                ["groupID"] = 3,
                ["geometry"] = geometryData,
            }),
            ["groups"] = new JsonArray(new JsonObject
            {
                ["id"] = 3,
                ["edgeIDs"] = new JsonArray(2),
            }),
        };
        var graph = new RoadGraph();
        graph.RestoreState(payload.ToJsonString());
        return graph;
    }

    private static JsonObject CreateNode(int id, Vector2 position) => new()
    {
        ["id"] = id,
        ["x"] = position.X,
        ["y"] = position.Y,
    };
}
