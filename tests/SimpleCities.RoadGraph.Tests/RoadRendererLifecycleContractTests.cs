namespace SimpleCities.Tests;

using Godot;

public sealed class RoadRendererLifecycleContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void SetGraphSynchronizesExistingEdgesAndExitTreeUnsubscribes()
    {
        string source = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string setGraph = ExtractMethod(source, "public void SetGraph", "private void OnGraphCleared");

        Assert.Contains("_edgePoints.Clear()", setGraph, StringComparison.Ordinal);
        Assert.Contains("_network.GetAllEdges()", setGraph, StringComparison.Ordinal);
        Assert.Contains("RebuildStaticBatches()", setGraph, StringComparison.Ordinal);
        Assert.Contains("public override void _ExitTree", source, StringComparison.Ordinal);
        Assert.Contains("EdgeAdded -= OnEdgeAdded", source, StringComparison.Ordinal);
        Assert.Contains("EdgeRemoved -= OnEdgeRemoved", source, StringComparison.Ordinal);
        Assert.Contains("GraphCleared -= OnGraphCleared", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NodeMarkersUseTopologyClassificationAndTypeSpecificRadius()
    {
        string source = File.ReadAllText(
            Path.Combine(ProjectRoot, "Scripts", "Road", "RoadRenderer.cs"));

        Assert.Contains("IsJunctionNode", source, StringComparison.Ordinal);
        Assert.Contains("GetNodeMarkerRadius", source, StringComparison.Ordinal);
        Assert.DoesNotContain("bool junction = node.EdgeCount >= 2", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Config.JunctionRadius * 1.3f", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StraightDegreeTwoNodeHasNoMarkerButTurnAndEndpointUseTheirOwnRadii()
    {
        var straightGraph = new RoadGraph();
        Assert.True(straightGraph.SubmitPath(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(5f, 0f)),
            new LineRoadGeometrySegment(new Vector2(5f, 0f), new Vector2(10f, 0f)),
        ])).Success);
        global::GraphNode straight = Assert.Single(
            straightGraph.GetAllNodes(),
            node => node.Position == new Vector2(5f, 0f));
        global::GraphNode endpoint = Assert.Single(
            straightGraph.GetAllNodes(),
            node => node.Position == Vector2.Zero);

        Assert.False(RoadRenderer.IsJunctionNode(straightGraph, straight));
        Assert.Equal(0f, RoadRenderer.GetNodeMarkerRadius(straightGraph, straight, 3f, 10f));
        Assert.Equal(3f, RoadRenderer.GetNodeMarkerRadius(straightGraph, endpoint, 3f, 10f));

        var turnGraph = new RoadGraph();
        Assert.True(turnGraph.SubmitPath(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(5f, 0f)),
            new LineRoadGeometrySegment(new Vector2(5f, 0f), new Vector2(5f, 5f)),
        ])).Success);
        global::GraphNode turn = Assert.Single(
            turnGraph.GetAllNodes(),
            node => node.Position == new Vector2(5f, 0f));

        Assert.True(RoadRenderer.IsJunctionNode(turnGraph, turn));
        Assert.Equal(10f, RoadRenderer.GetNodeMarkerRadius(turnGraph, turn, 3f, 10f));
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not extract {startMarker}.");
        return source[start..end];
    }
}
