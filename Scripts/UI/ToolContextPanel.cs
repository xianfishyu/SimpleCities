using Godot;
using SimpleCities.Road.V3;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 右侧工具上下文面板。根据当前工具和分类资源显示说明，并在窄屏时折叠为可展开入口。
/// </summary>
public partial class ToolContextPanel : PanelContainer
{
    private const float WideWidth = 320f;
    private const float CompactWidth = 44f;
    private const float RightMargin = 16f;
    private const float WideTop = 148f;
    private const float CompactTop = 148f;
    private const int CompactExpandedMargin = 8;
    private const int ExpandedMargin = 20;

    [Export] public RoadConfig? Config { get; set; }
    [Export] public ConstructionCategoryDefinition? Category { get; set; }
    [Export] public float ReservedBottomTop { get; set; } = -1f;

    private readonly StyleBoxEmpty _compactPanelStyle = new();

    private MarginContainer _panelMargin = null!;
    private Button _focusEntryButton = null!;
    private ScrollContainer _contentScroll = null!;
    private VBoxContainer _content = null!;
    private Label _categoryValue = null!;
    private Label _toolValue = null!;
    private Label _operationValue = null!;
    private Label _shortcutValue = null!;
    private Label _cellSizeValue = null!;
    private VBoxContainer _shortcutRow = null!;
    private VBoxContainer _cellSizeRow = null!;
    private VBoxContainer _roadTypeRow = null!;
    private HBoxContainer _roadTypeButtons = null!;
    private readonly Dictionary<RoadType, Button> _roadTypeButtonsByType = new();
    private Action<RoadType>? _roadTypeChanged;

    private bool _compact;
    private bool _compactExpanded;
    private bool _usesCompactViewport;

