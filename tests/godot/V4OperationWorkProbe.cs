using Godot;
using System;
using System.Threading;

/// <summary>Debug-only controllable worker boundary; releases late work even after cancellation.</summary>
public partial class V4OperationWorkProbe : RefCounted
{
    private readonly ManualResetEventSlim _release = new(false);
    private int _started;
    private int _completed;
    private int _workerThread;
    private int _mainThread;
    private bool _disposed;

    public bool Started => Volatile.Read(ref _started) != 0;
    public bool Completed => Volatile.Read(ref _completed) != 0;
    public bool UsedBackgroundThread => Started && Volatile.Read(ref _workerThread) != _mainThread;

    public void Install(V4MapScene scene)
    {
        _mainThread = System.Environment.CurrentManagedThreadId;
        scene.BeforeBuildWork = Wait;
    }

    public void InstallPreview(V4MapScene scene)
    {
        _mainThread = System.Environment.CurrentManagedThreadId;
        scene.BeforePreviewWork = Wait;
    }

    public void RemovePreview(V4MapScene scene)
    {
        scene.BeforePreviewWork = null;
        Release();
    }

    private void Wait()
    {
        Volatile.Write(ref _workerThread, System.Environment.CurrentManagedThreadId);
        Volatile.Write(ref _started, 1);
        _release.Wait();
        Volatile.Write(ref _completed, 1);
    }

    public void Release() => _release.Set();

    public void Remove(V4MapScene scene)
    {
        scene.BeforeBuildWork = null;
        Release();
    }

    public void Cleanup()
    {
        if (_disposed) return;
        if (!Completed)
            throw new InvalidOperationException("Release and await the worker before disposing its gate.");
        _release.Dispose();
        _disposed = true;
    }
}
