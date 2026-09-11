using System;
using System.Threading;

public partial class SaveDeleteOperationProbe : Godot.RefCounted
{
    private SaveManager? _saveManager;

    public void ArmRecoverGate(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        Disarm();
        saveManager.ArmNextDeleteRecoverGate();
        _saveManager = saveManager;
    }

    public void ArmPostCommitGate(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        Disarm();
        saveManager.ArmNextDeletePostCommitGate();
        _saveManager = saveManager;
    }

    public void ReleaseRecoverGate() =>
        _saveManager?.ReleaseDeleteRecoverGate();

    public void ReleasePostCommitGate() =>
        _saveManager?.ReleaseDeletePostCommitGate();

    public void Disarm()
    {
        _saveManager?.DisarmDeleteRecoverGate();
        _saveManager?.DisarmDeletePostCommitGate();
        _saveManager = null;
    }

    public bool IsRecoverGateArmed() =>
        _saveManager?.IsDeleteRecoverGateArmed() ?? false;

    public bool HasEnteredRecoverGate() =>
        _saveManager?.HasEnteredDeleteRecoverGate() ?? false;

    public int GetRecoverGateTriggerCount() =>
        _saveManager?.GetDeleteRecoverGateTriggerCount() ?? 0;

    public bool IsPostCommitGateArmed() =>
        _saveManager?.IsDeletePostCommitGateArmed() ?? false;

    public bool HasEnteredPostCommitGate() =>
        _saveManager?.HasEnteredDeletePostCommitGate() ?? false;

    public int GetPostCommitGateTriggerCount() =>
        _saveManager?.GetDeletePostCommitGateTriggerCount() ?? 0;

    public int GetCancelRequestCount() =>
        _saveManager?.GetDeleteRecoverGateCancelRequestCount() ?? 0;

    public string GetCancelOperationToken() =>
        _saveManager?.GetDeleteRecoverGateCancelOperationToken() ?? string.Empty;

    public int GetPostCommitCancelRequestCount() =>
        _saveManager?.GetDeletePostCommitGateCancelRequestCount() ?? 0;

    public string GetPostCommitCancelOperationToken() =>
        _saveManager?.GetDeletePostCommitGateCancelOperationToken() ?? string.Empty;
}

public partial class SaveManager
{
    private const string DeleteRecoverGateTimeoutMessage =
        "Timed out waiting to release the Delete Recover test gate.";
    private const string DeletePostCommitGateTimeoutMessage =
        "Timed out waiting to release the Delete post-commit test gate.";
    private static readonly TimeSpan DeleteRecoverGateTimeout = TimeSpan.FromSeconds(15);

    private readonly ManualResetEventSlim _deleteRecoverGateRelease = new(initialState: true);
    private int _deleteRecoverGateArmed;
    private int _deleteRecoverGateEntered;
    private int _deleteRecoverGateTriggerCount;
    private int _deleteRecoverGateCancelRequestCount;
    private string _deleteRecoverGateCancelOperationToken = string.Empty;
    private readonly ManualResetEventSlim _deletePostCommitGateRelease = new(initialState: true);
    private int _deletePostCommitGateArmed;
    private int _deletePostCommitGateEntered;
    private int _deletePostCommitGateTriggerCount;
    private int _deletePostCommitGateCancelRequestCount;
    private string _deletePostCommitGateCancelOperationToken = string.Empty;

    partial void ProbeObserveCancelOperation(ref string operationToken)
    {
        if (Volatile.Read(ref _deleteRecoverGateEntered) != 0)
        {
            _deleteRecoverGateCancelOperationToken = operationToken;
            Interlocked.Increment(ref _deleteRecoverGateCancelRequestCount);
        }

        if (Volatile.Read(ref _deletePostCommitGateEntered) != 0)
        {
            _deletePostCommitGateCancelOperationToken = operationToken;
            Interlocked.Increment(ref _deletePostCommitGateCancelRequestCount);
        }
    }

