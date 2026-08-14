using System.Text.Json;

namespace SimpleCities.Tests;

public sealed class SaveOperationCoordinatorTests
{
    [Fact]
    public async Task ManualOperations_AreExclusiveAndKeepDistinctTokens()
    {
        await using var coordinator = new SaveOperationCoordinator();
        SaveOperationAdmission firstAdmission = await coordinator.AdmitManualAsync(
            SaveOperationKind.Publish,
            "manual-1");
        SaveOperationLease first = Assert.IsType<SaveOperationLease>(firstAdmission.Lease);
        Task<SaveOperationAdmission> secondTask = coordinator.AdmitManualAsync(
            SaveOperationKind.Load,
            "manual-2").AsTask();

        Assert.False(secondTask.IsCompleted);
        first.AdvanceTo(SaveOperationPhase.Capture);
        first.EnterCommitBoundary();
        SaveOperationResult firstResult = first.Complete(SaveOperationResultKind.Succeeded);

        SaveOperationLease second = Assert.IsType<SaveOperationLease>((await secondTask).Lease);
        Assert.NotEqual(first.OperationToken, second.OperationToken);
        Assert.True(firstResult.Committed);
        Assert.Equal(SaveOperationPhase.Publish, firstResult.FinalPhase);
        second.AdvanceTo(SaveOperationPhase.Prepare);
        second.EnterCommitBoundary();
        second.Complete(SaveOperationResultKind.Succeeded);
    }

    [Fact]
    public async Task AutosaveBusy_CoalescesOnePendingAndManualWaiterRunsFirst()
    {
        await using var coordinator = new SaveOperationCoordinator();
        int pendingReadyCount = 0;
        coordinator.PendingAutosaveReady += () => pendingReadyCount++;
        SaveOperationLease first = Assert.IsType<SaveOperationLease>((await coordinator
            .AdmitManualAsync(SaveOperationKind.Publish, "manual-1")).Lease);

        Assert.Equal(
            SaveOperationResultKind.SkippedBusy,
            coordinator.AdmitAutosave("autosave").TerminalResult?.ResultKind);
        Assert.Equal(
            SaveOperationResultKind.SkippedBusy,
            coordinator.AdmitAutosave("autosave").TerminalResult?.ResultKind);
        Assert.True(coordinator.HasPendingAutosave);

        Task<SaveOperationAdmission> waitingManual = coordinator.AdmitManualAsync(
            SaveOperationKind.Delete,
            "manual-2").AsTask();
        first.EnterCommitBoundary();
        first.Complete(SaveOperationResultKind.Succeeded);

        SaveOperationLease second = Assert.IsType<SaveOperationLease>((await waitingManual).Lease);
        Assert.Equal(0, pendingReadyCount);
        second.AdvanceTo(SaveOperationPhase.Recover);
        second.EnterCommitBoundary();
        second.Complete(SaveOperationResultKind.Succeeded);

        Assert.Equal(1, pendingReadyCount);
        SaveOperationAdmission pending = coordinator.TryAdmitPendingAutosave("autosave");
        SaveOperationLease autosave = Assert.IsType<SaveOperationLease>(pending.Lease);
        Assert.False(coordinator.HasPendingAutosave);
        Assert.Equal(
            SaveOperationResultKind.SkippedBusy,
            coordinator.TryAdmitPendingAutosave("autosave").TerminalResult?.ResultKind);
        autosave.AdvanceTo(SaveOperationPhase.Capture);
        autosave.EnterCommitBoundary();
        autosave.Complete(SaveOperationResultKind.Succeeded);
    }

