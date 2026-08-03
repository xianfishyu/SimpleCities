using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphEncapsulationTests
{
    [Fact]
    public void GetAllCollections_ReturnStableSnapshotsAfterGraphMutation()
    {
        var graph = new RoadGraph();
        int firstGroupID = graph.AddRoad(Vector2.Zero, new Vector2(10f, 0f), []);
        IEnumerable<GraphNode> nodes = graph.GetAllNodes();
        IEnumerable<GraphEdge> edges = graph.GetAllEdges();
        IEnumerable<RoadGroup> groups = graph.GetAllGroups();

        graph.AddRoad(new Vector2(100f, 0f), new Vector2(110f, 0f), []);
        Assert.True(graph.RemoveRoadGroup(firstGroupID));

        Assert.Equal(2, nodes.Count());
        Assert.Single(edges);
        Assert.Single(groups);
        Assert.Equal(firstGroupID, Assert.Single(groups).ID);
    }

    [Fact]
    public void NodeAndGroupCollections_CannotMutateGraphState()
    {
        var graph = new RoadGraph();
        int groupID = graph.AddRoad(Vector2.Zero, new Vector2(10f, 0f), []);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        GraphNode node = Assert.IsType<GraphNode>(graph.GetNode(edge.NodeA));
        RoadGroup group = Assert.IsType<RoadGroup>(graph.GetGroup(groupID));
        string stateBefore = SaveJson.Serialize(graph.CaptureState());

        var nodeEdges = Assert.IsAssignableFrom<ICollection<EdgeRef>>(node.Edges);
        Assert.Throws<NotSupportedException>(() => nodeEdges.Clear());
        int[] groupEdgeIDs = Assert.IsType<int[]>(group.EdgeIDs);
        groupEdgeIDs[0] = int.MaxValue;

        Assert.Equal(stateBefore, SaveJson.Serialize(graph.CaptureState()));
        graph.AssertInvariants();
    }

    [Fact]
    public void GetFullPath_MissingEndpoint_ThrowsInsteadOfReturningPartialPath()
    {
        var edge = new GraphEdge(
            2,
            0,
            1,
            [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f))],
            3);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => edge.GetFullPath(nodeID => nodeID == 0 ? new GraphNode(0, Vector2.Zero) : null));

        Assert.Contains("endpoint nodes 0 and 1", exception.Message);
    }
}
