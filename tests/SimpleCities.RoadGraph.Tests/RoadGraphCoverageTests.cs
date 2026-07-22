using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphCoverageTests
{
    private static readonly Vector2 Start = new(0, 0);
    private static readonly Vector2 End = new(192, 0);

    [Fact]
    public void AddRoad_ExactDuplicate_ReturnsMinusOneWithoutChangingState()
    {
        var graph = CreateGraphWithExistingRoad();
        string before = CaptureState(graph);

        int duplicateGroupID = graph.AddRoad(Start, End, []);

        Assert.Equal(-1, duplicateGroupID);
        Assert.Equal(before, CaptureState(graph));
    }

    [Fact]
    public void AddRoad_CoveredPathWithInteriorAnchors_DoesNotSplitExistingEdge()
    {
        var graph = CreateGraphWithExistingRoad();
        string before = CaptureState(graph);

        int duplicateGroupID = graph.AddRoad(Start, End, [new(64, 0), new(128, 0)]);

        Assert.Equal(-1, duplicateGroupID);
        Assert.Equal(before, CaptureState(graph));
        Assert.Equal(2, graph.GetAllNodes().Count());
        Assert.Single(graph.GetAllEdges());
        Assert.Single(graph.GetAllGroups());
    }

    [Fact]
    public void AddRoad_RejectedCoveredPath_DoesNotConsumeIDs()
    {
        var subject = CreateGraphWithExistingRoad();
        var control = CreateGraphWithExistingRoad();

        Assert.Equal(-1, subject.AddRoad(Start, End, [new(64, 0), new(128, 0)]));

        Assert.True(subject.AddRoad(new(0, 64), new(64, 64), []) >= 0);
        Assert.True(control.AddRoad(new(0, 64), new(64, 64), []) >= 0);
        Assert.Equal(CaptureState(control), CaptureState(subject));
    }

    private static RoadGraph CreateGraphWithExistingRoad()
    {
        var graph = new RoadGraph();
        Assert.True(graph.AddRoad(Start, End, []) >= 0);
        return graph;
    }

    private static string CaptureState(RoadGraph graph) => SaveJson.Serialize(graph.CaptureState());
}
