using Godot;

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
        _shortcutValue = GetNode<Label>("PanelMargin/Rows/ContextContentScroll/ContextContent/ShortcutRow/ShortcutValue");
        _cellSizeValue = GetNode<Label>("PanelMargin/Rows/ContextContentScroll/ContextContent/CellSizeRow/CellSizeValue");
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

    public void UpdateContext(ToolType currentTool, RoadConfig? config)
    {
        Config = config;
        ConstructionToolDefinition? definition = FindTool(currentTool);
        _categoryValue.Text = Category?.DisplayName ?? "道路 unavailable";
        _toolValue.Text = definition?.DisplayName ?? currentTool.ToString();
        _operationValue.Text = definition?.Description ?? "工具定义不可用。";
        _shortcutValue.Text = definition?.ShortcutHint ?? "--";
        _cellSizeValue.Text = Config == null ? "CellSize: unavailable" : $"CellSize: {Config.CellSize:F0}";
    }

    public void ApplyResponsiveLayout()
    {
        if (_contentScroll == null || _content == null || _focusEntryButton == null) return;

        ApplyResponsiveLayoutForViewport(GetViewportRect().Size);
    }

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
}
