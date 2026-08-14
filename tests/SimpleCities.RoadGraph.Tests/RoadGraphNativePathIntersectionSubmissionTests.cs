using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphNativePathIntersectionSubmissionTests
{
    public static TheoryData<RoadGeometrySegment> NativeCurveCases => new()
    {
        new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 5f), new Vector2(7f, -3f), new Vector2(10f, 1f)),
        new CubicHermiteRoadGeometrySegment(
            new Vector2(20f, 0f), new Vector2(6f, 4f),
            new Vector2(28f, 2f), new Vector2(4f, -3f)),
        new CircularArcRoadGeometrySegment(new Vector2(38f, 0f), 5f, 0f, Mathf.Pi),
        new ClothoidRoadGeometrySegment(new Vector2(50f, 0f), 0.15f, 0f, 0.08f, 8f),
        new RationalQuadraticRoadGeometrySegment(
            new Vector2(65f, 0f), 1f, new Vector2(69f, 6f), 0.65f,
            new Vector2(74f, 1f), 1.2f),
    };

    [Fact]
    public void SubmitPath_LineCrossingBezierSplitsBothSidesAndReportsChanges()
    {
        var existing = new CubicBezierRoadGeometrySegment(
            new Vector2(-6f, 0f), new Vector2(-2f, 4f),
            new Vector2(2f, -4f), new Vector2(6f, 0f));
        const float existingParameter = 0.5f;
        Vector2 crossing = existing.GetPosition(existingParameter);
        Vector2 tangent = existing.GetUnitTangent(existingParameter);
        Vector2 normal = new(-tangent.Y, tangent.X);
        var incoming = new LineRoadGeometrySegment(crossing - normal * 6f, crossing + normal * 6f);
        var graph = new RoadGraph();
        RoadPathSubmissionResult existingResult = graph.SubmitPath(new RoadBuildRequest(new RoadPath([existing]), RoadType.Street));
        int originalEdgeID = Assert.Single(existingResult.Changes.CreatedEdgeIDs);
        var events = new List<RoadGraphChangedEvent>();
        graph.GraphChanged += events.Add;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([incoming]), RoadType.Street));

        Assert.True(result.Success);
        RoadGraphChangedEvent change = Assert.Single(events);
        Assert.Equal([originalEdgeID], change.Changes.UpdatedEdgeIDs);
        Assert.Equal(4, change.Delta.Edges.Count);
        Assert.Empty(result.Changes.RemovedEdgeIDs);
        Assert.DoesNotContain(originalEdgeID, result.Changes.CreatedEdgeIDs);
        Assert.Equal(3, result.Changes.CreatedEdgeIDs.Count);
        Assert.Equal(originalEdgeID, Assert.IsType<GraphEdge>(graph.GetEdge(originalEdgeID)).ID);

        GraphNode intersection = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(crossing) <= 1e-3f);
        Assert.Equal(4, intersection.IncidenceCount);
        Assert.Equal(2, graph.GetAllEdges().Count(edge =>
            Assert.Single(edge.GeometrySegments) is CubicBezierRoadGeometrySegment));
        Assert.Equal(2, graph.GetAllEdges().Count(edge =>
            Assert.Single(edge.GeometrySegments) is LineRoadGeometrySegment));

        string state = RoadGraphTestCodec.CaptureJson(graph);
        var restored = new RoadGraph();
        RoadGraphTestCodec.LoadJson(restored, state);
        Assert.Equal(state, RoadGraphTestCodec.CaptureJson(restored));
    }

    [Fact]
    public void SubmitPath_MultipleBezierIntersectionsCreateOrderedTopologyPieces()
    {
        var wave = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 8f), new Vector2(8f, -8f), new Vector2(10f, 0f));
        var baseline = LinearBezier(new Vector2(-1f, 0f), new Vector2(11f, 0f));
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([wave]), RoadType.Street)).Success);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([baseline]), RoadType.Street));

        Assert.True(result.Success);
        Assert.True(
            graph.GetAllEdges().Count() == 6,
            DescribeTopology(graph));
        Assert.Equal(3, graph.GetAllNodes().Count(node =>
            node.Position.DistanceTo(new Vector2(node.Position.X, 0f)) <= 1e-3f &&
            node.Position.X >= -1e-3f && node.Position.X <= 10f + 1e-3f));
        GraphNode center = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(new Vector2(5f, 0f)) <= 2e-3f);
        Assert.Equal(4, center.IncidenceCount);
    }

    [Fact]
    public void SubmitPath_InteriorTangencyCreatesConnectedTopologyNode()
    {
        var tangent = new CubicBezierRoadGeometrySegment(
            new Vector2(0f, 4f), new Vector2(3f, 0f),
            new Vector2(7f, 0f), new Vector2(10f, 4f));
        var baseline = new LineRoadGeometrySegment(new Vector2(-1f, 1f), new Vector2(11f, 1f));
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([tangent]), RoadType.Street)).Success);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([baseline]), RoadType.Street));

        Assert.True(result.Success);
        Assert.Equal(4, graph.GetAllEdges().Count());
        GraphNode touch = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(new Vector2(5f, 1f)) <= 2e-3f);
        Assert.Equal(4, touch.IncidenceCount);
    }

    [Fact]
    public void SubmitPath_EndpointTouchMergesAcrossSubmissionsAndRetainsOriginalID()
    {
        var existing = LinearBezier(Vector2.Zero, new Vector2(5f, 0f));
        var graph = new RoadGraph();
        RoadPathSubmissionResult existingResult = graph.SubmitPath(new RoadBuildRequest(new RoadPath([existing]), RoadType.Street));
        int originalEdgeID = Assert.Single(existingResult.Changes.CreatedEdgeIDs);
        var events = new List<RoadGraphChangedEvent>();
        graph.GraphChanged += events.Add;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(existing.End, new Vector2(5f, 5f)),
        ]), RoadType.Street));

        Assert.True(result.Success);
        Assert.Empty(result.Changes.RemovedEdgeIDs);
        Assert.True(
            result.Changes.CreatedEdgeIDs.Count == 0,
            $"Unexpected created edges: [{string.Join(", ", result.Changes.CreatedEdgeIDs)}]. " +
            DescribeTopology(graph));
        RoadGraphChangedEvent change = Assert.Single(events);
        Assert.Equal([originalEdgeID], change.Changes.UpdatedEdgeIDs);
        GraphEdge merged = Assert.Single(graph.GetAllEdges());
        Assert.Equal(originalEdgeID, merged.ID);
        Assert.Collection(
            merged.GeometrySegments,
            segment => Assert.IsType<CubicBezierRoadGeometrySegment>(segment),
            segment => Assert.IsType<LineRoadGeometrySegment>(segment));
        Assert.DoesNotContain(graph.GetAllNodes(), node => node.Position == existing.End);
    }

    [Fact]
    public void SubmitPath_ConflictingCanonicalPositionsAtSameSplitParameterAreRejectedAtomically()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(400_000f, 0f)),
        ]), RoadType.Street)).Success);
        string stateBefore = RoadGraphTestCodec.CaptureJson(graph);
        int nextIDBefore = graph.NextIDWatermark;
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(
                new Vector2(200_000f, -10f),
                new Vector2(200_000f, 10f)),
            new LineRoadGeometrySegment(
                new Vector2(200_000f, 10f),
                new Vector2(200_002f, -10f)),
        ]), RoadType.Street));

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.AmbiguousIntersection, result.Error);
        Assert.Equal(stateBefore, RoadGraphTestCodec.CaptureJson(graph));
        Assert.Equal(nextIDBefore, graph.NextIDWatermark);
        Assert.Equal(0, changedEvents);
        graph.AssertInvariants();
    }

    [Theory]
    [MemberData(nameof(NativeCurveCases))]
    public void SubmitPath_LineCrossingEveryNativeCurveCreatesFourWayNode(
        RoadGeometrySegment existing)
    {
        const float existingParameter = 0.37f;
        Vector2 crossing = existing.GetPosition(existingParameter);
        Vector2 tangent = existing.GetUnitTangent(existingParameter);
        Vector2 normal = new(-tangent.Y, tangent.X);
        var incoming = new LineRoadGeometrySegment(crossing - normal * 5f, crossing + normal * 5f);
        var graph = new RoadGraph();
        RoadPathSubmissionResult existingResult = graph.SubmitPath(new RoadBuildRequest(new RoadPath([existing]), RoadType.Street));
        int originalEdgeID = Assert.Single(existingResult.Changes.CreatedEdgeIDs);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([incoming]), RoadType.Street));

        Assert.True(result.Success);
        Assert.Empty(result.Changes.RemovedEdgeIDs);
        Assert.NotNull(graph.GetEdge(originalEdgeID));
        Assert.Equal(4, graph.GetAllEdges().Count());
        GraphNode intersection = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(crossing) <= 2e-3f);
        Assert.Equal(4, intersection.IncidenceCount);
        Assert.Equal(2, graph.GetAllEdges().Count(edge =>
            Assert.Single(edge.GeometrySegments).GetType() == existing.GetType()));
        Assert.Equal(2, graph.GetAllEdges().Count(edge =>
            Assert.Single(edge.GeometrySegments) is LineRoadGeometrySegment));
    }

    [Fact]
    public void SubmitPolyline_CrossingBezierUsesNativeCurveGeometry()
    {
        var curve = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(0f, 16f),
            new Vector2(16f, 16f), new Vector2(16f, 0f));
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([curve]), RoadType.Street)).Success);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            new Vector2(8f, 8f),
            new Vector2(8f, 16f),
        ]);

        Assert.True(result.Success);
        Assert.Equal(4, graph.GetAllEdges().Count());
        GraphNode intersection = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(new Vector2(8f, 12f)) <= 2e-3f);
        Assert.Equal(4, intersection.IncidenceCount);
        Assert.Equal(2, graph.GetAllEdges().Count(edge =>
            Assert.Single(edge.GeometrySegments) is CubicBezierRoadGeometrySegment));
        Assert.Equal(2, graph.GetAllEdges().Count(edge =>
            Assert.Single(edge.GeometrySegments) is LineRoadGeometrySegment));
    }

    [Fact]
    public void SubmitPolyline_CrossingOnlyBezierEndpointChordDoesNotCreateFalseIntersection()
    {
        var curve = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(0f, 16f),
            new Vector2(16f, 16f), new Vector2(16f, 0f));
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([curve]), RoadType.Street)).Success);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            new Vector2(8f, -4f),
            new Vector2(8f, 4f),
        ]);

        Assert.True(result.Success);
        Assert.Equal(2, graph.GetAllEdges().Count());
        Assert.DoesNotContain(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(new Vector2(8f, 0f)) <= 2e-3f);
        Assert.All(graph.GetAllNodes(), node => Assert.Equal(1, node.IncidenceCount));
    }

    private static CubicBezierRoadGeometrySegment LinearBezier(Vector2 start, Vector2 end) =>
        new(start, start.Lerp(end, 1f / 3f), start.Lerp(end, 2f / 3f), end);

    private static string DescribeTopology(RoadGraph graph) =>
        "Nodes=" + string.Join(
            "; ",
            graph.GetAllNodes().OrderBy(node => node.ID).Select(node =>
                $"{node.ID}@{node.Position}:inc={node.IncidenceCount}")) +
        " Edges=" + string.Join(
            "; ",
            graph.GetAllEdges().OrderBy(edge => edge.ID).Select(edge =>
                $"{edge.ID}:{edge.NodeA}->{edge.NodeB}:" +
                $"{edge.GeometrySegments[0].Start}->{edge.GeometrySegments[^1].End}"));
}
