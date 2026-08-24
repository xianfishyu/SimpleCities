using System;

public partial class RoadLoadObserverFailureProbe : Godot.RefCounted
{
    private const string ObserverFailureMessage =
        "Injected RoadRenderer presentation observer failure.";
    private const string GraphObserverFailureMessage =
        "Injected RoadGraph observer failure.";

    private RoadRenderer? _renderer;
    private RoadGraph? _graphObserverOwner;
    private RoadRenderer? _rendererCleanupOwner;
    private RoadGraph? _graphCleanupOwner;
    private ToolManager? _toolManager;
    private SaveManager? _slotCleanupOwner;
    private SaveManager? _postCommitOwner;
    private Action<RoadRenderToken>? _handler;
    private Action<RoadGraphChangedEvent>? _graphHandler;
    private int _triggerCount;
    private int _graphObserverTriggerCount;

    public void FlushPendingManagedFinalizers()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public void Arm(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        DisarmObserver();
        _renderer = renderer;
        _handler = OnPresentationReady;
        renderer.PresentationReady += _handler;
    }

    public void ArmGraphObserverFailure(RoadSystem roadSystem)
    {
        ArgumentNullException.ThrowIfNull(roadSystem);
        RoadGraph graph = roadSystem.Graph;
        ArgumentNullException.ThrowIfNull(graph);
        DisarmGraphObserver();
        _graphObserverOwner = graph;
        _graphHandler = OnGraphChanged;
        graph.GraphChanged += _graphHandler;
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

    public void ArmPostCommitFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        _postCommitOwner?.DisarmAggregateLoadPostCommitFailure();
        _postCommitOwner = saveManager;
        saveManager.ArmNextAggregateLoadPostCommitFailure();
    }

    public void Disarm()
    {
        DisarmObserver();
        DisarmGraphObserver();
        _rendererCleanupOwner?.DisarmLoadCompleteCommitFailure();
        _rendererCleanupOwner = null;
        _graphCleanupOwner?.DisarmLoadCompleteCommitFailure();
        _graphCleanupOwner = null;
        _toolManager?.DisarmLoadCompleteCommitFailure();
        _toolManager = null;
        _slotCleanupOwner?.DisarmSlotTargetLoadCompleteCommitFailure();
        _slotCleanupOwner = null;
        _postCommitOwner?.DisarmAggregateLoadPostCommitFailure();
        _postCommitOwner = null;
    }

    private void DisarmObserver()
    {
        if (_renderer is not null && _handler is not null)
            _renderer.PresentationReady -= _handler;
        _renderer = null;
        _handler = null;
    }

    private void DisarmGraphObserver()
    {
        if (_graphObserverOwner is not null && _graphHandler is not null)
            _graphObserverOwner.GraphChanged -= _graphHandler;
        _graphObserverOwner = null;
        _graphHandler = null;
    }

    public int GetTriggerCount() => _triggerCount;
    public int GetGraphObserverTriggerCount() => _graphObserverTriggerCount;
    public bool IsGraphObserverFailureArmed() =>
        _graphObserverOwner is not null && _graphHandler is not null;
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
    public int GetPostCommitFailureCount() =>
        _postCommitOwner?.GetAggregateLoadPostCommitFailureCount() ?? 0;
    public bool IsPostCommitFailureArmed() =>
        _postCommitOwner?.IsAggregateLoadPostCommitFailureArmed() ?? false;

    private void OnPresentationReady(RoadRenderToken _)
    {
        _triggerCount++;
        DisarmObserver();
        throw new InvalidOperationException(ObserverFailureMessage);
    }

    private void OnGraphChanged(RoadGraphChangedEvent _)
    {
        _graphObserverTriggerCount++;
        DisarmGraphObserver();
        throw new InvalidOperationException(GraphObserverFailureMessage);
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
    internal const string AggregateLoadPostCommitFailureMessage =
        "Injected aggregate Load post-commit work failure.";
    internal const string SlotTargetLoadCompleteCommitFailureMessage =
        "Injected slot target load cleanup failure.";

    private bool _aggregateLoadPostCommitFailureArmed;
    private int _aggregateLoadPostCommitFailureCount;
    private bool _slotTargetLoadCompleteCommitFailureArmed;
    private int _slotTargetLoadCompleteCommitFailureCount;

    partial void ProbeAggregateLoadPostCommitFailure()
    {
        if (!_aggregateLoadPostCommitFailureArmed)
            return;

        _aggregateLoadPostCommitFailureArmed = false;
        _aggregateLoadPostCommitFailureCount++;
        throw new InvalidOperationException(AggregateLoadPostCommitFailureMessage);
    }

    internal void ArmNextAggregateLoadPostCommitFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate post-commit failure probe.");
        }
        if (_aggregateLoadPostCommitFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load post-commit failure probe is already armed.");
        }

        _aggregateLoadPostCommitFailureArmed = true;
    }

    internal void DisarmAggregateLoadPostCommitFailure() =>
        _aggregateLoadPostCommitFailureArmed = false;

    internal bool IsAggregateLoadPostCommitFailureArmed() =>
        _aggregateLoadPostCommitFailureArmed;

    internal int GetAggregateLoadPostCommitFailureCount() =>
        _aggregateLoadPostCommitFailureCount;

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
