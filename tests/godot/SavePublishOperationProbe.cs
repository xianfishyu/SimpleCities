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

    public void ArmStagedGate(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        Disarm();
        saveManager.ArmNextPublishStagedGate();
        _saveManager = saveManager;
    }

    public void ArmPostCommitGate(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        Disarm();
        saveManager.ArmNextPublishPostCommitGate();
        _saveManager = saveManager;
    }

    public void ReleasePrepareGate() =>
        _saveManager?.ReleasePublishPrepareGate();

    public void ReleaseStagedGate() =>
        _saveManager?.ReleasePublishStagedGate();

    public void ReleasePostCommitGate() =>
        _saveManager?.ReleasePublishPostCommitGate();

    public void Disarm()
    {
        _saveManager?.DisarmPublishPrepareGate();
        _saveManager?.DisarmPublishStagedGate();
        _saveManager?.DisarmPublishPostCommitGate();
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

    public bool IsStagedGateArmed() =>
        _saveManager?.IsPublishStagedGateArmed() ?? false;

    public bool HasEnteredStagedGate() =>
        _saveManager?.HasEnteredPublishStagedGate() ?? false;

    public int GetStagedGateTriggerCount() =>
        _saveManager?.GetPublishStagedGateTriggerCount() ?? 0;

    public int GetStagedCancelRequestCount() =>
        _saveManager?.GetPublishStagedGateCancelRequestCount() ?? 0;

    public string GetStagedCancelOperationToken() =>
        _saveManager?.GetPublishStagedGateCancelOperationToken() ?? string.Empty;

    public bool IsPostCommitGateArmed() =>
        _saveManager?.IsPublishPostCommitGateArmed() ?? false;

    public bool HasEnteredPostCommitGate() =>
        _saveManager?.HasEnteredPublishPostCommitGate() ?? false;

    public int GetPostCommitGateTriggerCount() =>
        _saveManager?.GetPublishPostCommitGateTriggerCount() ?? 0;

    public int GetPostCommitCancelRequestCount() =>
        _saveManager?.GetPublishPostCommitGateCancelRequestCount() ?? 0;

    public string GetPostCommitCancelOperationToken() =>
        _saveManager?.GetPublishPostCommitGateCancelOperationToken() ?? string.Empty;
}

public partial class SaveManager
{
    private const string PublishPrepareGateTimeoutMessage =
        "Timed out waiting to release the Save publish Prepare test gate.";
    private const string PublishStagedGateTimeoutMessage =
        "Timed out waiting to release the Save publish staged test gate.";
    private const string PublishPostCommitGateTimeoutMessage =
        "Timed out waiting to release the Save publish post-commit test gate.";
    private static readonly TimeSpan PublishOperationGateTimeout = TimeSpan.FromSeconds(15);

    private readonly ManualResetEventSlim _publishPrepareGateRelease = new(initialState: true);
    private int _publishPrepareGateArmed;
    private int _publishPrepareGateEntered;
    private int _publishPrepareGateTriggerCount;
    private int _publishPrepareGateCancelRequestCount;
    private string _publishPrepareGateCancelOperationToken = string.Empty;
    private readonly ManualResetEventSlim _publishStagedGateRelease = new(initialState: true);
    private int _publishStagedGateArmed;
    private int _publishStagedGateEntered;
    private int _publishStagedGateTriggerCount;
    private int _publishStagedGateCancelRequestCount;
    private string _publishStagedGateCancelOperationToken = string.Empty;
    private readonly ManualResetEventSlim _publishPostCommitGateRelease = new(initialState: true);
    private int _publishPostCommitGateArmed;
    private int _publishPostCommitGateEntered;
    private int _publishPostCommitGateTriggerCount;
    private int _publishPostCommitGateCancelRequestCount;
    private string _publishPostCommitGateCancelOperationToken = string.Empty;

    partial void ProbeObservePublishCancelOperation(ref string operationToken)
    {
        if (Volatile.Read(ref _publishPrepareGateEntered) != 0)
        {
            _publishPrepareGateCancelOperationToken = operationToken;
            Interlocked.Increment(ref _publishPrepareGateCancelRequestCount);
        }

        if (Volatile.Read(ref _publishStagedGateEntered) != 0)
        {
            _publishStagedGateCancelOperationToken = operationToken;
            Interlocked.Increment(ref _publishStagedGateCancelRequestCount);
        }

        if (Volatile.Read(ref _publishPostCommitGateEntered) != 0)
        {
            _publishPostCommitGateCancelOperationToken = operationToken;
            Interlocked.Increment(ref _publishPostCommitGateCancelRequestCount);
        }
    }

