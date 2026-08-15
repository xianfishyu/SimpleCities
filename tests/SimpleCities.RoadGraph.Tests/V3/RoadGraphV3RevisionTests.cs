using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3RevisionTests
{
    [Fact]
    public void AddNode_ReturnsNewRevisionAndLeavesOldUnchanged()
    {
        RoadGraphV3Revision oldRevision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);

        Assert.True(oldRevision.TryAddNode(Vector2.Zero, out RoadGraphV3Revision newRevision, out int nodeID));

        Assert.Equal(0, nodeID);
        Assert.Empty(oldRevision.Nodes);
        Assert.Single(newRevision.Nodes);
        Assert.Equal(0, oldRevision.NextNodeID);
        Assert.Equal(1, newRevision.NextNodeID);
    }

    [Fact]
    public void AddEdge_OldRevisionUnchanged()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);

        Assert.True(revision.TryAddEdge(
            a,
            b,
            [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))],
            RoadType.Street,
            out RoadGraphV3Revision newRevision,
            out _));

        Assert.Empty(revision.Edges);
        Assert.Single(newRevision.Edges);
    }

    [Fact]
    public void RemoveEdge_RemovesOnlyInNewRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out int edgeID);

        Assert.True(revision.TryRemoveEdge(edgeID, out RoadGraphV3Revision newRevision));

        Assert.Single(revision.Edges);
        Assert.Empty(newRevision.Edges);
    }

    [Fact]
    public void ChangeRoadType_OldRevisionUnchanged()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out int edgeID);

        Assert.True(revision.TryChangeRoadType(edgeID, RoadType.Highway, out RoadGraphV3Revision newRevision));

        Assert.Equal(RoadType.Street, revision.Edges[edgeID].RoadType);
        Assert.Equal(RoadType.Highway, newRevision.Edges[edgeID].RoadType);
    }

    [Fact]
    public void CapacityExceeded_FailsWithoutMutation()
    {
        var capacity = RoadGraphCapacity.Default with
        {
            MaxNodes = 1,
            MaxEdges = 1,
            MaxTotalGeometrySegments = 1,
            MaxQueryFragments = 1,
            MaxBuckets = 1,
            MaxBucketReferences = 1,
            MaxMutationCandidates = 1,
            MaxID = 1,
        };
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(capacity);

        Assert.True(revision.TryAddNode(Vector2.Zero, out revision, out _));
        Assert.False(revision.TryAddNode(new Vector2(1f, 0f), out RoadGraphV3Revision unchanged, out _));

        Assert.Same(revision, unchanged);
        Assert.Single(revision.Nodes);
    }
}
