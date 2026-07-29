using System;
using System.Collections.Generic;
using Godot;

public partial class ConstructionDock : Control
{
    private const float BottomMargin = 16f;
    private const float HorizontalMargin = 16f;
    private const float SmallViewportHorizontalMargin = 12f;
    private const float MinimumWidth = 520f;
    private const float MaximumWidth = 600f;
    private const float CollapsedHeight = 64f;
    private const float CategoryBarHeight = 48f;
    private const float TrayMaximumHeight = 240f;
    private const float ToolButtonMinimumWidth = 112f;
    private const float ToolButtonMinimumHeight = 40f;

    [Export] public ConstructionCategoryDefinition? Category { get; set; }
    [Export] public bool TrayVisibleOnReady { get; set; }

    public event Action<bool>? TrayVisibilityChanged;

    public bool IsTrayVisible => _toolTray?.Visible == true;

    private PanelContainer _dockPanel = null!;
    private PanelContainer _toolTray = null!;
    private ScrollContainer _toolScroll = null!;
    private HBoxContainer _categoryBar = null!;
    private Button _roadsCategoryButton = null!;
    private Label _currentToolLabel = null!;
    private VBoxContainer _toolList = null!;
    private readonly Dictionary<ToolType, Button> _toolButtons = new();
    private readonly Dictionary<ToolType, ConstructionToolDefinition> _toolDefinitions = new();
    private readonly List<Action> _disconnectActions = [];

    private ToolManager? _toolManager;
    private ToolType _lastSyncedTool;
    private bool _hasSyncedTool;
    private bool _loggedMissingToolManager;
    private bool _categoryValid;
    private NodePath _contextFocusPath = new();
    private NodePath _saveFocusPath = new();
    private NodePath _loadFocusPath = new();
    private NodePath _debugFocusPath = new();

    public override void _Ready()
    {
        ResolveNodes();
        _toolManager = GodotObject.IsInstanceValid(ToolManager.Instance) ? ToolManager.Instance : null;
        if (_toolManager == null) LogMissingToolManager();

        _categoryValid = ValidateCategory();
        BuildRoadsCategory();
        if (_categoryValid)
            BuildToolButtons();
        else
            DisableInvalidCategory();

        SetTrayVisible(TrayVisibleOnReady && _categoryValid);
        SyncFromToolManager();
        ApplyDockLayout();
    }

    public override void _Process(double delta) => SyncFromToolManager();

    public override void _Notification(int what)
    {
        if (what == NotificationResized) ApplyDockLayout();
    }

    public override void _ExitTree()
    {
        foreach (Action disconnect in _disconnectActions)
            disconnect();
        _disconnectActions.Clear();
        _toolButtons.Clear();
        _toolDefinitions.Clear();
        _toolManager = null;
    }

    public ConstructionToolDefinition? GetToolDefinition(ToolType toolType)
    {
        return _toolDefinitions.TryGetValue(toolType, out ConstructionToolDefinition? definition)
            ? definition
            : null;
    }

    public void ConfigureFocusChain(NodePath contextFocusPath, NodePath saveFocusPath, NodePath loadFocusPath, NodePath debugFocusPath)
    {
        _contextFocusPath = contextFocusPath;
        _saveFocusPath = saveFocusPath;
        _loadFocusPath = loadFocusPath;
        _debugFocusPath = debugFocusPath;
        UpdateFocusChain();
    }

    private void ResolveNodes()
    {
        _dockPanel = GetNode<PanelContainer>("DockPanel");
        _toolTray = GetNode<PanelContainer>("DockPanel/DockStack/ToolTray");
        _toolScroll = GetNode<ScrollContainer>("DockPanel/DockStack/ToolTray/TrayMargin/ToolScroll");
        _categoryBar = GetNode<HBoxContainer>("DockPanel/DockStack/CategoryBar");
        _roadsCategoryButton = GetNode<Button>("DockPanel/DockStack/CategoryBar/RoadsCategoryButton");
        _currentToolLabel = GetNode<Label>("DockPanel/DockStack/CategoryBar/CurrentToolLabel");
        _toolList = GetNode<VBoxContainer>("DockPanel/DockStack/ToolTray/TrayMargin/ToolScroll/ToolList");
    }

