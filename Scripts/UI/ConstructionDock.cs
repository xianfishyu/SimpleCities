using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// 常驻底部建造栏：预置分类按钮，按资源动态生成当前分类的工具按钮，并与 ToolManager 双向同步选中状态。
/// </summary>
public partial class ConstructionDock : Control
{
    private const string RoadsCategoryId = "roads";
    private const float CollapsedHeight = 76f;
    private const float ExpandedHeight = 140f;
    private const float CategoryBarHeight = 76f;
    private const float ToolTrayHeight = ExpandedHeight - CollapsedHeight;
    private const float DockButtonWidth = 104f;
    private const float DockButtonSeparation = 8f;
    private const float CompactToolIconSize = 24f;

    // Select 与 RoadRemove 没有资源化定义，仍需在上下文面板中提供可读说明。
    private static readonly IReadOnlyDictionary<ToolType, BuiltInToolPresentation> BuiltInToolPresentations =
        new Dictionary<ToolType, BuiltInToolPresentation>
        {
            [ToolType.Select] = new("选择", "查看当前状态。", string.Empty),
            [ToolType.RoadRemove] = new("拆路", "点击已有道路进行拆除。", string.Empty),
            [ToolType.RoadUpgrade] = new("道路改造", "选择已有道路并修改道路类型。", "U"),
        };

    [Export] public ConstructionCategoryDefinition? Category { get; set; }
    [Export] public bool TrayVisibleOnReady { get; set; }

    public event Action<bool>? TrayVisibilityChanged;
    public event Action<string, bool>? ContextDisplayChanged;

    public bool IsTrayVisible => _toolTray?.Visible == true;
    public bool UsesCatalogContext => _activeCategoryId == RoadsCategoryId;

    private sealed record CategoryDescriptor(
        string Id,
        string DisplayName,
        string NodeName,
        PlaceholderDescriptor[] Placeholders);

    private sealed record PlaceholderDescriptor(string Id, string DisplayName, string NodeName);

    private sealed record BuiltInToolPresentation(string DisplayName, string Description, string ShortcutHint);

    private static readonly CategoryDescriptor[] Categories =
    [
        new(RoadsCategoryId, "道路", "RoadsCategoryButton", []),
        new("zoning", "区域", "ZoningCategoryButton",
        [
            new("residential-zone", "住宅区", "ResidentialZonePlaceholder"),
            new("commercial-zone", "商业区", "CommercialZonePlaceholder"),
        ]),
        new("facilities", "公共设施", "FacilitiesCategoryButton",
        [
            new("school", "学校", "SchoolPlaceholder"),
            new("clinic", "诊所", "ClinicPlaceholder"),
        ]),
        new("transit", "交通", "TransitCategoryButton",
        [
            new("bus-stop", "公交站", "BusStopPlaceholder"),
            new("metro-station", "地铁站", "MetroStationPlaceholder"),
        ]),
        new("landscaping", "景观", "LandscapingCategoryButton",
        [
            new("park", "公园", "ParkPlaceholder"),
            new("plaza", "广场", "PlazaPlaceholder"),
        ]),
    ];

    private PanelContainer _dockPanel = null!;
    private PanelContainer _toolTray = null!;
    private ScrollContainer _toolScroll = null!;
    private ScrollContainer _categoryScroll = null!;
    private HBoxContainer _categoryBar = null!;
    private HBoxContainer _toolList = null!;
    private readonly Dictionary<string, Button> _categoryButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<ToolType, Button> _toolButtons = new();
    private readonly Dictionary<ToolType, ConstructionToolDefinition> _toolDefinitions = new();
    private readonly List<Action> _disconnectActions = [];

    private ToolManager? _toolManager;
    private ToolType _lastSyncedTool;
    private bool _hasSyncedTool;
    private bool _loggedMissingToolManager;
    private bool _categoryValid;
    private string _activeCategoryId = RoadsCategoryId;
    private NodePath _contextFocusPath = new();
    private NodePath _debugFocusPath = new();

