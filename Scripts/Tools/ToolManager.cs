using Godot;

public partial class ToolManager : Node2D
{
    public static ToolManager Instance { get; private set; } = null!;

    private ToolType _currentTool = ToolType.Select;

    /// <summary>切换工具时，若当前是 Road 工具且正在拖拽，则取消拖拽，避免 _isDragging 卡死。</summary>
    public ToolType CurrentTool
    {
        get => _currentTool;
        set
        {
            if (_currentTool == value) return;
            // Bug #3: 切出 Road 工具前必须取消进行中的拖拽
            if (_currentTool == ToolType.Road)
                _roadBuilder?.CancelPlaceDrag();
            // 切出 RoadRemove 工具前清除悬停高亮
            if (_currentTool == ToolType.RoadRemove)
                _roadBuilder?.SetRemoveHoverActive(false);
            _currentTool = value;
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
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null!;
        _roadBuilder = null;
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
