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
        Assert.True(graph.SubmitPolyline(RoadType.Street, [new Vector2(64f, -64f), new Vector2(64f, 192f)]).Success);
        var session = CreateSession();
        Assert.True(session.TryAddPoint(new Vector2(130f, 0f)));
        Assert.True(session.TryAddPoint(new Vector2(130f, 140f)));
        session.Update(new Vector2(0f, 140f));

        RoadPath path = Assert.IsType<RoadPath>(session.CurrentDraft.Path);
        RoadPathSubmissionResult result = graph.SubmitPath(
            new RoadBuildRequest(path, RoadType.Street));

        Assert.True(result.Success);
        Assert.Contains(graph.GetAllNodes(), node => node.Position == new Vector2(64f, 0f));
        graph.AssertInvariants();
    }

    [Fact]
    public void ClosingCompleteDraftCreatesOneRootedLoop()
    {
        var graph = new RoadGraph();
        var session = CreateSession();
        Assert.True(session.TryAddPoint(new Vector2(130f, 0f)));
        Assert.True(session.TryAddPoint(new Vector2(130f, 140f)));
        session.Update(Vector2.Zero);
        RoadPath path = Assert.IsType<RoadPath>(session.CurrentDraft.Path);
        RoadPathSubmissionResult result = graph.SubmitPath(
            new RoadBuildRequest(path, RoadType.Street));

        Assert.True(session.CurrentDraft.IsClosed);
        Assert.True(result.Success, $"Submission failed with {result.Error}.");
        GraphEdge loop = Assert.Single(graph.GetAllEdges());
        Assert.Equal(loop.NodeA, loop.NodeB);
        Assert.Single(graph.GetAllNodes());
        graph.AssertInvariants();
    }

    [Theory]
    [InlineData(0f, 0f, true)]
    [InlineData(32f, 0f, true)]
    [InlineData(32.01f, 0f, false)]
    public void ClosurePreviewUsesExactFirstAnchorInsideInclusiveIdentityRadius(
        float pointerX,
        float pointerY,
        bool expectedClosed)
    {
        var session = CreateDirectSession();
        Assert.True(session.TryAddPoint(new Vector2(128f, 0f)));
        Assert.True(session.TryAddPoint(new Vector2(128f, 128f)));
        Assert.True(session.TryAddPoint(new Vector2(0f, 128f)));

        RoadPathDraft draft = session.Update(new Vector2(pointerX, pointerY));

        Assert.Equal(expectedClosed, draft.IsClosed);
        if (expectedClosed)
        {
            Assert.Equal(session.StartPosition, draft.PreviewTo);
            Assert.Equal(session.StartPosition, draft.Path!.Segments[^1]!.End);
        }
        AssertGeometryMatchesPreview(draft);
    }

    [Fact]
    public void AddingClosureDoesNotAppendAZeroLengthSegment()
    {
        var session = CreateDirectSession();
        Assert.True(session.TryAddPoint(new Vector2(128f, 0f)));
        Assert.True(session.TryAddPoint(new Vector2(128f, 128f)));
        Assert.True(session.TryAddPoint(new Vector2(0f, 128f)));

        Assert.True(session.TryAddPoint(new Vector2(20f, 0f)));
        int segmentCount = session.CurrentDraft.Path!.Segments.Count;
        Assert.True(session.CurrentDraft.IsClosed);

        Assert.False(session.TryAddPoint(Vector2.Zero));
        Assert.Equal(segmentCount, session.CurrentDraft.Path!.Segments.Count);
        Assert.True(session.CurrentDraft.IsClosed);
        AssertGeometryMatchesPreview(session.CurrentDraft);
    }

    [Fact]
    public void RejectedBacktrackClosureDoesNotEnterHistoryOrConsumeIDs()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(graph);
        var session = CreateDirectSession();
        Assert.True(session.TryAddPoint(new Vector2(128f, 0f)));
        RoadPathDraft draft = session.Update(Vector2.Zero);
        RoadPath path = Assert.IsType<RoadPath>(draft.Path);
        string stateBefore = RoadGraphTestCodec.CaptureJson(graph);
        int nextIDBefore = graph.NextIDWatermark;
        RoadPathSubmissionResult? result = null;

        bool submitted = history.Execute(() =>
        {
            result = graph.SubmitPath(new RoadBuildRequest(path, RoadType.Street));
            return result.Success;
        });

        Assert.True(draft.IsClosed);
        Assert.False(submitted);
        Assert.Equal(RoadPathSubmissionError.SelfOverlap, result?.Error);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
        Assert.Equal(nextIDBefore, graph.NextIDWatermark);
        Assert.Equal(stateBefore, RoadGraphTestCodec.CaptureJson(graph));
        graph.AssertInvariants();
    }

    private static RoadPlacementSession CreateSession() =>
        new(new SquareEightRoadInputStrategy(CellSize), Vector2.Zero);

    private static RoadPlacementSession CreateDirectSession() =>
        new(new DirectRoadInputStrategy(32f), Vector2.Zero);

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

    private sealed class DirectRoadInputStrategy(float interactionRadius) : IRoadInputStrategy
    {
        public float InteractionRadius { get; } = interactionRadius;

        public Vector2 SnapPointer(Vector2 worldPosition) => worldPosition;

        public RoadPathDraft BuildDraft(Vector2 startPosition, Vector2 pointerPosition) =>
            startPosition == pointerPosition
                ? RoadPathDraft.Empty(startPosition)
                : RoadPathDraft.FromPolyline([startPosition, pointerPosition]);
    }
}
