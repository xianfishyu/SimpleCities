using System.Text;
using System.Text.Json.Nodes;

namespace SimpleCities.RoadCore.Tests;

public sealed class RoadEditHistoryTests
{
    [Fact]
    public void LoadStartsFreshLineageWithPersistedWatermarksAndNoSessionHistory()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 0));
        Build(network, new(0, 100), new(100, 100));
        Assert.True(network.TryCommit(network.PlanUndo().Plan!));
        RoadSnapshot before = network.Snapshot;
        RoadGridSpan oldSpan = Pick(before, Assert.Single(before.Edges));
        RoadPlan staleUndo = network.PlanUndo().Plan!, staleRedo = network.PlanRedo().Plan!;
        var emptyMap = new RoadNetwork(new MapDefinition(25));
        byte[] bytes = Save(emptyMap.Snapshot);
        using var stream = new MemoryStream(bytes);

        RoadPlan load = network.PlanLoad(RoadCodec.Read(stream));
        Assert.Equal(2, network.History.RetainedCount);
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(load));

        Assert.Equal(0, network.History.RetainedCount);
        Assert.Equal(0, network.History.EstimatedBytes);
        Assert.Equal(before.Token.NetworkInstance, network.Snapshot.Token.NetworkInstance);
        Assert.NotEqual(before.Token.Lineage, network.Snapshot.Token.Lineage);
        Assert.Equal(before.Token.ChangeSequence + 1, network.Snapshot.Token.ChangeSequence);
        Assert.Equal(25, network.Snapshot.Map.CellSizeMetres);
        Assert.Equal(1, network.Snapshot.NextNodeId);
        Assert.Equal(1, network.Snapshot.NextEdgeId);
        Assert.Equal(bytes, Save(network.Snapshot));
        Assert.False(network.TryCommit(staleUndo));
        Assert.False(network.TryCommit(staleRedo));
        Assert.Equal(RoadEditStatus.Rejected, network.PlanRemove(oldSpan).Status);
        Assert.Equal(RoadEditStatus.NoChange, network.PlanUndo().Status);
        Assert.Equal(RoadEditStatus.NoChange, network.PlanRedo().Status);
        Build(network, new(0, 0), new(25, 0));
        Assert.Equal(2, network.Snapshot.Token.ContentRevision);
        Assert.Equal(1, network.History.UndoCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UndoAndRedoRestoreSyntheticLoopSeamAfterLocalRemovalOrTypeChange(bool changeProfile)
    {
        var network = new RoadNetwork();
        Build(network, new(50, 50), new(0, 0));
        Build(network, new(0, 0), new(100, 0));
        RoadSnapshot open = network.Snapshot;
        Build(network, new(100, 0), new(50, 50));
        RoadSnapshot loop = network.Snapshot;
        Assert.True(network.TryCommit(network.PlanUndo().Plan!));
        AssertRestoredContent(open, network.Snapshot);
        Assert.True(network.TryCommit(network.PlanRedo().Plan!));
        AssertRestoredContent(loop, network.Snapshot);
        RoadGridSpan wrap = Pick(network.Snapshot, Assert.Single(network.Snapshot.Edges), 0.01);
        Assert.Equal(2, wrap.Ranges.Count);

        RoadEditResult edit = changeProfile ? network.PlanChangeProfile(wrap, RoadProfileId.Highway) : network.PlanRemove(wrap);
        Assert.True(network.TryCommit(edit.Plan!));
        RoadSnapshot edited = network.Snapshot;
        Assert.Equal(4, network.History.UndoCount);
        Assert.DoesNotContain(edited.Nodes, node => node.Position == new RoadPoint(50, 50));
        Assert.True(network.TryCommit(network.PlanUndo().Plan!));

        AssertRestoredContent(loop, network.Snapshot);
        RoadNode seam = Assert.Single(network.Snapshot.Nodes);
        Assert.Equal(new RoadPoint(50, 50), seam.Position);
        Assert.Equal(2, RoadJunctionQuery.Read(network.Snapshot, seam.Id)!.Incidences.Count);
        Assert.NotNull(RoadPresentation.Prepare(network.Snapshot.Nodes, network.Snapshot.Edges));
        Assert.Null(RoadSpanQuery.Pick(network.Snapshot, new(wrap.Source, wrap.Edge, 0.01)));
        Assert.True(network.TryCommit(network.PlanRedo().Plan!));
        Assert.Equal(Save(edited), Save(network.Snapshot));
        AssertRoundTrip(network.Snapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BatchEditsAcrossJunctionSidesUndoAsOneRecordAndRestoreUnselectedBranches(bool changeProfile)
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(1000, 0));
        Build(network, new(500, -100), new(500, 100));
        using (var saved = new MemoryStream(Save(network.Snapshot)))
            Assert.True(network.TryCommit(network.PlanLoad(RoadCodec.Read(saved))));
        RoadSnapshot before = network.Snapshot;
        RoadEdge left = Assert.Single(before.Edges, edge => edge.Points.Min(point => point.X) == 0);
        RoadEdge right = Assert.Single(before.Edges, edge => edge.Points.Max(point => point.X) == 1000);
        RoadGridSpan a = Pick(before, left, 0.3), b = Pick(before, right, 0.7);

        RoadEditResult edit = changeProfile
            ? network.PlanChangeProfile(new[] { a, b, a }, RoadProfileId.Highway)
            : network.PlanRemove(new[] { a, b, a });
        Assert.True(network.TryCommit(edit.Plan!));
        RoadSnapshot edited = network.Snapshot;
        Assert.Equal(1, network.History.UndoCount);
        Assert.Equal(changeProfile ? 1200 : 1000, edited.Edges.Sum(edge => edge.Length));
        Assert.True(network.TryCommit(network.PlanUndo().Plan!));

        AssertRestoredContent(before, network.Snapshot);
        Assert.Equal(0, network.History.UndoCount);
        Assert.Equal(1, network.History.RedoCount);
        RoadNode junction = Assert.Single(network.Snapshot.Nodes, node => node.Position == new RoadPoint(500, 0));
        Assert.Equal(4, RoadJunctionQuery.Read(network.Snapshot, junction.Id)!.Incidences.Count);
        Assert.True(network.TryCommit(network.PlanRedo().Plan!));
        Assert.Equal(Save(edited), Save(network.Snapshot));
        Assert.Equal(1, network.History.RetainedCount);
        AssertRoundTrip(network.Snapshot);
    }

    [Fact]
    public void UndoCannotMakeAnExhaustedContentRevisionAvailableForAnotherBranch()
    {
        var network = new RoadNetwork();
        JsonObject document = JsonNode.Parse(Save(network.Snapshot))!.AsObject();
        document["contentRevision"] = long.MaxValue - 2;
        using (var saved = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString())))
            Assert.True(network.TryCommit(network.PlanLoad(RoadCodec.Read(saved))));
        Build(network, new(0, 0), new(100, 0));
        Assert.Equal(long.MaxValue - 1, network.Snapshot.Token.ContentRevision);
        Assert.True(network.TryCommit(network.PlanUndo().Plan!));
        RoadSnapshot before = network.Snapshot;
        RoadEditHistory history = network.History;

        RoadBuildResult attempt = network.PlanBuild(new(before.Token, new(0, 100), new(100, 100), RoadProfileId.Street));

        Assert.Equal(RoadBuildStatus.Rejected, attempt.Status);
        Assert.Same(before, network.Snapshot);
        Assert.Same(history, network.History);
        Assert.Equal(1, network.History.RedoCount);
        Assert.True(network.TryCommit(network.PlanRedo().Plan!));
    }

    [Fact]
    public void EditingAfterUndoDiscardsRedoAndAllocatesFreshRevisionAndIdentities()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 0));
        Build(network, new(0, 100), new(100, 100));
        RoadSnapshot abandoned = network.Snapshot;
        Assert.True(network.TryCommit(network.PlanUndo().Plan!));
        RoadPlan staleRedo = network.PlanRedo().Plan!;

        RoadBuildResult fork = network.PlanBuild(new(network.Snapshot.Token, new(0, 200), new(100, 200), RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, fork.Status);
        Assert.Equal(1, network.History.RedoCount);
        Assert.True(network.TryCommit(fork.Plan!));

        Assert.Equal(abandoned.Token.ContentRevision + 1, network.Snapshot.Token.ContentRevision);
        RoadEdge branch = Assert.Single(network.Snapshot.Edges, edge => edge.Points[0].Y == 200);
        Assert.True(branch.Id.Value >= abandoned.NextEdgeId);
        Assert.True(branch.Start.Value >= abandoned.NextNodeId);
        Assert.True(branch.End.Value >= abandoned.NextNodeId);
        Assert.Equal(2, network.History.RetainedCount);
        Assert.Equal(2, network.History.UndoCount);
        Assert.Equal(0, network.History.RedoCount);
        Assert.False(network.TryCommit(staleRedo));
        Assert.Equal(RoadEditStatus.NoChange, network.PlanRedo().Status);
        Assert.True(network.TryCommit(network.PlanUndo().Plan!));
        Assert.Single(network.Snapshot.Edges);
        Assert.True(network.TryCommit(network.PlanRedo().Plan!));
        Assert.Contains(network.Snapshot.Edges, edge => edge.Id == branch.Id);
        Assert.DoesNotContain(network.Snapshot.Edges, edge => edge.Points[0].Y == 100);
    }

    [Fact]
    public void FailedCancelledUncommittedAndNoChangeOperationsLeaveBothHistoryDirectionsUntouched()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 0));
        Build(network, new(0, 100), new(100, 100));
        Assert.True(network.TryCommit(network.PlanUndo().Plan!));
        RoadSnapshot before = network.Snapshot;
        RoadEditHistory history = network.History;
        RoadGridSpan span = Pick(before, Assert.Single(before.Edges));
        var cancellation = new CancellationToken(true);

        Assert.Equal(RoadEditStatus.NoChange, network.PlanChangeProfile(span, RoadProfileId.Street).Status);
        Assert.Equal(RoadEditStatus.NoChange, network.PlanRemove(Array.Empty<RoadGridSpan>()).Status);
        Assert.Equal(RoadBuildStatus.NoChange, network.PlanBuild(new(before.Token, new(0, 0), new(0, 0), RoadProfileId.Street)).Status);
        Assert.Equal(RoadBuildStatus.Rejected, network.PlanBuild(new(before.Token, new(0, 0), new(100, 0), RoadProfileId.Street)).Status);
        Assert.Throws<OperationCanceledException>(() => network.PlanUndo(cancellation));
        Assert.Throws<OperationCanceledException>(() => network.PlanRedo(cancellation));
        Assert.Throws<OperationCanceledException>(() => network.PlanRemove(span, cancellation));
        Assert.Throws<OperationCanceledException>(() => network.PlanBuild(new(before.Token, new(0, 200), new(100, 200), RoadProfileId.Street), cancellation));
        Assert.Equal(RoadEditStatus.Ready, network.PlanUndo().Status);
        Assert.Equal(RoadEditStatus.Ready, network.PlanRedo().Status);
        Assert.Equal(RoadEditStatus.Ready, network.PlanRemove(span).Status);

        Assert.Same(before, network.Snapshot);
        Assert.Same(history, network.History);
        Assert.Equal(1, network.History.UndoCount);
        Assert.Equal(1, network.History.RedoCount);
    }

    [Fact]
    public void PreparedHistoryPlansCannotCommitToAnotherNetworkOrAfterAnotherPublication()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 0));
        RoadPlan undo = network.PlanUndo().Plan!;
        var foreign = new RoadNetwork();
        Assert.False(foreign.TryCommit(undo));
        Assert.Equal(0, foreign.History.RetainedCount);
        RoadPlan competingUndo = network.PlanUndo().Plan!;
        Assert.True(network.TryCommit(undo));
        Assert.False(network.TryCommit(undo));
        Assert.False(network.TryCommit(competingUndo));
        Assert.True(network.TryCommit(network.PlanRedo().Plan!));
        Assert.False(network.TryCommit(undo));
        Assert.False(network.TryCommit(competingUndo));
        Assert.Equal(1, network.History.UndoCount);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(65)]
    public void OldestEffectiveEditsAreEvictedWhileUndoAndRedoShareSixtyFourRecords(int count)
    {
        var network = new RoadNetwork();
        for (int i = 0; i < count; i++) Build(network, new(0, -3200 + i * 100), new(100, -3200 + i * 100));
        RoadSnapshot built = network.Snapshot;
        RoadEditHistory completeHistory = network.History;
        long bytes = completeHistory.EstimatedBytes;
        Assert.Equal(64, completeHistory.RetainedCount);
        Assert.True(bytes > 0);

        for (int i = 0; i < 64; i++)
        {
            Assert.True(network.TryCommit(network.PlanUndo().Plan!));
            Assert.Equal(63 - i, network.History.UndoCount);
            Assert.Equal(i + 1, network.History.RedoCount);
            Assert.Equal(64, network.History.RetainedCount);
            Assert.Equal(bytes, network.History.EstimatedBytes);
        }
        Assert.Equal(count - 64, network.Snapshot.EdgeCount);
        if (count == 65)
            Assert.Equal(new[] { new RoadPoint(0, -3200), new RoadPoint(100, -3200) }, Assert.Single(network.Snapshot.Edges).Points);
        RoadSnapshot oldest = network.Snapshot;
        Assert.Equal(RoadEditStatus.NoChange, network.PlanUndo().Status);
        Assert.Same(oldest, network.Snapshot);
        Assert.Equal(64, completeHistory.UndoCount);

        for (int i = 0; i < 64; i++) Assert.True(network.TryCommit(network.PlanRedo().Plan!));
        Assert.Equal(Save(built), Save(network.Snapshot));
        Assert.Equal(64, network.History.UndoCount);
        Assert.Equal(0, network.History.RedoCount);
        Assert.Equal(RoadEditStatus.NoChange, network.PlanRedo().Status);
    }

    [Fact]
    public void CompletedBuildCanUndoAndRedoWithoutReusingVersionsOrIdentityWatermarks()
    {
        var network = new RoadNetwork();
        RoadSnapshot empty = network.Snapshot;
        RoadBuildResult build = network.PlanBuild(new(empty.Token, new(0, 0), new(100, 0), RoadProfileId.Street));
        Assert.True(network.TryCommit(build.Plan!));
        RoadSnapshot built = network.Snapshot;
        Assert.Equal(1, network.History.UndoCount);
        Assert.Equal(0, network.History.RedoCount);
        var location = new RoadLocation(built.Token, Assert.Single(built.Edges).Id, 0.5);

        RoadEditResult undo = network.PlanUndo();
        Assert.Equal(RoadEditStatus.Ready, undo.Status);
        Assert.Same(built, network.Snapshot);
        Assert.Equal(1, network.History.UndoCount);
        Assert.True(network.TryCommit(undo.Plan!));
        Assert.Empty(network.Snapshot.Edges);
        Assert.Equal(empty.Token.ContentRevision, network.Snapshot.Token.ContentRevision);
        Assert.Equal(built.Token.ChangeSequence + 1, network.Snapshot.Token.ChangeSequence);
        Assert.Equal(built.NextNodeId, network.Snapshot.NextNodeId);
        Assert.Equal(built.NextEdgeId, network.Snapshot.NextEdgeId);
        Assert.Equal(0, network.History.UndoCount);
        Assert.Equal(1, network.History.RedoCount);
        Assert.Null(network.Snapshot.Resolve(location));

        Assert.True(network.TryCommit(network.PlanRedo().Plan!));
        RoadSnapshot redone = network.Snapshot;
        Assert.Equal(built.Nodes, redone.Nodes);
        Assert.Equal(Assert.Single(built.Edges).Id, Assert.Single(redone.Edges).Id);
        Assert.Equal(Assert.Single(built.Edges).Points, Assert.Single(redone.Edges).Points);
        Assert.Equal(built.Token.ContentRevision, redone.Token.ContentRevision);
        Assert.Equal(built.Token.ChangeSequence + 2, redone.Token.ChangeSequence);
        Assert.Equal(1, network.History.RetainedCount);
        Assert.Equal(1, network.History.UndoCount);
        Assert.Equal(0, network.History.RedoCount);
        Assert.Null(redone.Resolve(location));
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, RoadProfileId.Street));
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

    private static void AssertRestoredContent(RoadSnapshot expected, RoadSnapshot actual)
    {
        Assert.Equal(expected.Map, actual.Map);
        Assert.Equal(expected.Token.ContentRevision, actual.Token.ContentRevision);
        Assert.Equal(expected.Nodes, actual.Nodes);
        Assert.Equal(expected.EdgeCount, actual.EdgeCount);
        foreach (RoadEdge edge in expected.Edges)
        {
            RoadEdge restored = Assert.Single(actual.Edges, candidate => candidate.Id == edge.Id);
            Assert.Equal(edge.Start, restored.Start);
            Assert.Equal(edge.End, restored.End);
            Assert.Equal(edge.Profile, restored.Profile);
            Assert.Equal(edge.Points, restored.Points);
        }
        Assert.True(actual.NextNodeId >= expected.NextNodeId);
        Assert.True(actual.NextEdgeId >= expected.NextEdgeId);
        AssertRoundTrip(actual);
    }

    private static void AssertRoundTrip(RoadSnapshot snapshot)
    {
        byte[] bytes = Save(snapshot);
        using var stream = new MemoryStream(bytes);
        var restored = new RoadNetwork();
        Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(stream))));
        Assert.Equal(bytes, Save(restored.Snapshot));
        Assert.Equal(0, restored.History.RetainedCount);
    }
}
