namespace SimpleCities.RoadCore.Tests;

public sealed class RoadSpatialQueryTests
{
    [Fact]
    public void LocalQueryAfterDistantGrowthKeepsExactWorkAndSourceRange()
    {
        var network = new RoadNetwork();
        Build(network, new(-4000, 0), new(4000, 0));
        var first = new RoadSpatialQueryIndex(network.Snapshot).QueryNearby(new(50, 0), 1);
        Assert.Equal(SpatialQueryStatus.Ready, first.Status);
        RoadQueryHit hit = Assert.Single(first.Results);
        Assert.Equal(new RoadPoint(50, 0), network.Snapshot.Resolve(hit.Location));
        Assert.InRange(hit.Fragment.Start.DistanceTo(hit.Fragment.End), 0, 100);
        Assert.Equal(0, first.Metrics.FullEdgeVisits);
        for (int y = 2000; y <= 3900; y += 100)
            Build(network, new(2000, y), new(3900, y));
        var second = new RoadSpatialQueryIndex(network.Snapshot).QueryNearby(new(50, 0), 1);
        Assert.Equal(SpatialQueryStatus.Ready, second.Status);
        Assert.Single(second.Results);
        Assert.Equal(first.Metrics.ExactGeometryTests, second.Metrics.ExactGeometryTests);
        Assert.Equal(first.Metrics.FragmentCandidates, second.Metrics.FragmentCandidates);
        Assert.Equal(network.Snapshot.Token, second.Source);
    }

