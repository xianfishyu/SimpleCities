using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

internal sealed record SaveOperationAdmission(
    SaveOperationLease? Lease,
    SaveOperationResult? TerminalResult)
{
    internal bool IsAdmitted => Lease is not null;
}

internal sealed class SaveOperationCoordinator : IAsyncDisposable
{
    private static readonly IReadOnlyDictionary<SaveOperationKind, SaveOperationPhase[]> PhaseOrders =
        new Dictionary<SaveOperationKind, SaveOperationPhase[]>
        {
            [SaveOperationKind.Publish] =
            [
                SaveOperationPhase.Admission,
                SaveOperationPhase.Capture,
                SaveOperationPhase.Prepare,
                SaveOperationPhase.Publish,
                SaveOperationPhase.Cleanup,
                SaveOperationPhase.Completed,
            ],
            [SaveOperationKind.Autosave] =
            [
                SaveOperationPhase.Admission,
                SaveOperationPhase.Capture,
                SaveOperationPhase.Prepare,
                SaveOperationPhase.Publish,
                SaveOperationPhase.Cleanup,
                SaveOperationPhase.Completed,
            ],
            [SaveOperationKind.Load] =
            [
                SaveOperationPhase.Admission,
                SaveOperationPhase.Prepare,
                SaveOperationPhase.Preflight,
                SaveOperationPhase.Commit,
                SaveOperationPhase.Completed,
            ],
            [SaveOperationKind.Delete] =
            [
                SaveOperationPhase.Admission,
                SaveOperationPhase.Recover,
                SaveOperationPhase.Commit,
                SaveOperationPhase.Cleanup,
                SaveOperationPhase.Completed,
            ],
        };

    private readonly object _sync = new();
    private readonly SemaphoreSlim _rootGate = new(1, 1);
    private readonly CancellationTokenSource _shutdownWaiters = new();
    private SaveOperationLease? _active;
    private int _manualWaiterCount;
    private bool _pendingAutosave;
    private bool _stopping;
    private bool _disposed;

    internal event Action<SaveOperationState>? StateChanged;
    internal event Action? PendingAutosaveReady;

    internal SaveOperationState? ActiveState
    {
        get
        {
            lock (_sync)
                return _active?.State;
        }
    }

    internal bool HasPendingAutosave
    {
        get
        {
            lock (_sync)
                return _pendingAutosave;
        }
    }

    internal bool IsStopping
    {
        get
        {
            lock (_sync)
                return _stopping;
        }
    }

    internal bool RequestCancellation(string operationToken)
    {
        if (string.IsNullOrWhiteSpace(operationToken))
            return false;

        SaveOperationLease? active;
        lock (_sync)
        {
            active = _active is not null &&
                string.Equals(_active.OperationToken, operationToken, StringComparison.Ordinal)
                    ? _active
                    : null;
        }
        return active?.RequestCancellation() == true;
    }

    internal bool RequestActiveCancellation()
    {
        SaveOperationLease? active;
        lock (_sync)
            active = _active;
        return active?.RequestCancellation() == true;
    }

    internal async ValueTask<SaveOperationAdmission> AdmitManualAsync(
        SaveOperationKind kind,
        string targetSlotID,
        CancellationToken cancellationToken = default,
        string? operationToken = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (kind == SaveOperationKind.Autosave)
            throw new ArgumentException("Autosave must use autosave admission.", nameof(kind));
        ValidateTarget(targetSlotID);
        string token = operationToken ?? Guid.NewGuid().ToString("N");

        lock (_sync)
        {
            if (_stopping)
                return Rejected(kind, targetSlotID, token);
            _manualWaiterCount++;
        }

        bool gateAcquired = false;
        try
        {
            using CancellationTokenSource waitCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _shutdownWaiters.Token);
            await _rootGate.WaitAsync(waitCancellation.Token).ConfigureAwait(true);
            gateAcquired = true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Rejected(kind, targetSlotID, token);
        }
        catch (OperationCanceledException)
        {
            return Canceled(kind, targetSlotID, token, SaveOperationPhase.Admission);
        }
        finally
        {
            lock (_sync)
                _manualWaiterCount--;
        }