    private bool ValidateCategory()
    {
        if (Category == null)
        {
            GD.PushWarning("ConstructionDock: Category resource is not assigned; construction tools are disabled.");
            return false;
        }

        if (Category.TryValidate(out string error)) return true;

        GD.PushWarning($"ConstructionDock: Category resource is invalid: {error}");
        return false;
    }

    private void BuildRoadsCategory()
    {
        _roadsCategoryButton.Text = Category == null ? "道路 unavailable" : $"{Category.DisplayName}  {CurrentToolDisplayName()}";
        _roadsCategoryButton.ToggleMode = true;
        _roadsCategoryButton.FocusMode = FocusModeEnum.All;
        _roadsCategoryButton.CustomMinimumSize = new Vector2(96f, 40f);
        _roadsCategoryButton.Pressed += OnRoadsCategoryPressed;
        _disconnectActions.Add(() => _roadsCategoryButton.Pressed -= OnRoadsCategoryPressed);
    }

    private void BuildToolButtons()
    {
        ClearToolList();
        if (Category?.Tools == null) return;

        var tools = new List<ConstructionToolDefinition>();
        foreach (ConstructionToolDefinition? tool in Category.Tools)
        {
            if (tool == null)
            {
                GD.PushWarning("ConstructionDock: Category contains an empty tool reference.");
                continue;
            }
            tools.Add(tool);
        }
        tools.Sort(static (left, right) => left.SortOrder.CompareTo(right.SortOrder));

        foreach (ConstructionToolDefinition tool in tools)
        {
            var button = new Button
            {
                Name = ToolNodeName(tool.ToolType),
                Text = ToolButtonText(tool),
                ToggleMode = true,
                FocusMode = FocusModeEnum.All,
                CustomMinimumSize = new Vector2(ToolButtonMinimumWidth, ToolButtonMinimumHeight),
                TooltipText = tool.Description,
            };

            ToolType toolType = tool.ToolType;
            Action handler = () => OnToolPressed(toolType);
            button.Pressed += handler;
            _disconnectActions.Add(() => button.Pressed -= handler);
            _toolList.AddChild(button);
            _toolButtons[toolType] = button;
            _toolDefinitions[toolType] = tool;
        }
    }

    private void DisableInvalidCategory()
    {
        ClearToolList();
        _toolTray.Visible = false;
        _roadsCategoryButton.Disabled = true;
        _roadsCategoryButton.Text = "道路 unavailable";
        _currentToolLabel.Text = "工具目录不可用";
    }

    private void ClearToolList()
    {
        foreach (Node child in _toolList.GetChildren())
            child.QueueFree();
        _toolButtons.Clear();
        _toolDefinitions.Clear();
    }

    private void OnRoadsCategoryPressed()
    {
        if (!_categoryValid)
        {
            SetTrayVisible(false);
            return;
        }

        SetTrayVisible(!_toolTray.Visible);
    }

    private void OnToolPressed(ToolType toolType)
    {
        if (_toolManager == null)
        {
            LogMissingToolManager();
            SyncSelectedTool(toolType: null);
            return;
        }

        _toolManager.CurrentTool = toolType;
        SyncFromToolManager(force: true);
    }

    private void SetTrayVisible(bool visible)
    {
        _toolTray.Visible = visible;
        _roadsCategoryButton.ButtonPressed = visible;
        ApplyDockLayout();
        UpdateFocusChain();
        TrayVisibilityChanged?.Invoke(visible);
    }

    private void SyncFromToolManager(bool force = false)
    {
        if (!_categoryValid) return;

        if (_toolManager == null)
        {
            _toolManager = GodotObject.IsInstanceValid(ToolManager.Instance) ? ToolManager.Instance : null;
            if (_toolManager == null)
            {
                LogMissingToolManager();
                return;
            }
        }

        ToolType currentTool = _toolManager.CurrentTool;
        if (!force && _hasSyncedTool && currentTool == _lastSyncedTool && _currentToolLabel.Text.Length > 0) return;

        _lastSyncedTool = currentTool;
        _hasSyncedTool = true;
        _currentToolLabel.Text = $"当前: {CurrentToolDisplayName()}";
        _roadsCategoryButton.Text = Category == null
            ? $"道路  {currentTool}"
            : $"{Category.DisplayName}  {CurrentToolDisplayName()}";
        SyncSelectedTool(currentTool);
    }