    [Fact]
    public async Task SceneStyleDrain_DiscardsPendingCancelsWaitersAndRemainsReusable()
    {
        await using var coordinator = new SaveOperationCoordinator();
        using var sceneCancellation = new CancellationTokenSource();
        SaveOperationLease active = Assert.IsType<SaveOperationLease>((await coordinator
            .AdmitManualAsync(
                SaveOperationKind.Load,
                "manual-1",
                sceneCancellation.Token)).Lease);
        active.AdvanceTo(SaveOperationPhase.Prepare);
        Task<SaveOperationAdmission> waiting = coordinator.AdmitManualAsync(
            SaveOperationKind.Publish,
            "manual-2",
            sceneCancellation.Token).AsTask();
        const string skippedToken = "0123456789abcdef0123456789abcdef";

        SaveOperationResult skipped = Assert.IsType<SaveOperationResult>(
            coordinator.AdmitAutosave("autosave", skippedToken).TerminalResult);
        Assert.Equal(skippedToken, skipped.OperationToken);
        Assert.True(coordinator.HasPendingAutosave);
        Assert.True(coordinator.DiscardPendingAutosave());
        Assert.False(coordinator.HasPendingAutosave);

        sceneCancellation.Cancel();
        Assert.True(coordinator.RequestActiveCancellation());
        Assert.Throws<OperationCanceledException>(active.ThrowIfCancellationRequested);
        active.Complete(SaveOperationResultKind.Canceled);

        SaveOperationAdmission canceledWaiting = await waiting;
        canceledWaiting.Lease?.Complete(SaveOperationResultKind.Canceled);
        Assert.Null(canceledWaiting.Lease);
        Assert.Equal(
            SaveOperationResultKind.Canceled,
            canceledWaiting.TerminalResult?.ResultKind);

        SaveOperationLease nextScene = Assert.IsType<SaveOperationLease>((await coordinator
            .AdmitManualAsync(SaveOperationKind.Publish, "manual-3")).Lease);
        nextScene.AdvanceTo(SaveOperationPhase.Capture);
        nextScene.EnterCommitBoundary();
        nextScene.Complete(SaveOperationResultKind.Succeeded);
    }

    [Fact]
    public async Task Cancellation_StopsBeforeBoundaryAndCannotInterruptAfterBoundary()
    {
        await using var coordinator = new SaveOperationCoordinator();
        using var cancellation = new CancellationTokenSource();
        SaveOperationLease load = Assert.IsType<SaveOperationLease>((await coordinator
            .AdmitManualAsync(SaveOperationKind.Load, "manual-1", cancellation.Token)).Lease);
        load.AdvanceTo(SaveOperationPhase.Prepare);

        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(load.ThrowIfCancellationRequested);
        SaveOperationResult canceled = load.Complete(SaveOperationResultKind.Canceled);
        Assert.False(canceled.Committed);
        Assert.Equal(SaveOperationPhase.Prepare, canceled.FinalPhase);

        SaveOperationLease delete = Assert.IsType<SaveOperationLease>((await coordinator
            .AdmitManualAsync(SaveOperationKind.Delete, "manual-1")).Lease);
        delete.AdvanceTo(SaveOperationPhase.Recover);
        delete.EnterCommitBoundary();
        Assert.False(delete.RequestCancellation());
        delete.ThrowIfCancellationRequested();
        SaveOperationResult completed = delete.Complete(SaveOperationResultKind.Succeeded);
        Assert.True(completed.Committed);
    }

    [Fact]
    public async Task Cancellation_ByTokenOnlyAffectsMatchingActiveOperation()
    {
        await using var coordinator = new SaveOperationCoordinator();
        SaveOperationLease load = Assert.IsType<SaveOperationLease>((await coordinator
            .AdmitManualAsync(SaveOperationKind.Load, "manual-1")).Lease);
        load.AdvanceTo(SaveOperationPhase.Prepare);

        Assert.False(coordinator.RequestCancellation("different-token"));
        load.ThrowIfCancellationRequested();
        Assert.True(coordinator.RequestCancellation(load.OperationToken));
        Assert.Throws<OperationCanceledException>(load.ThrowIfCancellationRequested);
        load.Complete(SaveOperationResultKind.Canceled);
    }

    [Fact]
    public async Task ActiveCancellation_RespectsCommitBoundary()
    {
        await using var coordinator = new SaveOperationCoordinator();
        SaveOperationLease operation = Assert.IsType<SaveOperationLease>((await coordinator
            .AdmitManualAsync(SaveOperationKind.Delete, "manual-1")).Lease);
        operation.AdvanceTo(SaveOperationPhase.Recover);
        operation.EnterCommitBoundary();

        Assert.False(coordinator.RequestActiveCancellation());
        operation.ThrowIfCancellationRequested();
        operation.Complete(SaveOperationResultKind.Succeeded);
        Assert.False(coordinator.RequestActiveCancellation());
    }

