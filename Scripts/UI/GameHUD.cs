using Godot;

/// <summary>
/// 游戏内 HUD 的协调器。它解析游戏系统依赖、连接各子面板事件，并统一处理响应式布局和存取档请求。
/// </summary>
public partial class GameHUD : CanvasLayer
{
    private const float PanelMargin = 16f;
    private const float PanelGap = 12f;
    private const string PauseMenuPanelName = "PauseMenu";
    private const string MainMenuScenePath = "res://Scenes/MainMenu.tscn";

    [Export] public RoadConfig Config { get; set; } = null!;

    private ConstructionDock? _constructionDock;
    private ToolContextPanel _toolContextPanel = null!;
    private DebugPanel _debugPanel = null!;
    private PauseMenu _pauseMenu = null!;
    private UIManager _uiManager = null!;

    private RoadGraph? _network;
    private ToolManager? _toolManager;
    private Viewport? _wiredViewport;
    private Callable _panelResizedCallable;
    private bool _layoutRefreshQueued;
    private bool _layoutSignalsConnected;

    /// <summary>解析依赖并完成一次 HUD 生命周期内的组件配置、信号连接和首帧布局。</summary>
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
        WireConstructionDock();
        WirePauseMenu();
        WireLayoutSignals();
        ConfigureFocusChain();
        ConnectViewportResize();
        QueueResponsiveLayoutRefresh();
    }

    /// <summary>断开所有跨节点事件和面板注册，保证 HUD 可安全地再次进入场景树。</summary>
    public override void _ExitTree()
    {
        if (_pauseMenu != null && _pauseMenu.IsOpen)
            _pauseMenu.Close();
        DisconnectViewportResize();
        if (_constructionDock != null)
        {
            _constructionDock.TrayVisibilityChanged -= OnDockTrayVisibilityChanged;
            _constructionDock.ContextDisplayChanged -= OnDockContextDisplayChanged;
        }
        if (_pauseMenu != null)
        {
            _pauseMenu.ContinueRequested -= ClosePauseMenu;
            _pauseMenu.SaveRequested -= OnPauseSave;
            _pauseMenu.LoadRequested -= OnPauseLoad;
            _pauseMenu.ReturnToMainMenuRequested -= ReturnToMainMenu;
            _pauseMenu.QuitToDesktopRequested -= QuitToDesktop;
        }
        DisconnectLayoutSignals();

        _uiManager?.Unregister("ContextPanel");
        _uiManager?.Unregister("DebugPanel");
        _uiManager?.Unregister(PauseMenuPanelName);
        _layoutRefreshQueued = false;
        RequestReadyOnReentry();
    }

    /// <summary>统一消费可配置的暂停和工具动作；暂停时由 PauseMenu 自身接管输入。</summary>
    public override void _Input(InputEvent @event)
    {
        if (!GodotObject.IsInstanceValid(InputBindingManager.Instance))
            return;

        if (InputBindingManager.Instance.EventMatchesAction(@event, InputBindingManager.PauseMenuAction))
        {
            OpenPauseMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_pauseMenu.IsOpen || _uiManager.IsModalActive || _toolManager == null)
            return;

        if (!InputBindingManager.Instance.TryGetToolForEvent(@event, out ToolType tool))
            return;

        _toolManager.CurrentTool = tool;
        GetViewport().SetInputAsHandled();
    }

    /// <summary>持续同步工具上下文和轻量调试指标，不在此执行任何游戏规则。</summary>
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
        _pauseMenu = GetNode<PauseMenu>(PauseMenuPanelName);
    }

    private void RequestReadyOnReentry()
    {
        RequestReady();
        _constructionDock?.RequestReady();
        _toolContextPanel?.RequestReady();
        _debugPanel?.RequestReady();
        _pauseMenu?.RequestReady();
    }

    private void ConfigureComponents()
    {
        _toolContextPanel.Config = Config;
        _toolContextPanel.SetCategory(_constructionDock?.Category);
        _debugPanel.SetDependencies(_network, Config);
    }

    /// <summary>将可独立显隐的面板登记到 HUD 私有的 UIManager；底栏始终常驻，不在此登记。</summary>
    private void RegisterManagedPanels()
    {
        _uiManager.Register("ContextPanel", _toolContextPanel);
        _uiManager.Register("DebugPanel", _debugPanel);
        _uiManager.Register(PauseMenuPanelName, _pauseMenu);
    }

    /// <summary>连接底栏状态变化，让工具上下文、焦点和布局保持同步。</summary>
    private void WireConstructionDock()
    {
        if (_constructionDock != null)
        {
            _constructionDock.TrayVisibilityChanged -= OnDockTrayVisibilityChanged;
            _constructionDock.ContextDisplayChanged -= OnDockContextDisplayChanged;
            _constructionDock.TrayVisibilityChanged += OnDockTrayVisibilityChanged;
            _constructionDock.ContextDisplayChanged += OnDockContextDisplayChanged;
        }
    }

    /// <summary>将暂停菜单意图映射到 HUD 所有的存档与场景切换流程。</summary>
    private void WirePauseMenu()
    {
        _pauseMenu.ContinueRequested -= ClosePauseMenu;
        _pauseMenu.SaveRequested -= OnPauseSave;
        _pauseMenu.LoadRequested -= OnPauseLoad;
        _pauseMenu.ReturnToMainMenuRequested -= ReturnToMainMenu;
        _pauseMenu.QuitToDesktopRequested -= QuitToDesktop;
        _pauseMenu.ContinueRequested += ClosePauseMenu;
        _pauseMenu.SaveRequested += OnPauseSave;
        _pauseMenu.LoadRequested += OnPauseLoad;
        _pauseMenu.ReturnToMainMenuRequested += ReturnToMainMenu;
        _pauseMenu.QuitToDesktopRequested += QuitToDesktop;
    }

    private void WireLayoutSignals()
    {
        if (_layoutSignalsConnected) return;

        ConnectResizeSignal(_toolContextPanel);
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
        Control contextPrevious = _constructionDock.GetLastDockFocusControl() ?? categoryButton;
        _toolContextPanel.ConfigureFocus(contextPrevious.GetPath(), _debugPanel.ToggleFocusPath);
        _debugPanel.ConfigureFocus(_toolContextPanel.FocusEntryPath, categoryButton.GetPath());
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

    /// <summary>将布局延后到容器完成尺寸计算后执行，并合并同一帧的多次尺寸变化。</summary>
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

    /// <summary>协调右侧上下文、右上系统操作和左上调试面板，避免彼此或底边栏重叠。</summary>
    private void ApplyResponsiveLayout()
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        if (_constructionDock == null)
        {
            _toolContextPanel.ApplyResponsiveLayout();
            PlaceTopLeftDebugPanel(viewportSize);
            return;
        }

        _toolContextPanel.ReservedBottomTop = _constructionDock.Position.Y;
        _toolContextPanel.ApplyResponsiveLayoutForViewport(viewportSize);
        PlaceTopLeftDebugPanel(viewportSize);
    }

    private void PlaceTopLeftDebugPanel(Vector2 viewportSize)
    {
        float reservedBottomTop = _constructionDock?.Position.Y ?? viewportSize.Y;
        Vector2 debugSize = EffectiveSize(_debugPanel);
        debugSize.Y = _debugPanel.GetCombinedMinimumSize().Y;
        debugSize.X = Mathf.Min(debugSize.X, Mathf.Max(0f, viewportSize.X - (PanelMargin * 2f)));
        debugSize.Y = Mathf.Min(debugSize.Y, Mathf.Max(0f, reservedBottomTop - PanelMargin - PanelGap));
        _debugPanel.Size = debugSize;
        _debugPanel.Position = new Vector2(PanelMargin, PanelMargin);
    }

    private static Vector2 EffectiveSize(Control panel)
    {
        Vector2 minimumSize = panel.GetCombinedMinimumSize();
        return new Vector2(Mathf.Max(panel.Size.X, minimumSize.X), Mathf.Max(panel.Size.Y, minimumSize.Y));
    }

    private void OpenPauseMenu()
    {
        if (_pauseMenu.IsOpen || _uiManager.IsModalActive)
            return;

        _uiManager.PushModal(PauseMenuPanelName);
        _pauseMenu.Open();
    }

    private void ClosePauseMenu()
    {
        if (!_pauseMenu.IsOpen)
            return;

        _pauseMenu.Close();
        _uiManager.PopModal();
    }

    private void OnPauseSave()
    {
        (string message, bool success) = SaveAutosave();
        _pauseMenu.ShowStatus(message, success);
    }

    private void OnPauseLoad()
    {
        (string message, bool success) = LoadAutosave();
        _pauseMenu.ShowStatus(message, success);
    }

    private void ReturnToMainMenu()
    {
        ClosePauseMenu();
        CallDeferred(MethodName.ChangeToMainMenu);
    }

    private void ChangeToMainMenu()
    {
        Error result = GetTree().ChangeSceneToFile(MainMenuScenePath);
        if (result != Error.Ok)
            GD.PushError($"GameHUD: failed to change to main menu ({result}).");
    }

    private void QuitToDesktop()
    {
        ClosePauseMenu();
        GetTree().Quit();
    }

    private (string Message, bool Success) SaveAutosave()
    {
        if (!GodotObject.IsInstanceValid(SaveManager.Instance))
        {
            GD.PushWarning("GameHUD: SaveManager.Instance is missing; save skipped.");
            return ("存档失败：SaveManager 不可用", false);
        }

        bool success = SaveManager.Instance.Save("autosave");
        if (success)
        {
            GD.Print("[GameHUD] 存档成功");
            return ("已保存 autosave", true);
        }

        GD.PushError("[GameHUD] 存档失败");
        return ("存档失败", false);
    }

    private (string Message, bool Success) LoadAutosave()
    {
        if (!GodotObject.IsInstanceValid(SaveManager.Instance))
        {
            GD.PushWarning("GameHUD: SaveManager.Instance is missing; load skipped.");
            return ("读档失败：SaveManager 不可用", false);
        }

        bool success = SaveManager.Instance.Load("autosave");
        if (success)
        {
            GD.Print("[GameHUD] 读档成功");
            return ("已加载 autosave", true);
        }

        GD.PushError("[GameHUD] 读档失败：存档不存在或损坏");
        return ("读档失败：存档不存在或损坏", false);
    }
}
