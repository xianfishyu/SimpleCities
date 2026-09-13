namespace SimpleCities.RoadCore.Tests;

public sealed class PrimaryJunctionPlannerTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void CrossingAtPrimaryPoint_SplitsBothRoadsAndPublishesOnce(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        Build(network, new(-2 * cellSize, 0), new(2 * cellSize, 0));
        RoadSnapshot before = network.Snapshot;
        RoadBuildResult result = network.PlanBuild(new(before.Token,
            new(0, -2 * cellSize), new(0, 2 * cellSize), RoadProfileId.Street));

        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(result.Plan!));
        Assert.Equal(5, network.Snapshot.NodeCount);
        Assert.Equal(4, network.Snapshot.EdgeCount);
        RoadNode junction = Assert.Single(network.Snapshot.Nodes, node => node.Position == new RoadPoint(0, 0));
        Assert.All(network.Snapshot.Edges, edge => Assert.True(edge.Start == junction.Id || edge.End == junction.Id));
        Assert.Equal(before.Token.ContentRevision + 1, network.Snapshot.Token.ContentRevision);
        Assert.Equal(before.Token.ChangeSequence + 1, network.Snapshot.Token.ChangeSequence);
    }

    [Theory]
    [InlineData(25, false)]
    [InlineData(50, false)]
    [InlineData(100, false)]
    [InlineData(200, false)]
    [InlineData(25, true)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    [InlineData(200, true)]
    public void EndingAtTheRoadInterior_CreatesATJunctionForCardinalAndDiagonalBranches(int cellSize, bool diagonal)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        Build(network, new(-2 * cellSize, 0), new(2 * cellSize, 0), RoadProfileId.Arterial);
        Build(network, new(diagonal ? -2 * cellSize : 0, -2 * cellSize), new(0, 0));

        Assert.Equal(4, network.Snapshot.NodeCount);
        Assert.Equal(3, network.Snapshot.EdgeCount);
        RoadNode junction = Assert.Single(network.Snapshot.Nodes, node => node.Position == new RoadPoint(0, 0));
        Assert.All(network.Snapshot.Edges, edge => Assert.True(edge.Start == junction.Id || edge.End == junction.Id));
        Assert.Equal(2, network.Snapshot.Edges.Count(edge => edge.Profile == RoadProfileId.Arterial));
        Assert.Single(network.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Street);
    }

    [Fact]
    public void BranchAtAnInternalTurn_PromotesTheTurnToOneStructuralJunction()
    {
        var network = new RoadNetwork();
        Build(network, new(-200, 0), new(0, 0));
        Build(network, new(0, 0), new(0, 200));
        Assert.DoesNotContain(network.Snapshot.Nodes, node => node.Position == new RoadPoint(0, 0));

        Build(network, new(0, 0), new(200, -200));

        Assert.Equal(4, network.Snapshot.NodeCount);
        Assert.Equal(3, network.Snapshot.EdgeCount);
        RoadNode junction = Assert.Single(network.Snapshot.Nodes, node => node.Position == new RoadPoint(0, 0));
        Assert.All(network.Snapshot.Edges, edge =>
        {
            Assert.True(edge.Start == junction.Id || edge.End == junction.Id);
            Assert.Equal(2, edge.Points.Count);
        });
    }

    [Fact]
    public void AdditionalBranch_ReusesTheJunctionAndPreservesExistingRoadIdentities()
    {
        var network = new RoadNetwork();
        Build(network, new(-200, 0), new(200, 0));
        Build(network, new(0, -200), new(0, 200));
        RoadSnapshot cross = network.Snapshot;
        RoadNode junction = Assert.Single(cross.Nodes, node => node.Position == new RoadPoint(0, 0));

        Build(network, new(200, 200), new(0, 0), RoadProfileId.Dirt);

        Assert.Equal(6, network.Snapshot.NodeCount);
        Assert.Equal(5, network.Snapshot.EdgeCount);
        Assert.Equal(junction, Assert.Single(network.Snapshot.Nodes, node => node.Position == new RoadPoint(0, 0)));
        Assert.Equal(cross.NextNodeId + 1, network.Snapshot.NextNodeId);
        Assert.Equal(cross.NextEdgeId + 1, network.Snapshot.NextEdgeId);
        foreach (RoadEdge edge in cross.Edges)
        {
            RoadEdge retained = Assert.Single(network.Snapshot.Edges, item => item.Id == edge.Id);
            Assert.Equal(edge.Start, retained.Start);
            Assert.Equal(edge.End, retained.End);
            Assert.Equal(edge.Points, retained.Points);
        }
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void OneStrokeThroughIndependentRoads_CreatesAllIntersectionsInOnePublication(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        Build(network, new(-2 * cellSize, -cellSize), new(2 * cellSize, -cellSize));
        Build(network, new(-2 * cellSize, cellSize), new(2 * cellSize, cellSize));
        RoadSnapshot before = network.Snapshot;
        RoadBuildResult result = network.PlanBuild(new(before.Token,
            new(0, -2 * cellSize), new(0, 2 * cellSize), RoadProfileId.Arterial));

        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(result.Plan!));
        Assert.Equal(8, network.Snapshot.NodeCount);
        Assert.Equal(7, network.Snapshot.EdgeCount);
        foreach (double y in new[] { -cellSize, cellSize })
        {
            RoadNode junction = Assert.Single(network.Snapshot.Nodes, node => node.Position == new RoadPoint(0, y));
            Assert.Equal(4, network.Snapshot.Edges.Count(edge => edge.Start == junction.Id || edge.End == junction.Id));
        }
        Assert.Equal(before.Token.ChangeSequence + 1, network.Snapshot.Token.ChangeSequence);
    }

    [Fact]
    public void NewJunctionIdentities_FollowTheStrokeAfterItsEndpointsRegardlessOfOldEdgeOrder()
    {
        var network = new RoadNetwork();
        Build(network, new(-200, 100), new(200, 100));
        Build(network, new(-200, -100), new(200, -100));
        long nextNode = network.Snapshot.NextNodeId;

        Build(network, new(0, -200), new(0, 200));

        Assert.Equal(new NodeId(nextNode), Assert.Single(network.Snapshot.Nodes,
            node => node.Position == new RoadPoint(0, -200)).Id);
        Assert.Equal(new NodeId(nextNode + 1), Assert.Single(network.Snapshot.Nodes,
            node => node.Position == new RoadPoint(0, 200)).Id);
        Assert.Equal(new NodeId(nextNode + 2), Assert.Single(network.Snapshot.Nodes,
            node => node.Position == new RoadPoint(0, -100)).Id);
        Assert.Equal(new NodeId(nextNode + 3), Assert.Single(network.Snapshot.Nodes,
            node => node.Position == new RoadPoint(0, 100)).Id);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void StrokeWithPrimaryAndCellCenterContacts_PublishesBothJunctionsTogether(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        Build(network, new(-2 * cellSize, -cellSize), new(2 * cellSize, -cellSize));
        Build(network, new(0, cellSize), new(cellSize, 0));
        RoadSnapshot before = network.Snapshot;
        byte[] content = Save(network);

        RoadBuildResult result = network.PlanBuild(new(before.Token,
            new(-2 * cellSize, -2 * cellSize), new(2 * cellSize, 2 * cellSize), RoadProfileId.Street));

        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.Empty(result.Conflicts);
        Assert.Same(before, network.Snapshot);
        Assert.Equal(content, Save(network));
        Assert.True(network.TryCommit(result.Plan!));
        Assert.Equal(before.Token.ChangeSequence + 1, network.Snapshot.Token.ChangeSequence);
        Assert.Equal(8, network.Snapshot.NodeCount);
        Assert.Equal(7, network.Snapshot.EdgeCount);
        foreach (RoadPoint point in new[] { new RoadPoint(-cellSize, -cellSize), new RoadPoint(cellSize / 2d, cellSize / 2d) })
        {
            RoadNode junction = Assert.Single(network.Snapshot.Nodes, node => node.Position == point);
            Assert.Equal(4, RoadJunctionQuery.Read(network.Snapshot, junction.Id)!.Incidences.Count);
        }
    }

    [Fact]
    public void CrossingThatAlsoOverlaps_ReportsOverlapBeforePlanningAnyJunction()
    {
        var network = new RoadNetwork();
        Build(network, new(-200, 0), new(200, 0));
        Build(network, new(0, 100), new(0, 200));
        RoadSnapshot before = network.Snapshot;
        byte[] content = Save(network);

        RoadBuildResult result = network.PlanBuild(new(before.Token, new(0, -200), new(0, 400), RoadProfileId.Dirt));

        Assert.Equal(RoadBuildStatus.Rejected, result.Status);
        Assert.Null(result.Plan);
        Assert.Equal(new[] { new RoadConflictSpan(0.5, 2d / 3) }, result.Conflicts);
        Assert.Same(before, network.Snapshot);
        Assert.Equal(content, Save(network));
    }

    [Fact]
    public void CancelledOrStaleJunctionPlans_CannotPublishPartialSplits()
    {
        var network = new RoadNetwork();
        Build(network, new(-200, 0), new(200, 0));
        RoadSnapshot before = network.Snapshot;
        var request = new RoadBuildRequest(before.Token, new(0, -200), new(0, 200), RoadProfileId.Street);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => network.PlanBuild(request, cancellation.Token));
        Assert.Same(before, network.Snapshot);
        RoadBuildResult planned = network.PlanBuild(request);
        Assert.Equal(RoadBuildStatus.Ready, planned.Status);
        Build(network, new(200, 0), new(300, 0));
        RoadSnapshot updated = network.Snapshot;
        Assert.False(network.TryCommit(planned.Plan!));
        Assert.Equal(RoadBuildStatus.Rejected, network.PlanBuild(request).Status);
        Assert.Same(updated, network.Snapshot);
        Assert.Equal(2, network.Snapshot.NodeCount);
        Assert.Single(network.Snapshot.Edges);
    }

    [Fact]
    public void JoiningExistingComponents_MergesOnlyOrdinarySameProfileBoundaries()
    {
        var network = new RoadNetwork();
        Build(network, new(-300, 0), new(-100, 0));
        Build(network, new(100, 0), new(300, 0));
        Build(network, new(-100, 0), new(100, 0));

        Assert.Equal(2, network.Snapshot.NodeCount);
        RoadEdge edge = Assert.Single(network.Snapshot.Edges);
        Assert.Equal(new[] { new RoadPoint(-300, 0), new RoadPoint(300, 0) }, edge.Points);
    }

    [Theory]
    [InlineData("nextNodeId")]
    [InlineData("nextEdgeId")]
    public void ExhaustionDuringIntersectionSplitting_LeavesPublishedContentWritableAndReloadable(string watermark)
    {
        var original = new RoadNetwork();
        Build(original, new(-200, 0), new(200, 0));
        System.Text.Json.Nodes.JsonNode payload = System.Text.Json.Nodes.JsonNode.Parse(Save(original))!;
        payload[watermark] = long.MaxValue - 2;
        var network = new RoadNetwork();
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload.ToJsonString()));
        Assert.True(network.TryCommit(network.PlanLoad(RoadCodec.Read(source))));
        RoadSnapshot before = network.Snapshot;
        byte[] content = Save(network);

        RoadBuildResult result = network.PlanBuild(new(before.Token, new(0, -200), new(0, 200), RoadProfileId.Street));

        Assert.Equal(RoadBuildStatus.Rejected, result.Status);
        Assert.Contains("耗尽", result.Reason);
        Assert.Null(result.Plan);
        Assert.Same(before, network.Snapshot);
        Assert.Equal(content, Save(network));
        using var saved = new MemoryStream(content);
        var restored = new RoadNetwork();
        Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(saved))));
        Assert.Equal(content, Save(restored));
    }

    [Fact]
    public void SplittingAtTheTopologyBudget_RejectsTheWholeStroke()
    {
        RoadNetwork network = TopologyCapacityTests.LoadLines(new RoadPoint[][] { [new(0, 1000), new(50, 1000)] }
            .Concat(TopologyCapacityTests.DisconnectedLines(16383)));
        RoadSnapshot before = network.Snapshot;
        Assert.Equal(32768, before.NodeCount);
        Assert.Equal(16384, before.EdgeCount);
        byte[] content = Save(network);

        RoadBuildResult result = network.PlanBuild(new(before.Token,
            new(25, 975), new(25, 1025), RoadProfileId.Street));

        Assert.Equal(RoadBuildStatus.Rejected, result.Status);
        Assert.Contains("资源上限", result.Reason);
        Assert.Null(result.Plan);
        Assert.Same(before, network.Snapshot);
        Assert.Equal(content, Save(network));
    }

    private static byte[] Save(RoadNetwork network)
    {
        using var destination = new MemoryStream();
        RoadCodec.Write(destination, network.Snapshot);
        return destination.ToArray();
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end, RoadProfileId? profile = null)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, profile ?? RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }
}
