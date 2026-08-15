using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3DeltaBuilderTests
{
    [Fact]
    public void BuildDelta_ChainMerge_ProducesRemovedNodeAndMergedEdge()
    {
        RoadGraphV3Revision before = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        before.TryAddNode(Vector2.Zero, out before, out int a);
        before.TryAddNode(new Vector2(1f, 0f), out before, out int b);
        before.TryAddNode(new Vector2(2f, 0f), out before, out int c);
        before.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out before, out _);
        before.TryAddEdge(b, c, [new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f))], RoadType.Street, out before, out _);

        RoadGraphV3Revision after = RoadGraphV3Canonicalizer.Canonicalize(before);

        RoadGraphV3Delta delta = RoadGraphV3DeltaBuilder.BuildDelta(before, after, 0, 1);

        Assert.Contains(delta.NodeChanges, change => change.IsRemoved);
        Assert.Contains(delta.EdgeChanges, change => change.IsRemoved);
        Assert.Contains(delta.EdgeChanges, change => change.IsUpdated);
    }

    [Fact]
    public void BuildDelta_NoChanges_ReturnsEmptyDelta()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out _);

        RoadGraphV3Delta delta = RoadGraphV3DeltaBuilder.BuildDelta(revision, revision, 1, 1);

        Assert.True(delta.IsEmpty);
    }
}
