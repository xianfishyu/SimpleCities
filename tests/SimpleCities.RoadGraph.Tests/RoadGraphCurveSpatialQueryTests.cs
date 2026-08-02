using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphCurveSpatialQueryTests
{
    public static TheoryData<RoadGeometrySegment> NativeGeometryCases => new()
    {
        new LineRoadGeometrySegment(Vector2.Zero, new Vector2(12f, 2f)),
        new CubicBezierRoadGeometrySegment(
            new Vector2(20f, 0f), new Vector2(20f, 12f),
            new Vector2(32f, 12f), new Vector2(32f, 0f)),
        new CubicHermiteRoadGeometrySegment(
            new Vector2(40f, 0f), new Vector2(2f, 14f),
            new Vector2(52f, 1f), new Vector2(3f, -12f)),
        new CircularArcRoadGeometrySegment(new Vector2(66f, 0f), 6f, Mathf.Pi, Mathf.Pi),
        new ClothoidRoadGeometrySegment(new Vector2(80f, 0f), 0.2f, 0f, 0.12f, 12f),
        new RationalQuadraticRoadGeometrySegment(
            new Vector2(100f, 0f), 1f, new Vector2(106f, 12f), 0.7f,
            new Vector2(112f, 1f), 1.1f),
    };

    [Theory]
    [MemberData(nameof(NativeGeometryCases))]
    public void FindClosestEdge_HitsInteriorOfEveryNativeGeometry(RoadGeometrySegment geometry)
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([geometry]));
        GraphEdge expected = Assert.IsType<GraphEdge>(graph.GetEdge(Assert.Single(result.Changes.CreatedEdgeIDs)));
        Vector2 query = geometry.GetPosition(0.5f);

        GraphEdge? actual = graph.FindClosestEdge(query, 0.001f);

        Assert.Equal(expected.ID, Assert.IsType<GraphEdge>(actual).ID);
    }

    [Fact]
    public void FindClosestEdge_HighCurvatureBezierHitsFarFromEndpointChord()
    {
        var cubic = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(0f, 16f), new Vector2(16f, 16f), new Vector2(16f, 0f));
        var graph = new RoadGraph();
        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([cubic]));
        int edgeID = Assert.Single(result.Changes.CreatedEdgeIDs);
        Vector2 query = cubic.GetPosition(0.5f);

        GraphEdge? actual = graph.FindClosestEdge(query, 0.001f);

        Assert.Equal(edgeID, Assert.IsType<GraphEdge>(actual).ID);
        Assert.True(query.DistanceTo(new Vector2(8f, 0f)) > 10f);
    }

    [Fact]
    public void FindClosestEdge_ChoosesTrueNearestCurve()
    {
        var upper = new CubicBezierRoadGeometrySegment(
            new Vector2(0f, 6f), new Vector2(3f, 12f),
            new Vector2(9f, 12f), new Vector2(12f, 6f));
        var lower = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(3f, 4f), new Vector2(9f, 4f), new Vector2(12f, 0f));
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadPath([upper])).Success);
        RoadPathSubmissionResult lowerResult = graph.SubmitPath(new RoadPath([lower]));
        int expectedID = Assert.Single(lowerResult.Changes.CreatedEdgeIDs);

        GraphEdge? actual = graph.FindClosestEdge(new Vector2(6f, 4f), 8f);

        Assert.Equal(expectedID, Assert.IsType<GraphEdge>(actual).ID);
    }

    [Fact]
    public void FindClosestEdge_UsesInclusiveRadiusWithoutAcceptingOutsideCurve()
    {
        var arc = new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, Mathf.Pi);
        var graph = new RoadGraph();
        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([arc]));
        int edgeID = Assert.Single(result.Changes.CreatedEdgeIDs);

        GraphEdge? boundary = graph.FindClosestEdge(new Vector2(0f, 7f), 2f);
        GraphEdge? outside = graph.FindClosestEdge(new Vector2(0f, 7.00005f), 2f);

        Assert.Equal(edgeID, Assert.IsType<GraphEdge>(boundary).ID);
        Assert.Null(outside);
    }

    [Fact]
    public void FindClosestEdge_EqualDistanceChoosesLowerEdgeID()
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult first = graph.SubmitPath(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                new Vector2(0f, -4f), new Vector2(3f, -4f),
                new Vector2(7f, -4f), new Vector2(10f, -4f)),
        ]));
        RoadPathSubmissionResult second = graph.SubmitPath(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                new Vector2(0f, 4f), new Vector2(3f, 4f),
                new Vector2(7f, 4f), new Vector2(10f, 4f)),
        ]));
        int expectedID = Math.Min(
            Assert.Single(first.Changes.CreatedEdgeIDs),
            Assert.Single(second.Changes.CreatedEdgeIDs));

        GraphEdge? actual = graph.FindClosestEdge(new Vector2(5f, 0f), 5f);

        Assert.Equal(expectedID, Assert.IsType<GraphEdge>(actual).ID);
    }

    [Fact]
    public void FindClosestEdge_RestoreRebuildsCurveSpatialIndex()
    {
        var cubic = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(0f, 10f), new Vector2(10f, 10f), new Vector2(10f, 0f));
        var source = new RoadGraph();
        Assert.True(source.SubmitPath(new RoadPath([cubic])).Success);
        int edgeID = Assert.Single(source.GetAllEdges()).ID;
        var restored = new RoadGraph();

        restored.RestoreState(SaveJson.Serialize(source.CaptureState()));

        GraphEdge? actual = restored.FindClosestEdge(cubic.GetPosition(0.5f), 0.001f);
        Assert.Equal(edgeID, Assert.IsType<GraphEdge>(actual).ID);
    }

    [Fact]
    public void FindClosestEdge_RemovedCurveIsAbsentFromSpatialIndex()
    {
        var cubic = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(0f, 10f), new Vector2(10f, 10f), new Vector2(10f, 0f));
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadPath([cubic])).Success);
        int edgeID = Assert.Single(graph.GetAllEdges()).ID;
        Vector2 query = cubic.GetPosition(0.5f);
        Assert.NotNull(graph.FindClosestEdge(query, 0.001f));

        Assert.True(graph.RemoveEdge(edgeID));

        Assert.Null(graph.FindClosestEdge(query, 0.001f));
    }
}
