using Godot;
using SimpleCities.Road.V3;
using System;
using System.Linq;

/// <summary>
/// 左上角的运行时诊断面板，展示帧率、鼠标网格位置和当前路网的规模统计。
/// </summary>
public partial class DebugPanel : PanelContainer
{
    [Export] public RoadConfig? Config { get; set; }

    private Button _toggleButton = null!;
    private VBoxContainer _debugContent = null!;
    private Label _fpsValue = null!;
    private Label _gridValue = null!;
    private Label _geometryValue = null!;
    private Label _selfLoopValue = null!;
    private Label _parallelValue = null!;
    private Label _graphEdgeValue = null!;
    private Label _graphNodeValue = null!;

    private RoadGraph? _network;
    private Func<RoadGraphV3Diagnostics?>? _v3DiagnosticsProvider;

    public NodePath ToggleFocusPath => _toggleButton.GetPath();

    public override void _Ready()
    {
        _toggleButton = GetNode<Button>("PanelMargin/Rows/DebugToggleButton");
        _debugContent = GetNode<VBoxContainer>("PanelMargin/Rows/DebugContent");
        _fpsValue = GetNode<Label>("PanelMargin/Rows/DebugContent/FpsRow/FpsValue");
        _gridValue = GetNode<Label>("PanelMargin/Rows/DebugContent/GridRow/GridValue");
        _geometryValue = GetNode<Label>("PanelMargin/Rows/DebugContent/GeometryRow/GeometryValue");
        _selfLoopValue = GetNode<Label>("PanelMargin/Rows/DebugContent/SelfLoopRow/SelfLoopValue");
        _parallelValue = GetNode<Label>("PanelMargin/Rows/DebugContent/ParallelRow/ParallelValue");
        _graphEdgeValue = GetNode<Label>("PanelMargin/Rows/DebugContent/GraphEdgeRow/GraphEdgeValue");
        _graphNodeValue = GetNode<Label>("PanelMargin/Rows/DebugContent/GraphNodeRow/GraphNodeValue");

        _debugContent.Visible = false;
        _toggleButton.FocusMode = FocusModeEnum.All;
        _toggleButton.Pressed += ToggleDebugContent;
    }

    public override void _ExitTree()
    {
        if (_toggleButton != null)
            _toggleButton.Pressed -= ToggleDebugContent;
    }

    /// <summary>注入 HUD 已解析的路网和配置，避免面板自行查找场景节点。</summary>
    public void SetDependencies(RoadGraph? network, RoadConfig? config)
    {
        _network = network;
        Config = config;
    }

    /// <summary>注入 V3 诊断快照提供器；存在时面板优先读取 O(1) 快照。</summary>
    public void SetDiagnosticsProvider(Func<RoadGraphV3Diagnostics?> provider)
    {
        _v3DiagnosticsProvider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public void ConfigureFocus(NodePath previousPath, NodePath nextPath)
    {
        _toggleButton.FocusPrevious = previousPath;
        _toggleButton.FocusNext = nextPath;
    }

    /// <summary>由 GameHUD 每帧调用，刷新轻量且可直接读取的调试指标。</summary>
    public void UpdateMetrics()
    {
        _fpsValue.Text = Engine.GetFramesPerSecond().ToString();
        _gridValue.Text = GridText();

        if (_v3DiagnosticsProvider?.Invoke() is { } diagnostics)
        {
            _graphNodeValue.Text = diagnostics.NodeCount.ToString();
            _graphEdgeValue.Text = diagnostics.EdgeCount.ToString();
            _geometryValue.Text = diagnostics.GeometrySegmentCount.ToString();
            _selfLoopValue.Text = diagnostics.SelfLoopCount.ToString();
            _parallelValue.Text = diagnostics.ParallelEdgeCount.ToString();
            return;
        }

        if (_network == null)
        {
            _graphEdgeValue.Text = "--";
            _graphNodeValue.Text = "--";
            _geometryValue.Text = "--";
            _selfLoopValue.Text = "--";
            _parallelValue.Text = "--";
            return;
        }

        _graphEdgeValue.Text = _network.GetAllEdges().Count().ToString();
        _graphNodeValue.Text = _network.GetAllNodes().Count().ToString();
        _geometryValue.Text = "--";
        _selfLoopValue.Text = "--";
        _parallelValue.Text = "--";
    }

    private void ToggleDebugContent()
    {
        _debugContent.Visible = !_debugContent.Visible;
        _toggleButton.Text = _debugContent.Visible ? "Debug ▲" : "Debug ▼";
    }

    /// <summary>将鼠标世界坐标吸附到网格，并标记该位置是否已有路口节点。</summary>
    private string GridText()
    {
        if (Config == null || MainCamera.Instance == null || !GodotObject.IsInstanceValid(MainCamera.Instance)) return "--";

        Vector2 mouseWorld = MainCamera.Instance.GetGlobalMousePosition();
        if (!mouseWorld.IsFinite()) return "--";
        Vector2 snapped = GridSystem.SnapToGrid(mouseWorld);
        if (!snapped.IsFinite()) return "--";
        bool hasJunction = _network?.FindClosestNode(snapped, Config.CellSize * 0.1f) != null;
        return $"({snapped.X:F0}, {snapped.Y:F0}){(hasJunction ? " [路口]" : string.Empty)}";
    }
}