    [Fact]
    public async Task Shutdown_CancelsUncommittedOperationWaitsAndRejectsNewAdmission()
    {
        await using var coordinator = new SaveOperationCoordinator();
        SaveOperationLease active = Assert.IsType<SaveOperationLease>((await coordinator
            .AdmitManualAsync(SaveOperationKind.Load, "manual-1")).Lease);
        active.AdvanceTo(SaveOperationPhase.Prepare);

        Task shutdown = coordinator.BeginShutdownAsync();

        Assert.False(shutdown.IsCompleted);
        Assert.True(active.State.CancellationRequested);
        Assert.Throws<OperationCanceledException>(active.ThrowIfCancellationRequested);
        active.Complete(SaveOperationResultKind.Canceled);
        await shutdown;

        SaveOperationAdmission rejected = await coordinator.AdmitManualAsync(
            SaveOperationKind.Publish,
            "manual-2");
        Assert.Null(rejected.Lease);
        Assert.Equal(
            SaveOperationResultKind.RejectedShuttingDown,
            rejected.TerminalResult?.ResultKind);
    }

    [Fact]
    public async Task Shutdown_WaitsForCommittedOperationRejectsWaiterAndDropsPendingAutosave()
    {
        await using var coordinator = new SaveOperationCoordinator();
        SaveOperationLease active = Assert.IsType<SaveOperationLease>((await coordinator
            .AdmitManualAsync(SaveOperationKind.Delete, "manual-1")).Lease);
        active.AdvanceTo(SaveOperationPhase.Recover);
        active.EnterCommitBoundary();
        Task<SaveOperationAdmission> waiting = coordinator.AdmitManualAsync(
            SaveOperationKind.Load,
            "manual-2").AsTask();
        Assert.Equal(
            SaveOperationResultKind.SkippedBusy,
            coordinator.AdmitAutosave("autosave").TerminalResult?.ResultKind);

        Task shutdown = coordinator.BeginShutdownAsync();

        Assert.False(shutdown.IsCompleted);
        Assert.True(active.State.CancellationRequested);
        active.ThrowIfCancellationRequested();
        Assert.False(coordinator.HasPendingAutosave);
        SaveOperationAdmission rejectedWaiting = await waiting;
        Assert.Equal(
            SaveOperationResultKind.RejectedShuttingDown,
            rejectedWaiting.TerminalResult?.ResultKind);
        Assert.False(shutdown.IsCompleted);

        active.Complete(SaveOperationResultKind.Succeeded);
        await shutdown;
        Assert.Equal(
            SaveOperationResultKind.RejectedShuttingDown,
            coordinator.AdmitAutosave("autosave").TerminalResult?.ResultKind);
    }

    [Fact]
    public async Task OperationState_ReportsOrderedPhasesTokenAndBoundary()
    {
        await using var coordinator = new SaveOperationCoordinator();
        var states = new List<SaveOperationState>();
        coordinator.StateChanged += states.Add;
        SaveOperationLease operation = Assert.IsType<SaveOperationLease>((await coordinator
            .AdmitManualAsync(SaveOperationKind.Load, "manual-1")).Lease);

        operation.AdvanceTo(SaveOperationPhase.Prepare);
        operation.AdvanceTo(SaveOperationPhase.Preflight);
        operation.EnterCommitBoundary();
        operation.Complete(SaveOperationResultKind.SucceededWithWarnings, ["observer"]);

        Assert.Equal(
            [
                SaveOperationPhase.Admission,
                SaveOperationPhase.Prepare,
                SaveOperationPhase.Preflight,
                SaveOperationPhase.Commit,
                SaveOperationPhase.Commit,
                SaveOperationPhase.Completed,
            ],
            states.Select(state => state.Phase));
        Assert.All(states, state => Assert.Equal(operation.OperationToken, state.OperationToken));
        Assert.False(states[2].HasCrossedCommitBoundary);
        Assert.False(states[3].HasCrossedCommitBoundary);
        Assert.True(states[4].HasCrossedCommitBoundary);
    }

