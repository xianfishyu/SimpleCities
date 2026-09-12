namespace SimpleCities.RoadCore.Tests;

public sealed class RoadSpanQueryTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void GridIntervalsFollowDirectionAndCellSize(int cell)
    {
        foreach (RoadPoint end in new RoadPoint[] { new(-10 * cell, 0), new(0, -10 * cell), new(10 * cell, 10 * cell) })
        {
            var network = new RoadNetwork(new MapDefinition(cell));
            Build(network, new(0, 0), end);
            RoadSnapshot snapshot = network.Snapshot;
            RoadEdge edge = Assert.Single(snapshot.Edges);
            RoadGridSpan span = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 0.45))!;
            Assert.Equal(new RoadPoint(end.X * 0.4, end.Y * 0.4), span.Points[0]);
            Assert.Equal(new RoadPoint(end.X * 0.5, end.Y * 0.5), span.Points[^1]);
            Assert.Equal(0.4, span.Ranges[0].StartParameter, 12);
            Assert.Equal(0.5, span.Ranges[0].EndParameter, 12);
        }
    }

    [Fact]
    public void OrdinaryCellCenterTurn_RemainsOneSpanAcrossBothDiagonalPieces()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(50, 50));
        Build(network, new(50, 50), new(100, 0));
        RoadSnapshot snapshot = network.Snapshot;
        RoadEdge edge = Assert.Single(snapshot.Edges);
        RoadGridSpan left = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 0.25))!;
        RoadGridSpan right = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 0.75))!;
        Assert.Equal(left.Key, right.Key);
        Assert.Equal(new[] { new RoadPoint(0, 0), new RoadPoint(50, 50), new RoadPoint(100, 0) }, left.Points);
        Assert.Equal(new[] { new RoadSpanRange(0, 1) }, left.Ranges);
    }

    [Fact]
    public void TrueCellCenterJunction_TruncatesEachHalfAndRejectsTheAmbiguousCenter()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 100));
        Build(network, new(0, 100), new(100, 0));
        RoadSnapshot snapshot = network.Snapshot;
        RoadNode center = Assert.Single(snapshot.Nodes, node => node.Position == new RoadPoint(50, 50));
        var keys = new HashSet<RoadSpanKey>();
        foreach (RoadEdge edge in snapshot.Edges)
        {
            double endpoint = edge.Start == center.Id ? 0 : 1;
            Assert.Null(RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, endpoint)));
            RoadGridSpan half = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 0.5))!;
            Assert.Equal(2, half.Points.Count);
            Assert.Contains(center.Position, half.Points);
            Assert.Equal(new[] { new RoadSpanRange(0, 1) }, half.Ranges);
            Assert.True(keys.Add(half.Key));
            Assert.NotNull(RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 1 - endpoint)));
        }
    }

    [Fact]
    public void CellCenterTypeBoundary_TruncatesSpanDespiteDegreeTwo()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(50, 50));
        Build(network, new(50, 50), new(100, 100), RoadProfileId.Highway);
        RoadSnapshot snapshot = network.Snapshot;
        Assert.Equal(2, snapshot.EdgeCount);
        Assert.All(snapshot.Edges, edge =>
        {
            RoadGridSpan span = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 0.5))!;
            Assert.Contains(new RoadPoint(50, 50), span.Points);
            Assert.Equal(2, span.Points.Count);
        });
    }

    [Fact]
    public void SyntheticCellCenterLoopSeam_IsTransparentAndHasOneStableWrapKey()
    {
        var network = new RoadNetwork();
        Build(network, new(50, 50), new(0, 0));
        Build(network, new(0, 0), new(100, 0));
        Build(network, new(100, 0), new(50, 50));
        RoadSnapshot snapshot = network.Snapshot;
        RoadEdge edge = Assert.Single(snapshot.Edges);
        Assert.Equal(new RoadPoint(50, 50), edge.Points[0]);
        RoadGridSpan first = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 0.01))!;
        RoadGridSpan last = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 0.99))!;
        Assert.Equal(first.Key, last.Key);
        Assert.Equal(first.Key, RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 0))!.Key);
        Assert.Equal(first.Key, RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 1))!.Key);
        Assert.Equal(2, first.Ranges.Count);
        Assert.Equal(1, first.Ranges[0].EndParameter);
        Assert.Equal(0, first.Ranges[1].StartParameter);
        Assert.Equal(new RoadPoint(50, 50), first.Points[1]);
        Assert.True(new HashSet<RoadPoint> { new(0, 0), new(100, 0) }.SetEquals(
            first.Points.Where(point => point != new RoadPoint(50, 50))));
    }

    [Fact]
    public void ExpiredForeignAndMalformedLocations_AreRejected()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(1000, 0));
        RoadSnapshot before = network.Snapshot;
        RoadEdge edge = Assert.Single(before.Edges);
        var stale = new RoadLocation(before.Token, edge.Id, 0.5);
        Build(network, new(0, 100), new(100, 100));
        Assert.Null(RoadSpanQuery.Pick(network.Snapshot, stale));
        Assert.Null(RoadSpanQuery.Pick(new RoadNetwork().Snapshot, stale));
        Assert.Null(RoadSpanQuery.Pick(before, new(before.Token, new EdgeId(999), 0.5)));
        foreach (double parameter in new[] { double.NaN, double.PositiveInfinity, -0.01, 1.01 })
            Assert.Null(RoadSpanQuery.Pick(before, new(before.Token, edge.Id, parameter)));
    }

    [Fact]
    public void LongFoldedCanonicalEdge_PicksOneIntervalWithoutExpandingItsOtherGridSpans()
    {
        var network = new RoadNetwork(new MapDefinition(25));
        Build(network, new(-4000, 0), new(4000, 0));
        Build(network, new(4000, 0), new(4000, 25));
        Build(network, new(4000, 25), new(-4000, 25));
        RoadSnapshot snapshot = network.Snapshot;
        RoadEdge edge = Assert.Single(snapshot.Edges);
        RoadGridSpan span = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 0.75))!;
        Assert.Equal(new[] { new RoadPoint(25, 25), new RoadPoint(0, 25) }, span.Points);
        Assert.Single(span.Ranges);
    }

    [Fact]
    public void LongCanonicalEdge_PicksOnlyTheHitPrimaryGridInterval()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(1000, 0));
        RoadSnapshot before = network.Snapshot;
        RoadEdge edge = Assert.Single(before.Edges);

        RoadGridSpan span = Assert.IsType<RoadGridSpan>(RoadSpanQuery.Pick(before, new(before.Token, edge.Id, 0.45)));

        Assert.Equal(before.Token, span.Source);
        Assert.Equal(edge.Id, span.Edge);
        Assert.Equal(new[] { new RoadSpanRange(0.4, 0.5) }, span.Ranges);
        Assert.Equal(new[] { new RoadPoint(400, 0), new RoadPoint(500, 0) }, span.Points);
        Assert.Same(before, network.Snapshot);
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end, RoadProfileId? profile = null)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, profile ?? RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }
}
