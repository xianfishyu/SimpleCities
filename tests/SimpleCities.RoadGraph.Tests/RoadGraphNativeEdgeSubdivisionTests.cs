using Godot;

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
    public void SplitEdgeAtGeometryParameters_RecanonicalizesEveryNativeGeometryType(
        RoadGeometrySegment geometry)
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult submitted = graph.SubmitPath(new RoadBuildRequest(new RoadPath([geometry]), RoadType.Street));
        int originalEdgeID = Assert.Single(submitted.Changes.CreatedEdgeIDs);
        GraphEdge original = Assert.IsType<GraphEdge>(graph.GetEdge(originalEdgeID));
        int originalNodeA = original.NodeA;
        int originalNodeB = original.NodeB;

        bool split = graph.SplitEdgeAtGeometryParameters(
            originalEdgeID,
            [new EdgeGeometrySplitPoint(0, 0.4f)]);

        Assert.True(split);
        GraphEdge replacement = Assert.Single(graph.GetAllEdges());
        Assert.Equal(originalEdgeID, replacement.ID);
        Assert.Equal(originalNodeA, replacement.NodeA);
        Assert.Equal(originalNodeB, replacement.NodeB);
        Assert.All(replacement.GeometrySegments, segment => Assert.IsType(geometry.GetType(), segment));
        Assert.Equal(2, replacement.GeometrySegments.Count);
        Assert.DoesNotContain(graph.GetAllNodes(), node =>
            node.Position.DistanceTo(geometry.GetPosition(0.4f)) <= 2e-3f);
    }

    [Fact]
    public void SplitEdgeAtGeometryParameters_CoalescesUnorderedParametersAndUpdatesGraphState()
    {
        var geometry = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(0f, 12f), new Vector2(16f, 12f), new Vector2(16f, 0f));
        var graph = new RoadGraph();
        RoadPathSubmissionResult submitted = graph.SubmitPath(new RoadBuildRequest(new RoadPath([geometry]), RoadType.Street));
        int originalEdgeID = Assert.Single(submitted.Changes.CreatedEdgeIDs);
        var events = new List<RoadGraphChangedEvent>();
        graph.GraphChanged += events.Add;

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
        RoadGraphChangedEvent change = Assert.Single(events);
        Assert.Equal([originalEdgeID], change.Changes.UpdatedEdgeIDs);
        Assert.Empty(change.Changes.CreatedEdgeIDs);
        Assert.Empty(change.Changes.RemovedEdgeIDs);
        GraphEdge replacement = Assert.Single(graph.GetAllEdges());
        Assert.Equal(originalEdgeID, replacement.ID);
        Assert.Equal(4, replacement.GeometrySegments.Count);
        Assert.Equal(2, graph.GetAllNodes().Count());
        GraphEdge closest = Assert.IsType<GraphEdge>(graph.FindClosestEdge(geometry.GetPosition(0.6f), 0.001f));
        Assert.Equal(originalEdgeID, closest.ID);
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

        GraphEdge replacement = Assert.Single(graph.GetAllEdges());
        Assert.Collection(
            replacement.GeometrySegments,
            segment => Assert.IsType<LineRoadGeometrySegment>(segment),
            segment => Assert.IsType<CubicBezierRoadGeometrySegment>(segment));
        Assert.DoesNotContain(graph.GetAllNodes(), node => node.Position == line.End);

        string state = RoadGraphTestCodec.CaptureJson(graph);
        var restored = new RoadGraph();
        RoadGraphTestCodec.LoadJson(restored, state);
        Assert.Equal(state, RoadGraphTestCodec.CaptureJson(restored));
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

        GraphEdge replacement = Assert.Single(graph.GetAllEdges());
        Assert.Collection(
            replacement.GeometrySegments,
            segment => Assert.IsType<LineRoadGeometrySegment>(segment),
            segment => Assert.IsType<CubicBezierRoadGeometrySegment>(segment),
            segment => Assert.IsType<CubicBezierRoadGeometrySegment>(segment));
    }

    [Fact]
    public void SplitEdgeAtGeometryParameters_EndpointOnlyRequestHasNoSideEffects()
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult submitted = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
        ]), RoadType.Street));
        int edgeID = Assert.Single(submitted.Changes.CreatedEdgeIDs);
        string stateBefore = RoadGraphTestCodec.CaptureJson(graph);
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        bool split = graph.SplitEdgeAtGeometryParameters(
            edgeID,
            [
                new EdgeGeometrySplitPoint(0, 0f),
                new EdgeGeometrySplitPoint(0, 1f),
            ]);

        Assert.False(split);
        Assert.Equal(stateBefore, RoadGraphTestCodec.CaptureJson(graph));
        Assert.Equal(0, changedEvents);
    }

    private static RoadGraph RestoreSingleEdge(IReadOnlyList<RoadGeometrySegment> geometry)
    {
        RoadGraph source = RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
            3,
            [
                new PreparedRoadNode(0, geometry[0].Start),
                new PreparedRoadNode(1, geometry[^1].End),
            ],
            [new PreparedRoadEdge(RoadType.Street, 2, 0, 1, geometry)]));
        return RoadGraphTestCodec.Clone(source);
    }
}
