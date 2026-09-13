using Godot;
using System;
using System.Threading;

/// <summary>Debug-only failures at display preflight/publish and a controllable retry worker boundary.</summary>
public partial class V4DisplayFailureProbe : RefCounted
{
    private readonly ManualResetEventSlim _release = new(false);
    private V4MapScene? _scene;
    private V4MapView? _view;
    private Action? _preflight;
    private Action? _publish;
    private Action? _draw;
    private Action? _retry;
    private int _faultCalls;
    private int _retryCalls;
    private int _completed;
    private int _workerThread;
    private int _mainThread;
    private bool _disposed;

    public int FaultCalls => Volatile.Read(ref _faultCalls);
    public int RetryCalls => Volatile.Read(ref _retryCalls);
    public bool Started => RetryCalls != 0;
    public bool Completed => Volatile.Read(ref _completed) != 0;
    public bool UsedBackgroundThread => Started && Volatile.Read(ref _workerThread) != _mainThread;

    public void Install(V4MapScene scene, string stage, bool persistent = false)
    {
        if (stage is not "preflight" and not "publish" and not "draw")
            throw new ArgumentOutOfRangeException(nameof(stage));
        _scene = scene;
        V4MapView view = scene.GetNode<V4MapView>("View");
        _view = view;
        string baselineToken = view.Presented?.Token.ToString() ?? "";
        Action fail = () =>
        {
            // Only fail a new candidate's draw; the current display and its fallback remain usable.
            if (stage == "draw" && view.Presented?.Token.ToString() == baselineToken) return;
            int call = Interlocked.Increment(ref _faultCalls);
            if (persistent || call == 1)
                throw new InvalidOperationException($"Injected V4 display {stage} failure.");
        };
        if (stage == "preflight") view.BeforeDisplayPreflight = _preflight = fail;
        else if (stage == "publish") view.BeforeDisplayPublish = _publish = fail;
        else view.BeforeDisplayDraw = _draw = fail;
    }

    public void InstallRetryGate(V4MapScene scene, bool failAfterRelease = false)
    {
        _scene = scene;
        _mainThread = System.Environment.CurrentManagedThreadId;
        scene.BeforeDisplayRetryWork = _retry = () =>
        {
            Volatile.Write(ref _workerThread, System.Environment.CurrentManagedThreadId);
            Interlocked.Increment(ref _retryCalls);
            _release.Wait();
            Volatile.Write(ref _completed, 1);
            if (failAfterRelease)
                throw new InvalidOperationException("Injected V4 display retry worker failure.");
        };
    }

    public void Release() => _release.Set();

    public void RemoveFault()
    {
        if (_view is not null && GodotObject.IsInstanceValid(_view))
        {
            if (ReferenceEquals(_view.BeforeDisplayPreflight, _preflight)) _view.BeforeDisplayPreflight = null;
            if (ReferenceEquals(_view.BeforeDisplayPublish, _publish)) _view.BeforeDisplayPublish = null;
            if (ReferenceEquals(_view.BeforeDisplayDraw, _draw)) _view.BeforeDisplayDraw = null;
        }
        _preflight = null;
        _publish = null;
        _draw = null;
    }

    public void Cleanup()
    {
        if (_disposed) return;
        Release();
        if (Started && !Completed)
            throw new InvalidOperationException("Await the released retry worker before disposing its gate.");
        RemoveFault();
        if (_scene is not null && GodotObject.IsInstanceValid(_scene) && ReferenceEquals(_scene.BeforeDisplayRetryWork, _retry))
            _scene.BeforeDisplayRetryWork = null;
        _retry = null;
        _release.Dispose();
        _disposed = true;
    }
}
