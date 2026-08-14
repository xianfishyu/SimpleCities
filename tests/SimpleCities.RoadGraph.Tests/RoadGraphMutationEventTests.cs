using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphMutationEventTests
{
    [Fact]
    public void RemoveEdges_EventsObserveTheFinalCommittedGraph()
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult first = graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(10f, 5f),
            new Vector2(20f, 0f),
        ]);
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(100f, 0f),
            new Vector2(120f, 0f),
        ]).Success);
        int[] removedEdgeIDs = [Assert.Single(first.Changes.CreatedEdgeIDs)];
        var observed = new List<RoadGraphChangedEvent>();
        graph.GraphChanged += change =>
        {
            graph.AssertInvariants();
            Assert.All(removedEdgeIDs, edgeID => Assert.Null(graph.GetEdge(edgeID)));
            observed.Add(change);
        };

        Assert.True(graph.RemoveEdges(removedEdgeIDs));

        RoadGraphChangedEvent change = Assert.Single(observed);
        Assert.Equal(removedEdgeIDs, change.Changes.RemovedEdgeIDs);
        Assert.Equal(removedEdgeIDs, change.Delta.Edges.Select(edge => edge.ID));
        Assert.All(change.Delta.Edges, edge =>
        {
            Assert.NotNull(edge.Before);
            Assert.Null(edge.After);
        });
    }

    [Fact]
    public void SplitEdgeWithoutSemanticBoundary_EventsObserveCanonicalReplacementAfterCommit()
    {
        var graph = new RoadGraph();
        int edgeID = Assert.Single(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                Vector2.Zero,
                new Vector2(0f, 10f),
                new Vector2(20f, 10f),
                new Vector2(20f, 0f)),
        ]), RoadType.Street)).Changes.CreatedEdgeIDs);
        var events = new List<RoadGraphChangedEvent>();
        graph.GraphChanged += change =>
        {
            graph.AssertInvariants();
            events.Add(change);
        };

        Assert.True(graph.SplitEdgeAtGeometryParameters(
            edgeID,
            [new EdgeGeometrySplitPoint(0, 0.5f)]));

        Assert.Single(graph.GetAllEdges());
        Assert.Equal(edgeID, Assert.Single(graph.GetAllEdges()).ID);
        RoadGraphChangedEvent change = Assert.Single(events);
        Assert.Equal([edgeID], change.Changes.UpdatedEdgeIDs);
        Assert.Empty(change.Changes.CreatedEdgeIDs);
        Assert.Empty(change.Changes.RemovedEdgeIDs);
        RoadGraphEntityDelta<GraphEdge> edgeDelta = Assert.Single(change.Delta.Edges);
        Assert.Equal(edgeID, edgeDelta.ID);
        Assert.NotSame(edgeDelta.Before, edgeDelta.After);
    }

    [Fact]
    public void CollinearMerge_EventsObserveReplacementTopologyAfterCommit()
    {
        var graph = new RoadGraph();
        var events = new List<RoadGraphChangedEvent>();
        graph.GraphChanged += change =>
        {
            graph.AssertInvariants();
            events.Add(change);
        };

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(10f, 5f),
            new Vector2(20f, 10f),
        ]);

        Assert.True(result.Success);
        Assert.Single(graph.GetAllEdges());
        RoadGraphChangedEvent change = Assert.Single(events);
        Assert.Equal(result.Changes.CreatedEdgeIDs, change.Changes.CreatedEdgeIDs);
        Assert.Empty(change.Changes.UpdatedEdgeIDs);
        Assert.Equal(1, change.StateToken.ChangeSequence);
    }
}
