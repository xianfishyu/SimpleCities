using Godot;

public partial class ToolManager : Node2D
{
    public static ToolManager Instance { get; private set; } = null!;

    private ToolType _currentTool = ToolType.Select;
    private ToolLoadAdmission? _loadAdmission;
    private long _loadAdmissionGeneration;

    /// <summary>切换工具时取消尚未提交的铺路会话，并清理拆除悬停状态。</summary>
    public ToolType CurrentTool
    {
        get => _currentTool;
        set
        {
            if (_loadAdmission is not null) return;
            if (_currentTool == value) return;
            // 切出 Road 工具前取消完整的连续铺路会话。
            if (_currentTool == ToolType.Road)
                _roadBuilder?.CancelPlaceSession();
            // 切出 RoadRemove 工具前清除悬停高亮
            if (_currentTool == ToolType.RoadRemove)
                _roadBuilder?.SetRemoveHoverActive(false);
            if (_currentTool == ToolType.RoadUpgrade)
                _roadBuilder?.SetUpgradeHoverActive(false);
            _currentTool = value;
            // 切入 RoadRemove 工具时开启悬停高亮
            if (_currentTool == ToolType.RoadRemove)
                _roadBuilder?.SetRemoveHoverActive(true);
            if (_currentTool == ToolType.RoadUpgrade)
                _roadBuilder?.SetUpgradeHoverActive(true);
        }
    }

    private RoadBuilder? _roadBuilder;
    private SaveManager? _registeredSaveManager;

    public override void _Ready()
    {
        Instance = this;
        _roadBuilder = GetNode<RoadBuilder>("../RoadSystem/RoadBuilder");
        RoadSystem? roadSystem = GodotObject.IsInstanceValid(RoadSystem.Instance)
            ? RoadSystem.Instance
            : null;
        RoadRenderer? renderer = GetNodeOrNull<RoadRenderer>("../RoadSystem/RoadRenderer");
        SaveManager? saveManager = GodotObject.IsInstanceValid(SaveManager.Instance)
            ? SaveManager.Instance
            : null;
        if (roadSystem is not null && renderer is not null && saveManager is not null &&
            saveManager.RegisterSceneParticipants(roadSystem.Graph, this, renderer))
        {
            _registeredSaveManager = saveManager;
        }
        else
        {
            GD.PushError("ToolManager: failed to register V3 load participants.");
        }
    }

    public override void _ExitTree()
    {
        if (_registeredSaveManager is not null &&
            GodotObject.IsInstanceValid(_registeredSaveManager))
        {
            _registeredSaveManager.UnregisterSceneParticipants(this);
        }
        _registeredSaveManager = null;
        if (ReferenceEquals(Instance, this))
            Instance = null!;
        _roadBuilder = null;
    }

    public bool UndoRoadEdit() => _loadAdmission is null && _roadBuilder?.UndoLastEdit() == true;

    public bool RedoRoadEdit() => _loadAdmission is null && _roadBuilder?.RedoLastEdit() == true;

    public bool CanUndoRoadEdit() => _roadBuilder?.CanUndoLastEdit() == true;

    public bool CanRedoRoadEdit() => _roadBuilder?.CanRedoLastEdit() == true;

    /// <summary>返回道路上下文面板使用的共享 RoadType 状态；缺少场景依赖时保持 Street 后备值。</summary>
    public RoadType GetSelectedRoadType() => _roadBuilder?.SelectedRoadType ?? RoadType.Street;

    /// <summary>只委托 RoadBuilder 的类型状态门禁，不直接触碰 RoadGraph。</summary>
    public bool SetSelectedRoadType(RoadType roadType) =>
        _loadAdmission is null && _roadBuilder?.SetSelectedRoadType(roadType) == true;

    public void CancelRoadSessions()
    {
        if (_loadAdmission is not null)
            return;
        _roadBuilder?.CancelPlaceSession();
        _roadBuilder?.CancelRemoveSession();
        _roadBuilder?.CancelUpgradeSession();
    }

    public override void _Input(InputEvent @event)
    {
        // Esc is owned by GameHUD's pause menu. ToolManager only forwards active tool input.
        if (_roadBuilder == null || _loadAdmission is not null) return;

        switch (_currentTool)
        {
            case ToolType.Road:
                _roadBuilder.HandlePlaceInput(@event);
                break;
            case ToolType.RoadRemove:
                _roadBuilder.HandleRemoveInput(@event);
                break;
            case ToolType.RoadUpgrade:
                _roadBuilder.HandleUpgradeInput(@event);
                break;
        }
    }
}
