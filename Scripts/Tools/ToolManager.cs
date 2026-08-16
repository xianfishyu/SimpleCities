using Godot;
using SimpleCities.Road.V3;

public partial class ToolManager : Node2D
{
    public static ToolManager Instance { get; private set; } = null!;

    private ToolType _currentTool = ToolType.Select;

    /// <summary>切换工具时取消尚未提交的铺路会话，并清理拆除悬停状态。</summary>
    public ToolType CurrentTool
    {
        get => _currentTool;
        set
        {
            if (_currentTool == value) return;
            // 切出 Road 工具前取消完整的连续铺路会话。
            if (_currentTool == ToolType.Road)
                _roadBuilder?.CancelPlaceSession();
            // 切出 RoadRemove 工具前清除悬停高亮
            if (_currentTool == ToolType.RoadRemove)
                _roadBuilder?.SetRemoveHoverActive(false);
            _currentTool = value;
            SyncV3Tool(value);
            // 切入 RoadRemove 工具时开启悬停高亮
            if (_currentTool == ToolType.RoadRemove)
                _roadBuilder?.SetRemoveHoverActive(true);
        }
    }

    private RoadBuilder? _roadBuilder;

    public override void _Ready()
    {
        Instance = this;
        _roadBuilder = GetNode<RoadBuilder>("../RoadSystem/RoadBuilder");
        SyncV3Tool(_currentTool);
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null!;
        _roadBuilder = null;
    }

    public bool UndoRoadEdit() => _roadBuilder?.UndoLastEdit() == true;

    public bool RedoRoadEdit() => _roadBuilder?.RedoLastEdit() == true;

    public bool CanUndoRoadEdit() => _roadBuilder?.CanUndoLastEdit() == true;

    public bool CanRedoRoadEdit() => _roadBuilder?.CanRedoLastEdit() == true;

    private void SyncV3Tool(ToolType tool)
    {
        if (!GodotObject.IsInstanceValid(RoadGraphV3System.Instance))
            return;

        RoadGraphV3System.Instance.ToolState.SwitchTo(tool.ToRoadToolType());
    }

    public override void _Input(InputEvent @event)
    {
        // Esc is owned by GameHUD's pause menu. ToolManager only forwards active tool input.
        if (_roadBuilder == null) return;

        switch (_currentTool)
        {
            case ToolType.Road:
                _roadBuilder.HandlePlaceInput(@event);
                break;
            case ToolType.RoadRemove:
                _roadBuilder.HandleRemoveInput(@event);
                break;
        }
    }
}
