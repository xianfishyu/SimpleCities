using Godot;

namespace SimpleCities.Tests;

public sealed class PreparedAggregateLoadTests
{
    [Fact]
    public void Commit_SwapsEveryParticipantBeforePublishingAnyNotification()
    {
        var state = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["graph"] = "old",
            ["tool"] = "old",
            ["presentation"] = "old",
            ["slot"] = "old",
        };
        var events = new List<string>();
        string[] participantIDs = ["graph", "tool", "presentation", "slot"];
        INonThrowingLoadCommitPlan[] plans = participantIDs.Select(id =>
            new FakePlan(id, state, events, participantIDs)).ToArray();
        using var aggregate = new PreparedAggregateLoad(plans);
        var operation = new UncoordinatedStorageOperationLease(SaveOperationKind.Load);

        IReadOnlyList<string> warnings = aggregate.Commit(operation);

        Assert.Empty(warnings);
        Assert.All(participantIDs, id => Assert.Equal("new", state[id]));
        Assert.Equal(
            [
                "commit:graph",
                "commit:tool",
                "commit:presentation",
                "commit:slot",
                "notify:graph",
                "notify:tool",
                "notify:presentation",
                "notify:slot",
                "complete:graph",
                "complete:tool",
                "complete:presentation",
                "complete:slot",
            ],
            events);
    }

    [Fact]
    public void GenerationMismatch_RejectsBeforeAnyReferenceSwap()
    {
        var state = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["graph"] = "old",
            ["tool"] = "old",
        };
        var events = new List<string>();
        string[] participantIDs = ["graph", "tool"];
        var graph = new FakePlan("graph", state, events, participantIDs);
        var tool = new FakePlan("tool", state, events, participantIDs)
        {
            IsGenerationCurrent = false,
        };
        using var aggregate = new PreparedAggregateLoad([graph, tool]);
        var operation = new UncoordinatedStorageOperationLease(SaveOperationKind.Load);

        Assert.Throws<LoadPreflightInvalidException>(() => aggregate.Commit(operation));

        Assert.Equal("old", state["graph"]);
        Assert.Equal("old", state["tool"]);
        Assert.Empty(events);
    }

    [Fact]
    public async Task CancellationBeforeCommit_LeavesEveryParticipantUnchanged()
    {
        var state = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["graph"] = "old",
            ["tool"] = "old",
        };
        var events = new List<string>();
        string[] participantIDs = ["graph", "tool"];
        using var aggregate = new PreparedAggregateLoad(participantIDs.Select(id =>
            new FakePlan(id, state, events, participantIDs)));
        await using var coordinator = new SaveOperationCoordinator();
        SaveOperationLease operation = Assert.IsType<SaveOperationLease>((await coordinator
            .AdmitManualAsync(SaveOperationKind.Load, "manual-1")).Lease);
        operation.AdvanceTo(SaveOperationPhase.Prepare);
        Assert.True(operation.RequestCancellation());

        Assert.Throws<OperationCanceledException>(() => aggregate.Commit(operation));

        Assert.Equal("old", state["graph"]);
        Assert.Equal("old", state["tool"]);
        Assert.Empty(events);
        operation.Complete(SaveOperationResultKind.Canceled);
    }

    [Fact]
    public void ObserverFailure_BecomesWarningAfterAllReferencesAreVisible()
    {
        var state = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["graph"] = "old",
            ["presentation"] = "old",
        };
        var events = new List<string>();
        string[] participantIDs = ["graph", "presentation"];
        var graph = new FakePlan("graph", state, events, participantIDs)
        {
            ThrowDuringNotification = true,
        };
        var presentation = new FakePlan("presentation", state, events, participantIDs);
        using var aggregate = new PreparedAggregateLoad([graph, presentation]);

        IReadOnlyList<string> warnings = aggregate.Commit(
            new UncoordinatedStorageOperationLease(SaveOperationKind.Load));

        Assert.Equal("new", state["graph"]);
        Assert.Equal("new", state["presentation"]);
        Assert.Single(warnings);
        Assert.Contains("observer 'graph' failed", warnings[0], StringComparison.Ordinal);
        Assert.Contains("notify:presentation", events);
    }

    [Fact]
    public void RoadGraphPreflightFailure_ReleasesAdmissionWithoutChangingGraph()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(4f, 0f)]).Success);
        RoadGraphRevision before = graph.CaptureRevision();
        RoadGraph.RoadGraphLoadAdmission admission = graph.BeginLoadAdmission();

        Assert.Equal(
            RoadPathSubmissionError.MutationReentrant,
            graph.SubmitPolyline(
                RoadType.Dirt,
                [new Vector2(0f, 2f), new Vector2(4f, 2f)]).Error);
        admission.Dispose();

        Assert.Same(before, graph.CaptureRevision());
        Assert.True(graph.SubmitPolyline(
            RoadType.Dirt,
            [new Vector2(0f, 2f), new Vector2(4f, 2f)]).Success);
    }

    [Fact]
    public void RoadGraphNotification_SeesOtherParticipantAlreadyCommitted()
    {
        var source = new RoadGraph();
        Assert.True(source.SubmitPolyline(
            RoadType.Highway,
            [Vector2.Zero, new Vector2(8f, 0f)]).Success);
        var target = new RoadGraph();
        Assert.True(target.SubmitPolyline(
            RoadType.Dirt,
            [Vector2.Zero, new Vector2(0f, 3f)]).Success);
        var state = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tool"] = "old",
        };
        var events = new List<string>();
        string[] participantIDs = ["tool"];
        RoadGraph.RoadGraphLoadAdmission admission = target.BeginLoadAdmission();
        INonThrowingLoadCommitPlan graphPlan = target.PreflightPreparedLoad(
            admission,
            source.CaptureRevision(),
            out _);
        var toolPlan = new FakePlan("tool", state, events, participantIDs);
        bool observerSawCommittedTool = false;
        target.GraphChanged += change =>
        {
            observerSawCommittedTool = change.Changes.IsFullReset && state["tool"] == "new";
        };
        using var aggregate = new PreparedAggregateLoad([graphPlan, toolPlan]);

        IReadOnlyList<string> warnings = aggregate.Commit(
            new UncoordinatedStorageOperationLease(SaveOperationKind.Load));

        Assert.Empty(warnings);
        Assert.True(observerSawCommittedTool);
        Assert.Equal(RoadType.Highway, Assert.Single(target.GetAllEdges()).RoadType);
    }

    [Fact]
    public void RoadGraphObserverFailure_IsReturnedAsWarningWithoutRollingBackRoot()
    {
        var source = new RoadGraph();
        Assert.True(source.SubmitPolyline(
            RoadType.Arterial,
            [Vector2.Zero, new Vector2(6f, 1f)]).Success);
        var target = new RoadGraph();
        RoadGraph.RoadGraphLoadAdmission admission = target.BeginLoadAdmission();
        INonThrowingLoadCommitPlan graphPlan = target.PreflightPreparedLoad(
            admission,
            source.CaptureRevision(),
            out _);
        target.GraphChanged += _ => throw new InvalidOperationException("observer failure");
        using var aggregate = new PreparedAggregateLoad([graphPlan]);

        IReadOnlyList<string> warnings = aggregate.Commit(
            new UncoordinatedStorageOperationLease(SaveOperationKind.Load));

        Assert.Single(warnings);
        Assert.Contains("observer failure", warnings[0], StringComparison.Ordinal);
        Assert.Equal(RoadType.Arterial, Assert.Single(target.GetAllEdges()).RoadType);
    }

    private sealed class FakePlan : INonThrowingLoadCommitPlan
    {
        private readonly Dictionary<string, string> _state;
        private readonly List<string> _events;
        private readonly IReadOnlyList<string> _allParticipantIDs;

        internal FakePlan(
            string participantID,
            Dictionary<string, string> state,
            List<string> events,
            IReadOnlyList<string> allParticipantIDs)
        {
            ParticipantID = participantID;
            _state = state;
            _events = events;
            _allParticipantIDs = allParticipantIDs;
        }

        public string ParticipantID { get; }
        public bool IsGenerationCurrent { get; set; } = true;
        internal bool ThrowDuringNotification { get; init; }

        public void CommitReferences()
        {
            _state[ParticipantID] = "new";
            _events.Add($"commit:{ParticipantID}");
        }

        public IReadOnlyList<string> PublishNotifications()
        {
            Assert.All(_allParticipantIDs, id => Assert.Equal("new", _state[id]));
            if (ThrowDuringNotification)
                throw new InvalidOperationException("observer failure");
            _events.Add($"notify:{ParticipantID}");
            return [];
        }

        public void CompleteCommit() => _events.Add($"complete:{ParticipantID}");

        public void Dispose() { }
    }
}
