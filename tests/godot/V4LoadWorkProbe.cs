using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

/// <summary>Debug-only load worker gate and external notification failure boundary.</summary>
public partial class V4LoadWorkProbe : RefCounted
{
    private static V4LoadWorkProbe? _active;
    private readonly ManualResetEventSlim _release = new(false);
    private SaveManager? _manager;
    private V4MapScene? _scene;
    private string _mode = "";
    private int _started;
    private int _completed;
    private int _workerThread;
    private int _mainThread;
    private FileStream? _saveLock;

    public bool Started => Volatile.Read(ref _started) != 0;
    public bool Completed => Volatile.Read(ref _completed) != 0;
    public bool UsedBackgroundThread => Started && Volatile.Read(ref _workerThread) != _mainThread;
    public bool NotificationObserved { get; private set; }
    public bool ObservedCleanState { get; private set; }

    public void Install(SaveManager manager, V4MapScene scene, string mode)
    {
        if (_active is not null || manager.IsOperationBusy || mode is not ("normal" or "fail-preflight" or "observe-notifications"))
            throw new InvalidOperationException("Load probe must be installed while idle with a known mode.");
        _manager = manager;
        _scene = scene;
        _mode = mode;
        _mainThread = System.Environment.CurrentManagedThreadId;
        Volatile.Write(ref _active, this);
    }

    internal static void AtWorkerEntry(SaveManager manager)
    {
        V4LoadWorkProbe? probe = Volatile.Read(ref _active);
        if (probe is null || !ReferenceEquals(probe._manager, manager)) return;
        Volatile.Write(ref probe._workerThread, System.Environment.CurrentManagedThreadId);
        Volatile.Write(ref probe._started, 1);
        probe._release.Wait();
        Volatile.Write(ref probe._completed, 1);
    }

    internal static void AfterPreflight(SaveManager manager, IReadOnlyList<INonThrowingLoadCommitPlan> plans)
    {
        V4LoadWorkProbe? probe = Volatile.Read(ref _active);
        if (probe is null || !ReferenceEquals(probe._manager, manager)) return;
        if (probe._mode == "fail-preflight")
            throw new InvalidOperationException("Injected V4 load failure after all participant preflights.");
        if (probe._mode == "observe-notifications")
        {
            if (plans is not List<INonThrowingLoadCommitPlan> mutable)
                throw new InvalidOperationException("Load probe requires the pre-aggregate plan list.");
            mutable.Insert(0, new Observer(probe));
        }
    }

    public void Release() => _release.Set();

    public void LockSaveManifest(string slotID)
    {
        if (_saveLock is not null || !slotID.StartsWith("manual-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(slotID[7..], "N", out _))
            throw new ArgumentException("Expected a fresh manual test slot.", nameof(slotID));
        string path = ProjectSettings.GlobalizePath($"user://saves-v4/{slotID}/manifest.json");
        _saveLock = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.None);
    }

    public void ReleaseSaveLock()
    {
        _saveLock?.Dispose();
        _saveLock = null;
    }

    public void Cleanup()
    {
        if (!Completed || _manager?.IsOperationBusy == true)
            throw new InvalidOperationException("Await the released load before cleaning its probe.");
        if (ReferenceEquals(_active, this)) Volatile.Write(ref _active, null);
        _manager = null;
        _scene = null;
        _release.Dispose();
    }

    private sealed class Observer(V4LoadWorkProbe probe) : INonThrowingLoadCommitPlan
    {
        public string ParticipantID => "v4-load-observer-probe";
        public bool IsGenerationCurrent => true;
        public void CommitReferences() { }
        public IReadOnlyList<string> PublishNotifications()
        {
            V4MapScene scene = probe._scene ?? throw new InvalidOperationException("Missing probe scene.");
            var selection = scene.GetSelectionState();
            var preview = scene.GetBuildPreview();
            probe.NotificationObserved = true;
            probe.ObservedCleanState = !scene.HasBuildPreview && scene.IsPresentationCurrent &&
                selection["selectedCount"].AsInt32() == 0 && !selection["hasHover"].AsBool() &&
                !selection["selecting"].AsBool() && selection["strokes"].AsGodotArray().Count == 0 &&
                preview["phase"].AsString() == "None" && preview["segments"].AsGodotArray().Count == 0 &&
                scene.GetHistoryState()["retainedCount"].AsInt32() == 0;
            throw new InvalidOperationException("Injected V4 external load observer failure.");
        }
        public void CompleteCommit() { }
        public void Dispose() { }
    }
}
