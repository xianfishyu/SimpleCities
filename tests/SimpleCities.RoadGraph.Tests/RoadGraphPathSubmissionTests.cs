using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphPathSubmissionTests
{
    public static TheoryData<Vector2[]> InvalidPolylines => new()
    {
        { [Vector2.Zero] },
        { [Vector2.Zero, new Vector2(float.NaN, 1)] },
        { [Vector2.Zero, new Vector2(float.PositiveInfinity, 1)] },
        { [Vector2.Zero, new Vector2(0.005f, 0)] },
        { [Vector2.Zero, new Vector2(0.5f, 0)] },
        { [Vector2.Zero, new Vector2(10, 0), new Vector2(5, 0)] },
    };

    public static TheoryData<Vector2[], RoadPathSubmissionError> InvalidPolylineReasons => new()
    {
        { [Vector2.Zero], RoadPathSubmissionError.TooFewPoints },
        { [Vector2.Zero, new Vector2(float.NaN, 1)], RoadPathSubmissionError.NonFiniteCoordinate },
        { [Vector2.Zero, new Vector2(float.NegativeInfinity, 1)], RoadPathSubmissionError.NonFiniteCoordinate },
        { [Vector2.Zero, new Vector2(0.005f, 0)], RoadPathSubmissionError.DegenerateSegment },
        { [Vector2.Zero, new Vector2(0.5f, 0)], RoadPathSubmissionError.CollapsedByNodeIdentity },
        {
            [Vector2.Zero, new Vector2(10, 0), new Vector2(5, 0)],
            RoadPathSubmissionError.SelfOverlap
        },
    };

    [Theory]
    [MemberData(nameof(InvalidPolylineReasons))]
    public void SubmitPolyline_InvalidPath_ReturnsStructuredReasonWithoutSideEffects(
        Vector2[] points,
        RoadPathSubmissionError expectedError)
    {
        var graph = CreateGraphWithExistingRoad();
        string stateBefore = CaptureState(graph);
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        var result = graph.SubmitPolyline(RoadType.Street, points);

        Assert.False(result.Success);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(stateBefore, CaptureState(graph));
        Assert.Equal(0, changedEvents);
    }

    [Fact]
    public void SubmitPolyline_EndpointsResolvingToSameExistingNode_AreRejectedWithoutSideEffects()
    {
        var graph = CreateGraphWithExistingRoad();
        string stateBefore = CaptureState(graph);

        var result = graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-0.4f, 0),
            new Vector2(0.4f, 0),
        ]);

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.CollapsedByNodeIdentity, result.Error);
        Assert.Equal(stateBefore, CaptureState(graph));
    }

    [Fact]
    public void SubmitPolyline_FullyCoveredPath_ReturnsStructuredReasonWithoutSideEffects()
    {
        var graph = CreateGraphWithExistingRoad();
        string stateBefore = CaptureState(graph);

        var result = graph.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(100, 0)]);

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.FullyCovered, result.Error);
        Assert.Equal(stateBefore, CaptureState(graph));
    }

    [Fact]
    public void SubmitPolyline_ArbitraryValidPath_ReturnsOneMaximalEdge()
    {
        var graph = new RoadGraph();

        var result = graph.SubmitPolyline(RoadType.Street, [
            new Vector2(1.25f, -3.5f),
            new Vector2(17.75f, 11.125f),
            new Vector2(31.5f, 4.25f),
        ]);

        Assert.True(result.Success);
        Assert.Equal(RoadPathSubmissionError.None, result.Error);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Equal(2, edge.GeometrySegments.Count);
        Assert.Equal(2, graph.GetAllNodes().Count());
    }

    [Theory]
    [MemberData(nameof(InvalidPolylines))]
    public void SubmitPolyline_InvalidPathOnEmptyGraph_HasNoSideEffects(Vector2[] points)
    {
        var graph = new RoadGraph();
        string stateBefore = CaptureState(graph);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, points);

        Assert.False(result.Success);
        Assert.Equal(stateBefore, CaptureState(graph));
    }

    private static RoadGraph CreateGraphWithExistingRoad()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(100, 0)]).Success);
        return graph;
    }

    private static string CaptureState(RoadGraph graph) => RoadGraphTestCodec.CaptureJson(graph);
}
