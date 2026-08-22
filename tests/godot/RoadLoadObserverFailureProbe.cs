using System;

public partial class RoadLoadObserverFailureProbe : Godot.RefCounted
{
    private const string ObserverFailureMessage =
        "Injected RoadRenderer presentation observer failure.";

    private RoadRenderer? _renderer;
    private RoadRenderer? _rendererCleanupOwner;
    private RoadGraph? _graphCleanupOwner;
    private ToolManager? _toolManager;
    private SaveManager? _slotCleanupOwner;
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

    public void ArmGraphCleanupFailure(RoadSystem roadSystem)
    {
        ArgumentNullException.ThrowIfNull(roadSystem);
        RoadGraph graph = roadSystem.Graph;
        ArgumentNullException.ThrowIfNull(graph);
        _graphCleanupOwner?.DisarmLoadCompleteCommitFailure();
        _graphCleanupOwner = graph;
        graph.ArmNextLoadCompleteCommitFailure();
    }

    public void ArmSlotCleanupFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        _slotCleanupOwner?.DisarmSlotTargetLoadCompleteCommitFailure();
        _slotCleanupOwner = saveManager;
        saveManager.ArmNextSlotTargetLoadCompleteCommitFailure();
    }

    public void Disarm()
    {
        DisarmObserver();
        _rendererCleanupOwner?.DisarmLoadCompleteCommitFailure();
        _rendererCleanupOwner = null;
        _graphCleanupOwner?.DisarmLoadCompleteCommitFailure();
        _graphCleanupOwner = null;
        _toolManager?.DisarmLoadCompleteCommitFailure();
        _toolManager = null;
        _slotCleanupOwner?.DisarmSlotTargetLoadCompleteCommitFailure();
        _slotCleanupOwner = null;
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
    public int GetGraphCleanupFailureCount() =>
        _graphCleanupOwner?.GetLoadCompleteCommitFailureCount() ?? 0;
    public bool IsGraphCleanupFailureArmed() =>
        _graphCleanupOwner?.IsLoadCompleteCommitFailureArmed() ?? false;
    public int GetSlotCleanupFailureCount() =>
        _slotCleanupOwner?.GetSlotTargetLoadCompleteCommitFailureCount() ?? 0;
    public bool IsSlotCleanupFailureArmed() =>
        _slotCleanupOwner?.IsSlotTargetLoadCompleteCommitFailureArmed() ?? false;

    private void OnPresentationReady(RoadRenderToken _)
    {
        _triggerCount++;
        DisarmObserver();
        throw new InvalidOperationException(ObserverFailureMessage);
    }
}

public partial class RoadGraph
{
    internal const string LoadCompleteCommitFailureMessage =
        "Injected RoadGraph load cleanup failure.";

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
                "RoadGraph must be idle before arming its load cleanup failure probe.");
        }
        if (_loadCompleteCommitFailureArmed)
        {
            throw new InvalidOperationException(
                "RoadGraph load cleanup failure probe is already armed.");
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

public partial class SaveManager
{
    internal const string SlotTargetLoadCompleteCommitFailureMessage =
        "Injected slot target load cleanup failure.";

    private bool _slotTargetLoadCompleteCommitFailureArmed;
    private int _slotTargetLoadCompleteCommitFailureCount;

    partial void ProbeSlotTargetLoadCompleteCommitFailure()
    {
        if (!_slotTargetLoadCompleteCommitFailureArmed)
            return;

        _slotTargetLoadCompleteCommitFailureArmed = false;
        _slotTargetLoadCompleteCommitFailureCount++;
        throw new InvalidOperationException(SlotTargetLoadCompleteCommitFailureMessage);
    }

    internal void ArmNextSlotTargetLoadCompleteCommitFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its slot cleanup failure probe.");
        }
        if (_slotTargetLoadCompleteCommitFailureArmed)
        {
            throw new InvalidOperationException(
                "Slot target load cleanup failure probe is already armed.");
        }

        _slotTargetLoadCompleteCommitFailureArmed = true;
    }

    internal void DisarmSlotTargetLoadCompleteCommitFailure() =>
        _slotTargetLoadCompleteCommitFailureArmed = false;

    internal bool IsSlotTargetLoadCompleteCommitFailureArmed() =>
        _slotTargetLoadCompleteCommitFailureArmed;

    internal int GetSlotTargetLoadCompleteCommitFailureCount() =>
        _slotTargetLoadCompleteCommitFailureCount;
}
