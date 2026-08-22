using System;

public partial class RoadLoadObserverFailureProbe : Godot.RefCounted
{
    private const string ObserverFailureMessage =
        "Injected RoadRenderer presentation observer failure.";

    private RoadRenderer? _renderer;
    private RoadRenderer? _rendererCleanupOwner;
    private ToolManager? _toolManager;
    private Action<RoadRenderToken>? _handler;
    private int _triggerCount;

    public void Arm(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        DisarmObserver();
        _renderer = renderer;
        _handler = OnPresentationReady;
        renderer.PresentationReady += _handler;
    }

    public void ArmToolCleanupFailure(ToolManager toolManager)
    {
        ArgumentNullException.ThrowIfNull(toolManager);
        _toolManager?.DisarmLoadCompleteCommitFailure();
        _toolManager = toolManager;
        toolManager.ArmNextLoadCompleteCommitFailure();
    }

    public void ArmRendererCleanupFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _rendererCleanupOwner?.DisarmLoadCompleteCommitFailure();
        _rendererCleanupOwner = renderer;
        renderer.ArmNextLoadCompleteCommitFailure();
    }

    public void Disarm()
    {
        DisarmObserver();
        _rendererCleanupOwner?.DisarmLoadCompleteCommitFailure();
        _rendererCleanupOwner = null;
        _toolManager?.DisarmLoadCompleteCommitFailure();
        _toolManager = null;
    }

    private void DisarmObserver()
    {
        if (_renderer is not null && _handler is not null)
            _renderer.PresentationReady -= _handler;
        _renderer = null;
        _handler = null;
    }

    public int GetTriggerCount() => _triggerCount;
    public int GetToolCleanupFailureCount() =>
        _toolManager?.GetLoadCompleteCommitFailureCount() ?? 0;
    public bool IsToolCleanupFailureArmed() =>
        _toolManager?.IsLoadCompleteCommitFailureArmed() ?? false;
    public int GetRendererCleanupFailureCount() =>
        _rendererCleanupOwner?.GetLoadCompleteCommitFailureCount() ?? 0;
    public bool IsRendererCleanupFailureArmed() =>
        _rendererCleanupOwner?.IsLoadCompleteCommitFailureArmed() ?? false;

    private void OnPresentationReady(RoadRenderToken _)
    {
        _triggerCount++;
        DisarmObserver();
        throw new InvalidOperationException(ObserverFailureMessage);
    }
}

public partial class RoadRenderer
{
    internal const string LoadCompleteCommitFailureMessage =
        "Injected RoadRenderer load cleanup failure.";

    private bool _loadCompleteCommitFailureArmed;
    private int _loadCompleteCommitFailureCount;

    partial void ProbeLoadCompleteCommitFailure()
    {
        if (!_loadCompleteCommitFailureArmed)
            return;

        _loadCompleteCommitFailureArmed = false;
        _loadCompleteCommitFailureCount++;
        throw new InvalidOperationException(LoadCompleteCommitFailureMessage);
    }

    internal void ArmNextLoadCompleteCommitFailure()
    {
        if (_loadAdmission is not null)
        {
            throw new InvalidOperationException(
                "RoadRenderer must be idle before arming its load cleanup failure probe.");
        }
        if (_loadCompleteCommitFailureArmed)
        {
            throw new InvalidOperationException(
                "RoadRenderer load cleanup failure probe is already armed.");
        }

        _loadCompleteCommitFailureArmed = true;
    }

    internal void DisarmLoadCompleteCommitFailure() =>
        _loadCompleteCommitFailureArmed = false;

    internal bool IsLoadCompleteCommitFailureArmed() =>
        _loadCompleteCommitFailureArmed;

    internal int GetLoadCompleteCommitFailureCount() =>
        _loadCompleteCommitFailureCount;
}

public partial class ToolManager
{
    internal const string LoadCompleteCommitFailureMessage =
        "Injected ToolManager load cleanup failure.";

    private bool _loadCompleteCommitFailureArmed;
    private int _loadCompleteCommitFailureCount;

    partial void ProbeLoadCompleteCommitFailure()
    {
        if (!_loadCompleteCommitFailureArmed)
            return;

        _loadCompleteCommitFailureArmed = false;
        _loadCompleteCommitFailureCount++;
        throw new InvalidOperationException(LoadCompleteCommitFailureMessage);
    }

    internal void ArmNextLoadCompleteCommitFailure()
    {
        if (_loadAdmission is not null)
        {
            throw new InvalidOperationException(
                "ToolManager must be idle before arming its load cleanup failure probe.");
        }
        if (_loadCompleteCommitFailureArmed)
        {
            throw new InvalidOperationException(
                "ToolManager load cleanup failure probe is already armed.");
        }

        _loadCompleteCommitFailureArmed = true;
    }

    internal void DisarmLoadCompleteCommitFailure() =>
        _loadCompleteCommitFailureArmed = false;

    internal bool IsLoadCompleteCommitFailureArmed() =>
        _loadCompleteCommitFailureArmed;

    internal int GetLoadCompleteCommitFailureCount() =>
        _loadCompleteCommitFailureCount;
}
