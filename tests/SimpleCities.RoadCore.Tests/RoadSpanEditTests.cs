namespace SimpleCities.RoadCore.Tests;

public sealed class RoadSpanEditTests
{
    [Fact]
    public void SpanFromDifferentUncommittedTargetWithSameToken_IsRejectedBeforeAnyEdit()
    {
        var network = new RoadNetwork();
        RoadSnapshot empty = network.Snapshot;
        RoadBuildResult candidate = network.PlanBuild(new(empty.Token, new(0, 0), new(200, 0), RoadProfileId.Street));
        RoadBuildResult committed = network.PlanBuild(new(empty.Token, new(0, 0), new(1000, 0), RoadProfileId.Street));
        RoadSnapshot other = candidate.Plan!.Target;
        RoadGridSpan span = RoadSpanQuery.Pick(other, new(other.Token, Assert.Single(other.Edges).Id, 0.75))!;
        Assert.True(network.TryCommit(committed.Plan!));
        RoadSnapshot before = network.Snapshot;
        Assert.Equal(other.Token, before.Token);

        Assert.Equal(RoadEditStatus.Rejected, network.PlanRemove(span).Status);
        Assert.Equal(RoadEditStatus.Rejected, network.PlanChangeProfile(span, RoadProfileId.Highway).Status);
        Assert.Equal(RoadEditStatus.Rejected, network.PlanChangeProfile(span, RoadProfileId.Street).Status);
        Assert.Same(before, network.Snapshot);
    }