    partial void ProbeWaitAtPublishPrepare(ref SaveSlotStore store)
    {
        if (Volatile.Read(ref _publishStagedGateArmed) != 0)
            store = new SaveSlotStore(_resolvedSaveBaseDir, WaitAtPublishStaged);
        else if (Volatile.Read(ref _publishPostCommitGateArmed) != 0)
            store = new SaveSlotStore(_resolvedSaveBaseDir, WaitAtPublishPostCommit);

        if (Interlocked.Exchange(ref _publishPrepareGateArmed, 0) == 0)
            return;

        Interlocked.Increment(ref _publishPrepareGateTriggerCount);
        Volatile.Write(ref _publishPrepareGateEntered, 1);
        try
        {
            if (!_publishPrepareGateRelease.Wait(PublishOperationGateTimeout))
                throw new TimeoutException(PublishPrepareGateTimeoutMessage);
        }
        finally
        {
            Volatile.Write(ref _publishPrepareGateEntered, 0);
        }
    }

    private void WaitAtPublishStaged(SavePublicationPhase phase)
    {
        if (phase != SavePublicationPhase.Staged ||
            Interlocked.Exchange(ref _publishStagedGateArmed, 0) == 0)
        {
            return;
        }

        Interlocked.Increment(ref _publishStagedGateTriggerCount);
        Volatile.Write(ref _publishStagedGateEntered, 1);
        try
        {
            if (!_publishStagedGateRelease.Wait(PublishOperationGateTimeout))
                throw new TimeoutException(PublishStagedGateTimeoutMessage);
        }
        finally
        {
            Volatile.Write(ref _publishStagedGateEntered, 0);
        }
    }

    private void WaitAtPublishPostCommit(SavePublicationPhase phase)
    {
        if (phase != SavePublicationPhase.CanonicalPublished ||
            Interlocked.Exchange(ref _publishPostCommitGateArmed, 0) == 0)
        {
            return;
        }

        Interlocked.Increment(ref _publishPostCommitGateTriggerCount);
        Volatile.Write(ref _publishPostCommitGateEntered, 1);
        try
        {
            if (!_publishPostCommitGateRelease.Wait(PublishOperationGateTimeout))
                throw new TimeoutException(PublishPostCommitGateTimeoutMessage);
        }
        finally
        {
            Volatile.Write(ref _publishPostCommitGateEntered, 0);
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

    internal void ArmNextPublishStagedGate()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its publish staged test gate.");
        }
        if (Interlocked.CompareExchange(ref _publishStagedGateArmed, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "Save publish staged test gate is already armed.");
        }

        Interlocked.Exchange(ref _publishStagedGateTriggerCount, 0);
        Interlocked.Exchange(ref _publishStagedGateCancelRequestCount, 0);
        _publishStagedGateCancelOperationToken = string.Empty;
        Volatile.Write(ref _publishStagedGateEntered, 0);
        _publishStagedGateRelease.Reset();
    }

    internal void ReleasePublishStagedGate() =>
        _publishStagedGateRelease.Set();

    internal void DisarmPublishStagedGate()
    {
        Interlocked.Exchange(ref _publishStagedGateArmed, 0);
        _publishStagedGateRelease.Set();
    }

    internal bool IsPublishStagedGateArmed() =>
        Volatile.Read(ref _publishStagedGateArmed) != 0;

    internal bool HasEnteredPublishStagedGate() =>
        Volatile.Read(ref _publishStagedGateEntered) != 0;

    internal int GetPublishStagedGateTriggerCount() =>
        Volatile.Read(ref _publishStagedGateTriggerCount);

    internal int GetPublishStagedGateCancelRequestCount() =>
        Volatile.Read(ref _publishStagedGateCancelRequestCount);

    internal string GetPublishStagedGateCancelOperationToken() =>
        _publishStagedGateCancelOperationToken;

    internal void ArmNextPublishPostCommitGate()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its publish post-commit test gate.");
        }
        if (Interlocked.CompareExchange(ref _publishPostCommitGateArmed, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "Save publish post-commit test gate is already armed.");
        }

        Interlocked.Exchange(ref _publishPostCommitGateTriggerCount, 0);
        Interlocked.Exchange(ref _publishPostCommitGateCancelRequestCount, 0);
        _publishPostCommitGateCancelOperationToken = string.Empty;
        Volatile.Write(ref _publishPostCommitGateEntered, 0);
        _publishPostCommitGateRelease.Reset();
    }

    internal void ReleasePublishPostCommitGate() =>
        _publishPostCommitGateRelease.Set();

    internal void DisarmPublishPostCommitGate()
    {
        Interlocked.Exchange(ref _publishPostCommitGateArmed, 0);
        _publishPostCommitGateRelease.Set();
    }

    internal bool IsPublishPostCommitGateArmed() =>
        Volatile.Read(ref _publishPostCommitGateArmed) != 0;

    internal bool HasEnteredPublishPostCommitGate() =>
        Volatile.Read(ref _publishPostCommitGateEntered) != 0;

    internal int GetPublishPostCommitGateTriggerCount() =>
        Volatile.Read(ref _publishPostCommitGateTriggerCount);

    internal int GetPublishPostCommitGateCancelRequestCount() =>
        Volatile.Read(ref _publishPostCommitGateCancelRequestCount);

    internal string GetPublishPostCommitGateCancelOperationToken() =>
        _publishPostCommitGateCancelOperationToken;
}
