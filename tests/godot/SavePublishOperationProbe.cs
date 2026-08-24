using System;
using System.Threading;

public partial class SavePublishOperationProbe : Godot.RefCounted
{
    private SaveManager? _saveManager;

    public void ArmPrepareGate(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        Disarm();
        saveManager.ArmNextPublishPrepareGate();
        _saveManager = saveManager;
    }

    public void ReleasePrepareGate() =>
        _saveManager?.ReleasePublishPrepareGate();

    public void Disarm()
    {
        _saveManager?.DisarmPublishPrepareGate();
        _saveManager = null;
    }

    public bool IsPrepareGateArmed() =>
        _saveManager?.IsPublishPrepareGateArmed() ?? false;

    public bool HasEnteredPrepareGate() =>
        _saveManager?.HasEnteredPublishPrepareGate() ?? false;

    public int GetPrepareGateTriggerCount() =>
        _saveManager?.GetPublishPrepareGateTriggerCount() ?? 0;

    public int GetCancelRequestCount() =>
        _saveManager?.GetPublishPrepareGateCancelRequestCount() ?? 0;

    public string GetCancelOperationToken() =>
        _saveManager?.GetPublishPrepareGateCancelOperationToken() ?? string.Empty;
}

public partial class SaveManager
{
    private const string PublishPrepareGateTimeoutMessage =
        "Timed out waiting to release the Save publish Prepare test gate.";
    private static readonly TimeSpan PublishPrepareGateTimeout = TimeSpan.FromSeconds(15);

    private readonly ManualResetEventSlim _publishPrepareGateRelease = new(initialState: true);
    private int _publishPrepareGateArmed;
    private int _publishPrepareGateEntered;
    private int _publishPrepareGateTriggerCount;
    private int _publishPrepareGateCancelRequestCount;
    private string _publishPrepareGateCancelOperationToken = string.Empty;

    partial void ProbeObservePublishCancelOperation(ref string operationToken)
    {
        if (Volatile.Read(ref _publishPrepareGateEntered) == 0)
            return;

        _publishPrepareGateCancelOperationToken = operationToken;
        Interlocked.Increment(ref _publishPrepareGateCancelRequestCount);
    }

    partial void ProbeWaitAtPublishPrepare(ref SaveSlotStore store)
    {
        if (Interlocked.Exchange(ref _publishPrepareGateArmed, 0) == 0)
            return;

        Interlocked.Increment(ref _publishPrepareGateTriggerCount);
        Volatile.Write(ref _publishPrepareGateEntered, 1);
        try
        {
            if (!_publishPrepareGateRelease.Wait(PublishPrepareGateTimeout))
                throw new TimeoutException(PublishPrepareGateTimeoutMessage);
        }
        finally
        {
            Volatile.Write(ref _publishPrepareGateEntered, 0);
        }
    }

    internal void ArmNextPublishPrepareGate()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its publish Prepare test gate.");
        }
        if (Interlocked.CompareExchange(ref _publishPrepareGateArmed, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "Save publish Prepare test gate is already armed.");
        }

        Interlocked.Exchange(ref _publishPrepareGateTriggerCount, 0);
        Interlocked.Exchange(ref _publishPrepareGateCancelRequestCount, 0);
        _publishPrepareGateCancelOperationToken = string.Empty;
        Volatile.Write(ref _publishPrepareGateEntered, 0);
        _publishPrepareGateRelease.Reset();
    }

    internal void ReleasePublishPrepareGate() =>
        _publishPrepareGateRelease.Set();

    internal void DisarmPublishPrepareGate()
    {
        Interlocked.Exchange(ref _publishPrepareGateArmed, 0);
        _publishPrepareGateRelease.Set();
    }

    internal bool IsPublishPrepareGateArmed() =>
        Volatile.Read(ref _publishPrepareGateArmed) != 0;

    internal bool HasEnteredPublishPrepareGate() =>
        Volatile.Read(ref _publishPrepareGateEntered) != 0;

    internal int GetPublishPrepareGateTriggerCount() =>
        Volatile.Read(ref _publishPrepareGateTriggerCount);

    internal int GetPublishPrepareGateCancelRequestCount() =>
        Volatile.Read(ref _publishPrepareGateCancelRequestCount);

    internal string GetPublishPrepareGateCancelOperationToken() =>
        _publishPrepareGateCancelOperationToken;
}
