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
        RoadPathSubmissionResult existingResult = graph.SubmitPath(new RoadPath([existing]));
        int originalEdgeID = Assert.Single(existingResult.Changes.CreatedEdgeIDs);
        int originalGroupID = existingResult.GroupID!.Value;
        int removedEvents = 0;
        var addedEdgeIDs = new List<int>();
        graph.EdgeRemoved += edge =>
        {
            Assert.Equal(originalEdgeID, edge.ID);
            removedEvents++;
        };
        graph.EdgeAdded += edge => addedEdgeIDs.Add(edge.ID);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([incoming]));

        Assert.True(result.Success);
        Assert.Equal(1, removedEvents);
        Assert.Equal(4, addedEdgeIDs.Count);
        Assert.Equal([originalEdgeID], result.Changes.RemovedEdgeIDs);
        Assert.Equal(addedEdgeIDs.Order(), result.Changes.CreatedEdgeIDs);
        Assert.Equal([result.GroupID!.Value], result.Changes.CreatedGroupIDs);
        Assert.Equal(2, Assert.IsType<RoadGroup>(graph.GetGroup(originalGroupID)).EdgeCount);
        Assert.Equal(2, Assert.IsType<RoadGroup>(graph.GetGroup(result.GroupID.Value)).EdgeCount);

        GraphNode intersection = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(crossing) <= 1e-3f);
        Assert.Equal(4, intersection.EdgeCount);
        Assert.Equal(2, graph.GetAllEdges().Count(edge =>
            Assert.Single(edge.GeometrySegments) is CubicBezierRoadGeometrySegment));
        Assert.Equal(2, graph.GetAllEdges().Count(edge =>
            Assert.Single(edge.GeometrySegments) is LineRoadGeometrySegment));
        Assert.All(graph.GetAllEdges(), edge =>
            Assert.Contains(edge.ID, Assert.IsType<RoadGroup>(graph.GetGroup(edge.GroupID)).EdgeIDs));

        string state = SaveJson.Serialize(graph.CaptureState());
        var restored = new RoadGraph();
        restored.RestoreState(state);
        Assert.Equal(state, SaveJson.Serialize(restored.CaptureState()));
    }

    [Fact]
    public void SubmitPath_MultipleBezierIntersectionsCreateOrderedTopologyPieces()
    {
        var wave = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 8f), new Vector2(8f, -8f), new Vector2(10f, 0f));
        var baseline = LinearBezier(new Vector2(-1f, 0f), new Vector2(11f, 0f));
        var graph = new RoadGraph();
        int originalGroupID = graph.SubmitPath(new RoadPath([wave])).GroupID!.Value;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([baseline]));

        Assert.True(result.Success);
        Assert.Equal(4, Assert.IsType<RoadGroup>(graph.GetGroup(result.GroupID!.Value)).EdgeCount);
        Assert.Equal(2, Assert.IsType<RoadGroup>(graph.GetGroup(originalGroupID)).EdgeCount);
        Assert.Equal(6, graph.GetAllEdges().Count());
        Assert.Equal(3, graph.GetAllNodes().Count(node =>
            node.Position.DistanceTo(new Vector2(node.Position.X, 0f)) <= 1e-3f &&
            node.Position.X >= -1e-3f && node.Position.X <= 10f + 1e-3f));
        GraphNode center = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(new Vector2(5f, 0f)) <= 2e-3f);
        Assert.Equal(4, center.EdgeCount);
    }

    [Fact]
    public void SubmitPath_InteriorTangencyCreatesConnectedTopologyNode()
    {
        var tangent = new CubicBezierRoadGeometrySegment(
            new Vector2(0f, 4f), new Vector2(3f, 0f),
            new Vector2(7f, 0f), new Vector2(10f, 4f));
        var baseline = new LineRoadGeometrySegment(new Vector2(-1f, 1f), new Vector2(11f, 1f));
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadPath([tangent])).Success);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([baseline]));

        Assert.True(result.Success);
        Assert.Equal(4, graph.GetAllEdges().Count());
        GraphNode touch = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(new Vector2(5f, 1f)) <= 2e-3f);
        Assert.Equal(4, touch.EdgeCount);
    }

    [Fact]
    public void SubmitPath_EndpointTouchReusesNodeWithoutReplacingExistingEdge()
    {
        var existing = LinearBezier(Vector2.Zero, new Vector2(5f, 0f));
        var graph = new RoadGraph();
        RoadPathSubmissionResult existingResult = graph.SubmitPath(new RoadPath([existing]));
        int originalEdgeID = Assert.Single(existingResult.Changes.CreatedEdgeIDs);
        int removedEvents = 0;
        graph.EdgeRemoved += _ => removedEvents++;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([
            new LineRoadGeometrySegment(existing.End, new Vector2(5f, 5f)),
        ]));

        Assert.True(result.Success);
        Assert.Empty(result.Changes.RemovedEdgeIDs);
        Assert.Equal(0, removedEvents);
        Assert.NotNull(graph.GetEdge(originalEdgeID));
        GraphNode shared = Assert.Single(graph.GetAllNodes(), node => node.Position == existing.End);
        Assert.Equal(2, shared.EdgeCount);
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
        RoadPathSubmissionResult existingResult = graph.SubmitPath(new RoadPath([existing]));
        int originalEdgeID = Assert.Single(existingResult.Changes.CreatedEdgeIDs);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([incoming]));

        Assert.True(result.Success);
        Assert.Contains(originalEdgeID, result.Changes.RemovedEdgeIDs);
        Assert.Equal(4, graph.GetAllEdges().Count());
        GraphNode intersection = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(crossing) <= 2e-3f);
        Assert.Equal(4, intersection.EdgeCount);
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
        int curveGroupID = graph.SubmitPath(new RoadPath([curve])).GroupID!.Value;

        RoadPathSubmissionResult result = graph.SubmitPolyline([
            new Vector2(8f, 8f),
            new Vector2(8f, 16f),
        ]);

        Assert.True(result.Success);
        Assert.Equal(2, Assert.IsType<RoadGroup>(graph.GetGroup(curveGroupID)).EdgeCount);
        Assert.Equal(2, Assert.IsType<RoadGroup>(graph.GetGroup(result.GroupID!.Value)).EdgeCount);
        GraphNode intersection = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(new Vector2(8f, 12f)) <= 2e-3f);
        Assert.Equal(4, intersection.EdgeCount);
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
        Assert.True(graph.SubmitPath(new RoadPath([curve])).Success);

        RoadPathSubmissionResult result = graph.SubmitPolyline([
            new Vector2(8f, -4f),
            new Vector2(8f, 4f),
        ]);

        Assert.True(result.Success);
        Assert.Equal(2, graph.GetAllEdges().Count());
        Assert.DoesNotContain(
            graph.GetAllNodes(),
            node => node.Position.DistanceTo(new Vector2(8f, 0f)) <= 2e-3f);
        Assert.All(graph.GetAllNodes(), node => Assert.Equal(1, node.EdgeCount));
    }

    private static CubicBezierRoadGeometrySegment LinearBezier(Vector2 start, Vector2 end) =>
        new(start, start.Lerp(end, 1f / 3f), start.Lerp(end, 2f / 3f), end);
}
