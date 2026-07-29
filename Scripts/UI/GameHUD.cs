using Godot;

public partial class GameHUD : CanvasLayer
{
    [Export] public RoadConfig Config { get; set; } = null!;

    private ConstructionDock? _constructionDock;
    private ToolContextPanel _toolContextPanel = null!;
    private DebugPanel _debugPanel = null!;
    private SystemControls _systemControls = null!;
    private UIManager _uiManager = null!;

    private RoadGraph? _network;
    private ToolManager? _toolManager;

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
        ConfigureComponents();
        RegisterManagedPanels();
        WireSystemControls();
        ConfigureFocusChain();
    }

    public override void _ExitTree()
    {
        if (_systemControls != null)
        {
            _systemControls.SaveRequested -= OnSave;
            _systemControls.LoadRequested -= OnLoad;
        }
        if (_constructionDock != null)
            _constructionDock.TrayVisibilityChanged -= OnDockTrayVisibilityChanged;

        _uiManager?.Unregister("ContextPanel");
        _uiManager?.Unregister("DebugPanel");
        _uiManager?.Unregister("SystemControls");
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
        _toolContextPanel.UpdateContext(currentTool, Config);
        _debugPanel.UpdateMetrics();
        ApplyResponsiveLayout();
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
        _systemControls.SaveRequested += OnSave;
        _systemControls.LoadRequested += OnLoad;
        if (_constructionDock != null)
            _constructionDock.TrayVisibilityChanged += OnDockTrayVisibilityChanged;
    }

    private void OnDockTrayVisibilityChanged(bool visible) => ConfigureFocusChain();

    private void ConfigureFocusChain()
    {
        if (_constructionDock == null) return;

        Button categoryButton = _constructionDock.GetNode<Button>("DockPanel/DockStack/CategoryBar/RoadsCategoryButton");
        _constructionDock.ConfigureFocusChain(_toolContextPanel.FocusEntryPath, _systemControls.SaveFocusPath, _systemControls.LoadFocusPath, _debugPanel.ToggleFocusPath);
        Control contextPrevious = _constructionDock.IsTrayVisible
            ? _constructionDock.GetNodeOrNull<Control>("DockPanel/DockStack/ToolTray/TrayMargin/ToolScroll/ToolList/RoadRemoveToolButton") ?? categoryButton
            : categoryButton;
        _toolContextPanel.ConfigureFocus(contextPrevious.GetPath(), _systemControls.SaveFocusPath);
        _systemControls.ConfigureFocus(_toolContextPanel.FocusEntryPath, _debugPanel.ToggleFocusPath);
        _debugPanel.ConfigureFocus(_systemControls.LoadFocusPath, categoryButton.GetPath());
    }

    private void ApplyResponsiveLayout()
    {
        if (_constructionDock == null)
        {
            _toolContextPanel.ApplyResponsiveLayout();
            return;
        }

        _toolContextPanel.ReservedBottomTop = _constructionDock.Position.Y;
        _toolContextPanel.ApplyResponsiveLayoutForViewport(GetViewport().GetVisibleRect().Size);
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
