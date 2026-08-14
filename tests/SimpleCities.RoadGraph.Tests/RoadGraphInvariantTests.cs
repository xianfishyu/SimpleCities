using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphInvariantTests
{
    [Fact]
    public void RemoveEdge_UsesTheCommittedTransactionBoundary()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(20f, 0f)]).Success);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        Assert.True(graph.RemoveEdge(edge.ID));

        Assert.Null(graph.GetEdge(edge.ID));
        Assert.Null(graph.GetNode(edge.NodeA));
        Assert.Null(graph.GetNode(edge.NodeB));
        Assert.Null(graph.FindClosestEdge(new Vector2(10f, 0f), 0.01f));
        Assert.Equal(1, changedEvents);
        graph.AssertInvariants();
    }

    [Fact]
    public void RemoveEdge_PreservesCommittedGraphInvariants()
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(10f, 5f),
            new Vector2(20f, 0f),
        ]);
        int edgeID = Assert.Single(result.Changes.CreatedEdgeIDs);

        Assert.True(graph.RemoveEdge(edgeID));

        graph.AssertInvariants();
    }

    [Fact]
    public void RemoveEdges_PreservesCommittedGraphInvariants()
    {
        var graph = new RoadGraph();
        int removedEdgeID = Assert.Single(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 5f),
        ]).Changes.CreatedEdgeIDs);
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(100f, 0f),
            new Vector2(120f, 5f),
        ]).Success);

        Assert.True(graph.RemoveEdges([removedEdgeID]));

        graph.AssertInvariants();
    }

    [Fact]
    public void SplitEdgeAtGeometryParameters_PreservesCommittedGraphInvariants()
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult submitted = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                Vector2.Zero,
                new Vector2(0f, 10f),
                new Vector2(20f, 10f),
                new Vector2(20f, 0f)),
        ]), RoadType.Street));
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

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
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
        Assert.True(graph.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(20f, 0f)]).Success);

        RoadPathSubmissionResult invalid = graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(float.NaN, 0f),
        ]);
        Assert.False(invalid.Success);
        graph.AssertInvariants();

        RoadPathSubmissionResult covered = graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]);
        Assert.False(covered.Success);
        Assert.Equal(RoadPathSubmissionError.FullyCovered, covered.Error);
        graph.AssertInvariants();
    }
}
