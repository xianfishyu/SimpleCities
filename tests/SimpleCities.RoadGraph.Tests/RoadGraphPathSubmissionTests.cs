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
        { [Vector2.Zero, new Vector2(10, 0), Vector2.Zero] },
        { [Vector2.Zero, new Vector2(10, 0), new Vector2(5, 0)] },
        { [Vector2.Zero, new Vector2(10, 10), new Vector2(0, 10), new Vector2(10, 0)] },
    };

    public static TheoryData<Vector2[], RoadPathSubmissionError> InvalidPolylineReasons => new()
    {
        { [Vector2.Zero], RoadPathSubmissionError.TooFewPoints },
        { [Vector2.Zero, new Vector2(float.NaN, 1)], RoadPathSubmissionError.NonFiniteCoordinate },
        { [Vector2.Zero, new Vector2(float.NegativeInfinity, 1)], RoadPathSubmissionError.NonFiniteCoordinate },
        { [Vector2.Zero, new Vector2(0.005f, 0)], RoadPathSubmissionError.DegenerateSegment },
        { [Vector2.Zero, new Vector2(0.5f, 0)], RoadPathSubmissionError.CollapsedByNodeIdentity },
        { [Vector2.Zero, new Vector2(10, 0), Vector2.Zero], RoadPathSubmissionError.RepeatedPoint },
        {
            [Vector2.Zero, new Vector2(10, 0), new Vector2(5, 0)],
            RoadPathSubmissionError.SelfIntersection
        },
        {
            [Vector2.Zero, new Vector2(10, 10), new Vector2(0, 10), new Vector2(10, 0)],
            RoadPathSubmissionError.SelfIntersection
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
        int addedEvents = 0;
        int removedEvents = 0;
        graph.EdgeAdded += _ => addedEvents++;
        graph.EdgeRemoved += _ => removedEvents++;

        var result = graph.SubmitPolyline(points);

        Assert.False(result.Success);
        Assert.Null(result.GroupID);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(stateBefore, CaptureState(graph));
        Assert.Equal(0, addedEvents);
        Assert.Equal(0, removedEvents);
    }

    [Fact]
    public void SubmitPolyline_EndpointsResolvingToSameExistingNode_AreRejectedWithoutSideEffects()
    {
        var graph = CreateGraphWithExistingRoad();
        string stateBefore = CaptureState(graph);

        var result = graph.SubmitPolyline([
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

        var result = graph.SubmitPolyline([Vector2.Zero, new Vector2(100, 0)]);

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.FullyCovered, result.Error);
        Assert.Equal(stateBefore, CaptureState(graph));
    }

    [Fact]
    public void SubmitPolyline_ArbitraryValidPath_ReturnsCreatedGroup()
    {
        var graph = new RoadGraph();

        var result = graph.SubmitPolyline([
            new Vector2(1.25f, -3.5f),
            new Vector2(17.75f, 11.125f),
            new Vector2(31.5f, 4.25f),
        ]);

        Assert.True(result.Success);
        int groupID = Assert.IsType<int>(result.GroupID);
        Assert.Equal(RoadPathSubmissionError.None, result.Error);
        Assert.NotNull(graph.GetGroup(groupID));
        Assert.Equal(2, graph.GetAllEdges().Count());
    }

    [Theory]
    [MemberData(nameof(InvalidPolylines))]
    public void AddRoad_InvalidPathCompatibilityAdapter_ReturnsMinusOneWithoutSideEffects(Vector2[] points)
    {
        var graph = new RoadGraph();
        string stateBefore = CaptureState(graph);
        var start = points[0];
        var end = points[^1];
        var waypoints = points.Skip(1).SkipLast(1).ToArray();

        int groupID = graph.AddRoad(start, end, waypoints);

        Assert.Equal(-1, groupID);
        Assert.Equal(stateBefore, CaptureState(graph));
    }

    private static RoadGraph CreateGraphWithExistingRoad()
    {
        var graph = new RoadGraph();
        Assert.True(graph.AddRoad(Vector2.Zero, new Vector2(100, 0), []) >= 0);
        return graph;
    }

    private static string CaptureState(RoadGraph graph) => SaveJson.Serialize(graph.CaptureState());
}
