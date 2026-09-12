namespace SimpleCities.RoadCore.Tests;

public sealed class RoadOverlapTests
{
    [Fact]
    public void CompleteDuplicate_IsRejectedAsOverlapWithoutChangingPublishedContent()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(400, 0));
        RoadSnapshot before = network.Snapshot;
        byte[] content = Save(network);

        RoadBuildResult result = network.PlanBuild(new(before.Token, new(0, 0), new(400, 0), RoadProfileId.Street));

        Assert.Equal(RoadBuildStatus.Rejected, result.Status);
        Assert.Null(result.Plan);
        Assert.Contains("重叠", result.Reason);
        Assert.Equal(new[] { new RoadConflictSpan(0, 1) }, result.Conflicts);
        Assert.Same(before, network.Snapshot);
        Assert.Equal(content, Save(network));
    }

    [Theory]
    [InlineData(1, 0, 25, "street")]
    [InlineData(-1, 0, 50, "dirt")]
    [InlineData(0, 1, 100, "arterial")]
    [InlineData(0, -1, 200, "highway")]
    [InlineData(1, 1, 25, "dirt")]
    [InlineData(-1, 1, 50, "arterial")]
    [InlineData(1, -1, 100, "highway")]
    [InlineData(-1, -1, 200, "street")]
    public void PartialOverlap_ReportsOnlyTheDraftConflictInEitherDrawingDirection(int x, int y, int cellSize, string profile)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        Build(network, new(0, 0), new(4 * x * cellSize, 4 * y * cellSize));
        RoadPoint a = new(-2 * x * cellSize, -2 * y * cellSize);
        RoadPoint b = new(2 * x * cellSize, 2 * y * cellSize);
        RoadSnapshot before = network.Snapshot;
        byte[] content = Save(network);

        AssertOverlap(network.PlanBuild(new(before.Token, a, b, new RoadProfileId(profile))), new RoadConflictSpan(0.5, 1));
        AssertOverlap(network.PlanBuild(new(before.Token, b, a, new RoadProfileId(profile))), new RoadConflictSpan(0, 0.5));

        Assert.Same(before, network.Snapshot);
        Assert.Equal(content, Save(network));
    }

    [Theory]
    [InlineData(0, 400, 0, 1)] // Same complete road.
    [InlineData(400, 0, 0, 1)] // Complete duplicate drawn backwards.
    [InlineData(100, 300, 0, 1)] // Draft entirely inside an existing road.
    [InlineData(-200, 600, 0.25, 0.75)] // Draft contains the entire existing road.
    [InlineData(200, 600, 0, 0.5)] // Draft extends beyond the old endpoint.
    public void SameAndDifferentProfiles_UseTheSameCoverageRule(double start, double end, double conflictStart, double conflictEnd)
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(400, 0));
        RoadSnapshot before = network.Snapshot;
        byte[] content = Save(network);
        foreach (RoadProfileId profile in new[] { RoadProfileId.Street, RoadProfileId.Highway })
        {
            RoadBuildResult result = network.PlanBuild(new(before.Token, new(start, 0), new(end, 0), profile));
            AssertOverlap(result, new RoadConflictSpan(conflictStart, conflictEnd));
        }
        Assert.Same(before, network.Snapshot);
        Assert.Equal(content, Save(network));
    }

    [Fact]
    public void SeparateConflictsWithinOneMergedPolyline_AreSortedInDraftDirectionAndSurviveReload()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 0));
        Build(network, new(100, 0), new(100, 100));
        Build(network, new(100, 100), new(300, 100));
        Build(network, new(300, 100), new(300, 0));
        Build(network, new(300, 0), new(500, 0));
        Assert.Single(network.Snapshot.Edges);
        byte[] content = Save(network);
        using var source = new MemoryStream(content);
        var restored = new RoadNetwork();
        Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(source))));

        foreach (RoadNetwork candidate in new[] { network, restored })
        {
            RoadSnapshot before = candidate.Snapshot;
            AssertOverlap(candidate.PlanBuild(new(before.Token, new(-100, 0), new(700, 0), RoadProfileId.Street)),
                new RoadConflictSpan(0.125, 0.25), new RoadConflictSpan(0.5, 0.75));
            AssertOverlap(candidate.PlanBuild(new(before.Token, new(700, 0), new(-100, 0), RoadProfileId.Dirt)),
                new RoadConflictSpan(0.25, 0.5), new RoadConflictSpan(0.75, 0.875));
            Assert.Same(before, candidate.Snapshot);
            Assert.Equal(content, Save(candidate));
        }
    }

    [Fact]
    public void AdjacentCoverageAcrossProfileBoundaries_IsOneImmutableConflict()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(200, 0));
        Build(network, new(200, 0), new(400, 0), RoadProfileId.Dirt);
        Assert.Equal(2, network.Snapshot.EdgeCount);

        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, new(-200, 0), new(600, 0), RoadProfileId.Highway));

        AssertOverlap(result, new RoadConflictSpan(0.25, 0.75));
        IList<RoadConflictSpan> list = Assert.IsAssignableFrom<IList<RoadConflictSpan>>(result.Conflicts);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = new(0, 1));
        AssertOverlap(result, new RoadConflictSpan(0.25, 0.75));
    }

    [Theory]
    [InlineData(400, 0, 600, 0, true)] // Endpoint touch, continuing straight.
    [InlineData(400, 0, 600, 200, true)] // Endpoint touch, turning away.
    [InlineData(200, -200, 200, 200, false)] // Geometric point crossing.
    [InlineData(200, 200, 200, 0, false)] // Touch inside an existing segment.
    [InlineData(0, 25, 400, 25, false)] // Parallel nearby centerline, regardless of road width.
    public void PointContactsAndNearbyCenterlines_AreNotConstructionOverlap(double ax, double ay, double bx, double by, bool ready)
    {
        var network = new RoadNetwork(new MapDefinition(25));
        Build(network, new(0, 0), new(400, 0), RoadProfileId.Highway);
        RoadSnapshot before = network.Snapshot;
        byte[] content = Save(network);

        RoadBuildResult result = network.PlanBuild(new(before.Token, new(ax, ay), new(bx, by), RoadProfileId.Highway));

        Assert.Empty(result.Conflicts);
        Assert.DoesNotContain("重叠", result.Reason);
        Assert.Equal(ready ? RoadBuildStatus.Ready : RoadBuildStatus.Rejected, result.Status);
        Assert.Same(before, network.Snapshot);
        Assert.Equal(content, Save(network));
    }

    [Fact]
    public void ZeroLengthAndInvalidInputs_AreHandledBeforeConflictClassification()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(400, 0));
        RoadSnapshot before = network.Snapshot;
        byte[] content = Save(network);

        RoadBuildResult unchanged = network.PlanBuild(new(before.Token, new(200, 0), new(200, 0), RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.NoChange, unchanged.Status);
        Assert.Null(unchanged.Plan);
        Assert.Empty(unchanged.Conflicts);
        foreach (RoadBuildRequest invalid in new[]
        {
            new RoadBuildRequest(before.Token, new(0, 0), new(400, 0), default),
            new RoadBuildRequest(before.Token, new(50, 0), new(400, 0), RoadProfileId.Street),
            new RoadBuildRequest(before.Token, new(0, 0), new(4100, 0), RoadProfileId.Street),
            new RoadBuildRequest(before.Token, new(0, 0), new(400, 100), RoadProfileId.Street),
        })
        {
            RoadBuildResult result = network.PlanBuild(invalid);
            Assert.Equal(RoadBuildStatus.Rejected, result.Status);
            Assert.Null(result.Plan);
            Assert.Empty(result.Conflicts);
            Assert.DoesNotContain("重叠", result.Reason);
        }
        Assert.Same(before, network.Snapshot);
        Assert.Equal(content, Save(network));
    }

    [Fact]
    public void StaleSuccessfulPreview_CannotCommitOverNewRoadAndFreshPlanningDetectsTheOverlap()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(200, 0));
        var request = new RoadBuildRequest(network.Snapshot.Token, new(200, 0), new(400, 0), RoadProfileId.Street);
        RoadBuildResult preview = network.PlanBuild(request);
        Assert.Equal(RoadBuildStatus.Ready, preview.Status);
        Assert.Empty(preview.Conflicts);
        Build(network, new(200, 0), new(300, 0));
        RoadSnapshot before = network.Snapshot;
        byte[] content = Save(network);

        Assert.False(network.TryCommit(preview.Plan!));
        RoadBuildResult stale = network.PlanBuild(request);
        Assert.Equal(RoadBuildStatus.Rejected, stale.Status);
        Assert.Contains("过期", stale.Reason);
        Assert.Empty(stale.Conflicts);
        Assert.Null(stale.Plan);
        AssertOverlap(network.PlanBuild(request with { Source = before.Token }), new RoadConflictSpan(0, 0.5));

        Assert.Same(before, network.Snapshot);
        Assert.Equal(content, Save(network));
    }

    [Fact]
    public void CancelledOverlapPlanning_DoesNotPublishOrConsumeIds()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(400, 0));
        RoadSnapshot before = network.Snapshot;
        byte[] content = Save(network);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => network.PlanBuild(
            new(before.Token, new(0, 0), new(400, 0), RoadProfileId.Street), cancellation.Token));

        Assert.Same(before, network.Snapshot);
        Assert.Equal(content, Save(network));
    }

    private static void AssertOverlap(RoadBuildResult result, params RoadConflictSpan[] expected)
    {
        Assert.Equal(RoadBuildStatus.Rejected, result.Status);
        Assert.Null(result.Plan);
        Assert.Contains("重叠", result.Reason);
        Assert.Equal(expected, result.Conflicts);
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end, RoadProfileId? profile = null)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, profile ?? RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }

    private static byte[] Save(RoadNetwork network)
    {
        using var destination = new MemoryStream();
        RoadCodec.Write(destination, network.Snapshot);
        return destination.ToArray();
    }
}
