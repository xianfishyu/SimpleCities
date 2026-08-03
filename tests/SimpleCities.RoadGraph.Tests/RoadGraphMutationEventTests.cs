using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphMutationEventTests
{
    [Fact]
    public void RemoveRoadGroup_EventsObserveTheFinalCommittedGraph()
    {
        var graph = new RoadGraph();
        int groupID = graph.AddRoad(
            Vector2.Zero,
            new Vector2(20f, 0f),
            [new Vector2(10f, 5f)]);
        graph.AddRoad(new Vector2(100f, 0f), new Vector2(120f, 0f), []);
        int[] removedEdgeIDs = Assert.IsType<RoadGroup>(graph.GetGroup(groupID))
            .EdgeIDs.Order().ToArray();
        var observed = new List<int>();
        graph.EdgeRemoved += edge =>
        {
            graph.AssertInvariants();
            Assert.Null(graph.GetGroup(groupID));
            Assert.All(removedEdgeIDs, edgeID => Assert.Null(graph.GetEdge(edgeID)));
            observed.Add(edge.ID);
        };

        Assert.True(graph.RemoveRoadGroup(groupID));

        Assert.Equal(removedEdgeIDs, observed);
    }

    [Fact]
    public void SplitEdge_EventsObserveReplacementTopologyAfterCommit()
    {
        var graph = new RoadGraph();
        int edgeID = Assert.Single(graph.SubmitPath(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                Vector2.Zero,
                new Vector2(0f, 10f),
                new Vector2(20f, 10f),
                new Vector2(20f, 0f)),
        ])).Changes.CreatedEdgeIDs);
        var events = new List<string>();
        graph.EdgeRemoved += edge =>
        {
            graph.AssertInvariants();
            events.Add($"removed:{edge.ID}");
        };
        graph.EdgeAdded += edge =>
        {
            graph.AssertInvariants();
            events.Add($"added:{edge.ID}");
        };

        Assert.True(graph.SplitEdgeAtGeometryParameters(
            edgeID,
            [new EdgeGeometrySplitPoint(0, 0.5f)]));

        Assert.Equal(3, events.Count);
        Assert.Equal($"removed:{edgeID}", events[0]);
        Assert.All(events.Skip(1), item => Assert.StartsWith("added:", item));
    }

    [Fact]
    public void CollinearMerge_EventsObserveReplacementTopologyAfterCommit()
    {
        var graph = new RoadGraph();
        var events = new List<string>();
        graph.EdgeRemoved += edge =>
        {
            graph.AssertInvariants();
            events.Add($"removed:{edge.ID}");
        };
        graph.EdgeAdded += edge =>
        {
            graph.AssertInvariants();
            events.Add($"added:{edge.ID}");
        };

        RoadPathSubmissionResult result = graph.SubmitPolyline([
            Vector2.Zero,
            new Vector2(10f, 5f),
            new Vector2(20f, 10f),
        ]);

        Assert.True(result.Success);
        Assert.Single(graph.GetAllEdges());
        Assert.Equal(5, events.Count);
        Assert.StartsWith("removed:", events[^3]);
        Assert.StartsWith("removed:", events[^2]);
        Assert.StartsWith("added:", events[^1]);
    }
}
