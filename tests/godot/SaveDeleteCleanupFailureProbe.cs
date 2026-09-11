using System;
using System.IO;
using System.Threading;

public partial class SaveDeleteCleanupFailureProbe : Godot.RefCounted
{
    private SaveManager? _saveManager;

    public void Arm(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        Disarm();
        _saveManager = saveManager;
        saveManager.ArmNextDeleteTombstoneCleanupFailure();
    }

    public void Disarm()
    {
        _saveManager?.DisarmDeleteTombstoneCleanupFailure();
        _saveManager = null;
    }

    public string GetFailureMessage() =>
        SaveManager.DeleteTombstoneCleanupFailureMessage;

    public bool IsArmed() =>
        _saveManager?.IsDeleteTombstoneCleanupFailureArmed() ?? false;

    public int GetTriggerCount() =>
        _saveManager?.GetDeleteTombstoneCleanupFailureCount() ?? 0;
}

public partial class SaveManager
{
    internal const string DeleteTombstoneCleanupFailureMessage =
        "Injected Delete tombstone cleanup failure.";

    private int _deleteTombstoneCleanupFailureArmed;
    private int _deleteTombstoneCleanupFailureCount;

    partial void ProbeConfigureDeleteCleanupFailure(ref SaveSlotStore store)
    {
        if (Volatile.Read(ref _deleteTombstoneCleanupFailureArmed) == 0)
            return;

        store = new SaveSlotStore(store.SaveBaseDirectory, phase =>
        {
            if (phase != SavePublicationPhase.DeletionTombstoned ||
                Interlocked.Exchange(ref _deleteTombstoneCleanupFailureArmed, 0) == 0)
            {
                return;
            }

            Interlocked.Increment(ref _deleteTombstoneCleanupFailureCount);
            throw new IOException(DeleteTombstoneCleanupFailureMessage);
        });
    }

    internal void ArmNextDeleteTombstoneCleanupFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its Delete cleanup failure probe.");
        }
        if (Interlocked.CompareExchange(
                ref _deleteTombstoneCleanupFailureArmed,
                1,
                0) != 0)
        {
            throw new InvalidOperationException(
                "Delete tombstone cleanup failure probe is already armed.");
        }
    }

    internal void DisarmDeleteTombstoneCleanupFailure() =>
        Interlocked.Exchange(ref _deleteTombstoneCleanupFailureArmed, 0);

    internal bool IsDeleteTombstoneCleanupFailureArmed() =>
        Volatile.Read(ref _deleteTombstoneCleanupFailureArmed) != 0;

    internal int GetDeleteTombstoneCleanupFailureCount() =>
        Volatile.Read(ref _deleteTombstoneCleanupFailureCount);
}
