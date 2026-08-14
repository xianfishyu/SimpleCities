using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphEncapsulationTests
{
    [Fact]
    public void GetAllCollections_ReturnStableSnapshotsAfterGraphMutation()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(10f, 0f)]).Success);
        int firstEdgeID = Assert.Single(graph.GetAllEdges()).ID;
        IEnumerable<GraphNode> nodes = graph.GetAllNodes();
        IEnumerable<GraphEdge> edges = graph.GetAllEdges();

        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(100f, 0f),
            new Vector2(110f, 0f),
        ]).Success);
        Assert.True(graph.RemoveEdge(firstEdgeID));

        Assert.Equal(2, nodes.Count());
        Assert.Single(edges);
    }

    [Fact]
    public void NodeAndEdgeCollections_CannotMutateGraphState()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(5f, 5f),
            new Vector2(10f, 0f),
        ]).Success);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        GraphNode node = Assert.IsType<GraphNode>(graph.GetNode(edge.NodeA));
        string stateBefore = RoadGraphTestCodec.CaptureJson(graph);

        var nodeIncidences = Assert.IsAssignableFrom<ICollection<EdgeIncidence>>(node.Incidences);
        Assert.Throws<NotSupportedException>(() => nodeIncidences.Clear());
        Vector2[] points = edge.Points;
        points[0] = new Vector2(float.MaxValue, float.MaxValue);

        Assert.Equal(stateBefore, RoadGraphTestCodec.CaptureJson(graph));
        graph.AssertInvariants();
    }

    [Fact]
    public void GetFullPath_MissingEndpoint_ThrowsInsteadOfReturningPartialPath()
    {
        var edge = new GraphEdge(RoadType.Street,
            2,
            0,
            1,
            [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f))]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => edge.GetFullPath(nodeID => nodeID == 0 ? new GraphNode(0, Vector2.Zero) : null));

        Assert.Contains("endpoint nodes 0 and 1", exception.Message);
    }
}
