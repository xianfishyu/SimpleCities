using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3CanonicalizerTests
{
    [Fact]
    public void Canonicalize_MergesChain()
    {
        RoadGraphV3Revision revision = BuildChain();

        RoadGraphV3Revision result = RoadGraphV3Canonicalizer.Canonicalize(revision);

        Assert.Equal(2, result.Nodes.Count);
        RoadGraphV3Edge edge = Assert.Single(result.Edges.Values);
        Assert.Equal(0, edge.ID);
        Assert.True(edge.NodeAID < edge.NodeBID);
    }

    [Fact]
    public void Canonicalize_KeepsSemanticBoundary()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddNode(new Vector2(2f, 0f), out revision, out int c);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(b, c, [new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f))], RoadType.Dirt, out revision, out _);

        RoadGraphV3Revision result = RoadGraphV3Canonicalizer.Canonicalize(revision);

        Assert.Equal(3, result.Nodes.Count);
        Assert.Equal(2, result.Edges.Count);
    }

    [Fact]
    public void Canonicalize_IsIdempotent()
    {
        RoadGraphV3Revision revision = BuildChain();

        RoadGraphV3Revision once = RoadGraphV3Canonicalizer.Canonicalize(revision);
        RoadGraphV3Revision twice = RoadGraphV3Canonicalizer.Canonicalize(once);

        Assert.Equal(once.Nodes.Count, twice.Nodes.Count);
        Assert.Equal(once.Edges.Count, twice.Edges.Count);
        Assert.Equal(once.Edges.Keys.Single(), twice.Edges.Keys.Single());
    }

    [Fact]
    public void Canonicalize_SelfLoopKeepsSeam()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int node);
        revision.TryAddEdge(
            node,
            node,
            [
                new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
                new LineRoadGeometrySegment(new Vector2(1f, 0f), Vector2.Zero),
            ],
            RoadType.Street,
            out revision,
            out _);

        RoadGraphV3Revision result = RoadGraphV3Canonicalizer.Canonicalize(revision);

        Assert.Single(result.Nodes);
        RoadGraphV3Edge edge = Assert.Single(result.Edges.Values);
        Assert.True(edge.IsSelfLoop);
    }

    [Fact]
    public void Canonicalize_EmptyRevision_ReturnsEmpty()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);

        RoadGraphV3Revision result = RoadGraphV3Canonicalizer.Canonicalize(revision);

        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }

    private static RoadGraphV3Revision BuildChain()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddNode(new Vector2(2f, 0f), out revision, out int c);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(b, c, [new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f))], RoadType.Street, out revision, out _);
        return revision;
    }
}
