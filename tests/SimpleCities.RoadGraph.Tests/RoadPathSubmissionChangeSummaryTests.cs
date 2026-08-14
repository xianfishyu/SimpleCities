using Godot;

namespace SimpleCities.Tests;

public sealed class RoadPathSubmissionChangeSummaryTests
{
    [Fact]
    public void RoadPathCopiesTheCallerOwnedGeometryList()
    {
        RoadGeometrySegment?[] callerOwned =
        [
            new LineRoadGeometrySegment(Vector2.Zero, Vector2.Right),
        ];

        var path = new RoadPath(callerOwned);
        callerOwned[0] = null;

        Assert.IsType<LineRoadGeometrySegment>(Assert.Single(path.Segments));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RoadGeometrySegment?>)path.Segments)[0] = null);
    }

    [Fact]
    public void SuccessfulPolylineReportsEveryCreatedEntity()
    {
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street,
            [Vector2.Zero, new Vector2(10f, 0f)]);

        Assert.True(result.Success);
        Assert.True(result.Changes.HasChanges);
        Assert.Equal(graph.GetAllNodes().Select(node => node.ID).Order(), result.Changes.CreatedNodeIDs);
        Assert.Equal(graph.GetAllEdges().Select(edge => edge.ID).Order(), result.Changes.CreatedEdgeIDs);
        Assert.Empty(result.Changes.RemovedNodeIDs);
        Assert.Empty(result.Changes.RemovedEdgeIDs);
    }

    [Fact]
    public void CrossingPolylineReportsReplacedAndCreatedEdgesInSortedOrder()
    {
        var graph = new RoadGraph();
        graph.SubmitPolyline(RoadType.Street, [new Vector2(-10f, 0f), new Vector2(10f, 0f)]);
        int originalEdgeID = Assert.Single(graph.GetAllEdges()).ID;

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street,
            [new Vector2(0f, -10f), new Vector2(0f, 10f)]);

        Assert.True(result.Success);
        Assert.DoesNotContain(originalEdgeID, result.Changes.RemovedEdgeIDs);
        Assert.NotNull(graph.GetEdge(originalEdgeID));
        Assert.Equal(result.Changes.CreatedEdgeIDs.Order(), result.Changes.CreatedEdgeIDs);
        Assert.All(result.Changes.CreatedEdgeIDs, id => Assert.NotNull(graph.GetEdge(id)));
    }

    [Fact]
    public void RejectedSubmissionHasNoChangeSummary()
    {
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [Vector2.Zero]);

        Assert.False(result.Success);
        Assert.False(result.Changes.HasChanges);
        Assert.Empty(result.Changes.CreatedNodeIDs);
        Assert.Empty(result.Changes.CreatedEdgeIDs);
        Assert.Empty(result.Changes.RemovedNodeIDs);
        Assert.Empty(result.Changes.RemovedEdgeIDs);
    }
}
