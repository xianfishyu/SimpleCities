using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphRegressionTests
{
    [Fact]
    public void SubmitPolyline_CollinearRoadsFromSeparateOperationsMergeIntoOneMaximalEdge()
    {
        var graph = new RoadGraph();
        int retainedEdgeID = SubmitLine(graph, new Vector2(-100f, 0f), Vector2.Zero);

        Assert.True(graph.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(100f, 0f)]).Success);

        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Equal(retainedEdgeID, edge.ID);
        Assert.Equal(2, graph.GetAllNodes().Count());
        var line = Assert.IsType<LineRoadGeometrySegment>(Assert.Single(edge.GeometrySegments));
        Assert.Equal(new Vector2(-100f, 0f), line.Start);
        Assert.Equal(new Vector2(100f, 0f), line.End);
        graph.AssertInvariants();
    }

    [Fact]
    public void FindClosestEdge_LongStraightEdge_HitsItsMiddle()
    {
        var graph = new RoadGraph();
        int edgeID = SubmitLine(graph, new Vector2(-500f, 0f), new Vector2(500f, 0f));

        GraphEdge closest = Assert.IsType<GraphEdge>(graph.FindClosestEdge(Vector2.Zero, 1f));

        Assert.Equal(edgeID, closest.ID);
    }

    [Fact]
    public void FindClosestEdge_EdgeAtRadiusBoundary_IsIncluded()
    {
        var graph = new RoadGraph();
        SubmitLine(graph, new Vector2(-10f, 0f), new Vector2(10f, 0f));

        Assert.NotNull(graph.FindClosestEdge(new Vector2(0f, 1f), 1f));
    }

    [Fact]
    public void FindClosestEdge_LongDiagonalEdge_HitsItsMiddle()
    {
        var graph = new RoadGraph();
        int edgeID = SubmitLine(
            graph,
            new Vector2(-500f, -500f),
            new Vector2(500f, 500f));

        GraphEdge closest = Assert.IsType<GraphEdge>(
            graph.FindClosestEdge(new Vector2(0f, 0.5f), 1f));

        Assert.Equal(edgeID, closest.ID);
    }

    [Fact]
    public void FindClosestEdge_ChoosesTheGeometricallyNearestCandidate()
    {
        var graph = new RoadGraph();
        SubmitLine(graph, new Vector2(-100f, 0f), new Vector2(100f, 0f));
        int upperEdgeID = SubmitLine(
            graph,
            new Vector2(-100f, 3f),
            new Vector2(100f, 3f));

        GraphEdge closest = Assert.IsType<GraphEdge>(
            graph.FindClosestEdge(new Vector2(0f, 2f), 3f));

        Assert.Equal(upperEdgeID, closest.ID);
    }

    [Fact]
    public void FindClosestEdge_OutsideRadius_ReturnsNull()
    {
        var graph = new RoadGraph();
        SubmitLine(graph, new Vector2(-10f, 0f), new Vector2(10f, 0f));

        Assert.Null(graph.FindClosestEdge(new Vector2(0f, 2f), 1f));
    }

    [Fact]
    public void SubmitPolyline_CrossingLongUnsegmentedEdge_CreatesConnectedIntersectionNode()
    {
        var graph = new RoadGraph();
        SubmitLine(graph, new Vector2(-500f, 0f), new Vector2(500f, 0f));

        SubmitLine(graph, new Vector2(0f, -10f), new Vector2(0f, 10f));

        AssertFourWayIntersection(graph, Vector2.Zero);
    }

    [Fact]
    public void SubmitPolyline_CrossingDiagonalRoads_CreatesOneFourWayIntersection()
    {
        var graph = new RoadGraph();
        SubmitLine(graph, new Vector2(-100f, -100f), new Vector2(100f, 100f));

        SubmitLine(graph, new Vector2(-100f, 100f), new Vector2(100f, -100f));

        AssertFourWayIntersection(graph, Vector2.Zero);
    }

    [Fact]
    public void SubmitPolyline_CrossingExistingGeometryJoin_CreatesOneFourWayIntersection()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-100f, 0f),
            Vector2.Zero,
            new Vector2(100f, 0f),
        ]).Success);

        SubmitLine(graph, new Vector2(0f, -100f), new Vector2(0f, 100f));

        AssertFourWayIntersection(graph, Vector2.Zero);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.005f)]
    public void SubmitPolyline_EndpointWithinGeometryEpsilon_MergesAtExistingEndpoint(
        float endpointOffset)
    {
        var graph = new RoadGraph();
        int existingEdgeID = SubmitLine(graph, Vector2.Zero, new Vector2(10f, 0f));

        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(0f, endpointOffset),
            new Vector2(1f, -1f),
        ]).Success);

        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Equal(existingEdgeID, edge.ID);
        Assert.Equal(2, edge.GeometrySegments.Count);
        Assert.DoesNotContain(graph.GetAllNodes(), node => node.Position == Vector2.Zero);
        graph.AssertInvariants();
    }

    [Fact]
    public void SubmitPolyline_IntersectionOutsideEndpointEpsilon_CreatesFourWayNode()
    {
        var graph = new RoadGraph();
        int existingEdgeID = SubmitLine(graph, Vector2.Zero, new Vector2(10f, 0f));

        SubmitLine(graph, new Vector2(0f, 1f), new Vector2(2f, -1f));

        Assert.NotNull(graph.GetEdge(existingEdgeID));
        AssertFourWayIntersection(graph, new Vector2(1f, 0f));
    }

    [Theory]
    [InlineData(8f)]
    [InlineData(64f)]
    [InlineData(256f)]
    public void SubmitPolyline_CrossingLongEdge_RemainsConnectedAcrossIndexBucketSizes(
        float bucketSize)
    {
        var graph = new RoadGraph(bucketSize);
        SubmitLine(graph, new Vector2(-500f, 0f), new Vector2(500f, 0f));

        SubmitLine(graph, new Vector2(0f, -10f), new Vector2(0f, 10f));

        AssertFourWayIntersection(graph, Vector2.Zero);
    }

    [Fact]
    public void RemoveEdges_CrossingBranchRemovalRecanonicalizesRemainingRoad()
    {
        var graph = new RoadGraph();
        int horizontalID = SubmitLine(
            graph,
            new Vector2(-100f, 0f),
            new Vector2(100f, 0f));
        SubmitLine(graph, new Vector2(0f, -100f), new Vector2(0f, 100f));
        int[] verticalEdgeIDs = graph.GetAllEdges()
            .Where(IsVertical)
            .Select(edge => edge.ID)
            .ToArray();

        Assert.Equal(2, verticalEdgeIDs.Length);
        Assert.True(graph.RemoveEdges(verticalEdgeIDs));

        GraphEdge remaining = Assert.Single(graph.GetAllEdges());
        Assert.Equal(horizontalID, remaining.ID);
        Assert.Equal(2, graph.GetAllNodes().Count());
        Assert.DoesNotContain(graph.GetAllNodes(), node => node.Position == Vector2.Zero);
        var line = Assert.IsType<LineRoadGeometrySegment>(Assert.Single(remaining.GeometrySegments));
        Assert.Equal(new Vector2(-100f, 0f), line.Start);
        Assert.Equal(new Vector2(100f, 0f), line.End);
        graph.AssertInvariants();
    }

    [Fact]
    public void SubmitPolyline_ArbitraryAngleCollinearSegmentsCanonicalizeToOneLine()
    {
        var graph = new RoadGraph();

        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(30f, 20f),
            new Vector2(60f, 40f),
        ]).Success);

        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Single(edge.GeometrySegments);
        Assert.Empty(edge.Points);
    }

    [Fact]
    public void SubmitPolyline_SlightlyBentSegmentsRemainInsideOneEdge()
    {
        var graph = new RoadGraph();

        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(10f, 0f),
            new Vector2(20f, 0.01f),
        ]).Success);

        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Equal(2, edge.GeometrySegments.Count);
        Assert.Equal([new Vector2(10f, 0f)], edge.Points);
        Assert.DoesNotContain(graph.GetAllNodes(), node => node.Position == new Vector2(10f, 0f));
    }

    [Fact]
    public void GraphEdgePoints_CannotMutateGraphStateOutsideRoadGraphApi()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-200f, 0f),
            new Vector2(-100f, 0f),
            new Vector2(0f, 1f),
            new Vector2(100f, 1f),
        ]).Success);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        string stateBeforeMutation = RoadGraphTestCodec.CaptureJson(graph);

        Vector2[] exposedPoints = edge.Points;
        exposedPoints[0] = new Vector2(0f, 100f);

        Assert.Equal(stateBeforeMutation, RoadGraphTestCodec.CaptureJson(graph));
    }

    [Fact]
    public void RemoveEdge_CrossingRoad_PreservesDegreeThreeJunction()
    {
        var graph = new RoadGraph();
        SubmitLine(graph, new Vector2(-100f, 0f), new Vector2(100f, 0f));
        SubmitLine(graph, new Vector2(0f, -100f), new Vector2(0f, 100f));
        int edgeToRemove = Assert.Single(
            graph.GetAllEdges(),
            edge => IsVertical(edge) && edge.GeometrySegments[0].Bounds.Position.Y < 0f).ID;

        Assert.True(graph.RemoveEdge(edgeToRemove));

        GraphNode crossing = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceSquaredTo(Vector2.Zero) < 1e-4f);
        Assert.Equal(3, crossing.IncidenceCount);
        Assert.Equal(3, graph.GetAllEdges().Count());
        Assert.Null(graph.GetEdge(edgeToRemove));
        graph.AssertInvariants();
    }

    private static int SubmitLine(RoadGraph graph, Vector2 start, Vector2 end)
    {
        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [start, end]);
        Assert.True(result.Success, $"Submission failed with {result.Error}.");
        return graph.FindClosestEdge((start + end) * 0.5f, 0.01f)!.ID;
    }

    private static void AssertFourWayIntersection(RoadGraph graph, Vector2 position)
    {
        GraphNode crossing = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position.DistanceSquaredTo(position) < 1e-4f);
        Assert.Equal(4, crossing.IncidenceCount);
        Assert.Equal(4, graph.GetAllEdges().Count());
        graph.AssertInvariants();
    }

    private static bool IsVertical(GraphEdge edge) =>
        edge.GeometrySegments.All(segment =>
            Mathf.IsEqualApprox(segment.Start.X, 0f) &&
            Mathf.IsEqualApprox(segment.End.X, 0f));
}
