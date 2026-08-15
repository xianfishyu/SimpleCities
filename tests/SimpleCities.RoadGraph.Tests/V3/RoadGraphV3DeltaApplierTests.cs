using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3DeltaApplierTests
{
    [Fact]
    public void Apply_CreatedNode_AddsToNewRevisionAndLeavesOldUnchanged()
    {
        RoadGraphV3Revision oldRevision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        var node = new RoadGraphV3Node(5, new Vector2(2f, 3f));
        var delta = new RoadGraphV3Delta(
            0,
            1,
            [new RoadGraphV3EntityChange<RoadGraphV3Node>(null, node)],
            []);

        Assert.True(RoadGraphV3DeltaApplier.TryApply(oldRevision, delta, out RoadGraphV3Revision newRevision));

        Assert.Empty(oldRevision.Nodes);
        Assert.True(newRevision.Nodes.ContainsKey(5));
        Assert.Equal(6, newRevision.NextNodeID);
    }

    [Fact]
    public void Apply_RemovedNode_RemovesFromNewRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(new Vector2(1f, 2f), out revision, out int nodeID);
        var node = revision.Nodes[nodeID];
        var delta = new RoadGraphV3Delta(
            1,
            2,
            [new RoadGraphV3EntityChange<RoadGraphV3Node>(node, null)],
            []);

        Assert.True(RoadGraphV3DeltaApplier.TryApply(revision, delta, out RoadGraphV3Revision newRevision));

        Assert.True(revision.Nodes.ContainsKey(nodeID));
        Assert.False(newRevision.Nodes.ContainsKey(nodeID));
    }

    [Fact]
    public void Apply_UpdatedEdge_ChangesTypeInNewRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out int edgeID);
        RoadGraphV3Edge before = revision.Edges[edgeID];
        var after = new RoadGraphV3Edge(edgeID, a, b, before.Geometry, RoadType.Highway);
        var delta = new RoadGraphV3Delta(
            2,
            3,
            [],
            [new RoadGraphV3EntityChange<RoadGraphV3Edge>(before, after)]);

        Assert.True(RoadGraphV3DeltaApplier.TryApply(revision, delta, out RoadGraphV3Revision newRevision));

        Assert.Equal(RoadType.Street, revision.Edges[edgeID].RoadType);
        Assert.Equal(RoadType.Highway, newRevision.Edges[edgeID].RoadType);
    }

    [Fact]
    public void Apply_InverseDelta_RestoresOriginalShape()
    {
        RoadGraphV3Revision empty = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        var node = new RoadGraphV3Node(1, Vector2.Zero);
        var addDelta = new RoadGraphV3Delta(0, 1, [new RoadGraphV3EntityChange<RoadGraphV3Node>(null, node)], []);

        Assert.True(RoadGraphV3DeltaApplier.TryApply(empty, addDelta, out RoadGraphV3Revision withNode));
        Assert.True(RoadGraphV3DeltaApplier.TryApply(withNode, addDelta.Invert(), out RoadGraphV3Revision restored));

        Assert.Empty(restored.Nodes);
        Assert.Empty(restored.Edges);
    }

    [Fact]
    public void Apply_FailsWhenRemovingMissingEntity_ReturnsSameRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        var missing = new RoadGraphV3Node(99, Vector2.Zero);
        var delta = new RoadGraphV3Delta(
            0,
            1,
            [new RoadGraphV3EntityChange<RoadGraphV3Node>(missing, null)],
            []);

        Assert.False(RoadGraphV3DeltaApplier.TryApply(revision, delta, out RoadGraphV3Revision result));

        Assert.Same(revision, result);
    }
}