        lock (_sync)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (gateAcquired)
                    _rootGate.Release();
                return Canceled(kind, targetSlotID, token, SaveOperationPhase.Admission);
            }
            if (_stopping)
            {
                if (gateAcquired)
                    _rootGate.Release();
                return Rejected(kind, targetSlotID, token);
            }

            SaveOperationLease lease = CreateLeaseLocked(
                kind,
                targetSlotID,
                token,
                cancellationToken);
            return new SaveOperationAdmission(lease, null);
        }
    }

    internal SaveOperationAdmission AdmitAutosave(
        string targetSlotID,
        string? operationToken = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateTarget(targetSlotID);
        string token = operationToken ?? Guid.NewGuid().ToString("N");
        lock (_sync)
        {
            if (_stopping)
                return Rejected(SaveOperationKind.Autosave, targetSlotID, token);
            if (_active is not null || _manualWaiterCount > 0 || !_rootGate.Wait(0))
            {
                _pendingAutosave = true;
                return new SaveOperationAdmission(
                    null,
                    new SaveOperationResult(
                        token,
                        SaveOperationKind.Autosave,
                        targetSlotID,
                        SaveOperationResultKind.SkippedBusy,
                        SaveOperationPhase.Admission,
                        committed: false));
            }

            return new SaveOperationAdmission(
                CreateLeaseLocked(SaveOperationKind.Autosave, targetSlotID, token, default),
                null);
        }
    }

    internal SaveOperationAdmission TryAdmitPendingAutosave(
        string targetSlotID,
        string? operationToken = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateTarget(targetSlotID);
        string token = operationToken ?? Guid.NewGuid().ToString("N");
        lock (_sync)
        {
            if (_stopping)
                return Rejected(SaveOperationKind.Autosave, targetSlotID, token);
            if (!_pendingAutosave || _active is not null || _manualWaiterCount > 0 ||
                !_rootGate.Wait(0))
            {
                return new SaveOperationAdmission(
                    null,
                    new SaveOperationResult(
                        token,
                        SaveOperationKind.Autosave,
                        targetSlotID,
                        SaveOperationResultKind.SkippedBusy,
                        SaveOperationPhase.Admission,
                        committed: false));
            }

            _pendingAutosave = false;
            return new SaveOperationAdmission(
                CreateLeaseLocked(SaveOperationKind.Autosave, targetSlotID, token, default),
                null);
        }
    }

    internal async Task BeginShutdownAsync(CancellationToken cancellationToken = default)
    {
        Task activeCompletion;
        lock (_sync)
        {
            if (!_stopping)
            {
                _stopping = true;
                _pendingAutosave = false;
                _shutdownWaiters.Cancel();
                _active?.RequestCancellation();
            }
            activeCompletion = _active?.Completion ?? Task.CompletedTask;
        }

        await activeCompletion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal bool DiscardPendingAutosave()
    {
        lock (_sync)
        {
            bool discarded = _pendingAutosave;
            _pendingAutosave = false;
            return discarded;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        await BeginShutdownAsync().ConfigureAwait(false);
        _shutdownWaiters.Dispose();
        _rootGate.Dispose();
        _disposed = true;
    }

    private SaveOperationLease CreateLeaseLocked(
        SaveOperationKind kind,
        string targetSlotID,
        string operationToken,
        CancellationToken cancellationToken)
    {
        if (_active is not null)
            throw new InvalidOperationException("The save root already has an active operation.");
        var lease = new SaveOperationLease(
            this,
            kind,
            targetSlotID,
            operationToken,
            PhaseOrders[kind],
            cancellationToken);
        _active = lease;
        PublishState(lease.State);
        return lease;
    }

    internal void PublishState(SaveOperationState state)
    {
        try
        {
            StateChanged?.Invoke(state);
        }
        catch
        {
            // State observers cannot affect operation ownership.
        }
    }

    internal void Release(SaveOperationLease lease)
    {
        bool signalPending;
        lock (_sync)
        {
            if (!ReferenceEquals(_active, lease))
                throw new InvalidOperationException("Only the active operation can release the save root.");
            _active = null;
            _rootGate.Release();
            signalPending = _pendingAutosave && _manualWaiterCount == 0 && !_stopping;
        }

        if (!signalPending)
            return;
        try
        {
            PendingAutosaveReady?.Invoke();
        }
        catch
        {
            // Scheduling diagnostics cannot retain the root lease.
        }
    }

    private static SaveOperationAdmission Rejected(
        SaveOperationKind kind,
        string targetSlotID,
        string operationToken) => new(
        null,
        new SaveOperationResult(
            operationToken,
            kind,
            targetSlotID,
            SaveOperationResultKind.RejectedShuttingDown,
            SaveOperationPhase.Admission,
            committed: false));

    private static SaveOperationAdmission Canceled(
        SaveOperationKind kind,
        string targetSlotID,
        string operationToken,
        SaveOperationPhase phase) => new(
        null,
        new SaveOperationResult(
            operationToken,
            kind,
            targetSlotID,
            SaveOperationResultKind.Canceled,
            phase,
            committed: false));

    private static void ValidateTarget(string targetSlotID)
    {
        if (string.IsNullOrWhiteSpace(targetSlotID))
            throw new ArgumentException("Target slot ID cannot be empty.", nameof(targetSlotID));
    }
}

internal sealed class SaveOperationLease : IStorageOperationLease, IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly SaveOperationCoordinator _owner;
    private readonly SaveOperationPhase[] _phaseOrder;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly CancellationTokenRegistration _externalCancellation;
    private readonly TaskCompletionSource _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private SaveOperationPhase _phase = SaveOperationPhase.Admission;
    private bool _commitLeaseAcquired;
    private bool _commitBoundary;
    private bool _committed;
    private bool _cancellationRequested;
    private bool _completed;

    internal SaveOperationLease(
        SaveOperationCoordinator owner,
        SaveOperationKind kind,
        string targetSlotID,
        string operationToken,
        SaveOperationPhase[] phaseOrder,
        CancellationToken externalCancellation)
    {
        _owner = owner;
        Kind = kind;
        TargetSlotID = targetSlotID;
        OperationToken = operationToken;
        _phaseOrder = phaseOrder;
        _externalCancellation = externalCancellation.Register(() =>
        {
            RequestCancellation();
        });
    }

    public string OperationToken { get; }
    public SaveOperationKind Kind { get; }
    internal string TargetSlotID { get; }
    internal Task Completion => _completion.Task;

    internal SaveOperationState State
    {
        get
        {
            lock (_sync)
                return CreateStateLocked();
        }
    }

    internal void AdvanceTo(SaveOperationPhase phase)
    {
        SaveOperationState state;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_completed, this);
            int current = Array.IndexOf(_phaseOrder, _phase);
            int next = Array.IndexOf(_phaseOrder, phase);
            if (next < 0 || next < current || phase == SaveOperationPhase.Completed)
                throw new InvalidOperationException(
                    $"Operation {Kind} cannot advance from {_phase} to {phase}.");
            _phase = phase;
            state = CreateStateLocked();
        }
        _owner.PublishState(state);
    }

    public void ThrowIfCancellationRequested()
    {
        lock (_sync)
        {
            if (!_commitBoundary && (_cancellationRequested || _cancellation.IsCancellationRequested))
                throw new OperationCanceledException(_cancellation.Token);
        }
    }

    public void AcquireCommitLease()
    {
        SaveOperationState state;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_completed, this);
            if (_commitLeaseAcquired)
                throw new InvalidOperationException("The operation already acquired its commit lease.");
            if (_cancellationRequested || _cancellation.IsCancellationRequested)
                throw new OperationCanceledException(_cancellation.Token);
            _phase = Kind is SaveOperationKind.Publish or SaveOperationKind.Autosave
                ? SaveOperationPhase.Publish
                : SaveOperationPhase.Commit;
            _commitLeaseAcquired = true;
            state = CreateStateLocked();
        }
        _owner.PublishState(state);
    }

    public void CrossCommitBoundary(Action boundaryAction)
    {
        ArgumentNullException.ThrowIfNull(boundaryAction);
        SaveOperationState state;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_completed, this);
            if (!_commitLeaseAcquired)
                throw new InvalidOperationException("The commit lease must be acquired before crossing the boundary.");
            if (_commitBoundary)
                throw new InvalidOperationException("The operation already crossed its commit boundary.");
            if (_cancellationRequested || _cancellation.IsCancellationRequested)
                throw new OperationCanceledException(_cancellation.Token);
            boundaryAction();
            _commitBoundary = true;
            state = CreateStateLocked();
        }
        _owner.PublishState(state);
    }

    public void EnterCommitBoundary()
    {
        AcquireCommitLease();
        CrossCommitBoundary(static () => { });
        MarkCommitted();
    }

    public void MarkCommitted()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_completed, this);
            if (!_commitBoundary)
                throw new InvalidOperationException("The commit boundary must be crossed before marking success.");
            if (_committed)
                throw new InvalidOperationException("The operation is already marked committed.");
            _committed = true;
        }
    }

    internal bool RequestCancellation()
    {
        SaveOperationState state;
        bool accepted;
        lock (_sync)
        {
            if (_completed)
                return false;
            _cancellationRequested = true;
            accepted = !_commitBoundary;
            if (accepted)
                _cancellation.Cancel();
            state = CreateStateLocked();
        }
        _owner.PublishState(state);
        return accepted;
    }

    internal SaveOperationResult Complete(
        SaveOperationResultKind resultKind,
        IEnumerable<string>? warnings = null,
        string? error = null)
    {
        SaveOperationResult result;
        SaveOperationState state;
        lock (_sync)
        {
            if (_completed)
                throw new InvalidOperationException("The operation is already complete.");
            _completed = true;
            result = new SaveOperationResult(
                OperationToken,
                Kind,
                TargetSlotID,
                resultKind,
                _phase,
                _committed,
                warnings,
                error);
            _phase = SaveOperationPhase.Completed;
            state = CreateStateLocked();
        }

        _owner.PublishState(state);
        _externalCancellation.Dispose();
        _cancellation.Dispose();
        _owner.Release(this);
        _completion.TrySetResult();
        return result;
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_completed)
                return ValueTask.CompletedTask;
        }
        Complete(
            SaveOperationResultKind.Failed,
            error: "The operation lease was disposed without a terminal result.");
        return ValueTask.CompletedTask;
    }

    private SaveOperationState CreateStateLocked() => new(
        OperationToken,
        Kind,
        TargetSlotID,
        _phase,
        _commitBoundary,
        _cancellationRequested);
}
