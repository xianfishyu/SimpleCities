using Godot;
using System.Linq;

public partial class DebugPanel : PanelContainer
{
    [Export] public RoadConfig? Config { get; set; }

    private Button _toggleButton = null!;
    private VBoxContainer _debugContent = null!;
    private Label _fpsValue = null!;
    private Label _gridValue = null!;
    private Label _roadGroupValue = null!;
    private Label _graphEdgeValue = null!;
    private Label _graphNodeValue = null!;

    private RoadGraph? _network;

    public NodePath ToggleFocusPath => _toggleButton.GetPath();

    public override void _Ready()
    {
        _toggleButton = GetNode<Button>("PanelMargin/Rows/DebugToggleButton");
        _debugContent = GetNode<VBoxContainer>("PanelMargin/Rows/DebugContent");
        _fpsValue = GetNode<Label>("PanelMargin/Rows/DebugContent/FpsRow/FpsValue");
        _gridValue = GetNode<Label>("PanelMargin/Rows/DebugContent/GridRow/GridValue");
        _roadGroupValue = GetNode<Label>("PanelMargin/Rows/DebugContent/RoadGroupRow/RoadGroupValue");
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

    public void SetDependencies(RoadGraph? network, RoadConfig? config)
    {
        _network = network;
        Config = config;
    }

    public void ConfigureFocus(NodePath previousPath, NodePath nextPath)
    {
        _toggleButton.FocusPrevious = previousPath;
        _toggleButton.FocusNext = nextPath;
    }

    public void UpdateMetrics()
    {
        _fpsValue.Text = Engine.GetFramesPerSecond().ToString();
        _gridValue.Text = GridText();

        if (_network == null)
        {
            _roadGroupValue.Text = "--";
            _graphEdgeValue.Text = "--";
            _graphNodeValue.Text = "--";
            return;
        }

        _roadGroupValue.Text = _network.GetAllGroups().Count().ToString();
        _graphEdgeValue.Text = _network.GetAllEdges().Count().ToString();
        _graphNodeValue.Text = _network.GetAllNodes().Count().ToString();
    }

    private void ToggleDebugContent()
    {
        _debugContent.Visible = !_debugContent.Visible;
        _toggleButton.Text = _debugContent.Visible ? "Debug ▲" : "Debug ▼";
    }

    private string GridText()
    {
        if (Config == null || MainCamera.Instance == null || !GodotObject.IsInstanceValid(MainCamera.Instance)) return "--";

        Vector2 mouseWorld = MainCamera.Instance.GetGlobalMousePosition();
        Vector2 snapped = GridSystem.SnapToGrid(mouseWorld);
        bool hasJunction = _network?.FindClosestNode(snapped, Config.CellSize * 0.1f) != null;
        return $"({snapped.X:F0}, {snapped.Y:F0}){(hasJunction ? " [路口]" : string.Empty)}";
    }
}
