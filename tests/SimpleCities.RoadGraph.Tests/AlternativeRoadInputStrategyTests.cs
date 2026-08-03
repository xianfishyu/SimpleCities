using Godot;

namespace SimpleCities.Tests;

public sealed class AlternativeRoadInputStrategyTests
{
    private const float StepLength = 64f;
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void HexStrategySnapsToNearestAxialCellCenter()
    {
        var strategy = new HexSixRoadInputStrategy(StepLength);

        Vector2 snapped = strategy.SnapPointer(new Vector2(30f, 58f));

        AssertVectorClose(new Vector2(0f, 64f), snapped);
        AssertVectorClose(snapped, strategy.SnapPointer(snapped));
    }

    [Fact]
    public void HexStrategyProjectsOntoSixEqualLengthDirections()
    {
        var strategy = new HexSixRoadInputStrategy(StepLength);
        var start = new Vector2(32f, -128f);

        RoadPathDraft draft = strategy.BuildDraft(start, new Vector2(32f, 130f));

        Assert.Equal(5, draft.PreviewPoints.Count);
        for (int index = 1; index < draft.PreviewPoints.Count; index++)
        {
            Vector2 step = draft.PreviewPoints[index] - draft.PreviewPoints[index - 1];
            Assert.Equal(StepLength, step.Length(), 3);
            AssertVectorClose(new Vector2(0f, StepLength), step);
        }
        AssertLineSegmentsMatchPreview(draft);
    }

    [Fact]
    public void TriangularStrategySnapsToNearestTriangleCenter()
    {
        var strategy = new TriangularThreeRoadInputStrategy(StepLength);

        Vector2 snapped = strategy.SnapPointer(new Vector2(4f, 60f));

        AssertVectorClose(new Vector2(0f, 64f), snapped);
        AssertVectorClose(snapped, strategy.SnapPointer(snapped));
    }

    [Fact]
    public void TriangularStrategyAlternatesThreeNeighborSets()
    {
        var strategy = new TriangularThreeRoadInputStrategy(StepLength);

        RoadPathDraft draft = strategy.BuildDraft(Vector2.Zero, new Vector2(300f, 0f));

        Assert.True(draft.PreviewPoints.Count > 3);
        Vector2 first = draft.PreviewPoints[1] - draft.PreviewPoints[0];
        Vector2 second = draft.PreviewPoints[2] - draft.PreviewPoints[1];
        Assert.Equal(StepLength, first.Length(), 3);
        Assert.Equal(StepLength, second.Length(), 3);
        Assert.True(first.X > 0f && first.Y < 0f);
        Assert.True(second.X > 0f && second.Y > 0f);
        Assert.NotEqual(first, second);
        AssertLineSegmentsMatchPreview(draft);
    }

    [Fact]
    public void SamePointerTraceProducesEachGridSpecificPath()
    {
        IRoadInputStrategy[] strategies =
        [
            new SquareEightRoadInputStrategy(StepLength),
            new HexSixRoadInputStrategy(StepLength),
            new TriangularThreeRoadInputStrategy(StepLength),
        ];
        var pointer = new Vector2(190f, 80f);

        Vector2[] endpoints = strategies
            .Select(strategy => strategy.BuildDraft(Vector2.Zero, pointer).PreviewTo)
            .ToArray();

        Assert.Equal(3, endpoints.Distinct().Count());
        AssertVectorClose(new Vector2(128f, 128f), endpoints[0]);
        AssertVectorClose(new Vector2(Mathf.Sqrt(3f) * 96f, 96f), endpoints[1]);
        AssertVectorClose(new Vector2(Mathf.Sqrt(3f) * 64f, 64f), endpoints[2]);
    }

