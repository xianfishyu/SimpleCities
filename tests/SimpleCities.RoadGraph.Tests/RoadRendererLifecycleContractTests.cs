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
        string setGraph = ExtractMethod(source, "public void SetGraph", "private void OnGraphChanged");

        Assert.Contains("_edgePoints.Clear()", setGraph, StringComparison.Ordinal);
        Assert.Contains("_network.GetAllEdges()", setGraph, StringComparison.Ordinal);
        Assert.Contains("RebuildStaticBatches()", setGraph, StringComparison.Ordinal);
        Assert.Contains("public override void _ExitTree", source, StringComparison.Ordinal);
        Assert.Contains("GraphChanged -= OnGraphChanged", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EdgeAdded", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EdgeRemoved", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GraphCleared", source, StringComparison.Ordinal);
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
    public void PrimitiveJoinsAreNotNodesAndBranchJunctionUsesItsOwnRadius()
    {
        var straightGraph = new RoadGraph();
        Assert.True(straightGraph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(5f, 0f)),
            new LineRoadGeometrySegment(new Vector2(5f, 0f), new Vector2(10f, 0f)),
        ]), RoadType.Street)).Success);
        global::GraphNode endpoint = Assert.Single(
            straightGraph.GetAllNodes(),
            node => node.Position == Vector2.Zero);

        Assert.DoesNotContain(straightGraph.GetAllNodes(), node => node.Position == new Vector2(5f, 0f));
        Assert.Equal(3f, RoadRenderer.GetNodeMarkerRadius(straightGraph, endpoint, 3f, 10f));

        var turnGraph = new RoadGraph();
        Assert.True(turnGraph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(5f, 0f)),
            new LineRoadGeometrySegment(new Vector2(5f, 0f), new Vector2(5f, 5f)),
        ]), RoadType.Street)).Success);
        Assert.DoesNotContain(turnGraph.GetAllNodes(), node => node.Position == new Vector2(5f, 0f));
        Assert.Single(turnGraph.GetAllEdges());
        Assert.Equal(2, Assert.Single(turnGraph.GetAllEdges()).GeometrySegments.Count);

        var branchGraph = new RoadGraph();
        Assert.True(branchGraph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(10f, 0f),
        ]).Success);
        Assert.True(branchGraph.SubmitPolyline(RoadType.Street, [
            new Vector2(5f, 0f),
            new Vector2(5f, 5f),
        ]).Success);
        global::GraphNode junction = Assert.Single(
            branchGraph.GetAllNodes(),
            node => node.Position == new Vector2(5f, 0f));

        Assert.True(RoadRenderer.IsJunctionNode(branchGraph, junction));
        Assert.Equal(10f, RoadRenderer.GetNodeMarkerRadius(branchGraph, junction, 3f, 10f));
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not extract {startMarker}.");
        return source[start..end];
    }
}