    public override void _Ready()
    {
        _panelMargin = GetNode<MarginContainer>("PanelMargin");
        _focusEntryButton = GetNode<Button>("PanelMargin/Rows/ContextFocusEntryButton");
        _contentScroll = GetNode<ScrollContainer>("PanelMargin/Rows/ContextContentScroll");
        _content = GetNode<VBoxContainer>("PanelMargin/Rows/ContextContentScroll/ContextContent");
        _categoryValue = GetNode<Label>("PanelMargin/Rows/ContextContentScroll/ContextContent/CategoryRow/CategoryValue");
        _toolValue = GetNode<Label>("PanelMargin/Rows/ContextContentScroll/ContextContent/CurrentToolRow/CurrentToolValue");
        _operationValue = GetNode<Label>("PanelMargin/Rows/ContextContentScroll/ContextContent/OperationRow/OperationValue");
        _shortcutRow = GetNode<VBoxContainer>("PanelMargin/Rows/ContextContentScroll/ContextContent/ShortcutRow");
        _shortcutValue = GetNode<Label>("PanelMargin/Rows/ContextContentScroll/ContextContent/ShortcutRow/ShortcutValue");
        _cellSizeRow = GetNode<VBoxContainer>("PanelMargin/Rows/ContextContentScroll/ContextContent/CellSizeRow");
        _cellSizeValue = GetNode<Label>("PanelMargin/Rows/ContextContentScroll/ContextContent/CellSizeRow/CellSizeValue");
        _roadTypeRow = GetNode<VBoxContainer>("PanelMargin/Rows/ContextContentScroll/ContextContent/RoadTypeRow");
        _roadTypeButtons = GetNode<HBoxContainer>("PanelMargin/Rows/ContextContentScroll/ContextContent/RoadTypeRow/RoadTypeButtons");
        _roadTypeRow.Visible = false;
        _focusEntryButton.FocusMode = FocusModeEnum.All;
        _focusEntryButton.Pressed += ToggleCompactExpanded;
        UpdateContext(ToolType.Select, Config);
        ApplyResponsiveLayout();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) ApplyResponsiveLayout();
    }

    public override void _ExitTree()
    {
        if (_focusEntryButton != null)
            _focusEntryButton.Pressed -= ToggleCompactExpanded;
    }

    public NodePath FocusEntryPath => _focusEntryButton.GetPath();

    public void ConfigureFocus(NodePath previousPath, NodePath nextPath)
    {
        _focusEntryButton.FocusPrevious = previousPath;
        _focusEntryButton.FocusNext = nextPath;
    }

    public void SetCategory(ConstructionCategoryDefinition? category)
    {
        Category = category;
    }

    public void SetRoadTypeSelectorVisible(bool visible)
    {
        if (_roadTypeRow != null)
            _roadTypeRow.Visible = visible;
    }

    public void ConfigureRoadTypeSelector(
        RoadTypeStyleCatalogResult catalog,
        RoadType selectedRoadType,
        Action<RoadType>? onChange)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!catalog.Success || catalog.Styles is null)
        {
            _roadTypeChanged = onChange;
            _roadTypeButtonsByType.Clear();
            SetRoadTypeSelectorVisible(false);
            return;
        }

        _roadTypeChanged = onChange;
        _roadTypeButtonsByType.Clear();
        foreach (Button child in _roadTypeButtons.GetChildren().OfType<Button>().ToList())
        {
            _roadTypeButtons.RemoveChild(child);
            child.QueueFree();
        }

        var createdButtons = new List<Button>();
        foreach (RoadTypeStyle style in catalog.Styles.Values.OrderBy(style => style.RoadType))
        {
            RoadType roadType = style.RoadType;
            var button = new Button
            {
                Text = style.DisplayName,
                ToggleMode = true,
                FocusMode = FocusModeEnum.All,
                CustomMinimumSize = new Vector2(0f, 32f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            button.AddThemeColorOverride("font_color", style.Color);
            button.Pressed += () =>
            {
                _roadTypeButtonsByType[roadType].ButtonPressed = true;
                foreach (KeyValuePair<RoadType, Button> pair in _roadTypeButtonsByType)
                    pair.Value.ButtonPressed = pair.Key == roadType;
                _roadTypeChanged?.Invoke(roadType);
            };
            _roadTypeButtons.AddChild(button);
            _roadTypeButtonsByType[roadType] = button;
            createdButtons.Add(button);
        }

        for (int index = 0; index < createdButtons.Count; index++)
        {
            if (index > 0)
                createdButtons[index].FocusNeighborLeft = createdButtons[index - 1].GetPath();
            if (index + 1 < createdButtons.Count)
                createdButtons[index].FocusNeighborRight = createdButtons[index + 1].GetPath();
        }

        _roadTypeRow.Visible = true;
        SelectRoadType(selectedRoadType);
    }

    public void SelectRoadType(RoadType roadType)
    {
        if (!_roadTypeButtonsByType.TryGetValue(roadType, out Button? button))
            return;

        foreach (KeyValuePair<RoadType, Button> pair in _roadTypeButtonsByType)
            pair.Value.ButtonPressed = pair.Key == roadType;
    }

    /// <summary>
    /// 同步工具说明。优先使用资源化工具定义；选择和拆除等内置工具则使用底栏提供的后备文案。
    /// </summary>
    public void UpdateContext(ToolType currentTool, RoadConfig? config)
    {
        Config = config;
        _shortcutRow.Visible = true;
        _cellSizeRow.Visible = true;
        ConstructionToolDefinition? definition = FindTool(currentTool);
        _categoryValue.Text = Category?.DisplayName ?? "道路 unavailable";
        if (definition != null)
        {
            _toolValue.Text = definition.DisplayName;
            _operationValue.Text = definition.Description;
            string shortcutHint = ResolveShortcutHint(currentTool, definition.ShortcutHint);
            _shortcutValue.Text = shortcutHint;
            _shortcutRow.Visible = !string.IsNullOrWhiteSpace(shortcutHint);
        }
        else if (ConstructionDock.TryGetBuiltInToolPresentation(currentTool, out string displayName, out string description, out string shortcutHint))
        {
            _toolValue.Text = displayName;
            _operationValue.Text = description;
            shortcutHint = ResolveShortcutHint(currentTool, shortcutHint);
            _shortcutValue.Text = shortcutHint;
            _shortcutRow.Visible = !string.IsNullOrWhiteSpace(shortcutHint);
        }
        else
        {
            _toolValue.Text = currentTool.ToString();
            _operationValue.Text = "工具定义不可用。";
            _shortcutValue.Text = "--";
        }
        _cellSizeValue.Text = Config == null ? "CellSize: unavailable" : $"CellSize: {Config.CellSize:F0}";
    }

    /// <summary>非道路分类目前尚未实现时，显示分类名称和明确的不可用状态。</summary>
    public void ShowUnavailableCategory(string categoryDisplayName)
    {
        _categoryValue.Text = categoryDisplayName;
        _toolValue.Text = "尚未开放";
        _operationValue.Text = "尚未开放";
        _shortcutRow.Visible = false;
        _cellSizeRow.Visible = false;
    }

    public void ApplyResponsiveLayout()
    {
        if (_contentScroll == null || _content == null || _focusEntryButton == null) return;

        ApplyResponsiveLayoutForViewport(GetViewportRect().Size);
    }

    /// <summary>
    /// 按视口和底边栏顶部位置重新计算面板边界，避免覆盖底栏；760px 以下使用紧凑模式。
    /// </summary>
    public void ApplyResponsiveLayoutForViewport(Vector2 viewportSize)
    {
        if (_contentScroll == null || _content == null || _focusEntryButton == null) return;
        if (viewportSize.X <= 0f) return;
        float reservedBottomTop = ReservedBottomTop > 0f ? ReservedBottomTop : viewportSize.Y - 16f;

        bool shouldCompact = viewportSize.X < 760f;
        _usesCompactViewport = shouldCompact;
        if (!shouldCompact)
            _compactExpanded = false;

        _compact = shouldCompact && !_compactExpanded;
        float width = _compact ? CompactWidth : WideWidth;
        OffsetLeft = -width - RightMargin;
        OffsetRight = -RightMargin;
        float top = shouldCompact ? CompactTop : WideTop;
        float bottomLimit = Mathf.Max(top + 44f, Mathf.Min(viewportSize.Y - 16f, reservedBottomTop - 16f));
        OffsetTop = top;
        OffsetBottom = _compact ? top + 44f : bottomLimit;
        ApplyMinimumContributionState();
    }

    private void ApplyMinimumContributionState()
    {
        bool contentVisible = !_compact;
        _contentScroll.Visible = contentVisible;
        _contentScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        _contentScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _focusEntryButton.Text = _compact ? "工" : "工具上下文";
        if (_compact)
        {
            CustomMinimumSize = new Vector2(CompactWidth, 44f);
            _focusEntryButton.CustomMinimumSize = new Vector2(CompactWidth, 36f);
            _contentScroll.CustomMinimumSize = Vector2.Zero;
            ApplyPanelMargins(0);
            AddThemeStyleboxOverride("panel", _compactPanelStyle);
            Size = new Vector2(CompactWidth, 44f);
            return;
        }

        CustomMinimumSize = new Vector2(WideWidth, 44f);
        _focusEntryButton.CustomMinimumSize = new Vector2(44f, 36f);
        _contentScroll.CustomMinimumSize = new Vector2(0f, 40f);
        ApplyPanelMargins(_usesCompactViewport ? CompactExpandedMargin : ExpandedMargin);
        if (_usesCompactViewport)
            AddThemeStyleboxOverride("panel", _compactPanelStyle);
        else
            RemoveThemeStyleboxOverride("panel");
    }

    private void ApplyPanelMargins(int margin)
    {
        _panelMargin.AddThemeConstantOverride("margin_left", margin);
        _panelMargin.AddThemeConstantOverride("margin_top", margin);
        _panelMargin.AddThemeConstantOverride("margin_right", margin);
        _panelMargin.AddThemeConstantOverride("margin_bottom", margin);
    }

    /// <summary>窄屏下切换折叠入口和完整上下文内容。</summary>
    private void ToggleCompactExpanded()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        if (viewportSize.X >= 760f) return;
        _compactExpanded = !_compactExpanded;
        ApplyResponsiveLayoutForViewport(viewportSize);
    }

    public void ToggleCompactExpandedForViewport(Vector2 viewportSize)
    {
        if (viewportSize.X >= 760f) return;
        _compactExpanded = !_compactExpanded;
        ApplyResponsiveLayoutForViewport(viewportSize);
    }

    private ConstructionToolDefinition? FindTool(ToolType currentTool)
    {
        if (Category?.Tools == null) return null;

        foreach (ConstructionToolDefinition? tool in Category.Tools)
            if (tool?.ToolType == currentTool)
                return tool;

        return null;
    }

    private static string ResolveShortcutHint(ToolType toolType, string fallback)
    {
        if (GodotObject.IsInstanceValid(InputBindingManager.Instance) &&
            InputBindingManager.TryGetToolAction(toolType, out string actionName))
            return InputBindingManager.Instance.GetBindingText(actionName);

        return fallback;
    }
}