    /// <summary>每次进入场景树时重建运行时按钮和信号绑定，支持 HUD 被移除后再次加入。</summary>
    public override void _EnterTree()
    {
        TeardownRuntimeState();
        ResolveNodes();
        _toolManager = GodotObject.IsInstanceValid(ToolManager.Instance) ? ToolManager.Instance : null;
        if (_toolManager == null) LogMissingToolManager();

        _categoryValid = ValidateCategory();
        BuildCategoryBar();
        if (_categoryValid)
            RenderActiveMenu();
        else
            DisableInvalidCategory();

        SetTrayVisible(TrayVisibleOnReady && _categoryValid);
        SyncFromToolManager(force: true);
        ApplyDockLayout();
    }

    public override void _Process(double delta) => SyncFromToolManager();

    public override void _Notification(int what)
    {
        if (what == NotificationResized) ApplyDockLayout();
    }

    public override void _ExitTree()
    {
        TeardownRuntimeState();
        _dockPanel = null!;
        _toolTray = null!;
        _toolScroll = null!;
        _categoryScroll = null!;
        _categoryBar = null!;
        _toolList = null!;
    }

    /// <summary>释放上一次进入场景树创建的按钮和事件订阅，避免重复绑定。</summary>
    private void TeardownRuntimeState()
    {
        foreach (Action disconnect in _disconnectActions)
            disconnect();
        _disconnectActions.Clear();

        if (_toolList != null)
            ClearToolList();

        _categoryButtons.Clear();
        _toolManager = null;
        _hasSyncedTool = false;
        _lastSyncedTool = default;
        _loggedMissingToolManager = false;
        _categoryValid = false;
        _activeCategoryId = RoadsCategoryId;
    }

    public ConstructionToolDefinition? GetToolDefinition(ToolType toolType)
    {
        return _toolDefinitions.TryGetValue(toolType, out ConstructionToolDefinition? definition)
            ? definition
            : null;
    }

    public static bool TryGetBuiltInToolPresentation(ToolType toolType, out string displayName, out string description, out string shortcutHint)
    {
        if (BuiltInToolPresentations.TryGetValue(toolType, out BuiltInToolPresentation? presentation))
        {
            displayName = presentation.DisplayName;
            description = presentation.Description;
            shortcutHint = presentation.ShortcutHint;
            return true;
        }

        displayName = string.Empty;
        description = string.Empty;
        shortcutHint = string.Empty;
        return false;
    }

    public Control? GetLastDockFocusControl()
    {
        if (_toolTray.Visible)
        {
            Button? lastTool = _toolList.GetChildren()
                .OfType<Button>()
                .Where(button => button.FocusMode != FocusModeEnum.None)
                .LastOrDefault();
            if (lastTool != null)
                return lastTool;
        }

        return _categoryButtons.TryGetValue(Categories[^1].Id, out Button? lastCategory)
            ? lastCategory
            : null;
    }

    public void ConfigureFocusChain(NodePath contextFocusPath, NodePath debugFocusPath)
    {
        _contextFocusPath = contextFocusPath;
        _debugFocusPath = debugFocusPath;
        UpdateFocusChain();
    }