    [Fact]
    public void SameProfile_ReturnsSilentNoChangeWithoutPublishingOrReservingIdentities()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(1000, 0));
        RoadSnapshot before = network.Snapshot;
        RoadGridSpan span = Pick(network, Assert.Single(before.Edges), 0.45);
        byte[] bytes = Save(before);

        RoadEditResult result = network.PlanChangeProfile(span, RoadProfileId.Street);

        Assert.Equal(RoadEditStatus.NoChange, result.Status);
        Assert.Null(result.Plan);
        Assert.Empty(result.Reason);
        Assert.Same(before, network.Snapshot);
        Assert.Equal(bytes, Save(network.Snapshot));
    }

    [Fact]
    public void ChangeMiddleGridSpan_ChangesOnlyItsProfileAndCanChangeBack()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(1000, 0));
        RoadSnapshot before = network.Snapshot;
        RoadGridSpan span = RoadSpanQuery.Pick(before, new(before.Token, Assert.Single(before.Edges).Id, 0.45))!;

        RoadEditResult result = network.PlanChangeProfile(span, RoadProfileId.Highway);

        Assert.Equal(RoadEditStatus.Ready, result.Status);
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(result.Plan!));
        RoadEdge changed = Assert.Single(network.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Highway);
        Assert.Equal(new[] { new RoadPoint(400, 0), new RoadPoint(500, 0) }, changed.Points);
        Assert.Equal(900, network.Snapshot.Edges.Where(edge => edge.Profile == RoadProfileId.Street).Sum(edge => edge.Length));
        RoadGridSpan reverse = RoadSpanQuery.Pick(network.Snapshot, new(network.Snapshot.Token, changed.Id, 0.5))!;
        RoadEditResult back = network.PlanChangeProfile(reverse, RoadProfileId.Street);
        Assert.Equal(RoadEditStatus.Ready, back.Status);
        Assert.True(network.TryCommit(back.Plan!));
        RoadEdge restored = Assert.Single(network.Snapshot.Edges);
        Assert.Equal(RoadProfileId.Street, restored.Profile);
        Assert.Equal(new[] { new RoadPoint(0, 0), new RoadPoint(1000, 0) }, restored.Points);
        Assert.Equal(2, network.Snapshot.NodeCount);
    }

    [Fact]
    public void RemoveMiddleGridSpan_PreservesBothRemainingRoadsAndPublishesOnlyOnCommit()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(1000, 0));
        RoadSnapshot before = network.Snapshot;
        RoadEdge original = Assert.Single(before.Edges);
        var location = new RoadLocation(before.Token, original.Id, 0.45);
        RoadGridSpan span = RoadSpanQuery.Pick(before, location)!;

        RoadEditResult result = network.PlanRemove(span);

        Assert.Equal(RoadEditStatus.Ready, result.Status);
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(result.Plan!));
        Assert.Equal(2, network.Snapshot.EdgeCount);
        Assert.Contains(network.Snapshot.Edges, edge => edge.Points.Contains(new RoadPoint(0, 0)) && edge.Points.Contains(new RoadPoint(400, 0)));
        Assert.Contains(network.Snapshot.Edges, edge => edge.Points.Contains(new RoadPoint(500, 0)) && edge.Points.Contains(new RoadPoint(1000, 0)));
        Assert.Equal(900, network.Snapshot.Edges.Sum(edge => edge.Length));
        Assert.Equal(before.Token.ContentRevision + 1, network.Snapshot.Token.ContentRevision);
        Assert.Null(network.Snapshot.Resolve(location));
    }

    [Fact]
    public void EveryBuiltInProfile_CanChangeToEveryOtherBuiltInProfile()
    {
        RoadProfileId[] profiles = [RoadProfileId.Dirt, RoadProfileId.Street, RoadProfileId.Arterial, RoadProfileId.Highway];
        foreach (RoadProfileId from in profiles)
        foreach (RoadProfileId to in profiles.Where(profile => profile != from))
        {
            var network = new RoadNetwork();
            Build(network, new(0, 0), new(100, 0), from);
            RoadEditResult result = network.PlanChangeProfile(Pick(network, Assert.Single(network.Snapshot.Edges)), to);
            Assert.Equal(RoadEditStatus.Ready, result.Status);
            Assert.True(network.TryCommit(result.Plan!));
            Assert.Equal(to, Assert.Single(network.Snapshot.Edges).Profile);
            AssertRoundTrip(network);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CellCenterJunction_EditAffectsOnlySelectedHalfAndPreservesOtherBranches(bool changeProfile)
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 100));
        Build(network, new(0, 100), new(100, 0));
        RoadSnapshot before = network.Snapshot;
        RoadEdge selected = Assert.Single(before.Edges, edge => edge.Points.Contains(new RoadPoint(0, 0)));
        RoadEditResult result = changeProfile
            ? network.PlanChangeProfile(Pick(network, selected), RoadProfileId.Dirt)
            : network.PlanRemove(Pick(network, selected));
        Assert.Equal(RoadEditStatus.Ready, result.Status);
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(result.Plan!));
        foreach (RoadEdge unaffected in before.Edges.Where(edge => edge.Id != selected.Id))
        {
            RoadEdge after = Assert.Single(network.Snapshot.Edges, edge => edge.Id == unaffected.Id);
            Assert.Equal(unaffected.Points, after.Points);
            Assert.Equal(unaffected.Profile, after.Profile);
        }
        RoadNode center = Assert.Single(network.Snapshot.Nodes, node => node.Position == new RoadPoint(50, 50));
        Assert.Equal(changeProfile ? 4 : 3, RoadJunctionQuery.Read(network.Snapshot, center.Id)!.Incidences.Count);
        if (changeProfile)
        {
            RoadEdge changed = Assert.Single(network.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Dirt);
            Assert.Equal(selected.Points, changed.Points);
        }
        else
        {
            Assert.DoesNotContain(network.Snapshot.Nodes, node => node.Position == new RoadPoint(0, 0));
            Assert.DoesNotContain(network.Snapshot.Edges, edge => edge.Id == selected.Id);
        }
        AssertRoundTrip(network);
    }

    [Fact]
    public void RemovingBranch_RejoinsRemainingRoadAndRemovingLastRoadLeavesAnEmptyNetwork()
    {
        var network = new RoadNetwork();
        Build(network, new(-100, 0), new(100, 0));
        Build(network, new(0, 0), new(0, 100));
        RoadEdge branch = Assert.Single(network.Snapshot.Edges, edge => edge.Points.Contains(new RoadPoint(0, 100)));
        RoadEditResult removal = network.PlanRemove(Pick(network, branch));
        Assert.Equal(RoadEditStatus.Ready, removal.Status);
        Assert.True(network.TryCommit(removal.Plan!));
        RoadEdge joined = Assert.Single(network.Snapshot.Edges);
        Assert.Equal(new[] { new RoadPoint(-100, 0), new RoadPoint(100, 0) }, joined.Points);
        Assert.Equal(2, network.Snapshot.NodeCount);
        AssertRoundTrip(network);
        for (int remaining = 1; remaining >= 0; remaining--)
        {
            RoadEditResult result = network.PlanRemove(Pick(network, Assert.Single(network.Snapshot.Edges), 0.25));
            Assert.Equal(RoadEditStatus.Ready, result.Status);
            Assert.True(network.TryCommit(result.Plan!));
            Assert.Equal(remaining, network.Snapshot.EdgeCount);
        }
        Assert.Empty(network.Snapshot.Nodes);
        AssertRoundTrip(network);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SyntheticCenterSeam_EditUsesBothWrapRangesAndRenormalizesTheLoop(bool changeProfile)
    {
        var network = new RoadNetwork();
        Build(network, new(50, 50), new(0, 0));
        Build(network, new(0, 0), new(100, 0));
        Build(network, new(100, 0), new(50, 50));
        RoadGridSpan wrap = Pick(network, Assert.Single(network.Snapshot.Edges), 0.01);
        Assert.Equal(2, wrap.Ranges.Count);
        RoadEditResult result = changeProfile
            ? network.PlanChangeProfile(wrap, RoadProfileId.Highway)
            : network.PlanRemove(wrap);
        Assert.Equal(RoadEditStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
        Assert.DoesNotContain(network.Snapshot.Nodes, node => node.Position == new RoadPoint(50, 50));
        RoadEdge straight = Assert.Single(network.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Street);
        Assert.Equal(100, straight.Length);
        Assert.Equal(2, straight.Points.Count);
        if (changeProfile)
        {
            RoadEdge changed = Assert.Single(network.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Highway);
            Assert.Contains(new RoadPoint(50, 50), changed.Points);
            Assert.Equal(100 * Math.Sqrt(2), changed.Length, 8);
            RoadEditResult back = network.PlanChangeProfile(Pick(network, changed), RoadProfileId.Street);
            Assert.Equal(RoadEditStatus.Ready, back.Status);
            Assert.True(network.TryCommit(back.Plan!));
            RoadEdge loop = Assert.Single(network.Snapshot.Edges);
            Assert.Equal(loop.Start, loop.End);
            Assert.Equal(1, network.Snapshot.NodeCount);
        }
        else Assert.Single(network.Snapshot.Edges);
        AssertRoundTrip(network);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForeignStaleCancelledAndSupersededEdits_LeaveTheCurrentSnapshotUntouched(bool changeProfile)
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(1000, 0));
        RoadGridSpan span = Pick(network, Assert.Single(network.Snapshot.Edges), 0.45);
        RoadEditResult Plan(RoadNetwork target, CancellationToken token = default) => changeProfile
            ? target.PlanChangeProfile(span, RoadProfileId.Highway, token)
            : target.PlanRemove(span, token);
        var foreign = new RoadNetwork();
        RoadSnapshot foreignBefore = foreign.Snapshot;
        Assert.Equal(RoadEditStatus.Rejected, Plan(foreign).Status);
        Assert.Same(foreignBefore, foreign.Snapshot);
        RoadSnapshot before = network.Snapshot;
        Assert.Throws<OperationCanceledException>(() => Plan(network, new CancellationToken(true)));
        Assert.Same(before, network.Snapshot);
        RoadEditResult planned = Plan(network);
        Assert.Equal(RoadEditStatus.Ready, planned.Status);
        Build(network, new(0, 100), new(100, 100));
        RoadSnapshot current = network.Snapshot;
        Assert.Equal(RoadEditStatus.Rejected, Plan(network).Status);
        Assert.False(network.TryCommit(planned.Plan!));
        Assert.Same(current, network.Snapshot);
    }

    private static void AssertRoundTrip(RoadNetwork network)
    {
        byte[] bytes = Save(network.Snapshot);
        using var stream = new MemoryStream(bytes);
        var restored = new RoadNetwork();
        Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(stream))));
        Assert.Equal(bytes, Save(restored.Snapshot));
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end, RoadProfileId? profile = null)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, profile ?? RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }

    private static RoadGridSpan Pick(RoadNetwork network, RoadEdge edge, double parameter = 0.5) =>
        RoadSpanQuery.Pick(network.Snapshot, new(network.Snapshot.Token, edge.Id, parameter))!;

    private static byte[] Save(RoadSnapshot snapshot)
    {
        using var stream = new MemoryStream();
        RoadCodec.Write(stream, snapshot);
        return stream.ToArray();
    }
}
