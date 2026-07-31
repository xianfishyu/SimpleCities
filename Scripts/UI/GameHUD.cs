using Godot;

public partial class GameHUD : CanvasLayer
{
    private const float PanelMargin = 16f;
    private const float PanelGap = 12f;

    [Export] public RoadConfig Config { get; set; } = null!;

    private ConstructionDock? _constructionDock;
    private ToolContextPanel _toolContextPanel = null!;
    private DebugPanel _debugPanel = null!;
    private SystemControls _systemControls = null!;
    private UIManager _uiManager = null!;

    private RoadGraph? _network;
    private ToolManager? _toolManager;
    private Viewport? _wiredViewport;
    private Callable _panelResizedCallable;
    private bool _layoutRefreshQueued;
    private bool _layoutSignalsConnected;

    public override void _Ready()
    {
        _toolManager = GodotObject.IsInstanceValid(ToolManager.Instance) ? ToolManager.Instance : null;
        RoadSystem? roadSystem = GodotObject.IsInstanceValid(RoadSystem.Instance) ? RoadSystem.Instance : null;
        _network = roadSystem?.Graph;
        if (_toolManager == null)
            GD.PushWarning("GameHUD: ToolManager.Instance is missing; tool display is degraded.");
        if (roadSystem == null)
            GD.PushWarning("GameHUD: RoadSystem.Instance is missing; debug metrics are degraded.");

        if (Config == null)
        {
            GD.PushWarning("GameHUD: Config (RoadConfig resource) is not assigned; using fallback RoadConfig for UI display.");
            Config = new RoadConfig();
        }

        EnsureUIManager();
        ResolveChildNodes();
        _panelResizedCallable = Callable.From(OnPanelResized);
        ConfigureComponents();
        RegisterManagedPanels();
        WireSystemControls();
        WireLayoutSignals();
        ConfigureFocusChain();
        ConnectViewportResize();
        QueueResponsiveLayoutRefresh();
    }

    public override void _ExitTree()
    {
        DisconnectViewportResize();
        if (_systemControls != null)
        {
            _systemControls.SaveRequested -= OnSave;
            _systemControls.LoadRequested -= OnLoad;
        }
        if (_constructionDock != null)
        {
            _constructionDock.TrayVisibilityChanged -= OnDockTrayVisibilityChanged;
            _constructionDock.ContextDisplayChanged -= OnDockContextDisplayChanged;
        }
        DisconnectLayoutSignals();

        _uiManager?.Unregister("ContextPanel");
        _uiManager?.Unregister("DebugPanel");
        _uiManager?.Unregister("SystemControls");
        _layoutRefreshQueued = false;
        RequestReadyOnReentry();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed) return;

