using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>V3 存档管理器 Autoload。</summary>
public partial class SaveManager : Node
{
    partial void ProbeAggregateLoadPostRendererPreflightFailure();
    partial void ProbeAggregateLoadPostSlotPreflightFailure();
    partial void ProbeAggregateLoadPostOwnershipPreCommitFailure();
    partial void ProbeAggregateLoadRendererCommitBoundaryGenerationMismatch(
        RoadRenderer renderer,
        ref IStorageOperationLease operationLease);
    partial void ProbeSlotTargetLoadCompleteCommitFailure();

    public static SaveManager Instance { get; private set; } = null!;

    private const string SaveBaseDir = "user://saves-v3";
    private const int MaximumRetainedResults = 256;
    internal const string RoadGraphSaveFileName = "road_network";
    private static readonly string[] RequiredSaveFileNames = [RoadGraphSaveFileName];

    private readonly List<IStreamingSaveable> _saveables = [];
    private readonly SaveOperationCoordinator _coordinator = new();
    private readonly object _operationSync = new();
    private readonly Dictionary<string, SaveOperationState> _operationStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SaveOperationResult> _operationResults = new(StringComparer.Ordinal);
    private readonly Queue<string> _resultOrder = new();
    private readonly Dictionary<string, CancellationTokenSource> _operationCancellations =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, TrackedOperation> _trackedOperations =
        new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<SaveOperationState> _pendingStateNotifications = new();
    private readonly ConcurrentQueue<SaveOperationResult> _pendingResultNotifications = new();
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();

    private long _slotListGeneration;
    private SaveDeletionAuthorization? _pendingDeletionAuthorization;
    private long _sceneGeneration;
    private SceneLoadContext? _sceneContext;
    private CancellationTokenSource _sceneCancellation = new();
    private LoadPerformanceMetrics? _lastLoadPerformanceMetrics;
    private long _currentSlotGeneration;
    private int _mainThreadID;
    private int _pendingAutosaveWakeup;
    private string _resolvedSaveBaseDir = string.Empty;
    private Task? _applicationQuitTask;
    private bool _sceneClosing;
    private bool _exiting;

    public const string AutosaveSlotID = "autosave";
    public const string AutosaveDisplayName = "自动存档";

    public string CurrentSlotID { get; private set; } = AutosaveSlotID;
    public int RegisteredSaveableCount => _saveables.Count;
    public bool IsOperationBusy => _coordinator.ActiveState is not null;
    public string ActiveOperationToken => _coordinator.ActiveState?.OperationToken ?? string.Empty;
    public long SceneGeneration => _sceneGeneration;
    public bool IsSceneClosing => _sceneClosing;
    public bool IsApplicationExitPending => _applicationQuitTask is not null;

    internal event Action<SaveOperationState>? OperationStateChanged;
    internal event Action<SaveOperationResult>? OperationCompleted;

    [Signal]
    public delegate void SaveOperationStateChangedEventHandler(
        string operationToken,
        int operationKind,
        string targetSlotID,
        int phase,
        bool hasCrossedCommitBoundary,
        bool cancellationRequested);