    private void ResolveNodes()
    {
        _dockPanel = GetNode<PanelContainer>("DockPanel");
        _toolTray = GetNode<PanelContainer>("DockPanel/DockStack/ToolTray");
        _toolScroll = GetNode<ScrollContainer>("DockPanel/DockStack/ToolTray/TrayMargin/ToolScroll");
        _categoryScroll = GetNode<ScrollContainer>("DockPanel/DockStack/CategoryScroll");
        _categoryBar = GetNode<HBoxContainer>("DockPanel/DockStack/CategoryScroll/CategoryBar");
        _toolList = GetNode<HBoxContainer>("DockPanel/DockStack/ToolTray/TrayMargin/ToolScroll/ToolList");
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

    /// <summary>为场景中预置的分类按钮写入显示数据，并登记各自的点击回调。</summary>
    private void BuildCategoryBar()
    {
        foreach (CategoryDescriptor category in Categories)
        {
            Button button = GetNode<Button>($"DockPanel/DockStack/CategoryScroll/CategoryBar/{category.NodeName}");
            if (button is ConstructionDockButton dockButton)
            {
                dockButton.DisplayText = category.DisplayName;
                dockButton.VisualRole = ConstructionDockButtonVisualRole.PrimaryCategory;
            }
            button.ToggleMode = true;
            button.FocusMode = FocusModeEnum.All;
            button.Disabled = false;
            button.TooltipText = string.Empty;
            string categoryId = category.Id;
            Action handler = () => OnCategoryPressed(categoryId);
            button.Pressed += handler;
            _disconnectActions.Add(() => button.Pressed -= handler);
            _categoryButtons[categoryId] = button;
        }
    }

    /// <summary>清空旧工具列表后，根据当前分类生成道路工具或未实现功能的占位项。</summary>
    private void RenderActiveMenu()
    {
        ClearToolList();
        if (_activeCategoryId == RoadsCategoryId)
            RenderRoadsMenu();
        else if (TryGetCategory(_activeCategoryId, out CategoryDescriptor? category))
            RenderPlaceholderMenu(category!);

        SyncCategoryButtons();
        SyncSelectedTool(_toolManager?.CurrentTool);
        UpdateFocusChain();
    }

    private void RenderRoadsMenu()
    {
        if (Category?.Tools == null) return;

        var tools = new List<ConstructionToolDefinition>();
        foreach (ConstructionToolDefinition? tool in Category.Tools)
        {
            if (tool == null)
            {
                GD.PushWarning("ConstructionDock: Category contains an empty tool reference.");
                continue;
            }
            if (tool.ToolType is ToolType.Road or ToolType.RoadUpgrade)
                tools.Add(tool);
        }
        tools.Sort(static (left, right) => left.SortOrder.CompareTo(right.SortOrder));

        foreach (ConstructionToolDefinition tool in tools)
            AddToolButton(tool);
    }

    private void RenderPlaceholderMenu(CategoryDescriptor category)
    {
        foreach (PlaceholderDescriptor placeholder in category.Placeholders)
        {
            var button = new ConstructionDockButton
            {
                Name = placeholder.NodeName,
                DisplayText = placeholder.DisplayName,
                VisualRole = ConstructionDockButtonVisualRole.SecondaryTool,
                Disabled = true,
                FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(DockButtonWidth, ToolTrayHeight),
                TooltipText = "尚未开放",
            };
            BuildDockButtonPresentation(button);
            _toolList.AddChild(button);
        }
    }

    /// <summary>从资源定义创建可交互工具按钮，并将点击操作映射到对应 ToolType。</summary>
    private void AddToolButton(ConstructionToolDefinition tool)
    {
        var button = new ConstructionDockButton
        {
            Name = ToolNodeName(tool.ToolType),
            DisplayText = tool.DisplayName,
            VisualRole = ConstructionDockButtonVisualRole.SecondaryTool,
            IconTexture = tool.Icon,
            ToggleMode = true,
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(DockButtonWidth, ToolTrayHeight),
            TooltipText = tool.Description,
        };
        BuildDockButtonPresentation(button);

        ToolType toolType = tool.ToolType;
        Action handler = () => OnToolPressed(toolType);
        button.Pressed += handler;
        _toolList.AddChild(button);
        _toolButtons[toolType] = button;
        _toolDefinitions[toolType] = tool;
    }

    /// <summary>为运行时生成的工具/占位按钮补齐图标、标签和分类选中指示器节点。</summary>
    private static void BuildDockButtonPresentation(ConstructionDockButton button)
    {
        var presentation = new VBoxContainer
        {
            Name = "Presentation",
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        presentation.AddThemeConstantOverride("separation", 3);
        presentation.SetAnchorsPreset(LayoutPreset.FullRect);

        var icon = new TextureRect
        {
            Name = "Icon",
            CustomMinimumSize = new Vector2(CompactToolIconSize, CompactToolIconSize),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };

        var label = new Label
        {
            Name = "Label",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var primarySelectionIndicator = new ColorRect
        {
            Name = "PrimarySelectionIndicator",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        primarySelectionIndicator.AnchorLeft = 0f;
        primarySelectionIndicator.AnchorTop = 1f;
        primarySelectionIndicator.AnchorRight = 1f;
        primarySelectionIndicator.AnchorBottom = 1f;
        primarySelectionIndicator.OffsetLeft = 0f;
        primarySelectionIndicator.OffsetTop = -4f;
        primarySelectionIndicator.OffsetRight = 0f;
        primarySelectionIndicator.OffsetBottom = 0f;

        presentation.AddChild(icon);
        presentation.AddChild(label);
        button.AddChild(presentation);
        button.AddChild(primarySelectionIndicator);
    }

    private void DisableInvalidCategory()
    {
        ClearToolList();
        _toolTray.Visible = false;
        foreach (Button button in _categoryButtons.Values)
            button.Disabled = true;
    }

    private void ClearToolList()
    {
        foreach (Node child in _toolList.GetChildren())
        {
            _toolList.RemoveChild(child);
            child.QueueFree();
        }
        _toolButtons.Clear();
        _toolDefinitions.Clear();
    }

    /// <summary>同类再次点击只切换托盘开关；切换分类则重建工具列表并展开托盘。</summary>
    private void OnCategoryPressed(string categoryId)
    {
        if (!_categoryValid)
        {
            SetTrayVisible(false);
            return;
        }

        if (categoryId == _activeCategoryId)
        {
            SetTrayVisible(!_toolTray.Visible);
            NotifyContextDisplay();
            return;
        }

        _activeCategoryId = categoryId;
        RenderActiveMenu();
        SetTrayVisible(true);
        _categoryScroll.EnsureControlVisible(_categoryButtons[categoryId]);
        NotifyContextDisplay();
    }

    /// <summary>由 UI 发起工具切换；实际建造输入仍由 ToolManager 转交 RoadBuilder。</summary>
    private void OnToolPressed(ToolType toolType)
    {
        if (_toolManager == null)
        {
            LogMissingToolManager();
            SyncSelectedTool(toolType: null);
            return;
        }

        _activeCategoryId = RoadsCategoryId;
        _toolManager.CurrentTool = toolType;
        SyncFromToolManager(force: true);
        NotifyContextDisplay();
    }

    private void SetTrayVisible(bool visible)
    {
        _toolTray.Visible = visible;
        SyncCategoryButtons();
        ApplyDockLayout();
        UpdateFocusChain();
        TrayVisibilityChanged?.Invoke(visible);
    }

    /// <summary>轮询外部工具切换，使底栏按钮与实际工具状态保持一致。</summary>
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
        if (!force && _hasSyncedTool && currentTool == _lastSyncedTool) return;

        _lastSyncedTool = currentTool;
        _hasSyncedTool = true;
        SyncSelectedTool(currentTool);
    }

    private void SyncSelectedTool(ToolType? toolType)
    {
        foreach ((ToolType candidate, Button button) in _toolButtons)
        {
            button.ButtonPressed = toolType == candidate;
            if (button is ConstructionDockButton dockButton)
                dockButton.Selected = button.ButtonPressed;
        }
    }

    private void SyncCategoryButtons()
    {
        foreach ((string categoryId, Button button) in _categoryButtons)
        {
            button.ButtonPressed = categoryId == _activeCategoryId;
            if (button is ConstructionDockButton dockButton)
                dockButton.Selected = button.ButtonPressed;
        }
    }

    /// <summary>将底栏固定到视口底部，并根据展开状态及可用宽度计算可滚动分类栏的尺寸。</summary>
    private void ApplyDockLayout()
    {
        if (_dockPanel == null || _toolTray == null || _toolScroll == null || _categoryScroll == null || _categoryBar == null)
            return;

        float height = _toolTray.Visible ? ExpandedHeight : CollapsedHeight;

        AnchorLeft = 0f;
        AnchorRight = 1f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetLeft = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        OffsetTop = OffsetBottom - height;

        _dockPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _dockPanel.OffsetLeft = 0f;
        _dockPanel.OffsetTop = 0f;
        _dockPanel.OffsetRight = 0f;
        _dockPanel.OffsetBottom = 0f;
        _toolTray.CustomMinimumSize = new Vector2(0f, ToolTrayHeight);
        _toolScroll.CustomMinimumSize = new Vector2(0f, ToolTrayHeight);
        _categoryScroll.CustomMinimumSize = new Vector2(0f, CategoryBarHeight);
        float categoryButtonWidth = Mathf.Min(
            DockButtonWidth,
            Mathf.Max(72f, Mathf.Floor((Size.X - (Categories.Length - 1) * DockButtonSeparation) / Categories.Length)));
        foreach (Button categoryButton in _categoryButtons.Values)
            categoryButton.CustomMinimumSize = new Vector2(categoryButtonWidth, CategoryBarHeight);
        _categoryBar.CustomMinimumSize = new Vector2(
            Categories.Length * categoryButtonWidth + (Categories.Length - 1) * DockButtonSeparation,
            CategoryBarHeight);
    }

    /// <summary>把分类、展开后的当前工具和 HUD 其他面板串成可循环的键盘焦点顺序。</summary>
    private void UpdateFocusChain()
    {
        if (!IsInsideTree()) return;
        if (!_categoryValid) return;
        if (_categoryButtons.Count == 0) return;

        Button? previousCategory = null;
        for (int index = 0; index < Categories.Length; index++)
        {
            CategoryDescriptor category = Categories[index];
            if (!_categoryButtons.TryGetValue(category.Id, out Button? button)) continue;
            if (!button.IsInsideTree()) return;

            button.FocusPrevious = previousCategory?.GetPath() ?? _debugFocusPath;
            if (previousCategory != null)
                previousCategory.FocusNext = button.GetPath();
            previousCategory = button;
        }

        if (previousCategory == null) return;

        if (_toolTray.Visible)
        {
            List<Button> toolButtons = _toolList.GetChildren()
                .OfType<Button>()
                .Where(button => button.FocusMode != FocusModeEnum.None)
                .ToList();
            if (toolButtons.Count > 0)
            {
                previousCategory.FocusNext = toolButtons[0].GetPath();
                toolButtons[0].FocusPrevious = previousCategory.GetPath();
                for (int index = 0; index < toolButtons.Count; index++)
                {
                    if (index > 0)
                        toolButtons[index].FocusPrevious = toolButtons[index - 1].GetPath();
                    if (index + 1 < toolButtons.Count)
                        toolButtons[index].FocusNext = toolButtons[index + 1].GetPath();
                }

                toolButtons[^1].FocusNext = _contextFocusPath;
                return;
            }
        }

        previousCategory.FocusNext = _contextFocusPath;
    }

    private void NotifyContextDisplay()
    {
        string displayName = TryGetCategory(_activeCategoryId, out CategoryDescriptor? category)
            ? category!.DisplayName
            : Category?.DisplayName ?? "道路";
        ContextDisplayChanged?.Invoke(displayName, UsesCatalogContext);
    }

    private bool TryGetCategory(string categoryId, out CategoryDescriptor? category)
    {
        foreach (CategoryDescriptor candidate in Categories)
        {
            if (candidate.Id != categoryId) continue;
            category = candidate;
            return true;
        }

        category = null;
        return false;
    }

    private void LogMissingToolManager()
    {
        if (_loggedMissingToolManager) return;
        _loggedMissingToolManager = true;
        GD.PushWarning("ConstructionDock: ToolManager.Instance is missing; tool commands are disabled until ToolManager exists.");
    }

    private static string ToolButtonText(ConstructionToolDefinition tool)
    {
        return string.IsNullOrWhiteSpace(tool.ShortcutHint)
            ? tool.DisplayName
            : $"{tool.DisplayName} {tool.ShortcutHint}";
    }

    private static string ToolNodeName(ToolType toolType) => toolType switch
    {
        ToolType.Road => "RoadToolButton",
        ToolType.RoadUpgrade => "RoadUpgradeToolButton",
        _ => $"{toolType}ToolButton",
    };
}