    [Fact]
    public async Task CapturedSave_WritesOnWorkerAndUsesCoordinatorToken()
    {
        string root = Path.Combine(Path.GetTempPath(), $"save-operation-{Guid.NewGuid():N}");
        try
        {
            var saveable = new ThreadRecordingSaveable(42);
            int captureThread = Environment.CurrentManagedThreadId;
            IReadOnlyList<CapturedSaveParticipant> captured =
                SaveSlotStore.CaptureSnapshots([saveable]);
            await using var coordinator = new SaveOperationCoordinator();
            SaveOperationLease operation = Assert.IsType<SaveOperationLease>((await coordinator
                .AdmitManualAsync(SaveOperationKind.Publish, "manual-1")).Lease);
            operation.AdvanceTo(SaveOperationPhase.Capture);

            SavePublishResult publish = await Task.Run(() => new SaveSlotStore(root).SaveCaptured(
                "manual-1",
                "Manual",
                captured,
                operation));
            operation.Complete(SaveOperationResultKind.Succeeded);

            Assert.Equal(captureThread, saveable.CaptureThreadID);
            Assert.NotEqual(captureThread, saveable.WriteThreadID);
            Assert.Equal(operation.OperationToken, publish.OperationToken);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PreparedSlot_DoesNotCommitUntilExplicitSceneBoundary()
    {
        string root = Path.Combine(Path.GetTempPath(), $"prepared-slot-{Guid.NewGuid():N}");
        try
        {
            var store = new SaveSlotStore(root);
            store.Save("manual-1", "Manual", [new ThreadRecordingSaveable(42)]);
            var active = new ThreadRecordingSaveable(7);
            var operation = new UncoordinatedStorageOperationLease(SaveOperationKind.Load);

            PreparedSaveSlot prepared = store.PrepareLoad("manual-1", [active], operation);

            Assert.Equal(7, active.Value);
            Assert.Equal(1, active.PrepareCount);
            Assert.Equal(0, active.CommitCount);
            operation.EnterCommitBoundary();
            Assert.Equal(1, prepared.CommitLegacy());
            Assert.Equal(42, active.Value);
            Assert.Equal(1, active.CommitCount);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadReader_IsCapturedOnAdmissionThreadAndPreparesOnWorker()
    {
        string root = Path.Combine(Path.GetTempPath(), $"load-reader-{Guid.NewGuid():N}");
        try
        {
            var store = new SaveSlotStore(root);
            store.Save("manual-1", "Manual", [new ThreadRecordingSaveable(42)]);
            var active = new ThreadRecordingSaveable(7);
            int admissionThreadID = Environment.CurrentManagedThreadId;
            IReadOnlyList<CapturedLoadParticipant> captured =
                SaveSlotStore.CaptureLoadParticipants([active]);
            var operation = new UncoordinatedStorageOperationLease(SaveOperationKind.Load);

            PreparedSaveSlot prepared = await Task.Run(() =>
                store.PrepareLoad("manual-1", captured, operation));

            Assert.Equal(admissionThreadID, active.LoadReaderCaptureThreadID);
            Assert.NotEqual(admissionThreadID, active.Reader.PrepareThreadID);
            Assert.Equal(0, active.CommitCount);
            operation.EnterCommitBoundary();
            prepared.CommitLegacy();
            Assert.Equal(42, active.Value);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed record Snapshot(int Value) : ISaveSnapshot;
    private sealed record Prepared(int Value) : IPreparedSaveState;

    private sealed class ThreadRecordingSaveable(int value)
        : IStreamingSaveable
    {
        private readonly ThreadRecordingLoadReader _reader = new();
        public string SaveFileName => "road_network";
        public int Value { get; private set; } = value;
        public int CaptureThreadID { get; private set; }
        public int WriteThreadID { get; private set; }
        public int PrepareCount { get; private set; }
        public int CommitCount { get; private set; }
        public int LoadReaderCaptureThreadID { get; private set; }
        internal ThreadRecordingLoadReader Reader => _reader;

        public ISaveSnapshot CaptureSnapshot()
        {
            CaptureThreadID = Environment.CurrentManagedThreadId;
            return new Snapshot(Value);
        }

        public void WriteSnapshot(Stream destination, ISaveSnapshot snapshot)
        {
            WriteThreadID = Environment.CurrentManagedThreadId;
            using var writer = new Utf8JsonWriter(destination);
            writer.WriteStartObject();
            writer.WriteNumber("value", Assert.IsType<Snapshot>(snapshot).Value);
            writer.WriteEndObject();
            writer.Flush();
        }

        public IStreamingLoadReader CaptureLoadReader()
        {
            LoadReaderCaptureThreadID = Environment.CurrentManagedThreadId;
            _reader.Preparing = () => PrepareCount++;
            return _reader;
        }

        public void CommitPreparedLoad(IPreparedSaveState preparedState)
        {
            Value = Assert.IsType<Prepared>(preparedState).Value;
            CommitCount++;
        }
    }

    internal sealed class ThreadRecordingLoadReader : IStreamingLoadReader
    {
        internal int PrepareThreadID { get; private set; }
        internal Action? Preparing { get; set; }

        public IPreparedSaveState PrepareLoad(Stream source)
        {
            PrepareThreadID = Environment.CurrentManagedThreadId;
            Preparing?.Invoke();
            using JsonDocument document = JsonDocument.Parse(source);
            return new Prepared(document.RootElement.GetProperty("value").GetInt32());
        }
    }
}
