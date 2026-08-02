using Godot;
using System.Text.Json.Nodes;

namespace SimpleCities.Tests;

public sealed class RoadGraphNodeIdentityTests
{
    [Theory]
    [InlineData(0.4999f, true)]
    [InlineData(0.5f, true)]
    [InlineData(0.5001f, false)]
    public void AddRoad_StartNearExistingNode_UsesInclusiveHalfUnitSnapRadius(
        float offset,
        bool shouldReuseExistingNode)
    {
        var graph = CreateGraphWithTerminalNodes(Vector2.Zero);
        int existingNodeID = FindNodeAt(graph, Vector2.Zero).ID;

        var addedEdge = AddProbeRoad(graph, new Vector2(offset, 0));

        bool reusedExistingNode = addedEdge.NodeA == existingNodeID || addedEdge.NodeB == existingNodeID;
        Assert.Equal(shouldReuseExistingNode, reusedExistingNode);
        Assert.Equal(shouldReuseExistingNode ? 3 : 4, graph.GetAllNodes().Count());
    }

    [Fact]
    public void FindClosestNode_NodeAtRadiusBoundary_IsIncluded()
    {
        var graph = CreateGraphWithTerminalNodes(Vector2.Zero);
        var expected = FindNodeAt(graph, Vector2.Zero);

        var closest = graph.FindClosestNode(new Vector2(0.5f, 0), 0.5f);

        Assert.NotNull(closest);
        Assert.Equal(expected.ID, closest!.ID);
    }

    [Fact]
    public void AddRoad_StartWithinTwoSnapRadii_ReusesNearestNode()
    {
        var graph = CreateGraphWithTerminalNodes(Vector2.Zero, new Vector2(0.75f, 0));
        var expected = FindNodeAt(graph, new Vector2(0.75f, 0));

        var addedEdge = AddProbeRoad(graph, new Vector2(0.4f, 0));

        Assert.True(addedEdge.NodeA == expected.ID || addedEdge.NodeB == expected.ID);
    }

    [Fact]
    public void AddRoad_StartEquidistantFromTwoNodes_ReusesLowerNodeIDAfterRestore()
    {
        var source = CreateGraphWithTerminalNodes(Vector2.Zero, new Vector2(0.75f, 0));
        int expectedNodeID = new[]
        {
            FindNodeAt(source, Vector2.Zero).ID,
            FindNodeAt(source, new Vector2(0.75f, 0)).ID,
        }.Min();
        var restored = RestoreWithReversedNodeOrder(source);

        var addedEdge = AddProbeRoad(restored, new Vector2(0.375f, 0));

        Assert.True(addedEdge.NodeA == expectedNodeID || addedEdge.NodeB == expectedNodeID);
    }

    [Fact]
    public void RestoreState_AddRoadNearLoadedNode_ReusesLoadedNode()
    {
        var source = CreateGraphWithTerminalNodes(Vector2.Zero);
        int loadedNodeID = FindNodeAt(source, Vector2.Zero).ID;
        var restored = new RoadGraph();
        restored.RestoreState(SaveJson.Serialize(source.CaptureState()));

        var addedEdge = AddProbeRoad(restored, new Vector2(0.25f, 0));

        Assert.True(addedEdge.NodeA == loadedNodeID || addedEdge.NodeB == loadedNodeID);
        Assert.Equal(3, restored.GetAllNodes().Count());
    }

    private static RoadGraph CreateGraphWithTerminalNodes(params Vector2[] terminalPositions)
    {
        var graph = new RoadGraph();
        for (int i = 0; i < terminalPositions.Length; i++)
        {
            Vector2 terminal = terminalPositions[i];
            Vector2 remote = terminal + new Vector2(-10 - i, 5 * i);
            Assert.True(graph.AddRoad(remote, terminal, []) >= 0);
        }

        return graph;
    }

    private static GraphNode FindNodeAt(RoadGraph graph, Vector2 position)
    {
        return Assert.Single(graph.GetAllNodes(), node => node.Position == position);
    }

    private static GraphEdge AddProbeRoad(RoadGraph graph, Vector2 start)
    {
        int groupID = graph.AddRoad(start, start + new Vector2(0, -10), []);
        var group = Assert.IsType<RoadGroup>(graph.GetGroup(groupID));
        int edgeID = Assert.Single(group.EdgeIDs);
        return Assert.IsType<GraphEdge>(graph.GetEdge(edgeID));
    }

    private static RoadGraph RestoreWithReversedNodeOrder(RoadGraph source)
    {
        var root = Assert.IsType<JsonObject>(JsonNode.Parse(SaveJson.Serialize(source.CaptureState())));
        var nodes = Assert.IsType<JsonArray>(root["nodes"]);
        var reversedNodes = nodes.Select(node => node!.DeepClone()).Reverse().ToArray();
        nodes.Clear();
        foreach (var node in reversedNodes)
            nodes.Add(node);

        var restored = new RoadGraph();
        restored.RestoreState(root.ToJsonString());
        return restored;
    }
}
