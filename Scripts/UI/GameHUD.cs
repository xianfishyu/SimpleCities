using Godot;
using System.Linq;

public partial class GameHUD : CanvasLayer
{
    /// <summary>
    /// 共享配置（与 RoadBuilder / RoadRenderer 同一份）。
    /// 需在场景里把 Scenes/road_config.tres 拖到这个槽，否则会用默认 64 像素 cellSize。
    /// </summary>
    [Export] public RoadConfig Config { get; set; } = null!;

    private Label _fpsLabel = null!;
    private Label _toolLabel = null!;
    private Label _mouseLabel = null!;
    private Label _statsRoadsLabel = null!;
    private Label _statsSegmentsLabel = null!;
    private Label _statsJunctionsLabel = null!;

    private RoadNetwork? _network;
    private ToolManager? _toolManager;

    public override void _Ready()
    {
        _toolManager = ToolManager.Instance;
        _network = RoadSystem.Instance.Network;
        if (Config == null)
        {
            GD.PushError("GameHUD: Config (RoadConfig resource) is not assigned in the scene.");
            Config = new RoadConfig();
        }
        BuildUI();
    }

    private void BuildUI()
    {
        var panel = new Panel
        {
            Position = new Vector2(10, 10),
            Size = new Vector2(280, 250),
            SelfModulate = new Color(0.08f, 0.08f, 0.08f, 0.88f)
        };
        AddChild(panel);

        var vbox = new VBoxContainer
        {
            Position = new Vector2(14, 14),
            Size = new Vector2(252, 222)
        };
        panel.AddChild(vbox);

        _fpsLabel = CreateLabel("FPS: --");
        _toolLabel = CreateLabel("工具: 选择");
        _mouseLabel = CreateLabel("鼠标格点: --");
        vbox.AddChild(_fpsLabel);
        vbox.AddChild(_toolLabel);
        vbox.AddChild(_mouseLabel);

        vbox.AddChild(new HSeparator());

        // 三层统计：道路（玩家一次画线 = 连续路径） / 路段（节点间的边） / 路口（节点）
        _statsRoadsLabel     = CreateLabel("道路: 0");
        _statsSegmentsLabel  = CreateLabel("路段: 0");
        _statsJunctionsLabel = CreateLabel("路口: 0");
        vbox.AddChild(_statsRoadsLabel);
        vbox.AddChild(_statsSegmentsLabel);
        vbox.AddChild(_statsJunctionsLabel);

        vbox.AddChild(CreateLabel("")); // spacer

        var btnRow = new HBoxContainer();
        vbox.AddChild(btnRow);

        var selectBtn = CreateToolButton("选择(Esc)", ToolType.Select);
        var roadBtn = CreateToolButton("铺路(R)", ToolType.Road);
        var removeBtn = CreateToolButton("拆路(E)", ToolType.RoadRemove);

        btnRow.AddChild(selectBtn);
        btnRow.AddChild(roadBtn);
        btnRow.AddChild(removeBtn);
    }

    private static Label CreateLabel(string text)
    {
        var label = new Label
        {
            Text = text,
        };
        label.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
        label.AddThemeFontSizeOverride("font_size", 13);
        return label;
    }

    private Button CreateToolButton(string text, ToolType tool)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(64, 28),
        };
        btn.AddThemeFontSizeOverride("font_size", 12);

        btn.Pressed += () =>
        {
            if (_toolManager != null)
                _toolManager.CurrentTool = tool;
        };
        return btn;
    }

    public override void _Process(double delta)
    {
        if (_network == null || _toolManager == null) return;

        _fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
        _toolLabel.Text = $"工具: {_toolManager.CurrentTool}";

        var mouseWorld = MainCamera.Instance.GetGlobalMousePosition();
        var snapped = RoadNetwork.SnapToGrid(mouseWorld, Config.CellSize);
        bool hasJunction = _network.HasJunctionAt(snapped);
        _mouseLabel.Text = $"鼠标格点: ({snapped.X:F0}, {snapped.Y:F0}) {(hasJunction ? "[路口]" : "")}";

        // 道路：玩家"一次画线"产生的连续路径（含被穿插劈分后仍是同一条 Road）
        // 路段：节点间的几何边（一条 Road 含 N≥1 个 Segment）
        // 路口：节点（含端点 + 真路口 + 半格交点 Junction）
        int roadCount     = _network.GetAllRoads().Count();
        int segmentCount  = _network.GetAllSegments().Count();
        int junctionCount = _network.GetAllJunctions().Count();

        _statsRoadsLabel.Text     = $"道路 (Road):     {roadCount}";
        _statsSegmentsLabel.Text  = $"路段 (Segment):  {segmentCount}";
        _statsJunctionsLabel.Text = $"路口 (Junction): {junctionCount}";
    }
}
