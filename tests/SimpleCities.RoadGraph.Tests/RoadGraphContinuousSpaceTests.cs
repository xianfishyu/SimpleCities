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
    public void AddRoad_ArbitraryAngleStraightLine_PreservesExactEndpoints()
    {
        var graph = new RoadGraph();
        var start = new Vector2(1.25f, -3.5f);
        var end = new Vector2(37.75f, 19.125f);

        int groupID = graph.AddRoad(start, end, []);

        Assert.True(groupID >= 0);
        var edge = Assert.Single(graph.GetAllEdges());
        Assert.Empty(edge.Points);
        Assert.Equal(start, Assert.IsType<GraphNode>(graph.GetNode(edge.NodeA)).Position);
        Assert.Equal(end, Assert.IsType<GraphNode>(graph.GetNode(edge.NodeB)).Position);
    }

    [Fact]
    public void AddRoad_NonOctilinearPolyline_PreservesEveryBend()
    {
        var graph = new RoadGraph();
        var start = new Vector2(-13.5f, 2.25f);
        var firstBend = new Vector2(4.75f, 17.5f);
        var secondBend = new Vector2(23.125f, 9.875f);
        var end = new Vector2(31.5f, 28.625f);

        int groupID = graph.AddRoad(start, end, [firstBend, secondBend]);

        var group = Assert.IsType<RoadGroup>(graph.GetGroup(groupID));
        Assert.Equal(3, group.EdgeIDs.Count);
        Assert.Equal(3, graph.GetAllEdges().Count());
        Assert.Equal(
            new HashSet<Vector2> { start, firstBend, secondBend, end },
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
    public void SecondGenerationRoadRuntime_DoesNotReferenceRoadType()
    {
        string roadSources = string.Join('\n',
            Directory.EnumerateFiles(RoadScriptsPath, "*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));
        string saveData = File.ReadAllText(SaveDataPath);

        Assert.DoesNotContain("RoadType", roadSources, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadType", saveData, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(RoadScriptsPath, "RoadType.cs")));
    }

    [Fact]
    public void OperationMetrics_DistinguishSpatialCandidatesFromFullGeometryScans()
    {
        var graph = new RoadGraph();
        graph.AddRoad(Vector2.Zero, new Vector2(10f, 0f), []);
        graph.AddRoad(new Vector2(100f, 0f), new Vector2(110f, 0f), []);

        Assert.NotNull(graph.FindClosestEdge(new Vector2(5f, 0f), 1f));
        Assert.Equal(1, graph.LastOperationMetrics.SpatialCandidateEdgeCount);
        Assert.Equal(0, graph.LastOperationMetrics.FullEdgeScanPassCount);
        Assert.Equal(0, graph.LastOperationMetrics.FullEdgeVisitCount);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([
            new LineRoadGeometrySegment(new Vector2(200f, 0f), new Vector2(210f, 0f))]));

        Assert.True(result.Success);
        Assert.Equal(0, graph.LastOperationMetrics.SpatialCandidateEdgeCount);
        Assert.Equal(2, graph.LastOperationMetrics.FullEdgeScanPassCount);
        Assert.Equal(4, graph.LastOperationMetrics.FullEdgeVisitCount);
    }
}