    [Fact]
    public void EveryGridStrategySatisfiesTheSameDraftAndSubmissionContract()
    {
        IRoadInputStrategy[] strategies =
        [
            new SquareEightRoadInputStrategy(StepLength),
            new HexSixRoadInputStrategy(StepLength),
            new TriangularThreeRoadInputStrategy(StepLength),
        ];

        foreach (IRoadInputStrategy strategy in strategies)
        {
            Vector2 start = strategy.SnapPointer(Vector2.Zero);
            RoadPathDraft tooShort = strategy.BuildDraft(start, new Vector2(20f, 0f));
            RoadPathDraft draft = strategy.BuildDraft(start, new Vector2(190f, 80f));
            var graph = new RoadGraph();

            Assert.False(tooShort.CanCommit);
            Assert.True(draft.CanCommit);
            AssertLineSegmentsMatchPreview(draft);
            Assert.True(graph.SubmitPath(draft.Path).Success);
            graph.AssertInvariants();
        }
    }

    [Fact]
    public void StrategyOutputsShareCrossingSplitAndPersistencePipeline()
    {
        var graph = new RoadGraph();
        var square = new SquareEightRoadInputStrategy(StepLength);
        var hex = new HexSixRoadInputStrategy(StepLength);
        var triangular = new TriangularThreeRoadInputStrategy(StepLength);

        RoadPathSubmissionResult squareResult = graph.SubmitPath(
            square.BuildDraft(Vector2.Zero, new Vector2(130f, 0f)).Path);
        Assert.True(squareResult.Success);
        int crossedEdgeID = Assert.Single(graph.GetAllEdges(), edge =>
            edge.GeometrySegments[0].Start == Vector2.Zero &&
            edge.GeometrySegments[0].End == new Vector2(64f, 0f)).ID;

        RoadPathSubmissionResult hexResult = graph.SubmitPath(
            hex.BuildDraft(new Vector2(32f, -128f), new Vector2(32f, 130f)).Path);
        Assert.True(hexResult.Success);
        Assert.Contains(crossedEdgeID, hexResult.Changes.RemovedEdgeIDs);
        Assert.Null(graph.GetEdge(crossedEdgeID));
        Assert.Contains(graph.GetAllNodes(), node => node.Position == new Vector2(32f, 0f));

        RoadPathSubmissionResult triangularResult = graph.SubmitPath(
            triangular.BuildDraft(new Vector2(320f, 0f), new Vector2(510f, 80f)).Path);
        Assert.True(triangularResult.Success);
        graph.AssertInvariants();

        string saved = SaveJson.Serialize(graph.CaptureState());
        var restored = new RoadGraph();
        restored.RestoreState(saved);

        restored.AssertInvariants();
        Assert.Equal(saved, SaveJson.Serialize(restored.CaptureState()));
    }

    [Fact]
    public void AlternativeStrategiesDoNotAddBranchesToRoadBuilderOrRoadGraph()
    {
        string roadBuilder = File.ReadAllText(Path.Combine(ProjectRoot, "Scripts", "Road", "RoadBuilder.cs"));
        Assert.DoesNotContain("TriangularThreeRoadInputStrategy", roadBuilder, StringComparison.Ordinal);
        Assert.DoesNotContain("HexSixRoadInputStrategy", roadBuilder, StringComparison.Ordinal);

        string[] roadGraphFiles = Directory.GetFiles(
            Path.Combine(ProjectRoot, "Scripts", "Road"),
            "RoadGraph*.cs",
            SearchOption.TopDirectoryOnly);
        Assert.All(roadGraphFiles, path =>
        {
            string source = File.ReadAllText(path);
            Assert.DoesNotContain("IRoadInputStrategy", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TriangularThreeRoadInputStrategy", source, StringComparison.Ordinal);
            Assert.DoesNotContain("HexSixRoadInputStrategy", source, StringComparison.Ordinal);
        });
    }

    private static void AssertLineSegmentsMatchPreview(RoadPathDraft draft)
    {
        RoadPath path = Assert.IsType<RoadPath>(draft.Path);
        Assert.Equal(draft.PreviewPoints.Count - 1, path.Segments.Count);
        for (int index = 0; index < path.Segments.Count; index++)
        {
            LineRoadGeometrySegment line = Assert.IsType<LineRoadGeometrySegment>(path.Segments[index]);
            AssertVectorClose(draft.PreviewPoints[index], line.Start);
            AssertVectorClose(draft.PreviewPoints[index + 1], line.End);
        }
    }

    private static void AssertVectorClose(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }
}
