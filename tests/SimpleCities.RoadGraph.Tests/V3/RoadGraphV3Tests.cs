using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3Tests
{
    [Fact]
    public void AddEdge_SelfLoop_RegistersTwoIncidences()
    {
        var graph = new RoadGraphV3();
        int node = graph.AddNode(Vector2.Zero);
        int edge = graph.AddEdge(
            node,
            node,
            [
                new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
                new LineRoadGeometrySegment(new Vector2(1f, 0f), Vector2.Zero),
            ],
            RoadType.Street);

        Assert.Equal(2, graph.GetDegree(node));
        Assert.Equal(1, graph.GetIncidentEdgeCount(node));
        Assert.Equal(2, graph.GetIncidences(node).Count);
        Assert.Contains(graph.GetIncidences(node), incidence => incidence.EdgeID == edge && incidence.Endpoint == EdgeEndpoint.A);
        Assert.Contains(graph.GetIncidences(node), incidence => incidence.EdgeID == edge && incidence.Endpoint == EdgeEndpoint.B);
    }

    [Fact]
    public void AddEdge_ParallelEdges_BothVisibleAndDistinct()
    {
        var graph = new RoadGraphV3();
        int a = graph.AddNode(Vector2.Zero);
        int b = graph.AddNode(new Vector2(1f, 0f));
        int first = graph.AddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street);
        int second = graph.AddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street);

        Assert.Equal(2, graph.GetAllEdges().Count);
        Assert.Equal(2, graph.GetDegree(a));
        Assert.Equal(2, graph.GetIncidentEdgeCount(a));
        Assert.Equal(2, graph.GetDegree(b));
        Assert.Equal(2, graph.GetIncidentEdgeCount(b));
        Assert.Equal(first, graph.GetAllEdges()[0].ID);
        Assert.Equal(second, graph.GetAllEdges()[1].ID);
    }

    [Fact]
    public void RemoveEdge_SelfLoop_RemovesBothIncidences()
    {
        var graph = new RoadGraphV3();
        int node = graph.AddNode(Vector2.Zero);
        int edge = graph.AddEdge(
            node,
            node,
            [
                new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
                new LineRoadGeometrySegment(new Vector2(1f, 0f), Vector2.Zero),
            ],
            RoadType.Dirt);

        Assert.True(graph.RemoveEdge(edge));

        Assert.Null(graph.GetEdge(edge));
        Assert.Equal(0, graph.GetDegree(node));
        Assert.Empty(graph.GetIncidences(node));
    }

    [Fact]
    public void RemoveEdge_NormalEdge_RemovesIncidenceFromEachEndpoint()
    {
        var graph = new RoadGraphV3();
        int a = graph.AddNode(Vector2.Zero);
        int b = graph.AddNode(new Vector2(1f, 0f));
        int edge = graph.AddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street);

        Assert.True(graph.RemoveEdge(edge));

        Assert.Equal(0, graph.GetDegree(a));
        Assert.Equal(0, graph.GetDegree(b));
    }

    [Fact]
    public void AddEdge_MissingNode_Throws()
    {
        var graph = new RoadGraphV3();
        int a = graph.AddNode(Vector2.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            graph.AddEdge(a, 999, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street));
    }

    [Fact]
    public void GetNeighborIDs_ReturnsDistinctNeighbors()
    {
        var graph = new RoadGraphV3();
        int self = graph.AddNode(Vector2.Zero);
        int other = graph.AddNode(new Vector2(1f, 0f));
        graph.AddEdge(
            self,
            self,
            [
                new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
                new LineRoadGeometrySegment(new Vector2(1f, 0f), Vector2.Zero),
            ],
            RoadType.Street);
        graph.AddEdge(self, other, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street);

        Assert.Equal(new[] { self, other }, graph.GetNeighborIDs(self));
    }

    [Fact]
    public void AddNode_NormalizesNegativeZero()
    {
        var graph = new RoadGraphV3();

        int node = graph.AddNode(new Vector2(-0f, 0f));

        Assert.Equal(0, BitConverter.SingleToInt32Bits(graph.GetNode(node)!.Position.X));
    }
}
