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
        RoadPath path = Assert.IsType<RoadPath>(draft.Path);
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPath(
            new RoadBuildRequest(path, RoadType.Street));

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
        Assert.Contains("public RoadType SelectedRoadType", source, StringComparison.Ordinal);
        Assert.Contains("SetSelectedRoadType", source, StringComparison.Ordinal);
        Assert.Contains(
            "SelectedRoadType);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "new RoadBuildRequest(draft.Path, session.RoadType)",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new RoadBuildRequest(draft.Path, RoadType.Street)",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RoadBuilderPlacementFreezesAndRevalidatesPresentedSurfaceToken()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "Road", "RoadBuilder.cs"));
        string loadCommitSource = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "Scripts",
            "Road",
            "RoadBuilder.LoadCommit.cs"));

        Assert.Contains("private RoadRenderToken? _placementRenderToken;", source, StringComparison.Ordinal);
        Assert.Contains(
            "!TryCaptureCurrentRoadSurfaceToken(surfaceProvider, out RoadRenderToken renderToken)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("_placementRenderToken = renderToken;", source, StringComparison.Ordinal);

        string beginPlace = ExtractMethod(source, "public bool BeginPlace", "public void UpdatePlace");
        Assert.True(
            beginPlace.IndexOf("if (_loadAdmission is not null)", StringComparison.Ordinal) <
            beginPlace.IndexOf("GetAdmittedPlacementSession()", StringComparison.Ordinal));
        string confirmPlace = ExtractMethod(source, "public bool ConfirmPlace", "public bool CommitPlace");
        Assert.Contains("GetAdmittedPlacementSession()", confirmPlace, StringComparison.Ordinal);
        Assert.Contains("_placementRenderToken = null;", source, StringComparison.Ordinal);
        Assert.Contains("_owner._placementRenderToken = null;", loadCommitSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RoadBuilderHistoryCommandsRequireCurrentPresentation()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "Road", "RoadBuilder.cs"));

        Assert.Contains(
            "public bool CanUndoLastEdit() => CanUndo && IsRoadCommandAdmitted();",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "public bool CanRedoLastEdit() => CanRedo && IsRoadCommandAdmitted();",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryCaptureCurrentRoadSurfaceToken(surfaceProvider, out _)",
            source,
            StringComparison.Ordinal);

        string undo = ExtractMethod(source, "public bool UndoLastEdit", "public bool RedoLastEdit");
        string redo = ExtractMethod(source, "public bool RedoLastEdit", "public void CancelPlaceSession");
        Assert.Contains("if (!IsRoadCommandAdmitted())", undo, StringComparison.Ordinal);
        Assert.Contains("if (!IsRoadCommandAdmitted())", redo, StringComparison.Ordinal);
    }

    [Fact]
    public void InputStrategyAndPathDraftRemainGeometryOnly()
    {
        string inputDirectory = Path.Combine(ProjectRoot, "Scripts", "Road", "Input");
        string strategy = File.ReadAllText(Path.Combine(inputDirectory, "IRoadInputStrategy.cs"));
        string draft = File.ReadAllText(Path.Combine(inputDirectory, "RoadPathDraft.cs"));

        Assert.DoesNotContain("RoadType", strategy, StringComparison.Ordinal);
        Assert.DoesNotContain("RoadType", draft, StringComparison.Ordinal);
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

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing source marker: {startMarker}");
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing source marker after {startMarker}: {endMarker}");
        return source[start..end];
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