    private void SyncSelectedTool(ToolType? toolType)
    {
        foreach ((ToolType candidate, Button button) in _toolButtons)
            button.ButtonPressed = toolType == candidate;
    }

    private void ApplyDockLayout()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f) return;

        float sideMargin = viewportSize.X < 760f ? SmallViewportHorizontalMargin : HorizontalMargin;
        float width = viewportSize.X < 760f
            ? Mathf.Max(0f, viewportSize.X - (sideMargin * 2f))
            : Mathf.Clamp(viewportSize.X - (HorizontalMargin * 2f), MinimumWidth, MaximumWidth);
        float trayHeight = _toolTray.Visible
            ? Mathf.Max(0f, Mathf.Min(TrayMaximumHeight, Mathf.Floor(viewportSize.Y / 3f) - 40f))
            : 0f;
        float height = CollapsedHeight + trayHeight;

        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetLeft = -width / 2f;
        OffsetRight = width / 2f;
        OffsetBottom = -BottomMargin;
        OffsetTop = OffsetBottom - height;

        _dockPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _dockPanel.OffsetLeft = 0f;
        _dockPanel.OffsetTop = 0f;
        _dockPanel.OffsetRight = 0f;
        _dockPanel.OffsetBottom = 0f;
        _toolTray.CustomMinimumSize = new Vector2(0f, trayHeight);
        _toolScroll.CustomMinimumSize = new Vector2(0f, Mathf.Max(0f, trayHeight - 24f));
        _categoryBar.CustomMinimumSize = new Vector2(0f, CategoryBarHeight);
    }

    private void UpdateFocusChain()
    {
        if (!_categoryValid) return;

        NodePath firstAfterDock = _contextFocusPath;
        if (_toolTray.Visible && _toolButtons.TryGetValue(ToolType.Select, out Button? selectButton))
            firstAfterDock = selectButton.GetPath();

        _roadsCategoryButton.FocusNext = firstAfterDock;
        _roadsCategoryButton.FocusPrevious = _debugFocusPath;

        if (_toolButtons.TryGetValue(ToolType.Select, out Button? select))
        {
            select.FocusNext = _toolButtons.TryGetValue(ToolType.Road, out Button? road) ? road.GetPath() : _contextFocusPath;
            select.FocusPrevious = _roadsCategoryButton.GetPath();
        }
        if (_toolButtons.TryGetValue(ToolType.Road, out Button? roadButton))
        {
            roadButton.FocusNext = _toolButtons.TryGetValue(ToolType.RoadRemove, out Button? remove) ? remove.GetPath() : _contextFocusPath;
            roadButton.FocusPrevious = _toolButtons.TryGetValue(ToolType.Select, out Button? selectPrevious) ? selectPrevious.GetPath() : _roadsCategoryButton.GetPath();
        }
        if (_toolButtons.TryGetValue(ToolType.RoadRemove, out Button? removeButton))
        {
            removeButton.FocusNext = _contextFocusPath;
            removeButton.FocusPrevious = _toolButtons.TryGetValue(ToolType.Road, out Button? roadPrevious) ? roadPrevious.GetPath() : _roadsCategoryButton.GetPath();
        }
    }

    private void LogMissingToolManager()
    {
        if (_loggedMissingToolManager) return;
        _loggedMissingToolManager = true;
        GD.PushWarning("ConstructionDock: ToolManager.Instance is missing; tool commands are disabled until ToolManager exists.");
    }

    private string CurrentToolDisplayName()
    {
        ToolType currentTool = _toolManager?.CurrentTool ?? ToolType.Select;
        ConstructionToolDefinition? definition = GetToolDefinition(currentTool);
        return definition?.DisplayName ?? currentTool.ToString();
    }

    private static string ToolButtonText(ConstructionToolDefinition tool)
    {
        return string.IsNullOrWhiteSpace(tool.ShortcutHint)
            ? tool.DisplayName
            : $"{tool.DisplayName} {tool.ShortcutHint}";
    }

    private static string ToolNodeName(ToolType toolType) => toolType switch
    {
        ToolType.Select => "SelectToolButton",
        ToolType.Road => "RoadToolButton",
        ToolType.RoadRemove => "RoadRemoveToolButton",
        _ => $"{toolType}ToolButton",
    };
}
