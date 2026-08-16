using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3FacadeTests
{
    [Fact]
    public void AddNode_IncrementsSequenceAndRevision()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        GraphStateToken before = facade.CurrentToken;

        Assert.True(facade.TryAddNode(Vector2.Zero, out RoadGraphV3ChangeSummary summary, out int nodeID));

        Assert.Equal(0, before.ChangeSequence);
        Assert.Equal(1, summary.ChangeSequence);
        Assert.Equal(1, facade.CurrentToken.DomainRevisionID);
        Assert.Equal(1, facade.CurrentToken.ChangeSequence);
        Assert.True(facade.Revision.Nodes.ContainsKey(nodeID));
    }

    [Fact]
    public void AddEdge_ProducesCreatedEdgeSummary()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out int a);
        facade.TryAddNode(new Vector2(1f, 0f), out _, out int b);

        Assert.True(facade.TryAddEdge(
            a,
            b,
            [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))],
            RoadType.Street,
            out RoadGraphV3ChangeSummary summary,
            out int edgeID));

        Assert.Equal(new[] { edgeID }, summary.CreatedEdgeIDs);
        Assert.False(summary.IsFullReset);
    }

    [Fact]
    public void RemoveEdge_ProducesRemovedEdgeSummary()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out int a);
        facade.TryAddNode(new Vector2(1f, 0f), out _, out int b);
        facade.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out _, out int edgeID);

        Assert.True(facade.TryRemoveEdge(edgeID, out RoadGraphV3ChangeSummary summary));

        Assert.Equal(new[] { edgeID }, summary.RemovedEdgeIDs);
        Assert.False(facade.Revision.Edges.ContainsKey(edgeID));
    }

    [Fact]
    public void ChangeRoadType_ProducesUpdatedEdgeSummary()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out int a);
        facade.TryAddNode(new Vector2(1f, 0f), out _, out int b);
        facade.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out _, out int edgeID);

        Assert.True(facade.TryChangeRoadType(edgeID, RoadType.Highway, out RoadGraphV3ChangeSummary summary));

        Assert.Equal(new[] { edgeID }, summary.UpdatedEdgeIDs);
        Assert.Equal(RoadType.Highway, facade.Revision.Edges[edgeID].RoadType);
    }

    [Fact]
    public void FullReset_CreatesNewLineage()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default), lineageID: 1);
        facade.TryAddNode(Vector2.Zero, out _, out _);
        long sequenceBefore = facade.CurrentToken.ChangeSequence;

        facade.ReplaceWithFullReset(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default), newLineageID: 99);

        Assert.Equal(99, facade.LineageID);
        Assert.Equal(99, facade.CurrentToken.LineageID);
        Assert.Equal(0, facade.CurrentToken.DomainRevisionID);
        Assert.Equal(sequenceBefore + 1, facade.CurrentToken.ChangeSequence);
    }

    [Fact]
    public void FailedMutation_DoesNotChangeToken()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        GraphStateToken before = facade.CurrentToken;

        Assert.False(facade.TryRemoveEdge(123, out _));

        Assert.Equal(before, facade.CurrentToken);
    }

    [Fact]
    public void Diagnostics_Empty_ReturnsZeroCounts()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));

        RoadGraphV3Diagnostics diagnostics = facade.Diagnostics;

        Assert.Equal(0, diagnostics.NodeCount);
        Assert.Equal(0, diagnostics.EdgeCount);
        Assert.Equal(0, diagnostics.GeometrySegmentCount);
        Assert.Equal(0, diagnostics.SelfLoopCount);
        Assert.Equal(0, diagnostics.ParallelEdgeCount);
        Assert.Equal(0, diagnostics.QueryFragmentCount);
        Assert.True(diagnostics.IsValid);
    }

    [Fact]
    public void Diagnostics_AfterAddEdge_ReflectsCounts()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out int a);
        facade.TryAddNode(new Vector2(1f, 0f), out _, out int b);
        facade.TryAddEdge(
            a,
            b,
            [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))],
            RoadType.Street,
            out _,
            out _);

        RoadGraphV3Diagnostics diagnostics = facade.Diagnostics;

        Assert.Equal(2, diagnostics.NodeCount);
        Assert.Equal(1, diagnostics.EdgeCount);
        Assert.Equal(1, diagnostics.GeometrySegmentCount);
        Assert.Equal(0, diagnostics.SelfLoopCount);
        Assert.Equal(0, diagnostics.ParallelEdgeCount);
        Assert.Equal(1, diagnostics.QueryFragmentCount);
        Assert.Equal(facade.CurrentToken.ChangeSequence, diagnostics.ChangeSequence);
    }

    [Fact]
    public void Diagnostics_ParallelEdges_CountsPairs()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out int a);
        facade.TryAddNode(new Vector2(1f, 0f), out _, out int b);
        facade.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out _, out _);
        facade.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Highway, out _, out _);

        Assert.Equal(1, facade.Diagnostics.ParallelEdgeCount);
    }

    [Fact]
    public void Diagnostics_CurveEdge_CountsSingleWholeBoundsFragment()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out int a);
        facade.TryAddNode(new Vector2(2f, 1f), out _, out int b);
        facade.TryAddEdge(
            a,
            b,
            [new CubicBezierRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(2f, 1f))],
            RoadType.Street,
            out _,
            out _);

        Assert.Equal(1, facade.Diagnostics.QueryFragmentCount);
    }

    [Fact]
    public void Diagnostics_AfterFullReset_ResetsCountsAndAdvancesSequence()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out _);
        long sequenceBefore = facade.CurrentToken.ChangeSequence;

        facade.ReplaceWithFullReset(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default), 7);

        Assert.Equal(0, facade.Diagnostics.NodeCount);
        Assert.Equal(0, facade.Diagnostics.EdgeCount);
        Assert.Equal(0, facade.Diagnostics.ParallelEdgeCount);
        Assert.Equal(sequenceBefore + 1, facade.Diagnostics.ChangeSequence);
    }
}
