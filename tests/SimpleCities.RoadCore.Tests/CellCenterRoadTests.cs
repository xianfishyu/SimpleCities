namespace SimpleCities.RoadCore.Tests;

public sealed class CellCenterRoadTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void DiagonalDrag_StopsAtEitherPrimaryOrCellCenterWithoutOvershootingAHalfCell(int cellSize)
    {
        var map = new MapDefinition(cellSize);
        var primary = new RoadPoint(0, 0);
        var center = new RoadPoint(cellSize / 2d, cellSize / 2d);
        Assert.Equal(center, map.SnapDragEnd(primary, center));
        Assert.Equal(primary, map.SnapDragEnd(center, primary));
        Assert.Equal(new RoadPoint(cellSize, cellSize), map.SnapDragEnd(center, new(cellSize, cellSize)));
    }

    [Fact]
    public void SnapBuildPoint_ChoosesTheNearestLatticeAndPrefersPrimaryAtEqualDistance()
    {
        var map = new MapDefinition(25);
        Assert.Equal(new RoadPoint(12.5, 12.5), map.SnapBuildPoint(new(13, 12)));
        Assert.Equal(new RoadPoint(0, 0), map.SnapBuildPoint(new(6.25, 6.25)));
        Assert.Equal(new RoadPoint(-12.5, -12.5), map.SnapBuildPoint(new(-13, -12)));
        Assert.Equal(new RoadPoint(-4000, 4000), map.SnapBuildPoint(new(-5000, 5000)));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void DiagonalCrossing_CreatesOneCellCenterJunctionWithoutPublishingDuringPlanning(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        Build(network, new(0, 0), new(cellSize, cellSize));
        RoadSnapshot before = network.Snapshot;

        RoadBuildResult result = network.PlanBuild(new(before.Token,
            new(0, cellSize), new(cellSize, 0), RoadProfileId.Street));

        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(result.Plan!));
        Assert.Equal(5, network.Snapshot.NodeCount);
        Assert.Equal(4, network.Snapshot.EdgeCount);
        RoadNode center = Assert.Single(network.Snapshot.Nodes,
            node => node.Position == new RoadPoint(cellSize / 2d, cellSize / 2d));
        Assert.All(network.Snapshot.Edges, edge => Assert.True(edge.Start == center.Id || edge.End == center.Id));
        Assert.Equal(before.Token.ChangeSequence + 1, network.Snapshot.Token.ChangeSequence);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void CellCenterTJunction_AcceptsItsFourthDiagonalBranchAndReusesTheNode(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        var center = new RoadPoint(cellSize / 2d, cellSize / 2d);
        Build(network, new(0, 0), new(cellSize, cellSize));
        Build(network, center, new(cellSize, 0));
        RoadSnapshot tee = network.Snapshot;
        RoadNode junction = Assert.Single(tee.Nodes, node => node.Position == center);
        Assert.Equal(4, tee.NodeCount);
        Assert.Equal(3, tee.EdgeCount);

        Build(network, new(0, cellSize), center);

        Assert.Equal(junction, Assert.Single(network.Snapshot.Nodes, node => node.Position == center));
        Assert.Equal(5, network.Snapshot.NodeCount);
        Assert.Equal(4, network.Snapshot.EdgeCount);
        Assert.Equal(tee.NextNodeId + 1, network.Snapshot.NextNodeId);
        Assert.Equal(tee.NextEdgeId + 1, network.Snapshot.NextEdgeId);
        Assert.All(tee.Edges, old => Assert.Contains(network.Snapshot.Edges, edge =>
            edge.Id == old.Id && edge.Start == old.Start && edge.End == old.End && edge.Points.SequenceEqual(old.Points)));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void AllFourDiagonals_CanStartOrEndAtACellCenter(int cellSize)
    {
        var center = new RoadPoint(cellSize / 2d, cellSize / 2d);
        foreach (int x in new[] { 0, cellSize })
        foreach (int y in new[] { 0, cellSize })
        foreach (bool outward in new[] { false, true })
        {
            var network = new RoadNetwork(new MapDefinition(cellSize));
            var primary = new RoadPoint(x, y);
            Build(network, outward ? center : primary, outward ? primary : center);
            Assert.Equal(2, network.Snapshot.NodeCount);
            Assert.Single(network.Snapshot.Nodes, node => node.Position == center);
            Assert.Single(network.Snapshot.Edges);
        }
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void CellCenterCardinalDirections_StayCardinalInPreviewAndAreExplicitlyRejected(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        var center = new RoadPoint(cellSize / 2d, cellSize / 2d);
        RoadSnapshot before = network.Snapshot;
        foreach (RoadPoint end in new RoadPoint[]
                 { new(center.X + cellSize, center.Y), new(center.X - cellSize, center.Y),
                     new(center.X, center.Y + cellSize), new(center.X, center.Y - cellSize) })
        {
            Assert.Equal(end, before.Map.SnapDragEnd(center, end));
            RoadBuildResult result = network.PlanBuild(new(before.Token, center, end, RoadProfileId.Street));
            Assert.Equal(RoadBuildStatus.Rejected, result.Status);
            Assert.Contains("格心仅", result.Reason);
            Assert.Null(result.Plan);
            Assert.Empty(result.Conflicts);
            Assert.Same(before, network.Snapshot);
        }
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void MixedParityQuarterCellsAndOutsidePoints_AreRejectedWithoutConsumingIds(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        RoadSnapshot before = network.Snapshot;
        Assert.True(before.Map.IsCellCenter(new(-cellSize / 2d, cellSize / 2d)));
        Assert.False(before.Map.IsPrimaryPoint(new(cellSize / 2d, cellSize / 2d)));
        foreach (RoadPoint invalid in new RoadPoint[]
                 { new(cellSize / 2d, 0), new(0, cellSize / 2d), new(cellSize / 4d, cellSize / 4d),
                     new(4000 + cellSize / 2d, 4000 + cellSize / 2d), new(double.NaN, 0) })
        {
            Assert.False(before.Map.IsBuildPoint(invalid));
            RoadBuildResult result = network.PlanBuild(new(before.Token, new(0, 0), invalid, RoadProfileId.Street));
            Assert.Equal(RoadBuildStatus.Rejected, result.Status);
            Assert.Null(result.Plan);
            Assert.Same(before, network.Snapshot);
        }
        Build(network, new(0, 0), new(cellSize / 2d, cellSize / 2d));
        Assert.Equal(new NodeId(1), network.Snapshot.Nodes[0].Id);
        Assert.Equal(new EdgeId(1), Assert.Single(network.Snapshot.Edges).Id);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void CellCenterOverlap_PreservesTheExactHalfCellConflictInBothStrokeDirections(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        Build(network, new(cellSize / 2d, cellSize / 2d), new(cellSize, cellSize));
        RoadSnapshot before = network.Snapshot;
        RoadBuildResult forward = network.PlanBuild(new(before.Token,
            new(0, 0), new(2 * cellSize, 2 * cellSize), RoadProfileId.Highway));
        RoadBuildResult reverse = network.PlanBuild(new(before.Token,
            new(2 * cellSize, 2 * cellSize), new(0, 0), RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Rejected, forward.Status);
        Assert.Equal(RoadBuildStatus.Rejected, reverse.Status);
        Assert.Equal(new[] { new RoadConflictSpan(0.25, 0.5) }, forward.Conflicts);
        Assert.Equal(new[] { new RoadConflictSpan(0.5, 0.75) }, reverse.Conflicts);
        Assert.Same(before, network.Snapshot);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void CellCenterBoundaryDrag_PreservesItsDirectionAndStopsAtTheLastValidPoint(int cellSize)
    {
        var map = new MapDefinition(cellSize);
        double half = cellSize / 2d;
        foreach (int sign in new[] { -1, 1 })
        {
            var network = new RoadNetwork(map);
            var center = new RoadPoint(sign * (4000 - half), sign * (4000 - half));
            RoadPoint boundary = map.SnapDragEnd(center, new(sign * 5000, sign * 5000));
            Assert.Equal(new RoadPoint(sign * 4000, sign * 4000), boundary);
            Build(network, center, boundary);
            Assert.Equal(boundary, map.SnapDragEnd(boundary, new(sign * 5000, sign * 5000)));
        }
        // 横竖草稿仍按整格保留格心奇偶，不因矩形裁剪产生混合奇偶的终点。
        var originCenter = new RoadPoint(half, half);
        Assert.Equal(new RoadPoint(4000 - half, half), map.SnapDragEnd(originCenter, new(5000, half)));
        Assert.Equal(new RoadPoint(half, -4000 + half), map.SnapDragEnd(originCenter, new(half, -5000)));
    }

    [Fact]
    public void CancelledOrStaleCellCenterPlan_DoesNotPublishSplitsOrConsumeIds()
    {
        var network = new RoadNetwork(new MapDefinition(25));
        Build(network, new(0, 0), new(25, 25));
        RoadSnapshot before = network.Snapshot;
        var request = new RoadBuildRequest(before.Token, new(0, 25), new(25, 0), RoadProfileId.Street);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => network.PlanBuild(request, cancellation.Token));
        Assert.Same(before, network.Snapshot);
        RoadBuildResult planned = network.PlanBuild(request);
        Assert.Equal(RoadBuildStatus.Ready, planned.Status);
        Assert.Same(before, network.Snapshot);
        Build(network, new(-100, -100), new(-75, -75));
        RoadSnapshot current = network.Snapshot;
        Assert.Equal(before.NextNodeId + 2, current.NextNodeId);
        Assert.Equal(before.NextEdgeId + 1, current.NextEdgeId);
        Assert.False(network.TryCommit(planned.Plan!));
        Assert.Equal(RoadBuildStatus.Rejected, network.PlanBuild(request).Status);
        Assert.Same(current, network.Snapshot);
        Assert.DoesNotContain(current.Nodes, node => node.Position == new RoadPoint(12.5, 12.5));
    }

    [Fact]
    public void CellCenterConnectionWithinOneComponent_CreatesLoopAndBranch()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 100));
        Build(network, new(0, 0), new(100, 0));
        RoadSnapshot before = network.Snapshot;
        RoadBuildResult result = network.PlanBuild(new(before.Token, new(100, 0), new(50, 50), RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(result.Plan!));
        RoadNode center = Assert.Single(network.Snapshot.Nodes, node => node.Position == new RoadPoint(50, 50));
        Assert.Equal(3, RoadJunctionQuery.Read(network.Snapshot, center.Id)!.Incidences.Count);
        Assert.Single(network.Snapshot.Edges, edge => edge.Start == edge.End);
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }
}