        if (keyEvent.Keycode == Key.F5)
            OnSave();
        else if (keyEvent.Keycode == Key.F9)
            OnLoad();
    }

    public override void _Process(double delta)
    {
        ToolType currentTool = _toolManager?.CurrentTool ?? ToolType.Select;
        if (_constructionDock?.UsesCatalogContext != false)
            _toolContextPanel.UpdateContext(currentTool, Config);
        _debugPanel.UpdateMetrics();
    }

    private void EnsureUIManager()
    {
        _uiManager = GetNodeOrNull<UIManager>("UIManager") ?? new UIManager { Name = "UIManager" };
        if (_uiManager.GetParent() == null)
            AddChild(_uiManager);
    }

    private void ResolveChildNodes()
    {
        _constructionDock = GetNodeOrNull<ConstructionDock>("ConstructionDock");
        _toolContextPanel = GetNode<ToolContextPanel>("ToolContextPanel");
        _debugPanel = GetNode<DebugPanel>("DebugPanel");
        _systemControls = GetNode<SystemControls>("SystemControls");
    }

    private void RequestReadyOnReentry()
    {
        RequestReady();
        _constructionDock?.RequestReady();
        _toolContextPanel?.RequestReady();
        _debugPanel?.RequestReady();
        _systemControls?.RequestReady();
    }

    private void ConfigureComponents()
    {
        _toolContextPanel.Config = Config;
        _toolContextPanel.SetCategory(_constructionDock?.Category);
        _debugPanel.SetDependencies(_network, Config);
    }

    private void RegisterManagedPanels()
    {
        _uiManager.Register("ContextPanel", _toolContextPanel);
        _uiManager.Register("DebugPanel", _debugPanel);
        _uiManager.Register("SystemControls", _systemControls);
    }

    private void WireSystemControls()
    {
        _systemControls.SaveRequested -= OnSave;
        _systemControls.LoadRequested -= OnLoad;
        _systemControls.SaveRequested += OnSave;
        _systemControls.LoadRequested += OnLoad;
        if (_constructionDock != null)
        {
            _constructionDock.TrayVisibilityChanged -= OnDockTrayVisibilityChanged;
            _constructionDock.ContextDisplayChanged -= OnDockContextDisplayChanged;
            _constructionDock.TrayVisibilityChanged += OnDockTrayVisibilityChanged;
            _constructionDock.ContextDisplayChanged += OnDockContextDisplayChanged;
        }
    }

    private void WireLayoutSignals()
    {
        if (_layoutSignalsConnected) return;

        ConnectResizeSignal(_toolContextPanel);
        ConnectResizeSignal(_systemControls);
        ConnectResizeSignal(_debugPanel);
        if (!_debugPanel.IsConnected(Control.SignalName.MinimumSizeChanged, _panelResizedCallable))
            _debugPanel.Connect(Control.SignalName.MinimumSizeChanged, _panelResizedCallable);
        if (_constructionDock != null)
            ConnectResizeSignal(_constructionDock);
        _layoutSignalsConnected = true;
    }

    private void DisconnectLayoutSignals()
    {
        if (!_layoutSignalsConnected) return;

        if (_toolContextPanel != null)
            DisconnectResizeSignal(_toolContextPanel);
        if (_systemControls != null)
            DisconnectResizeSignal(_systemControls);
        if (_debugPanel != null)
        {
            DisconnectResizeSignal(_debugPanel);
            if (_debugPanel.IsConnected(Control.SignalName.MinimumSizeChanged, _panelResizedCallable))
                _debugPanel.Disconnect(Control.SignalName.MinimumSizeChanged, _panelResizedCallable);
        }
        if (_constructionDock != null)
            DisconnectResizeSignal(_constructionDock);
        _layoutSignalsConnected = false;
    }

    private void ConnectResizeSignal(Control panel)
    {
        if (!panel.IsConnected(Control.SignalName.Resized, _panelResizedCallable))
            panel.Connect(Control.SignalName.Resized, _panelResizedCallable);
    }

    private void DisconnectResizeSignal(Control panel)
    {
        if (panel.IsConnected(Control.SignalName.Resized, _panelResizedCallable))
            panel.Disconnect(Control.SignalName.Resized, _panelResizedCallable);
    }

    private void OnPanelResized() => QueueResponsiveLayoutRefresh();

    private void OnDockTrayVisibilityChanged(bool visible)
    {
        ConfigureFocusChain();
        QueueResponsiveLayoutRefresh();
    }

    private void OnDockContextDisplayChanged(string categoryDisplayName, bool usesCatalogContext)
    {
        if (usesCatalogContext)
            _toolContextPanel.UpdateContext(_toolManager?.CurrentTool ?? ToolType.Select, Config);
        else
            _toolContextPanel.ShowUnavailableCategory(categoryDisplayName);
        QueueResponsiveLayoutRefresh();
    }

    private void ConfigureFocusChain()
    {
        if (_constructionDock == null) return;

        Button categoryButton = _constructionDock.GetNode<Button>("DockPanel/DockStack/CategoryBar/RoadsCategoryButton");
        _constructionDock.ConfigureFocusChain(_toolContextPanel.FocusEntryPath, _systemControls.SaveFocusPath, _systemControls.LoadFocusPath, _debugPanel.ToggleFocusPath);
        Control contextPrevious = _constructionDock.GetLastDockFocusControl() ?? categoryButton;
        _toolContextPanel.ConfigureFocus(contextPrevious.GetPath(), _systemControls.SaveFocusPath);
        _systemControls.ConfigureFocus(_toolContextPanel.FocusEntryPath, _debugPanel.ToggleFocusPath);
        _debugPanel.ConfigureFocus(_systemControls.LoadFocusPath, categoryButton.GetPath());
    }

    private void ConnectViewportResize()
    {
        Viewport viewport = GetViewport();
        if (_wiredViewport == viewport) return;

        DisconnectViewportResize();
        _wiredViewport = viewport;
        _wiredViewport.SizeChanged += OnViewportSizeChanged;
    }

    private void DisconnectViewportResize()
    {
        if (_wiredViewport == null) return;

        _wiredViewport.SizeChanged -= OnViewportSizeChanged;
        _wiredViewport = null;
    }

    private void OnViewportSizeChanged() => QueueResponsiveLayoutRefresh();

    private void QueueResponsiveLayoutRefresh()
    {
        if (_layoutRefreshQueued || !IsInsideTree()) return;

        _layoutRefreshQueued = true;
        CallDeferred(MethodName.ApplyResponsiveLayoutAfterContainersSettled);
    }

    private void ApplyResponsiveLayoutAfterContainersSettled()
    {
        _layoutRefreshQueued = false;
        if (!IsInsideTree()) return;

        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        if (_constructionDock == null)
        {
            _toolContextPanel.ApplyResponsiveLayout();
            PlaceTopRightPanels(viewportSize);
            PlaceTopLeftDebugPanel(viewportSize);
            return;
        }

        _toolContextPanel.ReservedBottomTop = _constructionDock.Position.Y;
        _toolContextPanel.ApplyResponsiveLayoutForViewport(viewportSize);
        PlaceTopRightPanels(viewportSize);
        PlaceTopLeftDebugPanel(viewportSize);
    }

    private void PlaceTopRightPanels(Vector2 viewportSize)
    {
        Vector2 systemSize = EffectiveSize(_systemControls);
        float systemWidth = Mathf.Min(systemSize.X, Mathf.Max(0f, viewportSize.X - (PanelMargin * 2f)));
        float systemLeft = Mathf.Max(PanelMargin, viewportSize.X - systemWidth - PanelMargin);
        Vector2 debugSize = EffectiveSize(_debugPanel);
        debugSize.Y = _debugPanel.GetCombinedMinimumSize().Y;
        var debugRect = new Rect2(new Vector2(PanelMargin, PanelMargin), debugSize);
        var systemRect = new Rect2(new Vector2(systemLeft, PanelMargin), new Vector2(systemWidth, systemSize.Y));
        float systemTop = debugRect.Intersects(systemRect)
            ? debugRect.End.Y + PanelGap
            : PanelMargin;
        PlaceRightAligned(_systemControls, viewportSize, systemTop);
    }

    private void PlaceTopLeftDebugPanel(Vector2 viewportSize)
    {
        float reservedBottomTop = _constructionDock?.Position.Y ?? viewportSize.Y;
        Vector2 debugSize = EffectiveSize(_debugPanel);
        debugSize.Y = _debugPanel.GetCombinedMinimumSize().Y;
        float availableWidthBeforeSystem = _systemControls.Position.X - PanelGap - PanelMargin;
        debugSize.X = Mathf.Min(debugSize.X, Mathf.Max(0f, availableWidthBeforeSystem));
        debugSize.Y = Mathf.Min(debugSize.Y, Mathf.Max(0f, reservedBottomTop - PanelMargin - PanelGap));
        _debugPanel.Size = debugSize;
        _debugPanel.Position = new Vector2(PanelMargin, PanelMargin);
    }

    private static void PlaceRightAligned(Control panel, Vector2 viewportSize, float top)
    {
        Vector2 size = EffectiveSize(panel);
        float width = Mathf.Min(size.X, Mathf.Max(0f, viewportSize.X - (PanelMargin * 2f)));
        panel.Position = new Vector2(Mathf.Max(PanelMargin, viewportSize.X - width - PanelMargin), top);
        panel.Size = new Vector2(width, size.Y);
    }

    private static Vector2 EffectiveSize(Control panel)
    {
        Vector2 minimumSize = panel.GetCombinedMinimumSize();
        return new Vector2(Mathf.Max(panel.Size.X, minimumSize.X), Mathf.Max(panel.Size.Y, minimumSize.Y));
    }

    private void OnSave()
    {
        if (!GodotObject.IsInstanceValid(SaveManager.Instance))
        {
            _systemControls.ShowStatus("存档失败：SaveManager 不可用", success: false);
            GD.PushWarning("GameHUD: SaveManager.Instance is missing; save skipped.");
            return;
        }

        bool success = SaveManager.Instance.Save("autosave");
        if (success)
        {
            _systemControls.ShowStatus("已保存 autosave", success: true);
            GD.Print("[GameHUD] 存档成功");
        }
        else
        {
            _systemControls.ShowStatus("存档失败", success: false);
            GD.PushError("[GameHUD] 存档失败");
        }
    }

    private void OnLoad()
    {
        if (!GodotObject.IsInstanceValid(SaveManager.Instance))
        {
            _systemControls.ShowStatus("读档失败：SaveManager 不可用", success: false);
            GD.PushWarning("GameHUD: SaveManager.Instance is missing; load skipped.");
            return;
        }

        bool success = SaveManager.Instance.Load("autosave");
        if (success)
        {
            _systemControls.ShowStatus("已加载 autosave", success: true);
            GD.Print("[GameHUD] 读档成功");
        }
        else
        {
            _systemControls.ShowStatus("读档失败：存档不存在或损坏", success: false);
            GD.PushError("[GameHUD] 读档失败：存档不存在或损坏");
        }
    }
}
