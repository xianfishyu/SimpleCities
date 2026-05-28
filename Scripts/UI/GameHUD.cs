using Godot;
using System.Linq;

/// <summary>
/// 主 HUD 浮层 — 常驻显示 FPS、当前工具、鼠标格点、路网统计，
/// 并提供工具切换按钮（选择 / 铺路 / 拆路）。
///
/// 数据流向：
///   ToolManager.Instance → 工具状态
///   RoadSystem.Instance.Network → 路网统计数据
///   MainCamera.Instance → 鼠标世界坐标 → 格点计算
///
/// 生命周期：
///   _Ready  → 构建 UI 控件树 + 初始化 UIManager
///   _Process → 帧更新所有动态 Label（轮询模式，后续可迁移为事件驱动）
/// </summary>
public partial class GameHUD : CanvasLayer
{
    /// <summary>共享路网配置（与 RoadBuilder / RoadRenderer 同一份）。</summary>
    [Export] public RoadConfig Config { get; set; } = null!;

    // ── 子控件引用 ────────────────────────────────────────
    private Label _fpsLabel = null!;
    private Label _toolLabel = null!;
    private Label _mouseLabel = null!;
    private Label _statsRoadsLabel = null!;
    private Label _statsSegmentsLabel = null!;
    private Label _statsJunctionsLabel = null!;

    // ── 依赖注入 ──────────────────────────────────────────
    private RoadNetwork? _network;
    private ToolManager? _toolManager;

    // ── 布局常量 ──────────────────────────────────────────
    private const float PanelX = 10f;
    private const float PanelY = 10f;
    private const float PanelW = 280f;
    private const float PanelH = 250f;
    private const float Pad = 4f;

    public override void _Ready()
    {
        // 解析依赖
        _toolManager = ToolManager.Instance;
        _network = RoadSystem.Instance.Network;

        if (Config == null)
        {
            GD.PushError("GameHUD: Config (RoadConfig resource) is not assigned in the scene.");
            Config = new RoadConfig();
        }

        // 初始化 UIManager（全局 UI 面板管理器）
        EnsureUIManager();

        // 构建 UI
        BuildUI();
    }

    /// <summary>确保 UIManager 单例存在（作为本节点的子节点）。</summary>
    private void EnsureUIManager()
    {
        if (UIManager.Instance != null) return;
        AddChild(new UIManager());
    }

    // ═══════════════════════════════════════════════════════
    //  UI 构建（一次性）
    // ═══════════════════════════════════════════════════════

    private void BuildUI()
    {
        var panel = UIHelpers.CreateDarkPanel(
            new Vector2(PanelX, PanelY),
            new Vector2(PanelW, PanelH));
        AddChild(panel);

        var vbox = new VBoxContainer
        {
            Position = new Vector2(PanelX + Pad, PanelY + Pad),
            Size = new Vector2(PanelW - Pad * 2, PanelH - Pad * 2)
        };
        panel.AddChild(vbox);

        BuildInfoSection(vbox);
        vbox.AddChild(new HSeparator());
        BuildStatsSection(vbox);
        vbox.AddChild(UIHelpers.CreateLabel("")); // spacer
        BuildToolBar(vbox);
    }

    private void BuildInfoSection(VBoxContainer parent)
    {
        _fpsLabel = UIHelpers.CreateLabel("FPS: --");
        _toolLabel = UIHelpers.CreateLabel("工具: 选择");
        _mouseLabel = UIHelpers.CreateLabel("鼠标格点: --");

        parent.AddChild(_fpsLabel);
        parent.AddChild(_toolLabel);
        parent.AddChild(_mouseLabel);
    }

    private void BuildStatsSection(VBoxContainer parent)
    {
        _statsRoadsLabel = UIHelpers.CreateLabel("道路: 0");
        _statsSegmentsLabel = UIHelpers.CreateLabel("路段: 0");
        _statsJunctionsLabel = UIHelpers.CreateLabel("路口: 0");

        parent.AddChild(_statsRoadsLabel);
        parent.AddChild(_statsSegmentsLabel);
        parent.AddChild(_statsJunctionsLabel);
    }

    private void BuildToolBar(VBoxContainer parent)
    {
        var btnRow = new HBoxContainer();
        parent.AddChild(btnRow);

        btnRow.AddChild(UIHelpers.CreateToolButton("选择(Esc)", ToolType.Select, OnToolSelected));
        btnRow.AddChild(UIHelpers.CreateToolButton("铺路(R)", ToolType.Road, OnToolSelected));
        btnRow.AddChild(UIHelpers.CreateToolButton("拆路(E)", ToolType.RoadRemove, OnToolSelected));
    }

    private void OnToolSelected(ToolType tool)
    {
        if (_toolManager != null)
            _toolManager.CurrentTool = tool;
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
