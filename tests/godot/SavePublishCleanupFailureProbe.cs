using System;
using System.IO;
using System.Threading;

public partial class SavePublishCleanupFailureProbe : Godot.RefCounted
{
    private SaveManager? _saveManager;

    public void Arm(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        Disarm();
        _saveManager = saveManager;
        saveManager.ArmNextPublishCleanupFailure();
    }

    public void Disarm()
    {
        _saveManager?.DisarmPublishCleanupFailure();
        _saveManager = null;
    }

    public string GetFailureMessage() =>
        SaveManager.PublishCleanupFailureMessage;

    public bool IsArmed() =>
        _saveManager?.IsPublishCleanupFailureArmed() ?? false;

    public int GetTriggerCount() =>
        _saveManager?.GetPublishCleanupFailureCount() ?? 0;
}

public partial class SaveManager
{
    internal const string PublishCleanupFailureMessage =
        "Injected Save publish cleanup failure.";

    private int _publishCleanupFailureArmed;
    private int _publishCleanupFailureCount;

    partial void ProbeConfigurePublishCleanupFailure(ref SaveSlotStore store)
    {
        if (Volatile.Read(ref _publishCleanupFailureArmed) == 0)
            return;

        store = new SaveSlotStore(_resolvedSaveBaseDir, phase =>
        {
            if (phase != SavePublicationPhase.CanonicalPublished ||
                Interlocked.Exchange(ref _publishCleanupFailureArmed, 0) == 0)
            {
                return;
            }

            Interlocked.Increment(ref _publishCleanupFailureCount);
            throw new IOException(PublishCleanupFailureMessage);
        });
    }

    internal void ArmNextPublishCleanupFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its publish cleanup failure probe.");
        }
        if (Interlocked.CompareExchange(ref _publishCleanupFailureArmed, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "Save publish cleanup failure probe is already armed.");
        }
    }

    internal void DisarmPublishCleanupFailure() =>
        Interlocked.Exchange(ref _publishCleanupFailureArmed, 0);

    internal bool IsPublishCleanupFailureArmed() =>
        Volatile.Read(ref _publishCleanupFailureArmed) != 0;

    internal int GetPublishCleanupFailureCount() =>
        Volatile.Read(ref _publishCleanupFailureCount);
}
