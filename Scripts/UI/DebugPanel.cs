using Godot;

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
    private Label _nodeValue = null!;
    private Label _canonicalEdgeValue = null!;
    private Label _geometrySegmentValue = null!;
    private Label _queryFragmentValue = null!;
    private Label _selfLoopValue = null!;

    private RoadGraph? _network;
    private long _lastDiagnosticsSequence = -1;

    public NodePath ToggleFocusPath => _toggleButton.GetPath();

    public override void _Ready()
    {
        _toggleButton = GetNode<Button>("PanelMargin/Rows/DebugToggleButton");
        _debugContent = GetNode<VBoxContainer>("PanelMargin/Rows/DebugContent");
        _fpsValue = GetNode<Label>("PanelMargin/Rows/DebugContent/FpsRow/FpsValue");
        _gridValue = GetNode<Label>("PanelMargin/Rows/DebugContent/GridRow/GridValue");
        _nodeValue = GetNode<Label>("PanelMargin/Rows/DebugContent/NodeRow/NodeValue");
        _canonicalEdgeValue = GetNode<Label>("PanelMargin/Rows/DebugContent/CanonicalEdgeRow/CanonicalEdgeValue");
        _geometrySegmentValue = GetNode<Label>("PanelMargin/Rows/DebugContent/GeometrySegmentRow/GeometrySegmentValue");
        _queryFragmentValue = GetNode<Label>("PanelMargin/Rows/DebugContent/QueryFragmentRow/QueryFragmentValue");
        _selfLoopValue = GetNode<Label>("PanelMargin/Rows/DebugContent/SelfLoopRow/SelfLoopValue");

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
        _lastDiagnosticsSequence = -1;
    }

    public void ConfigureFocus(NodePath previousPath, NodePath nextPath)
    {
        _toggleButton.FocusPrevious = previousPath;
        _toggleButton.FocusNext = nextPath;
    }

    /// <summary>由 GameHUD 每帧调用；图指标只读取已提交的不可变快照。</summary>
    public void UpdateMetrics()
    {
        _fpsValue.Text = Engine.GetFramesPerSecond().ToString();

        if (!_debugContent.Visible)
            return;

        _gridValue.Text = GridText();
        RefreshDiagnostics();
    }

    private void ToggleDebugContent()
    {
        _debugContent.Visible = !_debugContent.Visible;
        _toggleButton.Text = _debugContent.Visible ? "Debug ▲" : "Debug ▼";
        if (_debugContent.Visible)
        {
            _lastDiagnosticsSequence = -1;
            RefreshDiagnostics();
        }
    }

    private void RefreshDiagnostics()
    {
        RoadGraphDiagnosticsSnapshot? snapshot = _network?.CaptureDiagnosticsSnapshot();
        if (snapshot == null)
        {
            SetDiagnosticsUnavailable();
            return;
        }

        if (snapshot.ChangeSequence == _lastDiagnosticsSequence)
            return;

        _lastDiagnosticsSequence = snapshot.ChangeSequence;
        _nodeValue.Text = snapshot.NodeCount.ToString();
        _canonicalEdgeValue.Text = snapshot.CanonicalEdgeCount.ToString();
        _geometrySegmentValue.Text = snapshot.GeometrySegmentCount.ToString();
        _queryFragmentValue.Text = snapshot.QueryFragmentCount.ToString();
        _selfLoopValue.Text = snapshot.SelfLoopCount.ToString();
    }

    private void SetDiagnosticsUnavailable()
    {
        _lastDiagnosticsSequence = -1;
        _nodeValue.Text = "--";
        _canonicalEdgeValue.Text = "--";
        _geometrySegmentValue.Text = "--";
        _queryFragmentValue.Text = "--";
        _selfLoopValue.Text = "--";
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
