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

    public void ReleaseRecoverGate() =>
        _saveManager?.ReleaseDeleteRecoverGate();

    public void Disarm()
    {
        _saveManager?.DisarmDeleteRecoverGate();
        _saveManager = null;
    }

    public bool IsRecoverGateArmed() =>
        _saveManager?.IsDeleteRecoverGateArmed() ?? false;

    public bool HasEnteredRecoverGate() =>
        _saveManager?.HasEnteredDeleteRecoverGate() ?? false;

    public int GetRecoverGateTriggerCount() =>
        _saveManager?.GetDeleteRecoverGateTriggerCount() ?? 0;

    public int GetCancelRequestCount() =>
        _saveManager?.GetDeleteRecoverGateCancelRequestCount() ?? 0;

    public string GetCancelOperationToken() =>
        _saveManager?.GetDeleteRecoverGateCancelOperationToken() ?? string.Empty;
}

public partial class SaveManager
{
    private const string DeleteRecoverGateTimeoutMessage =
        "Timed out waiting to release the Delete Recover test gate.";
    private static readonly TimeSpan DeleteRecoverGateTimeout = TimeSpan.FromSeconds(15);

    private readonly ManualResetEventSlim _deleteRecoverGateRelease = new(initialState: true);
    private int _deleteRecoverGateArmed;
    private int _deleteRecoverGateEntered;
    private int _deleteRecoverGateTriggerCount;
    private int _deleteRecoverGateCancelRequestCount;
    private string _deleteRecoverGateCancelOperationToken = string.Empty;

    partial void ProbeObserveCancelOperation(ref string operationToken)
    {
        if (Volatile.Read(ref _deleteRecoverGateEntered) == 0)
            return;

        _deleteRecoverGateCancelOperationToken = operationToken;
        Interlocked.Increment(ref _deleteRecoverGateCancelRequestCount);
    }

    partial void ProbeWaitAtDeleteRecover(ref SaveSlotStore store)
    {
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
}
