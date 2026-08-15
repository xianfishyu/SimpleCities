using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphCanonicalizerTests
{
    [Fact]
    public void Canonicalize_MergesChainOfTwoEdgesIntoOne()
    {
        var graph = new RoadCanonicalGraph(
            [
                new RoadCanonicalNode(1, Vector2.Zero),
                new RoadCanonicalNode(2, new Vector2(1f, 0f)),
                new RoadCanonicalNode(3, new Vector2(2f, 0f)),
            ],
            [
                new RoadCanonicalEdge(10, 1, 2, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], "street"),
                new RoadCanonicalEdge(20, 2, 3, [new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f))], "street"),
            ]);

        RoadCanonicalGraph result = RoadGraphCanonicalizer.Canonicalize(graph);

        Assert.Equal(2, result.Nodes.Count);
        Assert.Equal(new[] { 1, 3 }, result.Nodes.Select(node => node.ID).ToArray());
        RoadCanonicalEdge edge = Assert.Single(result.Edges);
        Assert.Equal(10, edge.ID);
        Assert.Equal(1, edge.NodeAID);
        Assert.Equal(3, edge.NodeBID);
        var line = Assert.IsType<LineRoadGeometrySegment>(Assert.Single(edge.Geometry));
        Assert.Equal(Vector2.Zero, line.Start);
        Assert.Equal(new Vector2(2f, 0f), line.End);
    }

    [Fact]
    public void Canonicalize_KeepsSemanticBoundaryWhenMergeKeyDiffers()
    {
        var graph = new RoadCanonicalGraph(
            [
                new RoadCanonicalNode(1, Vector2.Zero),
                new RoadCanonicalNode(2, new Vector2(1f, 0f)),
                new RoadCanonicalNode(3, new Vector2(2f, 0f)),
            ],
            [
                new RoadCanonicalEdge(10, 1, 2, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], "street"),
                new RoadCanonicalEdge(20, 2, 3, [new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f))], "dirt"),
            ]);

        RoadCanonicalGraph result = RoadGraphCanonicalizer.Canonicalize(graph);

        Assert.Equal(3, result.Nodes.Count);
        Assert.Equal(2, result.Edges.Count);
    }

    [Fact]
    public void Canonicalize_KeepsLoopSeam()
    {
        var graph = new RoadCanonicalGraph(
            [
                new RoadCanonicalNode(1, Vector2.Zero),
            ],
            [
                new RoadCanonicalEdge(
                    7,
                    1,
                    1,
                    [
                        new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
                        new LineRoadGeometrySegment(new Vector2(1f, 0f), Vector2.Zero),
                    ],
                    "street"),
            ]);

        RoadCanonicalGraph result = RoadGraphCanonicalizer.Canonicalize(graph);

        Assert.Single(result.Nodes);
        RoadCanonicalEdge edge = Assert.Single(result.Edges);
        Assert.True(edge.IsSelfLoop);
        Assert.Equal(1, edge.NodeAID);
        Assert.Equal(1, edge.NodeBID);
    }

    [Fact]
    public void Canonicalize_MergesTwoEdgesIntoSelfLoopWhenFarEndpointsSame()
    {
        var graph = new RoadCanonicalGraph(
            [
                new RoadCanonicalNode(1, Vector2.Zero),
                new RoadCanonicalNode(2, new Vector2(1f, 0f)),
            ],
            [
                new RoadCanonicalEdge(10, 1, 2, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], "street"),
                new RoadCanonicalEdge(20, 1, 2, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], "street"),
            ]);

        RoadCanonicalGraph result = RoadGraphCanonicalizer.Canonicalize(graph);

        Assert.Single(result.Nodes);
        RoadCanonicalEdge edge = Assert.Single(result.Edges);
        Assert.Equal(10, edge.ID);
        Assert.True(edge.IsSelfLoop);
        Assert.Equal(1, edge.NodeAID);
        Assert.Equal(1, edge.NodeBID);
    }

    [Fact]
    public void Canonicalize_NonLoopResultUsesLowerNodeIdAsA()
    {
        var graph = new RoadCanonicalGraph(
            [
                new RoadCanonicalNode(3, new Vector2(2f, 0f)),
                new RoadCanonicalNode(2, new Vector2(1f, 0f)),
                new RoadCanonicalNode(1, Vector2.Zero),
            ],
            [
                new RoadCanonicalEdge(30, 3, 2, [new LineRoadGeometrySegment(new Vector2(2f, 0f), new Vector2(1f, 0f))], "street"),
                new RoadCanonicalEdge(20, 2, 1, [new LineRoadGeometrySegment(new Vector2(1f, 0f), Vector2.Zero)], "street"),
            ]);

        RoadCanonicalGraph result = RoadGraphCanonicalizer.Canonicalize(graph);

        RoadCanonicalEdge edge = Assert.Single(result.Edges);
        Assert.True(edge.NodeAID < edge.NodeBID);
        Assert.Equal(20, edge.ID);
    }

    [Fact]
    public void Canonicalize_IsIdempotent()
    {
        var graph = new RoadCanonicalGraph(
            [
                new RoadCanonicalNode(1, Vector2.Zero),
                new RoadCanonicalNode(2, new Vector2(1f, 0f)),
                new RoadCanonicalNode(3, new Vector2(2f, 0f)),
            ],
            [
                new RoadCanonicalEdge(10, 1, 2, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], "street"),
                new RoadCanonicalEdge(20, 2, 3, [new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f))], "street"),
            ]);

        RoadCanonicalGraph once = RoadGraphCanonicalizer.Canonicalize(graph);
        RoadCanonicalGraph twice = RoadGraphCanonicalizer.Canonicalize(once);

        Assert.Equal(once.Nodes.Count, twice.Nodes.Count);
        Assert.Equal(once.Edges.Count, twice.Edges.Count);
        Assert.Equal(once.Edges[0].ID, twice.Edges[0].ID);
        Assert.Equal(once.Edges[0].NodeAID, twice.Edges[0].NodeAID);
        Assert.Equal(once.Edges[0].NodeBID, twice.Edges[0].NodeBID);
    }

    [Fact]
    public void Canonicalize_EmptyGraph_ReturnsEmpty()
    {
        RoadCanonicalGraph result = RoadGraphCanonicalizer.Canonicalize(new RoadCanonicalGraph([], []));

        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }
}
