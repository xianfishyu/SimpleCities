using Godot;

namespace SimpleCities.Tests;

public sealed class RoadPlacementSessionTests
{
    private const float CellSize = 64f;

    [Fact]
    public void ZeroLengthPointCannotBeAddedOrCommitted()
    {
        var session = CreateSession();

        Assert.False(session.TryAddPoint(new Vector2(20f, 0f)));

        Assert.Equal(0, session.FixedCornerCount);
        Assert.False(session.CurrentDraft.CanCommit);
        Assert.Equal([Vector2.Zero], session.CurrentDraft.PreviewPoints);
    }

    [Fact]
    public void MovingEndpointProducesTheCompleteSingleSegmentPreview()
    {
        var session = CreateSession();

        RoadPathDraft draft = session.Update(new Vector2(130f, 10f));

        Assert.Equal(
            [Vector2.Zero, new Vector2(64f, 0f), new Vector2(128f, 0f)],
            draft.PreviewPoints);
        AssertGeometryMatchesPreview(draft);
    }

    [Fact]
    public void FixedCornersAndMovingEndpointComposeOneContinuousDraft()
    {
        var session = CreateSession();

        Assert.True(session.TryAddPoint(new Vector2(130f, 10f)));
        Assert.True(session.TryAddPoint(new Vector2(130f, 140f)));
        RoadPathDraft draft = session.Update(new Vector2(0f, 140f));

        Assert.Equal(2, session.FixedCornerCount);
        Assert.Equal(
            [
                Vector2.Zero,
                new Vector2(64f, 0f),
                new Vector2(128f, 0f),
                new Vector2(128f, 64f),
                new Vector2(128f, 128f),
                new Vector2(64f, 128f),
                new Vector2(0f, 128f),
            ],
            draft.PreviewPoints);
        AssertGeometryMatchesPreview(draft);
    }

    [Fact]
    public void RemovingLastCornerKeepsEarlierSegmentsEditable()
    {
        var session = CreateSession();
        Assert.True(session.TryAddPoint(new Vector2(130f, 10f)));
        Assert.True(session.TryAddPoint(new Vector2(130f, 140f)));

        Assert.True(session.TryRemoveLastPoint(new Vector2(250f, 0f)));

        Assert.Equal(1, session.FixedCornerCount);
        Assert.Equal(new Vector2(128f, 0f), session.CurrentAnchor);
        Assert.Equal(new Vector2(256f, 0f), session.CurrentDraft.PreviewTo);
        Assert.DoesNotContain(new Vector2(128f, 128f), session.CurrentDraft.PreviewPoints);
        AssertGeometryMatchesPreview(session.CurrentDraft);
    }

    [Fact]
    public void InvalidMovingSegmentDoesNotDiscardFixedPath()
    {
        var session = CreateSession();
        Assert.True(session.TryAddPoint(new Vector2(130f, 0f)));

        Assert.False(session.TryAddPoint(new Vector2(140f, 0f)));

        Assert.Equal(1, session.FixedCornerCount);
        Assert.True(session.CurrentDraft.CanCommit);
        Assert.Equal(new Vector2(128f, 0f), session.CurrentDraft.PreviewTo);
        AssertGeometryMatchesPreview(session.CurrentDraft);
    }

    [Fact]
    public void CompleteMultiSegmentDraftUsesThePublicCrossingPipeline()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline([new Vector2(64f, -64f), new Vector2(64f, 192f)]).Success);
        var session = CreateSession();
        Assert.True(session.TryAddPoint(new Vector2(130f, 0f)));
        Assert.True(session.TryAddPoint(new Vector2(130f, 140f)));
        session.Update(new Vector2(0f, 140f));

        RoadPathSubmissionResult result = graph.SubmitPath(session.CurrentDraft.Path);

        Assert.True(result.Success);
        Assert.Contains(graph.GetAllNodes(), node => node.Position == new Vector2(64f, 0f));
        graph.AssertInvariants();
    }

    [Fact]
    public void RejectedCompleteDraftLeavesGraphUnchanged()
    {
        var graph = new RoadGraph();
        var session = CreateSession();
        Assert.True(session.TryAddPoint(new Vector2(130f, 0f)));
        Assert.True(session.TryAddPoint(new Vector2(130f, 140f)));
        session.Update(Vector2.Zero);
        string before = SaveJson.Serialize(graph.CaptureState());

        RoadPathSubmissionResult result = graph.SubmitPath(session.CurrentDraft.Path);

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.RepeatedPoint, result.Error);
        Assert.Equal(before, SaveJson.Serialize(graph.CaptureState()));
        graph.AssertInvariants();
    }

    private static RoadPlacementSession CreateSession() =>
        new(new SquareEightRoadInputStrategy(CellSize), Vector2.Zero);

    private static void AssertGeometryMatchesPreview(RoadPathDraft draft)
    {
        RoadPath path = Assert.IsType<RoadPath>(draft.Path);
        Assert.Equal(draft.PreviewPoints.Count - 1, path.Segments.Count);
        for (int index = 0; index < path.Segments.Count; index++)
        {
            LineRoadGeometrySegment line = Assert.IsType<LineRoadGeometrySegment>(path.Segments[index]);
            Assert.Equal(draft.PreviewPoints[index], line.Start);
            Assert.Equal(draft.PreviewPoints[index + 1], line.End);
        }
    }
}