    [Signal]
    public delegate void SaveOperationCompletedEventHandler(
        string operationToken,
        int operationKind,
        string targetSlotID,
        int resultKind,
        int finalPhase,
        bool committed,
        string warnings,
        string error);

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        _mainThreadID = System.Environment.CurrentManagedThreadId;
        _resolvedSaveBaseDir = ResolveSaveBaseDir(ProjectSettings.GlobalizePath);
        _coordinator.StateChanged += OnCoordinatorStateChanged;
        _coordinator.PendingAutosaveReady += OnPendingAutosaveReady;
        GetTree().AutoAcceptQuit = false;
    }

    public override void _Process(double delta)
    {
        while (_mainThreadActions.TryDequeue(out Action? action))
            action();
        while (_pendingStateNotifications.TryDequeue(out SaveOperationState? state))
            PublishStateNotification(state);
        while (_pendingResultNotifications.TryDequeue(out SaveOperationResult? result))
            PublishResultNotification(result);

        if (Interlocked.Exchange(ref _pendingAutosaveWakeup, 0) != 0)
            StartPendingAutosaveIfReady();
    }

    public override void _ExitTree()
    {
        _exiting = true;
        _coordinator.StateChanged -= OnCoordinatorStateChanged;
        _coordinator.PendingAutosaveReady -= OnPendingAutosaveReady;
        CancelSceneOperations();
        if (_coordinator.ActiveState is not null || HasTrackedOperations())
        {
            GD.PushError(
                "SaveManager exited before storage operations converged; application exits must use RequestApplicationQuit().");
        }
        else
        {
            _coordinator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        _sceneCancellation.Dispose();
        if (ReferenceEquals(Instance, this))
            Instance = null!;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
            RequestApplicationQuit();
    }

    public bool Register(IStreamingSaveable saveable)
    {
        EnsureMainThread();
        ArgumentNullException.ThrowIfNull(saveable);
        if (_saveables.Contains(saveable))
            return true;

        IStreamingSaveable? conflict = _saveables.Find(existing =>
            string.Equals(existing.SaveFileName, saveable.SaveFileName, StringComparison.OrdinalIgnoreCase));
        if (conflict is not null)
        {
            GD.PushError($"SaveManager: duplicate active SaveFileName '{saveable.SaveFileName}' rejected.");
            return false;
        }
        _saveables.Add(saveable);
        return true;
    }

    public bool Unregister(IStreamingSaveable saveable)
    {
        EnsureMainThread();
        if (_sceneContext?.Graph == saveable)
            UnregisterSceneParticipants(_sceneContext.ToolManager);
        return _saveables.Remove(saveable);
    }

    internal bool RegisterSceneParticipants(
        RoadGraph graph,
        ToolManager toolManager,
        RoadRenderer renderer)
    {
        EnsureMainThread();
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(toolManager);
        ArgumentNullException.ThrowIfNull(renderer);
        if (!_saveables.Contains(graph) || _sceneContext is not null)
            return false;

        CancellationTokenSource previousCancellation = _sceneCancellation;
        previousCancellation.Cancel();
        previousCancellation.Dispose();
        _sceneCancellation = new CancellationTokenSource();
        _sceneGeneration = NextGeneration(_sceneGeneration);
        _sceneClosing = false;
        renderer.ConfigureSceneGeneration(_sceneGeneration);
        _sceneContext = new SceneLoadContext(
            _sceneGeneration,
            graph,
            toolManager,
            renderer);
        SetCurrentSlot(AutosaveSlotID);
        return true;
    }

    internal void UnregisterSceneParticipants(ToolManager toolManager)
    {
        EnsureMainThread();
        if (_sceneContext is not SceneLoadContext context ||
            !ReferenceEquals(context.ToolManager, toolManager))
        {
            return;
        }

        BeginSceneClose();
        _sceneContext = null;
        _sceneGeneration = NextGeneration(_sceneGeneration);
    }

    public string StartSave(string slotID)
    {
        EnsureMainThread();
        string operationToken = Guid.NewGuid().ToString("N");
        if (RejectClosedSceneOperation(
                operationToken,
                SaveOperationKind.Publish,
                slotID))
        {
            return operationToken;
        }
        StartTrackedOperation(
            operationToken,
            SaveOperationKind.Publish,
            slotID,
            _sceneGeneration,
            RunPublishAsync(
                operationToken,
                slotID,
                displayName: null,
                isAutosave: false,
                requireExisting: true));
        return operationToken;
    }

    public string StartSaveAs(string displayName)
    {
        EnsureMainThread();
        string operationToken = Guid.NewGuid().ToString("N");
        string slotID = $"manual-{Guid.NewGuid():N}";
        if (RejectClosedSceneOperation(
                operationToken,
                SaveOperationKind.Publish,
                slotID))
        {
            return operationToken;
        }
        StartTrackedOperation(
            operationToken,
            SaveOperationKind.Publish,
            slotID,
            _sceneGeneration,
            RunPublishAsync(
                operationToken,
                slotID,
                displayName,
                isAutosave: false,
                requireExisting: false));
        return operationToken;
    }

    public string StartAutosave()
    {
        EnsureMainThread();
        string operationToken = Guid.NewGuid().ToString("N");
        if (RejectClosedSceneOperation(
                operationToken,
                SaveOperationKind.Autosave,
                AutosaveSlotID))
        {
            return operationToken;
        }
        SaveOperationAdmission admission = _coordinator.AdmitAutosave(
            AutosaveSlotID,
            operationToken);
        if (admission.TerminalResult is SaveOperationResult terminal)
        {
            RecordTerminalResult(terminal);
            return terminal.OperationToken;
        }

        SaveOperationLease lease = admission.Lease!;
        StartTrackedOperation(
            lease.OperationToken,
            SaveOperationKind.Autosave,
            AutosaveSlotID,
            _sceneGeneration,
            RunAdmittedPublishAsync(
                lease,
                AutosaveSlotID,
                AutosaveDisplayName,
                isAutosave: true,
                requireExisting: false,
                CaptureSceneRequest()));
        return lease.OperationToken;
    }

    public string StartLoad(string slotID)
    {
        EnsureMainThread();
        string operationToken = Guid.NewGuid().ToString("N");
        if (RejectClosedSceneOperation(
                operationToken,
                SaveOperationKind.Load,
                slotID))
        {
            return operationToken;
        }
        StartTrackedOperation(
            operationToken,
            SaveOperationKind.Load,
            slotID,
            _sceneGeneration,
            RunLoadAsync(operationToken, slotID));
        return operationToken;
    }

    public string StartDeleteSlot(string slotID, string operationToken)
    {
        EnsureMainThread();
        if (string.IsNullOrWhiteSpace(operationToken))
            return string.Empty;
        if (RejectClosedSceneOperation(
                operationToken,
                SaveOperationKind.Delete,
                slotID))
        {
            _pendingDeletionAuthorization = null;
            return operationToken;
        }
        SaveDeletionAuthorization? authorization = _pendingDeletionAuthorization;
        _pendingDeletionAuthorization = null;
        if (authorization is null ||
            authorization.UIGeneration != _slotListGeneration ||
            !string.Equals(authorization.SlotID, slotID, StringComparison.Ordinal) ||
            !string.Equals(authorization.OperationToken, operationToken, StringComparison.Ordinal))
        {
            GD.PushError($"[SaveManager] V3 delete authorization is stale or does not match slot '{slotID}'.");
            return string.Empty;
        }

        StartTrackedOperation(
            operationToken,
            SaveOperationKind.Delete,
            slotID,
            _sceneGeneration,
            RunDeleteAsync(authorization, CaptureSceneRequest()));
        return operationToken;
    }

    internal Task DrainCurrentSceneOperationsAsync()
    {
        EnsureMainThread();
        long generation = _sceneGeneration;
        BeginSceneClose();
        return WaitForTrackedOperationsAsync(operation =>
            operation.SceneGeneration == generation);
    }

    internal bool ResumeCurrentSceneOperations()
    {
        EnsureMainThread();
        if (_exiting || _coordinator.IsStopping || _sceneContext is not SceneLoadContext context ||
            HasTrackedOperations())
        {
            return false;
        }
        if (!_sceneClosing)
            return true;

        _sceneCancellation.Dispose();
        _sceneCancellation = new CancellationTokenSource();
        _sceneGeneration = NextGeneration(_sceneGeneration);
        _sceneContext = context with { Generation = _sceneGeneration };
        _sceneClosing = false;
        return true;
    }

    public void RequestApplicationQuit()
    {
        EnsureMainThread();
        _applicationQuitTask ??= QuitAfterStorageShutdownAsync();
    }

    private async Task QuitAfterStorageShutdownAsync()
    {
        try
        {
            BeginSceneClose();
            Task coordinatorShutdown = _coordinator.BeginShutdownAsync();
            await Task.WhenAll(
                coordinatorShutdown,
                WaitForTrackedOperationsAsync(static _ => true));
            if (!_exiting && IsInsideTree())
                GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"SaveManager: application exit convergence failed: {exception.Message}");
            _applicationQuitTask = null;
        }
    }

    public bool CancelOperation(string operationToken)
    {
        EnsureMainThread();
        if (string.IsNullOrEmpty(operationToken))
            return false;
        bool hasCancellation;
        lock (_operationSync)
        {
            hasCancellation = _operationCancellations.TryGetValue(
                operationToken,
                out CancellationTokenSource? cancellation);
            if (cancellation is not null)
                cancellation.Cancel();
        }
        bool activeAccepted = _coordinator.RequestCancellation(operationToken);
        SaveOperationState? active = _coordinator.ActiveState;
        bool waitingAccepted = hasCancellation &&
            (active is null || !string.Equals(
                active.OperationToken,
                operationToken,
                StringComparison.Ordinal));
        return activeAccepted || waitingAccepted;
    }

    public bool HasOperationResult(string operationToken)
    {
        lock (_operationSync)
            return _operationResults.ContainsKey(operationToken);
    }

    public Godot.Collections.Dictionary GetOperationResult(string operationToken)
    {
        SaveOperationResult? result;
        lock (_operationSync)
            _operationResults.TryGetValue(operationToken, out result);
        if (result is null)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["operationToken"] = result.OperationToken,
            ["operationKind"] = (int)result.Kind,
            ["targetSlotID"] = result.TargetSlotID,
            ["resultKind"] = (int)result.ResultKind,
            ["finalPhase"] = (int)result.FinalPhase,
            ["committed"] = result.Committed,
            ["warnings"] = string.Join("\n", result.Warnings),
            ["error"] = result.Error ?? string.Empty,
        };
    }

    public Godot.Collections.Dictionary GetLastLoadPerformanceMetrics()
    {
        LoadPerformanceMetrics? metrics;
        lock (_operationSync)
            metrics = _lastLoadPerformanceMetrics;
        if (metrics is null)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["operationToken"] = metrics.OperationToken,
            ["targetSlotID"] = metrics.TargetSlotID,
            ["workerPrepareMs"] = metrics.WorkerPrepareDuration.TotalMilliseconds,
            ["preflightMs"] = metrics.PreflightDuration.TotalMilliseconds,
            ["referenceCommitMs"] = metrics.ReferenceCommitDuration.TotalMilliseconds,
            ["aggregateCommitMs"] = metrics.AggregateCommitDuration.TotalMilliseconds,
            ["totalMs"] = metrics.TotalDuration.TotalMilliseconds,
        };
    }

    public bool SaveSlotExists(string slotID)
    {
        try
        {
            return CreateSlotStore().Exists(slotID);
        }
        catch (Exception exception)
        {
            GD.PushError($"[SaveManager] Cannot inspect V3 slot '{slotID}': {exception.Message}");
            return false;
        }
    }

    public IReadOnlyList<SaveSlotSummary> ListSlots()
    {
        try
        {
            IReadOnlyList<SaveSlotSummary> summaries = CreateSlotStore().ListSlots();
            long generation = NextSlotListGeneration();
            _pendingDeletionAuthorization = null;
            foreach (SaveSlotSummary summary in summaries)
            {
                summary.UIGeneration = generation;
                summary.DeleteOperationToken = Guid.NewGuid().ToString("N");
                if (!summary.IsValid)
                    GD.PushWarning($"[SaveManager] Corrupt V3 slot '{summary.SlotID}': {summary.Error}");
            }
            return summaries;
        }
        catch (Exception exception)
        {
            GD.PushError($"[SaveManager] Cannot list V3 save slots: {exception.Message}");
            return Array.Empty<SaveSlotSummary>();
        }
    }

    public string RequestDeleteSlot(string slotID)
    {
        try
        {
            SaveSlotSummary? summary = ListSlots()
                .SingleOrDefault(candidate => string.Equals(
                    candidate.SlotID, slotID, StringComparison.Ordinal));
            return summary is null ? string.Empty : ArmDeletion(summary);
        }
        catch (Exception exception)
        {
            GD.PushError($"[SaveManager] Cannot authorize V3 delete for slot '{slotID}': {exception.Message}");
            return string.Empty;
        }
    }

    internal string ArmDeletion(SaveSlotSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.UIGeneration <= 0 || summary.UIGeneration != _slotListGeneration ||
            string.IsNullOrEmpty(summary.DeleteOperationToken) ||
            string.IsNullOrEmpty(summary.OccupantDigest) ||
            summary.OccupantKind is not SaveSlotOccupantKind.CompleteV3 and
                not SaveSlotOccupantKind.CorruptV3)
        {
            return string.Empty;
        }

        _pendingDeletionAuthorization = new SaveDeletionAuthorization(
            summary.SlotID,
            summary.UIGeneration,
            summary.DeleteOperationToken,
            summary.OccupantKind,
            summary.OccupantDigest,
            summary.IsValid ? summary.DisplayName : summary.SlotID);
        return summary.DeleteOperationToken;
    }

    private async Task<SaveOperationResult> RunPublishAsync(
        string operationToken,
        string slotID,
        string? displayName,
        bool isAutosave,
        bool requireExisting)
    {
        SceneRequest sceneRequest = CaptureSceneRequest();
        SaveOperationKind kind = isAutosave
            ? SaveOperationKind.Autosave
            : SaveOperationKind.Publish;
        using CancellationTokenSource cancellation = CreateOperationCancellation(
            operationToken,
            sceneRequest.CancellationToken);
        SaveOperationAdmission admission = await _coordinator.AdmitManualAsync(
            kind,
            slotID,
            cancellation.Token,
            operationToken);
        if (admission.TerminalResult is SaveOperationResult terminal)
            return terminal;
        return await RunAdmittedPublishAsync(
            admission.Lease!,
            slotID,
            displayName,
            isAutosave,
            requireExisting,
            sceneRequest);
    }

    private async Task<SaveOperationResult> RunAdmittedPublishAsync(
        SaveOperationLease lease,
        string slotID,
        string? displayName,
        bool isAutosave,
        bool requireExisting,
        SceneRequest sceneRequest)
    {
        await using (lease)
        {
            try
            {
                EnsureSceneRequestCurrent(sceneRequest);
                lease.AdvanceTo(SaveOperationPhase.Capture);
                IReadOnlyList<CapturedSaveParticipant> captured =
                    SaveSlotStore.CaptureSnapshots(GetRequiredSaveables());
                lease.ThrowIfCancellationRequested();
                lease.AdvanceTo(SaveOperationPhase.Prepare);
                SavePublishResult publish = await Task.Run(() =>
                {
                    SaveSlotStore store = CreateSlotStore();
                    return requireExisting
                        ? store.SaveCapturedExisting(slotID, captured, lease)
                        : store.SaveCaptured(
                            slotID,
                            displayName ?? throw new InvalidOperationException(
                                "A new save requires a display name."),
                            captured,
                            lease);
                });

                lease.AdvanceTo(SaveOperationPhase.Cleanup);
                InvalidateSlotListing();
                if (!isAutosave && IsSceneRequestCurrent(sceneRequest))
                    SetCurrentSlot(slotID);
                IReadOnlyList<string> warnings = publish.Warning is null
                    ? []
                    : [publish.Warning];
                return lease.Complete(
                    warnings.Count == 0
                        ? SaveOperationResultKind.Succeeded
                        : SaveOperationResultKind.SucceededWithWarnings,
                    warnings);
            }
            catch (OperationCanceledException)
            {
                return lease.Complete(SaveOperationResultKind.Canceled);
            }
            catch (Exception exception)
            {
                return lease.Complete(
                    SaveOperationResultKind.Failed,
                    error: exception.Message);
            }
        }
    }

    private async Task<SaveOperationResult> RunLoadAsync(
        string operationToken,
        string slotID)
    {
        long loadStarted = Stopwatch.GetTimestamp();
        SceneRequest sceneRequest = CaptureSceneRequest();
        using CancellationTokenSource cancellation = CreateOperationCancellation(
            operationToken,
            sceneRequest.CancellationToken);
        SaveOperationAdmission admission = await _coordinator.AdmitManualAsync(
            SaveOperationKind.Load,
            slotID,
            cancellation.Token,
            operationToken);
        if (admission.TerminalResult is SaveOperationResult terminal)
            return terminal;

        SaveOperationLease lease = admission.Lease!;
        await using (lease)
        {
            RoadGraph.RoadGraphLoadAdmission? graphAdmission = null;
            ToolManager.ToolLoadAdmission? toolAdmission = null;
            RoadRenderer.RoadRendererLoadAdmission? rendererAdmission = null;
            var preflightPlans = new List<INonThrowingLoadCommitPlan>();
            bool aggregateOwnsPlans = false;
            try
            {
                SceneLoadContext context = RequireLoadContext(sceneRequest);
                graphAdmission = context.Graph.BeginLoadAdmission();
                toolAdmission = context.ToolManager.BeginLoadAdmission();
                rendererAdmission = context.Renderer.BeginLoadAdmission();
                IReadOnlyList<CapturedLoadParticipant> loadParticipants =
                    SaveSlotStore.CaptureLoadParticipants(GetRequiredSaveables());
                lease.AdvanceTo(SaveOperationPhase.Prepare);
                PreparedLoadWork prepared = await Task.Run(() =>
                {
                    long workerPrepareStarted = Stopwatch.GetTimestamp();
                    PreparedSaveSlot slot = CreateSlotStore().PrepareLoad(
                        slotID,
                        loadParticipants,
                        lease);
                    IPreparedSaveState graphState = slot.GetPreparedState(context.Graph);
                    RoadGraphRevision graphRevision = graphState as RoadGraphRevision
                        ?? throw new InvalidOperationException(
                            "RoadGraph load reader did not produce a revision.");
                    RoadRendererPreparedLoad presentation =
                        rendererAdmission.Preparer.Prepare(graphRevision);
                    return new PreparedLoadWork(
                        slot,
                        graphState,
                        presentation,
                        Stopwatch.GetElapsedTime(workerPrepareStarted));
                });

                EnsureSceneRequestCurrent(sceneRequest);
                lease.ThrowIfCancellationRequested();
                lease.AdvanceTo(SaveOperationPhase.Preflight);
                long preflightStarted = Stopwatch.GetTimestamp();
                INonThrowingLoadCommitPlan graphPlan = context.Graph.PreflightPreparedLoad(
                    graphAdmission,
                    prepared.GraphState,
                    out RoadGraphRevision targetRevision);
                preflightPlans.Add(graphPlan);
                preflightPlans.Add(context.ToolManager.PreflightFullReset(toolAdmission));
                preflightPlans.Add(context.Renderer.PreflightPreparedLoad(
                    rendererAdmission,
                    prepared.Presentation,
                    targetRevision.StateToken));
                ProbeAggregateLoadPostRendererPreflightFailure();
                long slotTargetGeneration = _currentSlotGeneration;
                preflightPlans.Add(new SlotTargetLoadCommitPlan(
                    slotID,
                    () => IsSceneContextCurrent(sceneRequest) &&
                        slotTargetGeneration == _currentSlotGeneration,
                    SetCurrentSlot,
                    CompleteSlotTargetLoadCommit));
                ProbeAggregateLoadPostSlotPreflightFailure();

                using var aggregate = new PreparedAggregateLoad(preflightPlans);
                aggregateOwnsPlans = true;
                ProbeAggregateLoadPostOwnershipPreCommitFailure();
                TimeSpan preflightDuration = Stopwatch.GetElapsedTime(preflightStarted);
                long aggregateCommitStarted = Stopwatch.GetTimestamp();
                IStorageOperationLease aggregateOperationLease = lease;
                ProbeAggregateLoadRendererCommitBoundaryGenerationMismatch(
                    context.Renderer,
                    ref aggregateOperationLease);
                IReadOnlyList<string> warnings = aggregate.Commit(aggregateOperationLease);
                TimeSpan aggregateCommitDuration = Stopwatch.GetElapsedTime(aggregateCommitStarted);
                TimeSpan referenceCommitDuration = aggregate.ReferenceCommitDuration
                    ?? throw new InvalidOperationException(
                        "A successful aggregate load did not record its reference commit duration.");
                InvalidateSlotListing();
                var metrics = new LoadPerformanceMetrics(
                    operationToken,
                    slotID,
                    prepared.WorkerPrepareDuration,
                    preflightDuration,
                    referenceCommitDuration,
                    aggregateCommitDuration,
                    Stopwatch.GetElapsedTime(loadStarted));
                lock (_operationSync)
                    _lastLoadPerformanceMetrics = metrics;
                return lease.Complete(
                    warnings.Count == 0
                        ? SaveOperationResultKind.Succeeded
                        : SaveOperationResultKind.SucceededWithWarnings,
                    warnings);
            }
            catch (OperationCanceledException)
            {
                return lease.Complete(SaveOperationResultKind.Canceled);
            }
            catch (Exception exception)
            {
                return lease.Complete(
                    SaveOperationResultKind.Failed,
                    error: exception.Message);
            }
            finally
            {
                if (!aggregateOwnsPlans)
                {
                    foreach (INonThrowingLoadCommitPlan plan in preflightPlans)
                        plan.Dispose();
                }
                rendererAdmission?.Dispose();
                toolAdmission?.Dispose();
                graphAdmission?.Dispose();
            }
        }
    }

    private async Task<SaveOperationResult> RunDeleteAsync(
        SaveDeletionAuthorization authorization,
        SceneRequest sceneRequest)
    {
        string operationToken = authorization.OperationToken;
        using CancellationTokenSource cancellation = CreateOperationCancellation(
            operationToken,
            sceneRequest.CancellationToken);
        SaveOperationAdmission admission = await _coordinator.AdmitManualAsync(
            SaveOperationKind.Delete,
            authorization.SlotID,
            cancellation.Token,
            operationToken);
        if (admission.TerminalResult is SaveOperationResult terminal)
            return terminal;

        SaveOperationLease lease = admission.Lease!;
        await using (lease)
        {
            try
            {
                lease.AdvanceTo(SaveOperationPhase.Recover);
                long targetGeneration = _currentSlotGeneration;
                SaveDeleteResult deleted = await Task.Run(() =>
                    CreateSlotStore().Delete(authorization, lease));
                lease.AdvanceTo(SaveOperationPhase.Cleanup);
                InvalidateSlotListing();
                if (deleted.IsDeleted &&
                    IsSceneRequestCurrent(sceneRequest) &&
                    targetGeneration == _currentSlotGeneration &&
                    string.Equals(CurrentSlotID, authorization.SlotID, StringComparison.Ordinal))
                {
                    SetCurrentSlot(AutosaveSlotID);
                }
                IReadOnlyList<string> warnings = deleted.Warning is null
                    ? []
                    : [deleted.Warning];
                return lease.Complete(
                    warnings.Count == 0
                        ? SaveOperationResultKind.Succeeded
                        : SaveOperationResultKind.SucceededWithWarnings,
                    warnings);
            }
            catch (OperationCanceledException)
            {
                return lease.Complete(SaveOperationResultKind.Canceled);
            }
            catch (Exception exception)
            {
                return lease.Complete(
                    SaveOperationResultKind.Failed,
                    error: exception.Message);
            }
        }
    }

    private void StartTrackedOperation(
        string operationToken,
        SaveOperationKind kind,
        string targetSlotID,
        long sceneGeneration,
        Task<SaveOperationResult> operation)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_operationSync)
        {
            if (!_trackedOperations.TryAdd(
                    operationToken,
                    new TrackedOperation(sceneGeneration, completion.Task)))
            {
                throw new InvalidOperationException(
                    $"Operation token '{operationToken}' is already tracked.");
            }
        }
        _ = CompleteTrackedOperationAsync(
            operationToken,
            kind,
            targetSlotID,
            operation,
            completion);
    }

    private async Task CompleteTrackedOperationAsync(
        string operationToken,
        SaveOperationKind kind,
        string targetSlotID,
        Task<SaveOperationResult> operation,
        TaskCompletionSource completion)
    {
        SaveOperationResult result;
        try
        {
            result = await operation;
        }
        catch (Exception exception)
        {
            result = new SaveOperationResult(
                operationToken,
                kind,
                targetSlotID,
                SaveOperationResultKind.Failed,
                SaveOperationPhase.Admission,
                committed: false,
                error: exception.Message);
        }
        finally
        {
            lock (_operationSync)
            {
                if (_operationCancellations.Remove(operationToken, out CancellationTokenSource? cancellation))
                    cancellation.Dispose();
            }
        }
        try
        {
            RecordTerminalResult(result);
        }
        finally
        {
            lock (_operationSync)
                _trackedOperations.Remove(operationToken);
            completion.TrySetResult();
        }
    }

    private CancellationTokenSource CreateOperationCancellation(
        string operationToken,
        CancellationToken sceneCancellation)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(sceneCancellation);
        lock (_operationSync)
        {
            if (!_operationCancellations.TryAdd(operationToken, cancellation))
                throw new InvalidOperationException($"Operation token '{operationToken}' is already active.");
        }
        return cancellation;
    }

    private void RecordTerminalResult(SaveOperationResult result)
    {
        lock (_operationSync)
        {
            _operationResults[result.OperationToken] = result;
            _resultOrder.Enqueue(result.OperationToken);
            while (_resultOrder.Count > MaximumRetainedResults)
            {
                string oldest = _resultOrder.Dequeue();
                _operationResults.Remove(oldest);
                _operationStates.Remove(oldest);
            }
        }
        _pendingResultNotifications.Enqueue(result);
    }

    private void OnCoordinatorStateChanged(SaveOperationState state)
    {
        lock (_operationSync)
            _operationStates[state.OperationToken] = state;
        _pendingStateNotifications.Enqueue(state);
    }

    private void OnPendingAutosaveReady() =>
        Interlocked.Exchange(ref _pendingAutosaveWakeup, 1);

    private void StartPendingAutosaveIfReady()
    {
        if (_exiting || _sceneClosing || _sceneContext is null)
            return;
        string operationToken = Guid.NewGuid().ToString("N");
        SaveOperationAdmission admission = _coordinator.TryAdmitPendingAutosave(
            AutosaveSlotID,
            operationToken);
        if (!admission.IsAdmitted)
            return;
        SaveOperationLease lease = admission.Lease!;
        StartTrackedOperation(
            lease.OperationToken,
            SaveOperationKind.Autosave,
            AutosaveSlotID,
            _sceneGeneration,
            RunAdmittedPublishAsync(
                lease,
                AutosaveSlotID,
                AutosaveDisplayName,
                isAutosave: true,
                requireExisting: false,
                CaptureSceneRequest()));
    }

    private void PublishStateNotification(SaveOperationState state)
    {
        PublishTypedState(state);
        EmitSignal(
            SignalName.SaveOperationStateChanged,
            state.OperationToken,
            (int)state.Kind,
            state.TargetSlotID,
            (int)state.Phase,
            state.HasCrossedCommitBoundary,
            state.CancellationRequested);
    }

    private void PublishResultNotification(SaveOperationResult result)
    {
        PublishTypedResult(result);
        EmitSignal(
            SignalName.SaveOperationCompleted,
            result.OperationToken,
            (int)result.Kind,
            result.TargetSlotID,
            (int)result.ResultKind,
            (int)result.FinalPhase,
            result.Committed,
            string.Join("\n", result.Warnings),
            result.Error ?? string.Empty);
    }

    private void PublishTypedState(SaveOperationState state)
    {
        Action<SaveOperationState>? handlers = OperationStateChanged;
        if (handlers is null)
            return;
        foreach (Action<SaveOperationState> handler in handlers
                     .GetInvocationList()
                     .Cast<Action<SaveOperationState>>())
        {
            try
            {
                handler(state);
            }
            catch (Exception exception)
            {
                GD.PushWarning($"Save operation state observer failed: {exception.Message}");
            }
        }
    }

    private void PublishTypedResult(SaveOperationResult result)
    {
        Action<SaveOperationResult>? handlers = OperationCompleted;
        if (handlers is null)
            return;
        foreach (Action<SaveOperationResult> handler in handlers
                     .GetInvocationList()
                     .Cast<Action<SaveOperationResult>>())
        {
            try
            {
                handler(result);
            }
            catch (Exception exception)
            {
                GD.PushWarning($"Save operation result observer failed: {exception.Message}");
            }
        }
    }

    private SceneRequest CaptureSceneRequest()
    {
        EnsureMainThread();
        return new SceneRequest(
            _sceneGeneration,
            _sceneCancellation.Token);
    }

    private SceneLoadContext RequireLoadContext(SceneRequest request)
    {
        EnsureSceneRequestCurrent(request);
        return _sceneContext ?? throw new InvalidOperationException(
            "The active scene has no complete load participants.");
    }

    private void EnsureSceneRequestCurrent(SceneRequest request)
    {
        if (!IsSceneRequestCurrent(request))
            throw new OperationCanceledException("The originating scene is no longer active.");
    }

    private bool IsSceneRequestCurrent(SceneRequest request) =>
        !_exiting &&
        !_sceneClosing &&
        _sceneContext is not null &&
        request.Generation == _sceneGeneration &&
        !request.CancellationToken.IsCancellationRequested;

    private void BeginSceneClose()
    {
        _sceneClosing = true;
        _coordinator.DiscardPendingAutosave();
        Interlocked.Exchange(ref _pendingAutosaveWakeup, 0);
        CancelSceneOperations();
    }

    private void CancelSceneOperations()
    {
        if (!_sceneCancellation.IsCancellationRequested)
            _sceneCancellation.Cancel();
        _coordinator.RequestActiveCancellation();
    }

    private bool RejectClosedSceneOperation(
        string operationToken,
        SaveOperationKind kind,
        string targetSlotID)
    {
        if (!_exiting && !_sceneClosing && _sceneContext is not null)
            return false;

        RecordTerminalResult(new SaveOperationResult(
            operationToken,
            kind,
            string.IsNullOrWhiteSpace(targetSlotID) ? "invalid-target" : targetSlotID,
            _exiting || _coordinator.IsStopping
                ? SaveOperationResultKind.RejectedShuttingDown
                : SaveOperationResultKind.RejectedSceneClosing,
            SaveOperationPhase.Admission,
            committed: false));
        return true;
    }

    private async Task WaitForTrackedOperationsAsync(
        Func<TrackedOperation, bool> predicate)
    {
        while (true)
        {
            Task[] pending;
            lock (_operationSync)
            {
                pending = _trackedOperations.Values
                    .Where(predicate)
                    .Select(tracked => tracked.Completion)
                    .ToArray();
            }
            if (pending.Length == 0)
                return;
            await Task.WhenAll(pending);
        }
    }

    private bool HasTrackedOperations()
    {
        lock (_operationSync)
            return _trackedOperations.Count != 0;
    }

    private bool IsSceneContextCurrent(SceneRequest request) =>
        IsSceneRequestCurrent(request);

    private SaveSlotStore CreateSlotStore()
    {
        if (_resolvedSaveBaseDir.Length == 0)
            throw new InvalidOperationException("SaveManager is not ready.");
        return new SaveSlotStore(_resolvedSaveBaseDir);
    }

    private void InvalidateSlotListing()
    {
        _pendingDeletionAuthorization = null;
        _slotListGeneration = NextGeneration(_slotListGeneration);
    }

    private long NextSlotListGeneration()
    {
        _slotListGeneration = NextGeneration(_slotListGeneration);
        return _slotListGeneration;
    }

    private void SetCurrentSlot(string slotID)
    {
        CurrentSlotID = slotID;
        _currentSlotGeneration = NextGeneration(_currentSlotGeneration);
    }

    private static long NextGeneration(long generation) =>
        generation == long.MaxValue ? 1 : generation + 1;

    private IReadOnlyList<IStreamingSaveable> GetRequiredSaveables() =>
        SelectSaveables(_saveables, RequiredSaveFileNames);

    internal static IReadOnlyList<IStreamingSaveable> SelectSaveables(
        IReadOnlyList<IStreamingSaveable> saveables,
        IReadOnlyList<string> saveFileNames)
    {
        var selected = new List<IStreamingSaveable>(saveFileNames.Count);
        foreach (string fileName in saveFileNames)
        {
            IStreamingSaveable? match = null;
            foreach (IStreamingSaveable candidate in saveables)
            {
                if (!string.Equals(candidate.SaveFileName, fileName, StringComparison.Ordinal))
                    continue;
                if (match is not null)
                    throw new InvalidOperationException($"Multiple saveables provide '{fileName}'.");
                match = candidate;
            }
            selected.Add(match ?? throw new InvalidOperationException(
                $"Required saveable '{fileName}' is not registered."));
        }
        return selected;
    }

    internal static string ResolveSaveBaseDir(Func<string, string> globalizePath)
    {
        ArgumentNullException.ThrowIfNull(globalizePath);
        return globalizePath(SaveBaseDir);
    }

    private void EnsureMainThread()
    {
        if (_mainThreadID != 0 && System.Environment.CurrentManagedThreadId != _mainThreadID)
            throw new InvalidOperationException("SaveManager scene state must be accessed on the main thread.");
    }

    private void CompleteSlotTargetLoadCommit()
    {
        ProbeSlotTargetLoadCompleteCommitFailure();
    }

    private sealed record SceneRequest(long Generation, CancellationToken CancellationToken);

    private sealed record TrackedOperation(long SceneGeneration, Task Completion);

    private sealed record SceneLoadContext(
        long Generation,
        RoadGraph Graph,
        ToolManager ToolManager,
        RoadRenderer Renderer);

    private sealed record PreparedLoadWork(
        PreparedSaveSlot Slot,
        IPreparedSaveState GraphState,
        RoadRendererPreparedLoad Presentation,
        TimeSpan WorkerPrepareDuration);

    private sealed record LoadPerformanceMetrics(
        string OperationToken,
        string TargetSlotID,
        TimeSpan WorkerPrepareDuration,
        TimeSpan PreflightDuration,
        TimeSpan ReferenceCommitDuration,
        TimeSpan AggregateCommitDuration,
        TimeSpan TotalDuration);

    internal sealed class SlotTargetLoadCommitPlan : INonThrowingLoadCommitPlan
    {
        private readonly string _slotID;
        private readonly Func<bool> _isGenerationCurrent;
        private readonly Action<string> _setCurrentSlot;
        private readonly Action _completeCommit;
        private bool _committed;

        internal SlotTargetLoadCommitPlan(
            string slotID,
            Func<bool> isGenerationCurrent,
            Action<string> setCurrentSlot,
            Action completeCommit)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(slotID);
            ArgumentNullException.ThrowIfNull(isGenerationCurrent);
            ArgumentNullException.ThrowIfNull(setCurrentSlot);
            ArgumentNullException.ThrowIfNull(completeCommit);
            _slotID = slotID;
            _isGenerationCurrent = isGenerationCurrent;
            _setCurrentSlot = setCurrentSlot;
            _completeCommit = completeCommit;
        }

        public string ParticipantID => "slot-target";
        public bool IsGenerationCurrent => !_committed && _isGenerationCurrent();

        public void CommitReferences()
        {
            _setCurrentSlot(_slotID);
            _committed = true;
        }

        public IReadOnlyList<string> PublishNotifications() => [];
        public void CompleteCommit() => _completeCommit();
        public void Dispose() { }
    }
}
