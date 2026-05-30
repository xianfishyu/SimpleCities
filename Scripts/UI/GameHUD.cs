using Godot;
using System.Linq;

/// <summary>
/// 主 HUD 浮层 — 常驻显示 FPS、当前工具、鼠标格点、路网统计，
/// 提供工具切换按钮（选择 / 铺路 / 拆路）和存档/读档按钮（保存 / 加载）。
///
/// UI 布局定义在 Scenes/UI/GameHUD.tscn 中（Godot 编辑器可视化编辑）。
/// 本脚本仅负责：
///   1. _Ready  — 解析子控件引用 + 绑定按钮事件 + 初始化 UIManager
///   2. _Process — 帧更新动态 Label（轮询模式）
///   3. _Input   — 快捷键：F5 保存 / F9 加载
///
/// 数据来源：
///   ToolManager.Instance → 工具状态
///   RoadSystem.Instance.Network → 路网统计
///   MainCamera.Instance → 鼠标世界坐标 → 格点计算
///   SaveManager.Instance → 存档/读档
/// </summary>
public partial class GameHUD : CanvasLayer
{
    /// <summary>共享路网配置（与 RoadBuilder / RoadRenderer 同一份）。</summary>
    [Export] public RoadConfig Config { get; set; } = null!;

    // ── .tscn 子控件引用（_Ready 中解析）───────────────────
    private Label _fpsLabel = null!;
    private Label _toolLabel = null!;
    private Label _mouseLabel = null!;
    private Label _statsRoadsLabel = null!;
    private Label _statsSegmentsLabel = null!;
    private Label _statsJunctionsLabel = null!;

    // ── 依赖 ──────────────────────────────────────────────
    private RoadNetwork? _network;
    private ToolManager? _toolManager;

    public override void _Ready()
    {
        _toolManager = ToolManager.Instance;
        _network = RoadSystem.Instance.Network;

        if (Config == null)
        {
            GD.PushError("GameHUD: Config (RoadConfig resource) is not assigned.");
            Config = new RoadConfig();
        }

        EnsureUIManager();
        ResolveChildNodes();
        WireButtons();
    }

    /// <summary>确保 UIManager 单例存在（作为本节点的子节点）。</summary>
    private void EnsureUIManager()
    {
        if (UIManager.Instance != null) return;
        AddChild(new UIManager());
    }

    /// <summary>
    /// 从 GameHUD.tscn 中解析子控件引用。
    /// 节点树结构参见 Scenes/UI/GameHUD.tscn。
    /// </summary>
    private void ResolveChildNodes()
    {
        _fpsLabel = GetNode<Label>("Panel/VBox/FPS");
        _toolLabel = GetNode<Label>("Panel/VBox/Tool");
        _mouseLabel = GetNode<Label>("Panel/VBox/MousePos");
        _statsRoadsLabel = GetNode<Label>("Panel/VBox/Roads");
        _statsSegmentsLabel = GetNode<Label>("Panel/VBox/Segments");
        _statsJunctionsLabel = GetNode<Label>("Panel/VBox/Junctions");
    }

    /// <summary>绑定工具按钮 + 存档/读档按钮点击事件。</summary>
    private void WireButtons()
    {
        // 工具切换按钮
        GetNode<Button>("Panel/VBox/ToolBar/SelectBtn").Pressed += () =>
            _toolManager!.CurrentTool = ToolType.Select;

        GetNode<Button>("Panel/VBox/ToolBar/RoadBtn").Pressed += () =>
            _toolManager!.CurrentTool = ToolType.Road;

        GetNode<Button>("Panel/VBox/ToolBar/RemoveBtn").Pressed += () =>
            _toolManager!.CurrentTool = ToolType.RoadRemove;

        // 存档 / 读档按钮
        GetNode<Button>("Panel/VBox/ToolBar/SaveBtn").Pressed += OnSave;
        GetNode<Button>("Panel/VBox/ToolBar/LoadBtn").Pressed += OnLoad;
    }

    /// <summary>快捷键：F5 保存 / F9 加载。</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed) return;

        if (keyEvent.Keycode == Key.F5)
            OnSave();
        else if (keyEvent.Keycode == Key.F9)
            OnLoad();
    }

    private void OnSave()
    {
        if (SaveManager.Instance.Save("autosave"))
            GD.Print("[GameHUD] 存档成功");
        else
            GD.PushError("[GameHUD] 存档失败");
    }

    private void OnLoad()
    {
        if (SaveManager.Instance.Load("autosave"))
            GD.Print("[GameHUD] 读档成功");
        else
            GD.PushError("[GameHUD] 读档失败：存档不存在或损坏");
    }

    // ═══════════════════════════════════════════════════════
    //  帧更新（轮询模式）
    // ═══════════════════════════════════════════════════════

    public override void _Process(double delta)
    {
        if (_network == null || _toolManager == null) return;

        UpdateFPS();
        UpdateToolInfo();
        UpdateMousePos();
        UpdateRoadStats();
    }

    private void UpdateFPS()
    {
        _fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
    }

    private void UpdateToolInfo()
    {
        _toolLabel.Text = $"工具: {_toolManager!.CurrentTool}";
    }

    private void UpdateMousePos()
    {
        var mouseWorld = MainCamera.Instance.GetGlobalMousePosition();
        var snapped = RoadNetwork.SnapToGrid(mouseWorld, Config.CellSize);
        bool hasJunction = _network!.HasJunctionAt(snapped);
        _mouseLabel.Text = $"鼠标格点: ({snapped.X:F0}, {snapped.Y:F0}) {(hasJunction ? "[路口]" : "")}";
    }

    private void UpdateRoadStats()
    {
        int roadCount = _network!.GetAllRoads().Count();
        int segmentCount = _network.GetAllSegments().Count();
        int junctionCount = _network.GetAllJunctions().Count();

        _statsRoadsLabel.Text = $"道路 (Road):     {roadCount}";
        _statsSegmentsLabel.Text = $"路段 (Segment):  {segmentCount}";
        _statsJunctionsLabel.Text = $"路口 (Junction): {junctionCount}";
    }
}
