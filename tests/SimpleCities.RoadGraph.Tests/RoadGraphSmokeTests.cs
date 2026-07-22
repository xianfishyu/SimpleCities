namespace SimpleCities.Tests;

public sealed class RoadGraphSmokeTests
{
    [Fact]
    public void Constructor_DoesNotRequireSceneTree()
    {
        var graph = new RoadGraph();

        Assert.NotNull(graph);
    }

    [Fact]
    public void NewGraph_HasNoEntities()
    {
        var graph = new RoadGraph();

        Assert.Empty(graph.GetAllNodes());
        Assert.Empty(graph.GetAllEdges());
        Assert.Empty(graph.GetAllGroups());
    }
}
