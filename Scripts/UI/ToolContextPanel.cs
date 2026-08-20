using Godot;
using System;
using System.Collections.Generic;

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

    private static readonly RoadType[] RoadTypeOrder =
    [
        RoadType.Dirt,
        RoadType.Street,
        RoadType.Arterial,
        RoadType.Highway,
    ];

    private static readonly IReadOnlyDictionary<RoadType, RoadTypePresentation> FallbackRoadTypePresentations =
        new Dictionary<RoadType, RoadTypePresentation>
        {
            [RoadType.Dirt] = new("土路", new Color("#8A6652")),
            [RoadType.Street] = new("街道", new Color("#60727C")),
            [RoadType.Arterial] = new("主干道", new Color("#D7A928")),
            [RoadType.Highway] = new("高速道路", new Color("#C84B3A")),
        };

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
    private HBoxContainer _roadTypeSelector = null!;
    private Label _roadTypeStatus = null!;

    private readonly Dictionary<RoadType, Button> _roadTypeButtons = new();
    private readonly List<Action> _roadTypeDisconnectActions = [];
    private ButtonGroup? _roadTypeButtonGroup;
    private Func<RoadType>? _selectedRoadTypeGetter;
    private Func<RoadType, bool>? _selectedRoadTypeSetter;
    private RoadConfig? _roadTypeStyleConfig;
    private bool _roadTypeStylesValid;
    private bool _roadTypeSelectorAvailable;

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
        _roadTypeSelector = GetNode<HBoxContainer>("PanelMargin/Rows/ContextContentScroll/ContextContent/RoadTypeRow/RoadTypeSelector");
        _roadTypeStatus = GetNode<Label>("PanelMargin/Rows/ContextContentScroll/ContextContent/RoadTypeRow/RoadTypeStatus");
        WireRoadTypeButtons();
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
        DisconnectRoadTypeButtons();
        _roadTypeButtonGroup = null;
        _selectedRoadTypeGetter = null;
        _selectedRoadTypeSetter = null;
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

    /// <summary>
    /// 注入共享工具状态的读写委托。面板只编辑 RoadBuilder 的 SelectedRoadType，
    /// 不直接依赖 RoadGraph 或执行道路命令。
    /// </summary>
    public void ConfigureRoadTypeState(
        Func<RoadType>? getter,
        Func<RoadType, bool>? setter)
    {
        _selectedRoadTypeGetter = getter;
        _selectedRoadTypeSetter = setter;
        _roadTypeSelectorAvailable = _roadTypeStylesValid &&
            _selectedRoadTypeGetter is not null &&
            _selectedRoadTypeSetter is not null;
        ApplyRoadTypeAvailability();
        SyncSelectedRoadTypeButtons();
    }

    public bool RoadTypeSelectorAvailable => _roadTypeSelectorAvailable;

    public bool RoadTypeSelectorVisible => _roadTypeRow?.Visible == true;

    public NodePath GetRoadTypeButtonPath(RoadType roadType) =>
        _roadTypeButtons.TryGetValue(roadType, out Button? button)
            ? button.GetPath()
            : new NodePath();

    /// <summary>
    /// 同步工具说明。优先使用资源化工具定义；选择和拆除等内置工具则使用底栏提供的后备文案。
    /// </summary>
    public void UpdateContext(ToolType currentTool, RoadConfig? config)
    {
        Config = config;
        RefreshRoadTypeStylesIfNeeded();
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
        _roadTypeRow.Visible = currentTool is ToolType.Road or ToolType.RoadUpgrade;
        SyncSelectedRoadTypeButtons();
    }

    /// <summary>非道路分类目前尚未实现时，显示分类名称和明确的不可用状态。</summary>
    public void ShowUnavailableCategory(string categoryDisplayName)
    {
        _categoryValue.Text = categoryDisplayName;
        _toolValue.Text = "尚未开放";
        _operationValue.Text = "尚未开放";
        _shortcutRow.Visible = false;
        _cellSizeRow.Visible = false;
        _roadTypeRow.Visible = false;
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

    private void WireRoadTypeButtons()
    {
        DisconnectRoadTypeButtons();
        _roadTypeButtons.Clear();
        _roadTypeButtonGroup = new ButtonGroup { AllowUnpress = false };

        foreach (RoadType roadType in RoadTypeOrder)
        {
            Button button = _roadTypeSelector.GetNode<Button>(GetRoadTypeButtonName(roadType));
            button.ButtonGroup = _roadTypeButtonGroup;
            button.ToggleMode = true;
            button.FocusMode = FocusModeEnum.All;
            button.MouseDefaultCursorShape = CursorShape.PointingHand;
            RoadType capturedRoadType = roadType;
            Action handler = () => OnRoadTypePressed(capturedRoadType);
            button.Pressed += handler;
            _roadTypeDisconnectActions.Add(() => button.Pressed -= handler);
            _roadTypeButtons[roadType] = button;
        }

        ConfigureRoadTypeFocusNeighbors();
    }

    private void DisconnectRoadTypeButtons()
    {
        foreach (Action disconnect in _roadTypeDisconnectActions)
            disconnect();
        _roadTypeDisconnectActions.Clear();
    }

    private void ConfigureRoadTypeFocusNeighbors()
    {
        for (int index = 0; index < RoadTypeOrder.Length; index++)
        {
            Button button = _roadTypeButtons[RoadTypeOrder[index]];
            if (index > 0)
                button.FocusNeighborLeft = _roadTypeButtons[RoadTypeOrder[index - 1]].GetPath();
            if (index + 1 < RoadTypeOrder.Length)
                button.FocusNeighborRight = _roadTypeButtons[RoadTypeOrder[index + 1]].GetPath();
        }
    }

    private void RefreshRoadTypeStylesIfNeeded()
    {
        if (ReferenceEquals(_roadTypeStyleConfig, Config))
            return;

        _roadTypeStyleConfig = Config;
        _roadTypeStylesValid = Config != null && Config.TryValidateRoadTypeStyles(out _);
        _roadTypeSelectorAvailable = _roadTypeStylesValid &&
            _selectedRoadTypeGetter is not null &&
            _selectedRoadTypeSetter is not null;

        foreach (RoadType roadType in RoadTypeOrder)
        {
            if (!_roadTypeButtons.TryGetValue(roadType, out Button? button))
                continue;

            RoadTypePresentation presentation = ResolveRoadTypePresentation(roadType);
            button.TooltipText = presentation.DisplayName;
            ColorRect swatch = button.GetNode<ColorRect>("Swatch");
            swatch.Color = presentation.Color;
            Label label = button.GetNode<Label>("Label");
            label.Text = presentation.DisplayName;
        }

        ApplyRoadTypeAvailability();
    }

    private void ApplyRoadTypeAvailability()
    {
        if (_roadTypeSelector == null || _roadTypeStatus == null)
            return;

        foreach (Button button in _roadTypeButtons.Values)
            button.Disabled = !_roadTypeSelectorAvailable;

        _roadTypeStatus.Visible = !_roadTypeStylesValid || !_roadTypeSelectorAvailable;
        _roadTypeStatus.Text = !_roadTypeStylesValid
            ? "道路类型样式不可用，选择已禁用。"
            : "道路类型控制器不可用，选择已禁用。";
    }

    private RoadTypePresentation ResolveRoadTypePresentation(RoadType roadType)
    {
        if (_roadTypeStylesValid && Config?.RoadTypeStyles is not null)
        {
            foreach (RoadTypeStyle? style in Config.RoadTypeStyles)
            {
                if (style?.RoadType != roadType)
                    continue;

                string displayName = string.IsNullOrWhiteSpace(style.DisplayName)
                    ? FallbackRoadTypePresentations[roadType].DisplayName
                    : style.DisplayName;
                return new RoadTypePresentation(displayName, style.Color);
            }
        }

        return FallbackRoadTypePresentations[roadType];
    }

    private void OnRoadTypePressed(RoadType roadType)
    {
        if (!_roadTypeSelectorAvailable ||
            _selectedRoadTypeSetter is null ||
            !RoadTypeContract.IsDefined(roadType) ||
            !TrySetSelectedRoadType(roadType))
        {
            SyncSelectedRoadTypeButtons();
            return;
        }

        SyncSelectedRoadTypeButtons();
    }

    private bool TrySetSelectedRoadType(RoadType roadType)
    {
        if (_selectedRoadTypeSetter is null)
            return false;

        try
        {
            return _selectedRoadTypeSetter(roadType);
        }
        catch (ObjectDisposedException)
        {
            _roadTypeSelectorAvailable = false;
            ApplyRoadTypeAvailability();
            return false;
        }
    }

    private void SyncSelectedRoadTypeButtons()
    {
        if (_roadTypeButtons.Count == 0)
            return;

        RoadType selectedRoadType = RoadType.Street;
        if (_selectedRoadTypeGetter is not null)
        {
            try
            {
                RoadType candidate = _selectedRoadTypeGetter();
                if (RoadTypeContract.IsDefined(candidate))
                    selectedRoadType = candidate;
            }
            catch (ObjectDisposedException)
            {
                _roadTypeSelectorAvailable = false;
            }
        }

        foreach (RoadType roadType in RoadTypeOrder)
            _roadTypeButtons[roadType].ButtonPressed = roadType == selectedRoadType;
    }

    private static string GetRoadTypeButtonName(RoadType roadType) => roadType switch
    {
        RoadType.Dirt => "DirtButton",
        RoadType.Street => "StreetButton",
        RoadType.Arterial => "ArterialButton",
        RoadType.Highway => "HighwayButton",
        _ => throw new ArgumentOutOfRangeException(nameof(roadType), roadType, "RoadType is not defined."),
    };

    private readonly record struct RoadTypePresentation(string DisplayName, Color Color);

    private static string ResolveShortcutHint(ToolType toolType, string fallback)
    {
        if (GodotObject.IsInstanceValid(InputBindingManager.Instance) &&
            InputBindingManager.TryGetToolAction(toolType, out string actionName))
            return InputBindingManager.Instance.GetBindingText(actionName);

        return fallback;
    }
}
