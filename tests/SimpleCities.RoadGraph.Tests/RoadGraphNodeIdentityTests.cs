using Godot;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SimpleCities.Tests;

public sealed class RoadGraphNodeIdentityTests
{
    [Theory]
    [InlineData(0.4999f, true)]
    [InlineData(0.5f, true)]
    [InlineData(0.5001f, false)]
    public void SubmitPolyline_StartNearExistingNode_UsesInclusiveHalfUnitSnapRadius(
        float offset,
        bool shouldReuseExistingNode)
    {
        var graph = CreateGraphWithTerminalNodes(Vector2.Zero);
        var addedEdge = AddProbeRoad(graph, new Vector2(offset, 0));

        bool reusedExistingNode = HasGeometryAnchor(addedEdge, Vector2.Zero);
        Assert.Equal(shouldReuseExistingNode, reusedExistingNode);
        Assert.Equal(shouldReuseExistingNode ? 2 : 4, graph.GetAllNodes().Count());
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
    public void SubmitPolyline_StartWithinTwoSnapRadii_ReusesNearestNode()
    {
        var graph = CreateGraphWithTerminalNodes(Vector2.Zero, new Vector2(0.75f, 0));
        var expected = FindNodeAt(graph, new Vector2(0.75f, 0));

        var addedEdge = AddProbeRoad(graph, new Vector2(0.4f, 0));

        Assert.True(HasGeometryAnchor(addedEdge, expected.Position));
    }

    [Fact]
    public void SubmitPolyline_StartEquidistantFromTwoNodes_ReusesLowerNodeIDAfterRoundTrip()
    {
        var source = CreateGraphWithTerminalNodes(Vector2.Zero, new Vector2(0.75f, 0));
        GraphNode expectedNode = new[]
        {
            FindNodeAt(source, Vector2.Zero),
            FindNodeAt(source, new Vector2(0.75f, 0)),
        }.MinBy(node => node.ID)!;
        RoadGraph restored = RoadGraphTestCodec.Clone(source);

        var addedEdge = AddProbeRoad(restored, new Vector2(0.375f, 0));

        Assert.True(HasGeometryAnchor(addedEdge, expectedNode.Position));
    }

    [Fact]
    public void Load_SubmitPolylineNearLoadedNode_ReusesLoadedNode()
    {
        var source = CreateGraphWithTerminalNodes(Vector2.Zero);
        var restored = new RoadGraph();
        RoadGraphTestCodec.LoadJson(restored, RoadGraphTestCodec.CaptureJson(source));

        var addedEdge = AddProbeRoad(restored, new Vector2(0.25f, 0));

        Assert.True(HasGeometryAnchor(addedEdge, Vector2.Zero));
        Assert.Equal(2, restored.GetAllNodes().Count());
    }

    private static RoadGraph CreateGraphWithTerminalNodes(params Vector2[] terminalPositions)
    {
        var graph = new RoadGraph();
        for (int i = 0; i < terminalPositions.Length; i++)
        {
            Vector2 terminal = terminalPositions[i];
            Vector2 remote = terminal + new Vector2(-10 - i, 5 * i);
            Assert.True(graph.SubmitPolyline(RoadType.Street, [remote, terminal]).Success);
        }

        return graph;
    }

    private static GraphNode FindNodeAt(RoadGraph graph, Vector2 position)
    {
        return Assert.Single(graph.GetAllNodes(), node => node.Position == position);
    }

    private static GraphEdge AddProbeRoad(RoadGraph graph, Vector2 start)
    {
        Vector2 end = start + new Vector2(0, -10);
        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [start, end]);
        Assert.True(result.Success);
        return Assert.IsType<GraphEdge>(graph.FindClosestEdge(end, 0.01f));
    }

    private static bool HasGeometryAnchor(GraphEdge edge, Vector2 position) =>
        edge.GeometrySegments.Any(segment => segment.Start == position || segment.End == position);

    [Fact]
    public void Reader_ReversedNodeOrderIsRejected()
    {
        RoadGraph source = CreateGraphWithTerminalNodes(Vector2.Zero, new Vector2(0.75f, 0));
        var root = Assert.IsType<JsonObject>(JsonNode.Parse(RoadGraphTestCodec.CaptureJson(source)));
        var nodes = Assert.IsType<JsonArray>(root["nodes"]);
        var reversedNodes = nodes.Select(node => node!.DeepClone()).Reverse().ToArray();
        nodes.Clear();
        foreach (var node in reversedNodes)
            nodes.Add(node);

        Assert.Throws<JsonException>(() =>
            RoadGraphTestCodec.PrepareJson(new RoadGraph(), root.ToJsonString()));
    }
}
