using System.Text.Json.Nodes;

namespace SimpleCities.RoadCore.Tests;

public sealed class RoadSpanBatchEditTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void AdjacentDiagonalSpans_DeduplicateRegardlessOfOrderAndDoNotAllocateInteriorBoundaries(int cell)
    {
        var network = new RoadNetwork(new MapDefinition(cell));
        Build(network, new(0, 0), new(10 * cell, 10 * cell));
        RoadSnapshot before = network.Snapshot;
        RoadEdge edge = Assert.Single(before.Edges);
        RoadGridSpan[] spans = Enumerable.Range(0, 8).Select(i => Pick(before, edge, (i + 0.5) / 10)).ToArray();

        RoadEditResult forward = network.PlanChangeProfile(spans, RoadProfileId.Highway);
        RoadEditResult returning = network.PlanChangeProfile(spans.Reverse().Concat(spans).ToArray(), RoadProfileId.Highway);

        Assert.Equal(RoadEditStatus.Ready, forward.Status);
        Assert.Equal(RoadEditStatus.Ready, returning.Status);
        Assert.Equal(Save(forward.Plan!.Target), Save(returning.Plan!.Target));
        Assert.Equal(before.NextNodeId + 1, forward.Plan.Target.NextNodeId);
        Assert.Equal(3, forward.Plan.Target.NodeCount);
        Assert.Equal(2, forward.Plan.Target.EdgeCount);
        Assert.Equal(8 * cell * Math.Sqrt(2), Assert.Single(forward.Plan.Target.Edges, road => road.Profile == RoadProfileId.Highway).Length, 8);
        Assert.Equal(Save(forward.Plan.Target), ApplyChange(before, forward.Plan.ChangeSet));
        Assert.True(network.TryCommit(forward.Plan));
        AssertRoundTrip(network.Snapshot);
    }

    [Fact]
    public void MixedProfilesAcrossAJunction_ChangeOnlyDifferentIntervalsAndAllSameOrEmptyIsSilent()
    {
        var network = new RoadNetwork();
        Build(network, new(-200, 0), new(200, 0));
        Build(network, new(0, -200), new(0, 200), RoadProfileId.Dirt);
        RoadSnapshot before = network.Snapshot;
        RoadGridSpan[] changed = before.Edges.Where(edge => edge.Profile == RoadProfileId.Street)
            .Select(edge => Pick(before, edge, 0.75)).ToArray();
        RoadEdge[] unchanged = before.Edges.Where(edge => edge.Profile == RoadProfileId.Dirt).ToArray();
        RoadGridSpan[] selection = changed.Concat(unchanged.Select(edge => Pick(before, edge, 0.25))).ToArray();

        RoadEditResult result = network.PlanChangeProfile(selection, RoadProfileId.Dirt);

        Assert.Equal(RoadEditStatus.Ready, result.Status);
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(result.Plan!));
        Assert.Equal(600, network.Snapshot.Edges.Where(edge => edge.Profile == RoadProfileId.Dirt).Sum(edge => edge.Length));
        Assert.Equal(200, network.Snapshot.Edges.Where(edge => edge.Profile == RoadProfileId.Street).Sum(edge => edge.Length));
        foreach (RoadEdge edge in unchanged)
        {
            RoadEdge current = network.Snapshot.FindEdge(edge.Id)!;
            Assert.Equal(edge.Points, current.Points);
            Assert.Equal(edge.Profile, current.Profile);
            Assert.DoesNotContain(result.Plan!.ChangeSet.Edges, change => change.Id == edge.Id);
        }
        RoadSnapshot after = network.Snapshot;
        byte[] bytes = Save(after);
        RoadGridSpan[] same = after.Edges.Where(edge => edge.Profile == RoadProfileId.Dirt)
            .Select(edge => Pick(after, edge, 0.25)).ToArray();
        foreach (RoadEditResult noChange in new[] { network.PlanChangeProfile(same, RoadProfileId.Dirt), network.PlanRemove(Array.Empty<RoadGridSpan>()) })
        {
            Assert.Equal(RoadEditStatus.NoChange, noChange.Status);
            Assert.Null(noChange.Plan);
            Assert.Empty(noChange.Reason);
        }
        Assert.Same(after, network.Snapshot);
        Assert.Equal(bytes, Save(network.Snapshot));
        AssertRoundTrip(after);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CellCenterOppositeHalves_EditTogetherWithoutSelectingTheOtherDiagonal(bool changeProfile)
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 100));
        Build(network, new(0, 100), new(100, 0));
        RoadSnapshot before = network.Snapshot;
        RoadGridSpan[] selected = before.Edges.Where(edge => edge.Points.Contains(new RoadPoint(0, 0)) ||
            edge.Points.Contains(new RoadPoint(100, 100))).Select(edge => Pick(before, edge)).ToArray();

        RoadEditResult result = changeProfile ? network.PlanChangeProfile(selected, RoadProfileId.Dirt) : network.PlanRemove(selected);

        Assert.Equal(RoadEditStatus.Ready, result.Status);
        Assert.Equal(Save(result.Plan!.Target), ApplyChange(before, result.Plan.ChangeSet));
        Assert.True(network.TryCommit(result.Plan));
        Assert.Equal(100 * Math.Sqrt(2), network.Snapshot.Edges.Where(edge => edge.Profile == RoadProfileId.Street).Sum(edge => edge.Length), 8);
        if (changeProfile)
        {
            Assert.Equal(4, network.Snapshot.EdgeCount);
            Assert.Equal(100 * Math.Sqrt(2), network.Snapshot.Edges.Where(edge => edge.Profile == RoadProfileId.Dirt).Sum(edge => edge.Length), 8);
        }
        else
        {
            RoadEdge remaining = Assert.Single(network.Snapshot.Edges);
            Assert.Equal(2, remaining.Points.Count);
            Assert.Contains(new RoadPoint(0, 100), remaining.Points);
            Assert.Contains(new RoadPoint(100, 0), remaining.Points);
        }
        AssertRoundTrip(network.Snapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SyntheticLoopSeamAndAdjacentSpan_FormOneSelectedIntervalAndPreserveTheRest(bool changeProfile)
    {
        var network = new RoadNetwork();
        Build(network, new(50, 50), new(0, 0));
        Build(network, new(0, 0), new(200, 0));
        Build(network, new(200, 0), new(200, 200));
        Build(network, new(200, 200), new(50, 50));
        RoadSnapshot before = network.Snapshot;
        RoadEdge loop = Assert.Single(before.Edges);
        RoadGridSpan wrap = Pick(before, loop, 0.01);
        RoadGridSpan next = Pick(before, loop, 0.18);
        Assert.Equal(2, wrap.Ranges.Count);
        RoadGridSpan[] selected = [wrap, next, wrap];

        RoadEditResult result = changeProfile ? network.PlanChangeProfile(selected, RoadProfileId.Highway) : network.PlanRemove(selected);

        Assert.Equal(RoadEditStatus.Ready, result.Status);
        Assert.Equal(Save(result.Plan!.Target), ApplyChange(before, result.Plan.ChangeSet));
        Assert.True(network.TryCommit(result.Plan));
        Assert.Equal(300 + 100 * Math.Sqrt(2), network.Snapshot.Edges.Where(edge => edge.Profile == RoadProfileId.Street).Sum(edge => edge.Length), 8);
        Assert.DoesNotContain(network.Snapshot.Nodes, node => node.Position == new RoadPoint(50, 50));
        Assert.Equal(2, network.Snapshot.NodeCount);
        Assert.Equal(changeProfile ? 2 : 1, network.Snapshot.EdgeCount);
        if (changeProfile)
            Assert.Equal(100 + 100 * Math.Sqrt(2), Assert.Single(network.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Highway).Length, 8);
        AssertRoundTrip(network.Snapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AForeignCandidateAfterAValidSpan_RejectsTheWholeBatchIncludingSameProfileSelections(bool changeProfile)
    {
        var network = new RoadNetwork();
        RoadStateToken empty = network.Snapshot.Token;
        RoadBuildResult candidate = network.PlanBuild(new(empty, new(0, 0), new(200, 0), RoadProfileId.Street));
        RoadBuildResult chosen = network.PlanBuild(new(empty, new(0, 0), new(1000, 0), RoadProfileId.Street));
        RoadSnapshot other = candidate.Plan!.Target;
        RoadGridSpan invalid = Pick(other, Assert.Single(other.Edges), 0.75);
        Assert.True(network.TryCommit(chosen.Plan!));
        RoadSnapshot before = network.Snapshot;
        Assert.Equal(other.Token, before.Token);
        RoadGridSpan valid = Pick(before, Assert.Single(before.Edges), 0.45);
        RoadGridSpan[] selected = [valid, invalid, valid];
        byte[] bytes = Save(before);

        RoadEditResult result = changeProfile ? network.PlanChangeProfile(selected, RoadProfileId.Street) : network.PlanRemove(selected);

        Assert.Equal(RoadEditStatus.Rejected, result.Status);
        Assert.Null(result.Plan);
        Assert.Same(before, network.Snapshot);
        Assert.Equal(bytes, Save(network.Snapshot));
        Assert.Throws<OperationCanceledException>(() => network.PlanRemove(new[] { valid }, new CancellationToken(true)));
        Assert.Same(before, network.Snapshot);
        RoadEditResult stale = network.PlanRemove(new[] { valid });
        Build(network, new(0, 200), new(1000, 200));
        RoadSnapshot current = network.Snapshot;
        Assert.False(network.TryCommit(stale.Plan!));
        Assert.Equal(RoadEditStatus.Rejected, network.PlanRemove(new[] { valid }).Status);
        Assert.Same(current, network.Snapshot);
    }

    [Fact]
    public void AllocationFailureInALaterSelectedRoad_DiscardsEarlierPrivateEdits()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(1000, 0));
        Build(network, new(0, 200), new(1000, 200));
        JsonObject document = JsonNode.Parse(Save(network.Snapshot))!.AsObject();
        document["nextNodeId"] = long.MaxValue - 4;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(document.ToJsonString()));
        Assert.True(network.TryCommit(network.PlanLoad(RoadCodec.Read(stream))));
        RoadSnapshot before = network.Snapshot;
        RoadGridSpan[] selected = before.Edges.Select(edge => Pick(before, edge, 0.45)).ToArray();
        byte[] bytes = Save(before);

        RoadEditResult result = network.PlanChangeProfile(selected, RoadProfileId.Highway);

        Assert.Equal(RoadEditStatus.Rejected, result.Status);
        Assert.Null(result.Plan);
        Assert.Same(before, network.Snapshot);
        Assert.Equal(bytes, Save(network.Snapshot));
        Assert.Equal(RoadEditStatus.NoChange, network.PlanChangeProfile(selected, RoadProfileId.Street).Status);
        Assert.Same(before, network.Snapshot);
    }

    [Fact]
    public void BatchChangeSet_ReconstructsTheCompleteTargetAndRetainsOnlyChangedEntities()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(1000, 0));
        Build(network, new(0, 500), new(1000, 500), RoadProfileId.Dirt);
        RoadSnapshot before = network.Snapshot;
        RoadEdge selected = Assert.Single(before.Edges, edge => edge.Profile == RoadProfileId.Street);
        RoadEdge distant = Assert.Single(before.Edges, edge => edge.Profile == RoadProfileId.Dirt);
        RoadEditResult result = network.PlanChangeProfile(new[] { Pick(before, selected, 0.15), Pick(before, selected, 0.65) }, RoadProfileId.Highway);
        Assert.Equal(RoadEditStatus.Ready, result.Status);
        RoadPlan plan = result.Plan!;

        RoadChangeSet change = plan.ChangeSet;

        Assert.Equal(before.Token, change.Before.Token);
        Assert.Equal(plan.Target.Token, change.After.Token);
        Assert.Equal(before.NextNodeId, change.Before.NextNodeId);
        Assert.Equal(before.NextEdgeId, change.Before.NextEdgeId);
        Assert.Equal(plan.Target.NextNodeId, change.After.NextNodeId);
        Assert.Equal(plan.Target.NextEdgeId, change.After.NextEdgeId);
        Assert.Equal(4, change.Nodes.Count);
        Assert.All(change.Nodes, node => Assert.Null(node.Before));
        Assert.Equal(5, change.Edges.Count);
        Assert.DoesNotContain(change.Edges, edge => edge.Id == distant.Id);
        Assert.Equal(Save(plan.Target), ApplyChange(before, change));
        Assert.Equal(Save(before), ApplyChange(plan.Target, change, reverse: true));
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(plan));
        Assert.Same(change, plan.ChangeSet);
    }

    [Fact]
    public void RemoveDisjointAndRepeatedSpans_PreservesGapsAndPublishesTheWholeGestureOnce()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(1000, 0));
        RoadSnapshot before = network.Snapshot;
        RoadEdge edge = Assert.Single(before.Edges);
        RoadGridSpan first = Pick(before, edge, 0.15);
        RoadGridSpan second = Pick(before, edge, 0.65);

        RoadEditResult result = network.PlanRemove(new[] { first, second, first });

        Assert.Equal(RoadEditStatus.Ready, result.Status);
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(result.Plan!));
        Assert.Equal(before.Token.ChangeSequence + 1, network.Snapshot.Token.ChangeSequence);
        Assert.Equal(before.Token.ContentRevision + 1, network.Snapshot.Token.ContentRevision);
        Assert.Equal(3, network.Snapshot.EdgeCount);
        Assert.Contains(network.Snapshot.Edges, road => road.Points.SequenceEqual(new[] { new RoadPoint(0, 0), new RoadPoint(100, 0) }));
        Assert.Contains(network.Snapshot.Edges, road => road.Points.Contains(new RoadPoint(200, 0)) && road.Points.Contains(new RoadPoint(600, 0)));
        Assert.Contains(network.Snapshot.Edges, road => road.Points.Contains(new RoadPoint(700, 0)) && road.Points.Contains(new RoadPoint(1000, 0)));
        Assert.False(network.TryCommit(result.Plan!));
        AssertRoundTrip(network.Snapshot);
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end, RoadProfileId? profile = null)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, profile ?? RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }

    private static RoadGridSpan Pick(RoadSnapshot snapshot, RoadEdge edge, double parameter = 0.5) =>
        RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, parameter))!;

    private static byte[] Save(RoadSnapshot snapshot)
    {
        using var stream = new MemoryStream();
        RoadCodec.Write(stream, snapshot);
        return stream.ToArray();
    }

    // A history consumer can reconstruct domain content through public metadata and codec alone.
    private static byte[] ApplyChange(RoadSnapshot before, RoadChangeSet change, bool reverse = false)
    {
        JsonObject document = JsonNode.Parse(Save(before))!.AsObject();
        RoadChangeMetadata target = reverse ? change.Before : change.After;
        document["contentRevision"] = target.Token.ContentRevision;
        document["nextNodeId"] = target.NextNodeId;
        document["nextEdgeId"] = target.NextEdgeId;
        document["map"]!["cellSizeMetres"] = target.Map.CellSizeMetres;
        var nodes = before.Nodes.ToDictionary(node => node.Id);
        foreach (RoadNodeChange node in change.Nodes)
        {
            nodes.Remove(node.Id);
            RoadNode? state = reverse ? node.Before : node.After;
            if (state is not null) nodes.Add(node.Id, state);
        }
        var edges = before.Edges.ToDictionary(edge => edge.Id);
        foreach (RoadEdgeChange edge in change.Edges)
        {
            edges.Remove(edge.Id);
            RoadEdge? state = reverse ? edge.Before : edge.After;
            if (state is not null) edges.Add(edge.Id, state);
        }
        document["nodes"] = new JsonArray(nodes.Values.OrderBy(node => node.Id.Value).Select(node => (JsonNode)new JsonObject
        {
            ["id"] = node.Id.Value, ["x"] = node.Position.X, ["y"] = node.Position.Y,
        }).ToArray());
        document["edges"] = new JsonArray(edges.Values.OrderBy(edge => edge.Id.Value).Select(edge => (JsonNode)new JsonObject
        {
            ["id"] = edge.Id.Value, ["startNodeId"] = edge.Start.Value, ["endNodeId"] = edge.End.Value,
            ["profile"] = edge.Profile.Value,
            ["points"] = new JsonArray(edge.Points.Select(point => (JsonNode)new JsonObject { ["x"] = point.X, ["y"] = point.Y }).ToArray()),
        }).ToArray());
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(document.ToJsonString()));
        var restored = new RoadNetwork();
        Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(stream))));
        return Save(restored.Snapshot);
    }

    private static void AssertRoundTrip(RoadSnapshot snapshot)
    {
        byte[] bytes = Save(snapshot);
        using var stream = new MemoryStream(bytes);
        var restored = new RoadNetwork();
        Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(stream))));
        Assert.Equal(bytes, Save(restored.Snapshot));
    }
}
