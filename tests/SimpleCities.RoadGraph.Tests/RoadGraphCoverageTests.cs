using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphCoverageTests
{
    private static readonly Vector2 Start = new(0, 0);
    private static readonly Vector2 End = new(192, 0);

    [Fact]
    public void SubmitPolyline_ExactDuplicateIsRejectedWithoutChangingState()
    {
        var graph = CreateGraphWithExistingRoad();
        string before = CaptureState(graph);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [Start, End]);

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.FullyCovered, result.Error);
        Assert.Equal(before, CaptureState(graph));
    }

    [Fact]
    public void SubmitPolyline_CoveredPathWithInteriorAnchors_DoesNotSplitExistingEdge()
    {
        var graph = CreateGraphWithExistingRoad();
        string before = CaptureState(graph);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            Start,
            new Vector2(64, 0),
            new Vector2(128, 0),
            End,
        ]);

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.FullyCovered, result.Error);
        Assert.Equal(before, CaptureState(graph));
        Assert.Equal(2, graph.GetAllNodes().Count());
        Assert.Single(graph.GetAllEdges());
    }

    [Fact]
    public void SubmitPolyline_RejectedCoveredPath_DoesNotConsumeIDs()
    {
        var subject = CreateGraphWithExistingRoad();
        var control = CreateGraphWithExistingRoad();

        RoadPathSubmissionResult rejected = subject.SubmitPolyline(RoadType.Street, [
            Start,
            new Vector2(64, 0),
            new Vector2(128, 0),
            End,
        ]);
        Assert.False(rejected.Success);

        Assert.True(subject.SubmitPolyline(RoadType.Street, [new(0, 64), new(64, 64)]).Success);
        Assert.True(control.SubmitPolyline(RoadType.Street, [new(0, 64), new(64, 64)]).Success);
        Assert.Equal(CaptureState(control), CaptureState(subject));
    }

    private static RoadGraph CreateGraphWithExistingRoad()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [Start, End]).Success);
        return graph;
    }

    private static string CaptureState(RoadGraph graph) => RoadGraphTestCodec.CaptureJson(graph);
}