    partial void ProbeWaitAtDeleteRecover(ref SaveSlotStore store)
    {
        if (Volatile.Read(ref _deletePostCommitGateArmed) != 0)
            store = new SaveSlotStore(store.SaveBaseDirectory, WaitAtDeletePostCommit);

        if (Interlocked.Exchange(ref _deleteRecoverGateArmed, 0) == 0)
            return;

        Interlocked.Increment(ref _deleteRecoverGateTriggerCount);
        Volatile.Write(ref _deleteRecoverGateEntered, 1);
        try
        {
            if (!_deleteRecoverGateRelease.Wait(DeleteRecoverGateTimeout))
                throw new TimeoutException(DeleteRecoverGateTimeoutMessage);
        }
        finally
        {
            Volatile.Write(ref _deleteRecoverGateEntered, 0);
        }
    }

    private void WaitAtDeletePostCommit(SavePublicationPhase phase)
    {
        if (phase != SavePublicationPhase.DeletionTombstoned ||
            Interlocked.Exchange(ref _deletePostCommitGateArmed, 0) == 0)
        {
            return;
        }

        Interlocked.Increment(ref _deletePostCommitGateTriggerCount);
        Volatile.Write(ref _deletePostCommitGateEntered, 1);
        try
        {
            if (!_deletePostCommitGateRelease.Wait(DeleteRecoverGateTimeout))
                throw new TimeoutException(DeletePostCommitGateTimeoutMessage);
        }
        finally
        {
            Volatile.Write(ref _deletePostCommitGateEntered, 0);
        }
    }

    internal void ArmNextDeleteRecoverGate()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its Delete Recover test gate.");
        }
        if (Interlocked.CompareExchange(ref _deleteRecoverGateArmed, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "Delete Recover test gate is already armed.");
        }

        Interlocked.Exchange(ref _deleteRecoverGateTriggerCount, 0);
        Interlocked.Exchange(ref _deleteRecoverGateCancelRequestCount, 0);
        _deleteRecoverGateCancelOperationToken = string.Empty;
        Volatile.Write(ref _deleteRecoverGateEntered, 0);
        _deleteRecoverGateRelease.Reset();
    }

    internal void ReleaseDeleteRecoverGate() =>
        _deleteRecoverGateRelease.Set();

    internal void DisarmDeleteRecoverGate()
    {
        Interlocked.Exchange(ref _deleteRecoverGateArmed, 0);
        _deleteRecoverGateRelease.Set();
    }

    internal bool IsDeleteRecoverGateArmed() =>
        Volatile.Read(ref _deleteRecoverGateArmed) != 0;

    internal bool HasEnteredDeleteRecoverGate() =>
        Volatile.Read(ref _deleteRecoverGateEntered) != 0;

    internal int GetDeleteRecoverGateTriggerCount() =>
        Volatile.Read(ref _deleteRecoverGateTriggerCount);

    internal int GetDeleteRecoverGateCancelRequestCount() =>
        Volatile.Read(ref _deleteRecoverGateCancelRequestCount);

    internal string GetDeleteRecoverGateCancelOperationToken() =>
        _deleteRecoverGateCancelOperationToken;

    internal void ArmNextDeletePostCommitGate()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its Delete post-commit test gate.");
        }
        if (Interlocked.CompareExchange(ref _deletePostCommitGateArmed, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "Delete post-commit test gate is already armed.");
        }

        Interlocked.Exchange(ref _deletePostCommitGateTriggerCount, 0);
        Interlocked.Exchange(ref _deletePostCommitGateCancelRequestCount, 0);
        _deletePostCommitGateCancelOperationToken = string.Empty;
        Volatile.Write(ref _deletePostCommitGateEntered, 0);
        _deletePostCommitGateRelease.Reset();
    }

    internal void ReleaseDeletePostCommitGate() =>
        _deletePostCommitGateRelease.Set();

    internal void DisarmDeletePostCommitGate()
    {
        Interlocked.Exchange(ref _deletePostCommitGateArmed, 0);
        _deletePostCommitGateRelease.Set();
    }

    internal bool IsDeletePostCommitGateArmed() =>
        Volatile.Read(ref _deletePostCommitGateArmed) != 0;

    internal bool HasEnteredDeletePostCommitGate() =>
        Volatile.Read(ref _deletePostCommitGateEntered) != 0;

    internal int GetDeletePostCommitGateTriggerCount() =>
        Volatile.Read(ref _deletePostCommitGateTriggerCount);

    internal int GetDeletePostCommitGateCancelRequestCount() =>
        Volatile.Read(ref _deletePostCommitGateCancelRequestCount);

    internal string GetDeletePostCommitGateCancelOperationToken() =>
        _deletePostCommitGateCancelOperationToken;
}
