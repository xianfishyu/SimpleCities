using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphInvariantTests
{
    [Fact]
    public void DetachEdge_OnlyDisconnectsEdgeWithoutCleanupOrEvents()
    {
        var graph = new RoadGraph();
        int groupID = graph.AddRoad(Vector2.Zero, new Vector2(20f, 0f), []);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        int removedEvents = 0;
        graph.EdgeRemoved += _ => removedEvents++;

        graph.DetachEdge(edge);

        Assert.Null(graph.GetEdge(edge.ID));
        Assert.Equal(0, Assert.IsType<GraphNode>(graph.GetNode(edge.NodeA)).EdgeCount);
        Assert.Equal(0, Assert.IsType<GraphNode>(graph.GetNode(edge.NodeB)).EdgeCount);
        Assert.True(Assert.IsType<RoadGroup>(graph.GetGroup(groupID)).IsEmpty);
        Assert.Null(graph.FindClosestEdge(new Vector2(10f, 0f), 0.01f));
        Assert.Equal(0, removedEvents);
    }

    [Fact]
    public void RemoveEdge_PreservesCommittedGraphInvariants()
    {
        var graph = new RoadGraph();
        int groupID = graph.AddRoad(
            Vector2.Zero,
            new Vector2(20f, 0f),
            [new Vector2(10f, 5f)]);
        int edgeID = Assert.IsType<RoadGroup>(graph.GetGroup(groupID)).EdgeIDs.First();

        Assert.True(graph.RemoveEdge(edgeID));

        graph.AssertInvariants();
    }

    [Fact]
    public void RemoveRoadGroup_PreservesCommittedGraphInvariants()
    {
        var graph = new RoadGraph();
        int removedGroupID = graph.AddRoad(Vector2.Zero, new Vector2(20f, 5f), []);
        graph.AddRoad(new Vector2(100f, 0f), new Vector2(120f, 5f), []);

        Assert.True(graph.RemoveRoadGroup(removedGroupID));

        graph.AssertInvariants();
    }

    [Fact]
    public void SplitEdgeAtGeometryParameters_PreservesCommittedGraphInvariants()
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult submitted = graph.SubmitPath(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                Vector2.Zero,
                new Vector2(0f, 10f),
                new Vector2(20f, 10f),
                new Vector2(20f, 0f)),
        ]));
        int edgeID = Assert.Single(submitted.Changes.CreatedEdgeIDs);

        Assert.True(graph.SplitEdgeAtGeometryParameters(
            edgeID,
            [new EdgeGeometrySplitPoint(0, 0.5f)]));

        graph.AssertInvariants();
    }

    [Fact]
    public void SubmitPolyline_CollinearMerge_PreservesCommittedGraphInvariants()
    {
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPolyline([
            Vector2.Zero,
            new Vector2(10f, 5f),
            new Vector2(20f, 10f),
        ]);

        Assert.True(result.Success);
        Assert.Single(graph.GetAllEdges());
        graph.AssertInvariants();
    }

    [Fact]
    public void SubmitPolyline_RejectedPaths_PreserveCommittedGraphInvariants()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline([Vector2.Zero, new Vector2(20f, 0f)]).Success);

        RoadPathSubmissionResult invalid = graph.SubmitPolyline([
            Vector2.Zero,
            new Vector2(float.NaN, 0f),
        ]);
        Assert.False(invalid.Success);
        graph.AssertInvariants();

        RoadPathSubmissionResult covered = graph.SubmitPolyline([
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]);
        Assert.False(covered.Success);
        Assert.Equal(RoadPathSubmissionError.FullyCovered, covered.Error);
        graph.AssertInvariants();
    }
}
