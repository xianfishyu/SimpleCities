using Godot;

namespace SimpleCities.Tests;

public sealed class RoadInputStrategyTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void SquareStrategySnapsPointerToNearestCellOrigin()
    {
        var strategy = new SquareEightRoadInputStrategy(64f);

        Vector2 snapped = strategy.SnapPointer(new Vector2(95f, -33f));

        Assert.Equal(new Vector2(64f, -64f), snapped);
        Assert.Equal(51.2f, strategy.InteractionRadius, 3);
    }

    [Fact]
    public void SquareStrategyProjectsOrthogonalDragIntoOneSegmentPerCell()
    {
        var strategy = new SquareEightRoadInputStrategy(64f);

        RoadPathDraft draft = strategy.BuildDraft(Vector2.Zero, new Vector2(130f, 10f));

        Assert.True(draft.CanCommit);
        Assert.Equal([Vector2.Zero, new Vector2(64f, 0f), new Vector2(128f, 0f)], draft.PreviewPoints);
        AssertLineSegmentsMatchPreview(draft);
    }

    [Fact]
    public void SquareStrategyProjectsDiagonalDragUsingDiagonalStepLength()
    {
        var strategy = new SquareEightRoadInputStrategy(64f);

        RoadPathDraft draft = strategy.BuildDraft(Vector2.Zero, new Vector2(125f, 130f));

        Assert.True(draft.CanCommit);
        Assert.Equal([Vector2.Zero, new Vector2(64f, 64f), new Vector2(128f, 128f)], draft.PreviewPoints);
        AssertLineSegmentsMatchPreview(draft);
    }

    [Fact]
    public void SquareStrategyRejectsDragBelowRoundedMinimumLength()
    {
        var strategy = new SquareEightRoadInputStrategy(64f);

        RoadPathDraft draft = strategy.BuildDraft(Vector2.Zero, new Vector2(31f, 0f));

        Assert.False(draft.CanCommit);
        Assert.Null(draft.Path);
        Assert.Equal([Vector2.Zero], draft.PreviewPoints);
    }

    [Fact]
    public void OffsetStartUsesDiagonalProjectionAndHalfCellAnchor()
    {
        var strategy = new SquareEightRoadInputStrategy(64f);
        var start = new Vector2(32f, 32f);

        RoadPathDraft draft = strategy.BuildDraft(start, new Vector2(160f, 32f));

        Assert.True(draft.CanCommit);
        Assert.Equal(start, draft.PreviewFrom);
        Vector2 end = draft.PreviewTo;
        Assert.Equal(Mathf.Abs(end.X - start.X), Mathf.Abs(end.Y - start.Y), 3);
        AssertLineSegmentsMatchPreview(draft);
    }

    [Fact]
    public void RoadPathDraftDefensivelyCopiesPreviewPoints()
    {
        Vector2[] callerOwned = [Vector2.Zero, Vector2.Right];
        var path = new RoadPath([new LineRoadGeometrySegment(Vector2.Zero, Vector2.Right)]);

        var draft = new RoadPathDraft(callerOwned, path);
        callerOwned[1] = Vector2.Down;

        Assert.Equal(Vector2.Right, draft.PreviewTo);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<Vector2>)draft.PreviewPoints)[1] = Vector2.Down);
    }

    [Fact]
    public void ReplacementStrategyPathCanUsePublicGraphSubmissionApi()
    {
        IRoadInputStrategy strategy = new ArbitraryAngleStrategy();
        RoadPathDraft draft = strategy.BuildDraft(Vector2.Zero, new Vector2(7f, 3f));
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPath(draft.Path);

        Assert.True(result.Success);
        LineRoadGeometrySegment geometry = Assert.IsType<LineRoadGeometrySegment>(
            Assert.Single(Assert.Single(graph.GetAllEdges()).GeometrySegments));
        Assert.Equal(new Vector2(7f, 3f), geometry.End);
    }

    [Fact]
    public void RoadBuilderDependsOnStrategyAndPublicPathSubmissionOnly()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "Road", "RoadBuilder.cs"));

        Assert.DoesNotContain("Direction", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GridSystem", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CellSize", source, StringComparison.Ordinal);
        Assert.Contains("SetInputStrategy", source, StringComparison.Ordinal);
        Assert.Contains("_graph.SubmitPath(draft.Path)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RoadBuilderCurveFallbackUsesNativeClosestPointInsteadOfPathAnchors()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "Road", "RoadBuilder.cs"));

        Assert.Contains("segment.FindClosestPoint(pointerPosition)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFullPath", source, StringComparison.Ordinal);
    }

    private static void AssertLineSegmentsMatchPreview(RoadPathDraft draft)
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

    private sealed class ArbitraryAngleStrategy : IRoadInputStrategy
    {
        public float InteractionRadius => 4f;

        public Vector2 SnapPointer(Vector2 worldPosition) => worldPosition;

        public RoadPathDraft BuildDraft(Vector2 startPosition, Vector2 pointerPosition)
        {
            Vector2[] preview = [startPosition, pointerPosition];
            return new RoadPathDraft(
                preview,
                new RoadPath([new LineRoadGeometrySegment(startPosition, pointerPosition)]));
        }
    }
}