    [Fact]
    public void RectangleAndGeometryQueriesSeparateBoundsCandidatesFromExactHits()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 100));
        var index = new RoadSpatialQueryIndex(network.Snapshot);
        var missed = index.QueryBounds(new(0, 30, 20, 50));
        Assert.Equal(SpatialQueryStatus.Ready, missed.Status);
        Assert.Empty(missed.Results);
        Assert.True(missed.Metrics.FragmentCandidates > 0);
        Assert.True(missed.Metrics.ExactGeometryTests > 0);
        var crossed = index.QuerySegment(new(0, 100), new(100, 0));
        Assert.Equal(SpatialQueryStatus.Ready, crossed.Status);
        Assert.All(crossed.Results, hit => Assert.Equal(0.5, hit.Location.Parameter, 12));
        Assert.NotEmpty(crossed.Results);
        Assert.Equal(1, crossed.Metrics.Hits);
    }

    [Fact]
    public void QueryBudgetsAndMalformedParametersNeverReturnSuccessfulEmptyResults()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(1000, 0));
        var index = new RoadSpatialQueryIndex(network.Snapshot);
        foreach (SpatialQueryBudget limits in new[]
        {
            new SpatialQueryBudget(1, 4096, 16384),
            new SpatialQueryBudget(4096, 1, 16384),
            new SpatialQueryBudget(4096, 4096, 1),
        })
        {
            var rejected = index.QueryNearby(new(500, 0), 500, limits);
            Assert.Equal(SpatialQueryStatus.BudgetExceeded, rejected.Status);
            Assert.Empty(rejected.Results);
            Assert.NotEmpty(rejected.Reason);
            Assert.Equal(0, rejected.Metrics.FullEdgeVisits);
            Assert.Equal(0, rejected.Metrics.Hits);
        }
        foreach (var invalid in new[]
        {
            index.QueryNearby(new(double.NaN, 0), 1),
            index.QueryNearby(new(50, 0), double.PositiveInfinity),
            index.QueryNearby(new(50, 0), -1),
            index.QueryNearby(new(50, 0), 1, new SpatialQueryBudget(0, 10, 10)),
            index.QueryBounds(new(10, 0, -10, 0)),
            index.QuerySegment(new(0, 0), new(double.NaN, 0)),
        })
        {
            Assert.Equal(SpatialQueryStatus.InvalidParameters, invalid.Status);
            Assert.Empty(invalid.Results);
            Assert.NotEmpty(invalid.Reason);
        }
    }

    [Fact]
    public void LoopSeamRetainsBothEndRolesAndItsStableSelectableSpan()
    {
        var network = new RoadNetwork();
        Build(network, new(50, 50), new(0, 0));
        Build(network, new(0, 0), new(100, 0));
        Build(network, new(100, 0), new(50, 50));
        RoadSnapshot snapshot = network.Snapshot;
        var atSeam = new RoadSpatialQueryIndex(snapshot).QueryNearby(new(50, 50), 0);
        Assert.Equal(SpatialQueryStatus.Ready, atSeam.Status);
        Assert.Equal(2, atSeam.Results.Count);
        Assert.Contains(atSeam.Results, hit => hit.Fragment.OwnsStart && hit.Location.Parameter == 0);
        Assert.Contains(atSeam.Results, hit => hit.Fragment.OwnsEnd && hit.Location.Parameter == 1);
        Assert.Single(atSeam.Results.Select(hit => RoadSpanQuery.Pick(snapshot, hit.Location)!.Key).Distinct());
        Assert.Equal(1, atSeam.Metrics.Hits);
    }

    [Fact]
    public void AcrossMapDiagonalUsesPathBucketsRatherThanItsWholeBoundingRectangle()
    {
        var network = new RoadNetwork();
        Build(network, new(-100, 0), new(100, 0));
        var result = new RoadSpatialQueryIndex(network.Snapshot).QuerySegment(new(-4000, -4000), new(4000, 4000),
            new SpatialQueryBudget(256, 4096, 16384));
        Assert.Equal(SpatialQueryStatus.Ready, result.Status);
        Assert.NotEmpty(result.Results);
        Assert.InRange(result.Metrics.BucketsVisited, 81, 241);
        Assert.Equal(0, result.Metrics.FullEdgeVisits);
        Assert.All(result.Results, hit => Assert.Equal(new RoadPoint(0, 0), network.Snapshot.Resolve(hit.Location)));
    }

    [Fact]
    public void DistinctParallelPathsKeepTheirEdgeAndGridSpanOwnership()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(300, 0));
        Build(network, new(0, 0), new(0, 100));
        Build(network, new(0, 100), new(300, 100));
        Build(network, new(300, 100), new(300, 0));
        Build(network, new(-100, 0), new(0, 0));
        Build(network, new(300, 0), new(400, 0));
        var index = new RoadSpatialQueryIndex(network.Snapshot);
        RoadQueryHit bottom = Assert.Single(index.QueryNearby(new(150, 0), 0).Results);
        RoadQueryHit top = Assert.Single(index.QueryNearby(new(150, 100), 0).Results);
        Assert.NotEqual(bottom.Location.Edge, top.Location.Edge);
        foreach ((RoadQueryHit hit, int y) in new[] { (bottom, 0), (top, 100) })
        {
            RoadGridSpan span = RoadSpanQuery.Pick(network.Snapshot, hit.Location)!;
            Assert.Equal(network.Snapshot.Token, span.Source);
            Assert.Equal(hit.Location.Edge, span.Edge);
            Assert.Contains(new RoadPoint(100, y), span.Points);
            Assert.Contains(new RoadPoint(200, y), span.Points);
        }
    }

    [Fact]
    public void DenseEightWayJunctionKeepsEndpointAmbiguityAndLocalBranchSpans()
    {
        var network = new RoadNetwork();
        Build(network, new(-100, 0), new(100, 0));
        Build(network, new(0, -100), new(0, 100));
        Build(network, new(-100, -100), new(100, 100));
        Build(network, new(-100, 100), new(100, -100));
        var index = new RoadSpatialQueryIndex(network.Snapshot);
        var center = index.QueryNearby(new(0, 0), 0);
        Assert.Equal(8, center.Metrics.Hits);
        Assert.All(center.Results, hit => Assert.Null(RoadSpanQuery.Pick(network.Snapshot, hit.Location)));
        RoadQueryHit branch = Assert.Single(index.QueryNearby(new(50, 0), 0).Results);
        RoadGridSpan span = RoadSpanQuery.Pick(network.Snapshot, branch.Location)!;
        Assert.Contains(new RoadPoint(0, 0), span.Points);
        Assert.Contains(new RoadPoint(100, 0), span.Points);
        Assert.Equal(0, center.Metrics.FullEdgeVisits);
    }

    [Fact]
    public void BroadPhaseConstructionRejectsNonFiniteAndUnboundedFragments()
    {
        RoadStateToken token = new RoadNetwork().Snapshot.Token;
        foreach (double size in new[] { double.NaN, double.PositiveInfinity, 0, -1 })
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialQueryIndex<int>(token, [], size));
        Assert.Throws<ArgumentException>(() => new SpatialQueryIndex<int>(token, [new(1, new(double.NaN, 0, 1, 1))]));
        Assert.Throws<ArgumentException>(() => new SpatialQueryIndex<int>(token, [new(1, new(-4000, -4000, 4000, 4000))]));
    }

    [Fact]
    public void ZeroRadiusQueryKeepsExactGridLocationsAlongLongOffsetDiagonal()
    {
        var network = new RoadNetwork();
        Build(network, new(-4000, -3900), new(3900, 4000));
        var index = new RoadSpatialQueryIndex(network.Snapshot);
        for (int coordinate = -3900; coordinate <= 3800; coordinate += 100)
        {
            var result = index.QueryNearby(new(coordinate, coordinate + 100), 0);
            Assert.Equal(SpatialQueryStatus.Ready, result.Status);
            Assert.NotEmpty(result.Results);
            Assert.All(result.Results, hit => Assert.Equal(0, hit.Distance));
        }
    }

    [Theory]
    [InlineData(-107374182349d, 0d, 107374182349d, 0d)]
    [InlineData(0d, -107374182349d, 0d, 107374182349d)]
    [InlineData(-107374182349d, -107374182349d, 107374182349d, 107374182349d)]
    public void BucketCoverageOverflowCannotBypassFragmentConstructionLimit(double minX, double minY, double maxX, double maxY)
    {
        RoadStateToken token = new RoadNetwork().Snapshot.Token;
        Assert.Throws<ArgumentException>(() => new SpatialQueryIndex<int>(token, [new(1, new(minX, minY, maxX, maxY))]));
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end)
    {
        RoadBuildResult built = network.PlanBuild(new(network.Snapshot.Token, start, end, RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, built.Status);
        Assert.True(network.TryCommit(built.Plan!));
    }
}
