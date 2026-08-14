using Godot;
using System.IO;

namespace SimpleCities.Tests;

public sealed class RoadGraphContinuousSpaceTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));
    private static readonly string RoadGraphPath = Path.Combine(ProjectRoot, "Scripts", "Road", "RoadGraph.cs");
    private static readonly string RoadScriptsPath = Path.Combine(ProjectRoot, "Scripts", "Road");
    private static readonly string SaveDataPath = Path.Combine(ProjectRoot, "Scripts", "Core", "SaveData.cs");

    [Fact]
    public void SubmitPolyline_ArbitraryAngleStraightLine_PreservesExactEndpoints()
    {
        var graph = new RoadGraph();
        var start = new Vector2(1.25f, -3.5f);
        var end = new Vector2(37.75f, 19.125f);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [start, end]);

        Assert.True(result.Success);
        var edge = Assert.Single(graph.GetAllEdges());
        Assert.Empty(edge.Points);
        Assert.Equal(start, Assert.IsType<GraphNode>(graph.GetNode(edge.NodeA)).Position);
        Assert.Equal(end, Assert.IsType<GraphNode>(graph.GetNode(edge.NodeB)).Position);
    }

    [Fact]
    public void SubmitPolyline_NonOctilinearPolyline_PreservesEveryBendInsideOneEdge()
    {
        var graph = new RoadGraph();
        var start = new Vector2(-13.5f, 2.25f);
        var firstBend = new Vector2(4.75f, 17.5f);
        var secondBend = new Vector2(23.125f, 9.875f);
        var end = new Vector2(31.5f, 28.625f);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            start,
            firstBend,
            secondBend,
            end,
        ]);

        Assert.True(result.Success);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Equal(3, edge.GeometrySegments.Count);
        Assert.Equal([firstBend, secondBend], edge.Points);
        Assert.Equal(
            new HashSet<Vector2> { start, end },
            graph.GetAllNodes().Select(node => node.Position).ToHashSet());
    }

    [Fact]
    public void RoadGraphSource_DoesNotReferenceInputLayerGridOrDirectionConcepts()
    {
        string source = File.ReadAllText(RoadGraphPath);

        Assert.DoesNotContain("Direction", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DirectionUtil", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GridSystem", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CellSize", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RoadTypeBelongsToEdgeAndBuildRequestNotGeometryOrInputStrategy()
    {
        string graphEdge = File.ReadAllText(Path.Combine(RoadScriptsPath, "GraphEdge.cs"));
        string buildRequest = File.ReadAllText(Path.Combine(RoadScriptsPath, "RoadBuildRequest.cs"));
        string roadPath = File.ReadAllText(Path.Combine(RoadScriptsPath, "RoadPath.cs"));
        string inputStrategy = File.ReadAllText(Path.Combine(RoadScriptsPath, "Input", "IRoadInputStrategy.cs"));

        Assert.Contains("public RoadType RoadType", graphEdge, StringComparison.Ordinal);
        Assert.Contains("public RoadType RoadType", buildRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadType", roadPath, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadType", inputStrategy, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(RoadScriptsPath, "RoadType.cs")));
    }

    [Fact]
    public void OperationMetrics_ReportLocalizedQueriesWithoutFullGeometryScans()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(10f, 0f)]).Success);
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(100f, 0f),
            new Vector2(110f, 0f),
        ]).Success);

        Assert.NotNull(graph.FindClosestEdge(new Vector2(5f, 0f), 1f));
        Assert.Equal(1, graph.LastOperationMetrics.SpatialCandidateEdgeCount);
        Assert.Equal(0, graph.LastOperationMetrics.FullEdgeScanPassCount);
        Assert.Equal(0, graph.LastOperationMetrics.FullEdgeVisitCount);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(new Vector2(200f, 0f), new Vector2(210f, 0f))]), RoadType.Street));

        Assert.True(result.Success);
        Assert.Equal(0, graph.LastOperationMetrics.SpatialCandidateEdgeCount);
        Assert.Equal(0, graph.LastOperationMetrics.FullEdgeScanPassCount);
        Assert.Equal(0, graph.LastOperationMetrics.FullEdgeVisitCount);
    }
}
