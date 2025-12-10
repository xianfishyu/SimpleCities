using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImGuiNET;
using System.Numerics;

/// <summary>
/// 铁路网络编辑器 - 使用管理器类解耦
/// </summary>
public partial class RailwayNetworkEditor : Node2D
{
    public static RailwayNetworkEditor Instance { get; private set; }

    [ExportGroup("File Settings")]
    [Export] public string NetworkPath = "res://Railwaydata/Networks/default.json";
    [Export] public bool AutoCreateTrains = true;

    [ExportGroup("Grid Settings")]
    [Export] public float GridSize = 10f;
    [Export] public bool SnapToGrid = true;
    [Export] public bool ShowGrid = true;
    [Export] public Color GridColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);

    [ExportGroup("Visual Settings")]
    [Export] public float NodeRadius = 4f;
    [Export] public float TrackWidth = 2f;
    [Export] public float StationHandleSize = 6f;

    [ExportGroup("Track Colors")]
    [Export] public Color MainLineColor = new Color(0.1f, 0.1f, 0.5f);
    [Export] public Color ArrivalDepartureColor = new Color(0.2f, 0.2f, 0.2f);
    [Export] public Color MainLineWithPlatformColor = new Color(0.3f, 0.1f, 0.5f);
    [Export] public Color CrossoverColor = new Color(0.1f, 0.4f, 0.1f);

    [ExportGroup("Node Colors")]
    [Export] public Color EndpointColor = new Color(1f, 0.2f, 0.2f);
    [Export] public Color ConnectionColor = Colors.SkyBlue;
    [Export] public Color SwitchColor = new Color(0.8f, 0.2f, 0.8f);
    [Export] public Color SelectedColor = new Color(1f, 1f, 0f);
    [Export] public Color HoverColor = new Color(0.5f, 1f, 0.5f);

    [ExportGroup("Station Colors")]
    [Export] public Color StationBorderColor = new Color(0.8f, 0.6f, 0.2f, 0.8f);
    [Export] public Color StationLabelColor = new Color(1.637f, 1.241f, 0.436f);
    [Export] public Color StationHandleColor = new Color(1f, 0.8f, 0.2f, 1f);

    // 网络数据
    private RailwayNetwork network;

    // 管理器
    private EditorConfig config;
    private EditorState state;
    private NodeEdgeVisualsManager nodeEdgeVisuals;
    private StationVisualsManager stationVisuals;

    // 预览线
    private Line2D previewLine;
    private ColorRect stationPreviewRect;

    // 时刻表和列车
    private List<TrainSchedule> schedules = new();
    private List<Train> trains = new();
    private Dictionary<string, List<ColorRect>> trainCarriages = new();  // 每列火车的车厢列表（独立节点）
    private Dictionary<string, Line2D> trainPathLines = new();  // 每列火车的路径可视化

    // 列车可视化参数
    private const int CarriageCount = 4;
    private const float CarriageLength = 25f;
    private const float CarriageWidth = 5f;
    private const float CarriageGap = 2f;
    private const float TotalTrainLength = CarriageCount * CarriageLength + (CarriageCount - 1) * CarriageGap;

    // 寻路服务
    private PathfindingService pathfindingService;

    public override void _Ready()
    {
        // 单例模式
        if (Instance != null)
        {
            QueueFree();
            return;
        }
        Instance = this;

        // 初始化配置
        config = CreateConfig();
        state = new EditorState();

        // 加载网络
        LoadNetwork();

        // 初始化寻路服务
        pathfindingService = new PathfindingService(network);

        // 初始化管理器
        nodeEdgeVisuals = new NodeEdgeVisualsManager(this, config, state, network);
        stationVisuals = new StationVisualsManager(this, config, state, network);

        // 渲染网络
        RenderNetwork();

        // 创建预览线
        previewLine = new Line2D();
        previewLine.Width = TrackWidth;
        previewLine.DefaultColor = new Color(1, 1, 1, 0.5f);
        previewLine.ZIndex = 100;
        AddChild(previewLine);

        // 创建站场预览矩形
        stationPreviewRect = new ColorRect();
        stationPreviewRect.Color = new Color(StationBorderColor.R, StationBorderColor.G, StationBorderColor.B, 0.15f);
        stationPreviewRect.Visible = false;
        stationPreviewRect.ZIndex = -10;
        AddChild(stationPreviewRect);

        // 加载时刻表
        LoadAllSchedules();

        // 自动创建列车
        if (AutoCreateTrains && schedules.Count > 0)
        {
            CreateAllTrainsFromSchedules(true);
        }
    }

    private EditorConfig CreateConfig()
    {
        return new EditorConfig
        {
            GridSize = GridSize,
            SnapToGrid = SnapToGrid,
            ShowGrid = ShowGrid,
            GridColor = GridColor,
            NodeRadius = NodeRadius,
            TrackWidth = TrackWidth,
            StationHandleSize = StationHandleSize,
            MainLineColor = MainLineColor,
            ArrivalDepartureColor = ArrivalDepartureColor,
            MainLineWithPlatformColor = MainLineWithPlatformColor,
            CrossoverColor = CrossoverColor,
            EndpointColor = EndpointColor,
            ConnectionColor = ConnectionColor,
            SwitchColor = SwitchColor,
            SelectedColor = SelectedColor,
            HoverColor = HoverColor,
            StationBorderColor = StationBorderColor,
            StationLabelColor = StationLabelColor,
            StationHandleColor = StationHandleColor
        };
    }

    public override void _Process(double delta)
    {
        UpdateHover();
        UpdatePreviewLine();
        
        if (state.ShowCollisionBoxes)
            nodeEdgeVisuals.UpdateCollisionBoxes();

        // 更新列车
        UpdateTrains((float)delta);
    }

    public override void _Input(InputEvent @event)
    {
        if (ImGui.GetIO().WantCaptureMouse && @event is InputEventMouse) return;
        if (ImGui.GetIO().WantCaptureKeyboard && @event is InputEventKey) return;

        Godot.Vector2 rawMousePos = GetGlobalMousePosition();
        Godot.Vector2 snappedMousePos = config.SnapToGrid ? config.SnapPosition(rawMousePos) : rawMousePos;

        // 鼠标移动 - 拖拽节点
        if (@event is InputEventMouseMotion && state.IsDragging && state.SelectedNodeId != null)
        {
            var node = network.GetNode(state.SelectedNodeId);
            if (node != null)
            {
                node.X = snappedMousePos.X;
                node.Y = snappedMousePos.Y;
                nodeEdgeVisuals.UpdateNodeVisual(node);
                nodeEdgeVisuals.UpdateConnectedEdgeVisuals(node.Id);
            }
        }

        // 鼠标移动 - 拖拽站场手柄
        if (@event is InputEventMouseMotion && state.DraggingStationHandle >= 0 && state.DraggingStationId != null)
        {
            stationVisuals.DragStationHandle(state.DraggingStationId, state.DraggingStationHandle, snappedMousePos);
        }

        // 鼠标移动 - 更新站场预览
        if (@event is InputEventMouseMotion && state.IsDrawingStation)
        {
            var currentPos = snappedMousePos;
            var minX = Mathf.Min(state.StationDrawStart.X, currentPos.X);
            var minY = Mathf.Min(state.StationDrawStart.Y, currentPos.Y);
            var maxX = Mathf.Max(state.StationDrawStart.X, currentPos.X);
            var maxY = Mathf.Max(state.StationDrawStart.Y, currentPos.Y);
            stationPreviewRect.Position = new Godot.Vector2(minX, minY);
            stationPreviewRect.Size = new Godot.Vector2(maxX - minX, maxY - minY);
        }

        // 鼠标按下
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                    HandleLeftMouseDown(rawMousePos, snappedMousePos);
                else
                    HandleLeftMouseUp();
            }
            else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
            {
                HandleRightMouseDown();
            }
        }

        // 键盘快捷键
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
            HandleKeyPress(keyEvent);
    }

    #region Input Handling

    private void HandleLeftMouseDown(Godot.Vector2 rawPos, Godot.Vector2 snappedPos)
    {
        switch (state.CurrentMode)
        {
            case EditMode.Select:
                HandleSelectModeClick(rawPos);
                break;
            case EditMode.DrawTrack:
                HandleDrawTrackModeClick(rawPos, snappedPos);
                break;
            case EditMode.DrawCrossover:
                HandleDrawCrossoverModeClick(rawPos);
                break;
            case EditMode.DrawStation:
                state.IsDrawingStation = true;
                state.StationDrawStart = snappedPos;
                stationPreviewRect.Position = snappedPos;
                stationPreviewRect.Size = Godot.Vector2.Zero;
                stationPreviewRect.Visible = true;
                break;
        }
    }

    private void HandleSelectModeClick(Godot.Vector2 rawPos)
    {
        // 先检查站场手柄
        if (state.ShowStationBorders && state.SelectedStationId != null)
        {
            int handleIndex;
            var stationId = stationVisuals.FindStationAtPosition(rawPos, out handleIndex);
            if (stationId == state.SelectedStationId && handleIndex >= 0)
            {
                state.DraggingStationHandle = handleIndex;
                state.DraggingStationId = state.SelectedStationId;
                return;
            }
        }

        // 选择节点
        var clickedNode = nodeEdgeVisuals.FindNodeAtPosition(rawPos);
        if (clickedNode != null)
        {
            state.SelectedNodeId = clickedNode;
            state.SelectedEdgeId = null;
            state.SelectedStationId = null;
            state.IsDragging = true;
            UpdateSelectionVisuals();
            return;
        }

        // 选择边
        var clickedEdge = nodeEdgeVisuals.FindEdgeAtPosition(rawPos);
        if (clickedEdge != null)
        {
            state.SelectedEdgeId = clickedEdge;
            state.SelectedNodeId = null;
            state.SelectedStationId = null;
            UpdateSelectionVisuals();
            return;
        }

        // 选择站场
        int _;
        var clickedStation = stationVisuals.FindStationAtPosition(rawPos, out _);
        if (clickedStation != null)
        {
            state.SelectedStationId = clickedStation;
            state.SelectedNodeId = null;
            state.SelectedEdgeId = null;
            UpdateSelectionVisuals();
            return;
        }

        // 清除选择
        state.ClearSelection();
        UpdateSelectionVisuals();
    }

    private void HandleDrawTrackModeClick(Godot.Vector2 rawPos, Godot.Vector2 snappedPos)
    {
        var existingNode = nodeEdgeVisuals.FindNodeAtPosition(rawPos);
        if (existingNode != null)
        {
            if (state.DrawStartNodeId == null)
            {
                state.DrawStartNodeId = existingNode;
            }
            else if (existingNode != state.DrawStartNodeId)
            {
                CreateEdge(state.DrawStartNodeId, existingNode);
                state.DrawStartNodeId = existingNode;
            }
            return;
        }

        var edgeOnClick = nodeEdgeVisuals.FindEdgeAtPosition(rawPos);
        if (edgeOnClick != null)
        {
            InsertNodeOnEdge(edgeOnClick, snappedPos);
            return;
        }

        // 创建新节点
        var newNode = network.AddNode(snappedPos.X, snappedPos.Y, RailwayNodeType.Connection);
        nodeEdgeVisuals.RenderNode(newNode);

        if (state.DrawStartNodeId != null)
            CreateEdge(state.DrawStartNodeId, newNode.Id);
        state.DrawStartNodeId = newNode.Id;
    }

    private void HandleDrawCrossoverModeClick(Godot.Vector2 rawPos)
    {
        var nodeForCrossover = nodeEdgeVisuals.FindNodeAtPosition(rawPos);
        if (nodeForCrossover == null) return;

        if (state.DrawStartNodeId == null)
        {
            state.DrawStartNodeId = nodeForCrossover;
        }
        else if (nodeForCrossover != state.DrawStartNodeId)
        {
            var edge = network.AddEdge(state.DrawStartNodeId, nodeForCrossover);
            if (edge != null)
            {
                edge.TrackType = TrackType.Crossover;
                nodeEdgeVisuals.RenderEdge(edge);
            }
            state.DrawStartNodeId = null;
        }
    }

    private void HandleLeftMouseUp()
    {
        state.IsDragging = false;

        if (state.DraggingStationHandle >= 0)
        {
            state.DraggingStationHandle = -1;
            state.DraggingStationId = null;
        }

        if (state.IsDrawingStation && state.CurrentMode == EditMode.DrawStation)
        {
            state.IsDrawingStation = false;
            stationPreviewRect.Visible = false;

            var size = stationPreviewRect.Size;
            if (size.X > 10 && size.Y > 10)
            {
                var pos = stationPreviewRect.Position;
                var station = network.AddStation(
                    $"Station_{network.Stations.Count + 1}",
                    pos.X, pos.Y, size.X, size.Y
                );
                stationVisuals.RenderStation(station);
            }
        }
    }

    private void HandleRightMouseDown()
    {
        state.ResetDrawingState();
        state.ClearSelection();
        stationPreviewRect.Visible = false;
        UpdateSelectionVisuals();
    }

    private void HandleKeyPress(InputEventKey keyEvent)
    {
        if (keyEvent.Keycode == Key.Key1) state.CurrentMode = EditMode.Select;
        else if (keyEvent.Keycode == Key.Key2) state.CurrentMode = EditMode.DrawTrack;
        else if (keyEvent.Keycode == Key.Key3) state.CurrentMode = EditMode.DrawCrossover;
        else if (keyEvent.Keycode == Key.Key4) state.CurrentMode = EditMode.DrawStation;
        else if (keyEvent.Keycode == Key.Q) state.CurrentTrackType = TrackType.MainLine;
        else if (keyEvent.Keycode == Key.W) state.CurrentTrackType = TrackType.ArrivalDeparture;
        else if (keyEvent.Keycode == Key.E) state.CurrentTrackType = TrackType.MainLineWithPlatform;
        else if (keyEvent.Keycode == Key.R) state.CurrentTrackType = TrackType.Crossover;
        else if (keyEvent.Keycode == Key.Delete)
        {
            if (state.SelectedNodeId != null)
                DeleteNode(state.SelectedNodeId);
            else if (state.SelectedEdgeId != null)
                DeleteEdge(state.SelectedEdgeId);
        }
        else if (keyEvent.Keycode == Key.S && keyEvent.CtrlPressed)
            SaveNetwork();
        else if (keyEvent.Keycode == Key.G)
            config.SnapToGrid = !config.SnapToGrid;
        else if (keyEvent.Keycode == Key.Escape)
        {
            state.DrawStartNodeId = null;
            state.CurrentMode = EditMode.Select;
        }
    }

    #endregion

    #region Operations

    private void CreateEdge(string fromId, string toId)
    {
        var edge = network.AddEdge(fromId, toId);
        if (edge != null)
        {
            edge.TrackType = state.CurrentTrackType;
            nodeEdgeVisuals.RenderEdge(edge);
        }
    }

    private void InsertNodeOnEdge(string edgeId, Godot.Vector2 newNodePos)
    {
        var edge = network.GetEdge(edgeId);
        if (edge == null) return;

        var newNode = network.AddNode(newNodePos.X, newNodePos.Y, RailwayNodeType.Connection);
        nodeEdgeVisuals.RenderNode(newNode);
        nodeEdgeVisuals.RemoveEdgeVisual(edgeId);

        var fromNodeId = edge.FromNode;
        var toNodeId = edge.ToNode;
        var trackType = edge.TrackType;
        var trackNumber = edge.TrackNumber;

        network.RemoveEdge(edgeId);

        var edge1 = network.AddEdge(fromNodeId, newNode.Id);
        if (edge1 != null) { edge1.TrackType = trackType; edge1.TrackNumber = trackNumber; nodeEdgeVisuals.RenderEdge(edge1); }

        var edge2 = network.AddEdge(newNode.Id, toNodeId);
        if (edge2 != null) { edge2.TrackType = trackType; edge2.TrackNumber = trackNumber; nodeEdgeVisuals.RenderEdge(edge2); }

        state.DrawStartNodeId = newNode.Id;
    }

    private void DeleteNode(string nodeId)
    {
        var connectedEdges = new List<string>(network.GetConnectedEdges(nodeId));
        foreach (var edgeId in connectedEdges)
            nodeEdgeVisuals.RemoveEdgeVisual(edgeId);
        nodeEdgeVisuals.RemoveNodeVisual(nodeId);
        network.RemoveNode(nodeId);
        state.SelectedNodeId = null;
        state.DrawStartNodeId = null;
    }

    private void DeleteEdge(string edgeId)
    {
        nodeEdgeVisuals.RemoveEdgeVisual(edgeId);
        network.RemoveEdge(edgeId);
        state.SelectedEdgeId = null;
    }

    #endregion

    #region Rendering

    private void LoadNetwork()
    {
        try
        {
            network = RailwayNetwork.LoadFromFile(NetworkPath);
            GD.Print($"Loaded network: {network.Name}");
        }
        catch
        {
            GD.Print("Creating new network");
            network = new RailwayNetwork { Name = "New Network" };
        }
    }

    private void SaveNetwork()
    {
        network.SaveToFile(NetworkPath);
        GD.Print($"Saved network to: {NetworkPath}");
    }

    private void RenderNetwork()
    {
        stationVisuals.RenderAll();
        nodeEdgeVisuals.RenderAll();
    }

    private void UpdateHover()
    {
        var mousePos = GetGlobalMousePosition();
        state.HoveredNodeId = nodeEdgeVisuals.FindNodeAtPosition(mousePos);
        if (state.HoveredNodeId == null)
        {
            state.HoveredEdgeId = nodeEdgeVisuals.FindEdgeAtPosition(mousePos);
            if (state.HoveredEdgeId == null)
            {
                int _;
                state.HoveredStationId = stationVisuals.FindStationAtPosition(mousePos, out _);
            }
            else
                state.HoveredStationId = null;
        }
        else
        {
            state.HoveredEdgeId = null;
            state.HoveredStationId = null;
        }
    }

    private void UpdatePreviewLine()
    {
        previewLine.ClearPoints();
        if (state.DrawStartNodeId != null && (state.CurrentMode == EditMode.DrawTrack || state.CurrentMode == EditMode.DrawCrossover))
        {
            var startNode = network.GetNode(state.DrawStartNodeId);
            if (startNode != null)
            {
                var mousePos = config.SnapToGrid ? config.SnapPosition(GetGlobalMousePosition()) : GetGlobalMousePosition();
                previewLine.AddPoint(startNode.Position);
                previewLine.AddPoint(mousePos);
                previewLine.DefaultColor = new Color(config.GetTrackColor(state.CurrentTrackType).Lightened(0.3f), 0.5f);
            }
        }
    }

    private void UpdateSelectionVisuals()
    {
        nodeEdgeVisuals.UpdateSelectionVisuals();
        stationVisuals.UpdateAllStationVisuals();
    }

    private void ClearVisuals()
    {
        nodeEdgeVisuals.Clear();
        stationVisuals.Clear();
    }

    #endregion

    #region ImGui

    [DebugGUI("Railway Editor", Opening = false)]
    public static void RenderEditorGUI()
    {
        var editor = Instance;
        if (editor == null) return;

        ImGui.Text($"Network: {editor.network?.Name ?? "None"}");
        ImGui.Text($"Nodes: {editor.network?.Nodes.Count ?? 0} | Edges: {editor.network?.Edges.Count ?? 0} | Stations: {editor.network?.Stations.Count ?? 0}");
        ImGui.Separator();

        // 编辑模式
        ImGui.Text("Edit Mode:");
        int modeIndex = (int)editor.state.CurrentMode;
        string[] modeNames = { "Select [1]", "Draw Track [2]", "Draw Crossover [3]", "Draw Station [4]" };
        for (int i = 0; i < modeNames.Length; i++)
        {
            if (ImGui.RadioButton(modeNames[i], modeIndex == i)) editor.state.CurrentMode = (EditMode)i;
            if (i < modeNames.Length - 1) ImGui.SameLine();
        }

        ImGui.Separator();

        // 轨道类型
        ImGui.Text("Track Type:");
        string[] trackTypeNames = { "Main Line [Q]", "Arrival/Departure [W]", "Main+Platform [E]", "Crossover [R]" };
        int trackTypeIndex = (int)editor.state.CurrentTrackType;
        if (ImGui.Combo("##TrackType", ref trackTypeIndex, trackTypeNames, trackTypeNames.Length))
            editor.state.CurrentTrackType = (TrackType)trackTypeIndex;

        ImGui.Separator();

        // 网格设置
        bool snapToGrid = editor.config.SnapToGrid;
        ImGui.Checkbox("Snap to Grid [G]", ref snapToGrid);
        editor.config.SnapToGrid = snapToGrid;

        ImGui.Separator();

        // 调试选项
        bool showCollisionBoxes = editor.state.ShowCollisionBoxes;
        ImGui.Checkbox("Show Collision Boxes", ref showCollisionBoxes);
        editor.state.ShowCollisionBoxes = showCollisionBoxes;

        bool showStationBorders = editor.state.ShowStationBorders;
        if (ImGui.Checkbox("Show Station Borders", ref showStationBorders))
        {
            editor.state.ShowStationBorders = showStationBorders;
            editor.stationVisuals.UpdateAllStationVisuals();
        }

        // 鼠标位置
        var mousePos = editor.GetGlobalMousePosition();
        ImGui.Text($"Mouse: ({mousePos.X:F0}, {mousePos.Y:F0})");
        ImGui.Text($"Hovered: {editor.state.HoveredNodeId ?? editor.state.HoveredEdgeId ?? editor.state.HoveredStationId ?? "None"}");

        ImGui.Separator();

        // 选中对象属性
        editor.RenderSelectedObjectProperties();

        ImGui.Separator();

        // 文件操作
        if (ImGui.Button("Save [Ctrl+S]")) editor.SaveNetwork();
        ImGui.SameLine();
        if (ImGui.Button("Reload"))
        {
            editor.LoadNetwork();
            editor.nodeEdgeVisuals = new NodeEdgeVisualsManager(editor, editor.config, editor.state, editor.network);
            editor.stationVisuals = new StationVisualsManager(editor, editor.config, editor.state, editor.network);
            editor.ClearVisuals();
            editor.RenderNetwork();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear All"))
        {
            editor.network = new RailwayNetwork { Name = "New Network" };
            editor.nodeEdgeVisuals = new NodeEdgeVisualsManager(editor, editor.config, editor.state, editor.network);
            editor.stationVisuals = new StationVisualsManager(editor, editor.config, editor.state, editor.network);
            editor.pathfindingService = new PathfindingService(editor.network);
            editor.ClearVisuals();
        }
    }

    // 寻路测试的静态变量
    private static string pathfindStartNode = "";
    private static string pathfindEndNode = "";
    private static bool pathfindIsDownbound = true;
    private static string pathfindResult = "";
    private static List<string> lastCalculatedPath = new();

    [DebugGUI("Pathfinding Test")]
    public static void RenderPathfindingTestGUI()
    {
        var editor = Instance;
        if (editor == null) return;

        ImGui.Text("寻路测试 (支持正线靠左行驶)");
        ImGui.Separator();

        // 输入起点和终点
        ImGui.InputText("起点节点ID", ref pathfindStartNode, 50);
        ImGui.InputText("终点节点ID", ref pathfindEndNode, 50);

        // 选择行驶方向
        ImGui.Checkbox("下行方向 (X增大)", ref pathfindIsDownbound);
        ImGui.TextDisabled(pathfindIsDownbound ? "下行: 走Y较大的正线 (II道)" : "上行: 走Y较小的正线 (I道)");

        ImGui.Separator();

        // 使用选中的节点作为起点/终点
        if (editor.state.SelectedNodeId != null)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1f, 0.5f, 1f), $"选中节点: {editor.state.SelectedNodeId}");
            if (ImGui.SmallButton("设为起点"))
            {
                pathfindStartNode = editor.state.SelectedNodeId;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("设为终点"))
            {
                pathfindEndNode = editor.state.SelectedNodeId;
            }
        }

        ImGui.Separator();

        // 寻路按钮
        if (ImGui.Button("计算路径"))
        {
            if (string.IsNullOrEmpty(pathfindStartNode) || string.IsNullOrEmpty(pathfindEndNode))
            {
                pathfindResult = "请输入起点和终点节点ID";
                lastCalculatedPath.Clear();
            }
            else
            {
                var options = new PathfindingOptions
                {
                    IsDownbound = pathfindIsDownbound,
                    UseLeftHandRule = true
                };

                var result = editor.pathfindingService.FindPath(pathfindStartNode, pathfindEndNode, options);

                if (result.Success)
                {
                    pathfindResult = $"成功! 路径长度: {result.TotalLength:F1}, 边数: {result.EdgeIds.Count}";
                    lastCalculatedPath = result.EdgeIds;

                    // 输出路径详情
                    GD.Print($"Path from {pathfindStartNode} to {pathfindEndNode}:");
                    foreach (var edgeId in result.EdgeIds)
                    {
                        var edge = editor.network.GetEdge(edgeId);
                        if (edge != null)
                        {
                            GD.Print($"  {edgeId}: {edge.FromNode} -> {edge.ToNode}, Type: {edge.TrackType}");
                        }
                    }
                }
                else
                {
                    pathfindResult = $"失败: {result.ErrorMessage}";
                    lastCalculatedPath.Clear();
                }
            }
        }

        // 显示结果
        if (!string.IsNullOrEmpty(pathfindResult))
        {
            ImGui.TextWrapped(pathfindResult);
        }

        // 显示路径边列表
        if (lastCalculatedPath.Count > 0)
        {
            if (ImGui.TreeNode("路径详情"))
            {
                for (int i = 0; i < lastCalculatedPath.Count; i++)
                {
                    var edgeId = lastCalculatedPath[i];
                    var edge = editor.network.GetEdge(edgeId);
                    if (edge != null)
                    {
                        string typeStr = edge.TrackType switch
                        {
                            TrackType.MainLine => "[正线]",
                            TrackType.MainLineWithPlatform => "[正线+站台]",
                            TrackType.ArrivalDeparture => "[到发线]",
                            TrackType.Crossover => "[渡线]",
                            _ => ""
                        };

                        ImGui.Text($"{i + 1}. {edgeId} {typeStr}");
                        ImGui.TextDisabled($"   {edge.FromNode} → {edge.ToNode}, 长度: {edge.Length:F1}");
                    }
                }
                ImGui.TreePop();
            }
        }
    }

    private void RenderSelectedObjectProperties()
    {
        if (state.SelectedNodeId != null)
        {
            var node = network.GetNode(state.SelectedNodeId);
            if (node != null)
            {
                ImGui.Text($"Node: {node.Id}");
                var pos = new System.Numerics.Vector2(node.X, node.Y);
                if (ImGui.DragFloat2("Position", ref pos, config.GridSize))
                {
                    node.X = pos.X;
                    node.Y = pos.Y;
                    nodeEdgeVisuals.UpdateNodeVisual(node);
                    nodeEdgeVisuals.UpdateConnectedEdgeVisuals(node.Id);
                }

                int typeIndex = (int)node.Type;
                string[] nodeTypeNames = { "Endpoint", "Connection", "Switch" };
                if (ImGui.Combo("Node Type", ref typeIndex, nodeTypeNames, nodeTypeNames.Length))
                {
                    node.Type = (RailwayNodeType)typeIndex;
                    nodeEdgeVisuals.UpdateNodeVisual(node);
                }
            }
        }

        if (state.SelectedEdgeId != null)
        {
            var edge = network.GetEdge(state.SelectedEdgeId);
            if (edge != null)
            {
                ImGui.Text($"Edge: {edge.Id}");
                ImGui.Text($"From: {edge.FromNode} -> {edge.ToNode}");
                ImGui.Text($"Length: {edge.Length:F1}m");

                string[] trackTypeNames = { "Main Line", "Arrival/Departure", "Main+Platform", "Crossover" };
                int trackType = (int)edge.TrackType;
                if (ImGui.Combo("Track Type##Edge", ref trackType, trackTypeNames, trackTypeNames.Length))
                {
                    edge.TrackType = (TrackType)trackType;
                    nodeEdgeVisuals.UpdateEdgeVisual(edge);
                }
            }
        }

        if (state.SelectedStationId != null)
        {
            var station = network.GetStation(state.SelectedStationId);
            if (station != null)
            {
                ImGui.Text($"Station: {station.Id}");
                string name = station.Name ?? "";
                if (ImGui.InputText("Name##Station", ref name, 100))
                    station.Name = name;

                var bounds = new System.Numerics.Vector4(station.BoundsX, station.BoundsY, station.BoundsWidth, station.BoundsHeight);
                if (ImGui.DragFloat4("Bounds", ref bounds, config.GridSize))
                {
                    station.BoundsX = bounds.X;
                    station.BoundsY = bounds.Y;
                    station.BoundsWidth = bounds.Z;
                    station.BoundsHeight = bounds.W;
                    stationVisuals.UpdateStationVisual(station);
                }

                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 1f));
                if (ImGui.Button("Delete Station"))
                {
                    stationVisuals.RemoveStationVisual(station.Id);
                    network.RemoveStation(station.Id);
                    state.SelectedStationId = null;
                }
                ImGui.PopStyleColor();
            }
        }
    }

    #endregion

    #region Schedules

    public void LoadAllSchedules()
    {
        schedules.Clear();
        string schedulesPath = ProjectSettings.GlobalizePath("res://Railwaydata/Schedules");
        
        if (!Directory.Exists(schedulesPath))
        {
            GD.Print($"Schedules directory not found: {schedulesPath}");
            return;
        }

        var files = Directory.GetFiles(schedulesPath, "*.json");
        foreach (var file in files)
        {
            try
            {
                var schedule = TrainSchedule.LoadFromFile(file);
                schedules.Add(schedule);
                GD.Print($"Loaded schedule: {schedule.TrainId}");
            }
            catch (System.Exception e)
            {
                GD.PrintErr($"Failed to load schedule from {file}: {e.Message}");
            }
        }

        GD.Print($"Loaded {schedules.Count} schedules");
    }

    public void AddTrainFromSchedule(TrainSchedule schedule)
    {
        // 检查是否已存在
        if (trains.Any(t => t.TrainId == schedule.TrainId))
        {
            GD.Print($"Train {schedule.TrainId} already exists, skipping");
            return;
        }

        // 计算初始偏移，避免重叠（每列车间隔100m）
        float offsetStep = 100f;
        float initialOffset = trains.Count * offsetStep;

        var train = new Train(schedule);
        train.TrainColor = GetTrainColor(schedule.TrainId);
        train.InitialPathOffset = initialOffset;
        train.ParkingOffset = trains.Count * 20f;  // 停车偏移，每列车间隔20m
        trains.Add(train);

        // 为列车计算初始路径
        CalculateTrainPath(train);

        // 创建独立的车厢节点列表（每节车厢独立定位和旋转）
        var carriages = new List<ColorRect>();
        for (int i = 0; i < CarriageCount; i++)
        {
            var carriage = new ColorRect();
            carriage.Name = $"Train_{schedule.TrainId}_Carriage_{i}";
            carriage.Size = new Godot.Vector2(CarriageLength, CarriageWidth);
            carriage.Color = train.TrainColor;  // 完全不透明
            carriage.ZIndex = 50;
            carriage.Visible = false;
            // 添加窗户装饰
            AddWindowsToCarriage(carriage, train.TrainColor);
            // 车头/车尾标记
            if (i == 0 || i == CarriageCount - 1)
            {
                var headMarker = new ColorRect();
                headMarker.Size = new Godot.Vector2(3f, CarriageWidth);
                headMarker.Position = i == 0 ? Godot.Vector2.Zero : new Godot.Vector2(CarriageLength - 3f, 0);
                headMarker.Color = train.TrainColor.Darkened(0.3f);
                carriage.AddChild(headMarker);
            }
            AddChild(carriage);
            carriages.Add(carriage);
        }
        trainCarriages[train.TrainId] = carriages;
        
        // 创建路径可视化线条
        CreatePathVisualization(train);
        
        GD.Print($"Added train {schedule.TrainId} with {CarriageCount} independent carriages, path edges: {train.CurrentPath?.Count ?? 0}, InitialPathOffset: {train.InitialPathOffset}");
    }
    
    /// <summary>
    /// 为车厢添加窗户装饰
    /// </summary>
    private void AddWindowsToCarriage(ColorRect carriage, Color baseColor)
    {
        float windowWidth = 2f;
        float windowHeight = CarriageWidth * 0.4f;
        float windowY = (CarriageWidth - windowHeight) / 2;
        int windowCount = 6;
        float startX = 4f;
        float spacing = (CarriageLength - startX * 2 - windowWidth) / (windowCount - 1);
        
        Color windowColor = new Color(0.7f, 0.85f, 1.0f, 1.0f);  // 浅蓝色窗户
        
        for (int w = 0; w < windowCount; w++)
        {
            var window = new ColorRect();
            window.Size = new Godot.Vector2(windowWidth, windowHeight);
            window.Position = new Godot.Vector2(startX + w * spacing, windowY);
            window.Color = windowColor;
            carriage.AddChild(window);
        }
    }

    /// <summary>
    /// 根据所有时刻表自动创建列车（包含路径信息）
    /// </summary>
    /// <param name="clearExisting">是否清除现有列车</param>
    /// <returns>成功创建的列车数量</returns>
    public int CreateAllTrainsFromSchedules(bool clearExisting = true)
    {
        if (clearExisting)
        {
            ClearAllTrains();
        }

        int successCount = 0;
        int failCount = 0;

        foreach (var schedule in schedules)
        {
            try
            {
                // 检查时刻表有效性
                if (schedule.Entries.Count < 2)
                {
                    GD.PrintErr($"Schedule {schedule.TrainId} has insufficient entries ({schedule.Entries.Count}), skipping");
                    failCount++;
                    continue;
                }

                AddTrainFromSchedule(schedule);

                // 验证路径是否创建成功
                var train = trains.Find(t => t.TrainId == schedule.TrainId);
                if (train != null && train.CurrentPath != null && train.CurrentPath.Count > 0)
                {
                    successCount++;
                }
                else
                {
                    GD.PrintErr($"Train {schedule.TrainId} created but path calculation failed");
                    failCount++;
                }
            }
            catch (System.Exception e)
            {
                GD.PrintErr($"Failed to create train from schedule {schedule.TrainId}: {e.Message}");
                failCount++;
            }
        }

        GD.Print($"Created {successCount} trains from {schedules.Count} schedules ({failCount} failed)");
        return successCount;
    }

    /// <summary>
    /// 清除所有列车
    /// </summary>
    public void ClearAllTrains()
    {
        foreach (var carriages in trainCarriages.Values)
        {
            foreach (var carriage in carriages)
            {
                carriage.QueueFree();
            }
        }
        foreach (var pathLine in trainPathLines.Values)
        {
            pathLine.QueueFree();
        }
        trains.Clear();
        trainCarriages.Clear();
        trainPathLines.Clear();
        GD.Print("Cleared all trains");
    }

    /// <summary>
    /// 创建列车路径可视化
    /// </summary>
    private void CreatePathVisualization(Train train)
    {
        if (train.CurrentPath == null || train.CurrentPath.Count == 0)
            return;

        // 移除旧的路径线条
        if (trainPathLines.TryGetValue(train.TrainId, out var oldLine))
        {
            oldLine.QueueFree();
            trainPathLines.Remove(train.TrainId);
        }

        var pathLine = new Line2D();
        pathLine.Name = $"PathLine_{train.TrainId}";
        pathLine.Width = 3f;
        pathLine.DefaultColor = new Color(train.TrainColor.R, train.TrainColor.G, train.TrainColor.B, 0.5f);
        pathLine.ZIndex = 40;  // 在轨道上方，车厢下方

        // 收集路径上的所有点
        var points = new List<Godot.Vector2>();
        
        for (int i = 0; i < train.CurrentPath.Count; i++)
        {
            var edgeId = train.CurrentPath[i];
            var edge = network.GetEdge(edgeId);
            if (edge == null) continue;

            var fromNode = network.GetNode(edge.FromNode);
            var toNode = network.GetNode(edge.ToNode);
            if (fromNode == null || toNode == null) continue;

            // 确定边的方向
            bool fromToDir = DetermineEdgeDirection(train, i);
            
            Godot.Vector2 startPoint = fromToDir 
                ? new Godot.Vector2(fromNode.X, fromNode.Y) 
                : new Godot.Vector2(toNode.X, toNode.Y);
            Godot.Vector2 endPoint = fromToDir 
                ? new Godot.Vector2(toNode.X, toNode.Y) 
                : new Godot.Vector2(fromNode.X, fromNode.Y);

            // 第一条边添加起点
            if (i == 0)
            {
                points.Add(startPoint);
            }
            
            // 添加终点
            points.Add(endPoint);
        }

        // 设置线条点
        pathLine.Points = points.ToArray();
        AddChild(pathLine);
        trainPathLines[train.TrainId] = pathLine;
    }

    /// <summary>
    /// 获取列车创建统计信息
    /// </summary>
    public (int total, int withPath, int withoutPath) GetTrainStatistics()
    {
        int withPath = trains.Count(t => t.CurrentPath != null && t.CurrentPath.Count > 0);
        return (trains.Count, withPath, trains.Count - withPath);
    }

    /// <summary>
    /// 为列车计算运行路径（基于时刻表和靠左行驶规则）
    /// </summary>
    private void CalculateTrainPath(Train train)
    {
        if (train.Schedule == null || train.Schedule.Entries.Count < 2)
            return;

        var entries = train.Schedule.Entries;

        // 找到第一个发车条目
        int firstDepartIndex = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Event == ScheduleEventType.Departure)
            {
                firstDepartIndex = i;
                break;
            }
        }

        if (firstDepartIndex < 0) return;

        // 获取始发站和终点站
        string fromStationName = entries[firstDepartIndex].Station;
        int fromTrack = entries[firstDepartIndex].Track;

        // 找最后一个到达条目
        int lastArriveIndex = -1;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Event == ScheduleEventType.Arrival)
            {
                lastArriveIndex = i;
                break;
            }
        }

        if (lastArriveIndex < 0) return;

        string toStationName = entries[lastArriveIndex].Station;
        int toTrack = entries[lastArriveIndex].Track;

        // 使用寻路服务计算路径
        var result = pathfindingService.FindPathForTrain(
            train, fromStationName, toStationName, fromTrack, toTrack);

        if (result.Success)
        {
            train.SetPath(result.EdgeIds, result.NodeIds.Count > 0 ? result.NodeIds[0] : null);
            GD.Print($"Path calculated for {train.TrainId}: {result.EdgeIds.Count} edges, {result.TotalLength:F1} units");
        }
        else
        {
            GD.PrintErr($"Failed to calculate path for {train.TrainId}: {result.ErrorMessage}");
            // 回退：使用站场中心进行简单移动
            train.SetPath(new List<string>(), null);
        }
    }

    private Color GetTrainColor(string trainId)
    {
        if (trainId.StartsWith("C"))
            return new Color(0.2f, 0.5f, 0.9f); // 城际蓝
        if (trainId.StartsWith("G"))
            return new Color(0.9f, 0.3f, 0.2f); // 高铁红
        if (trainId.StartsWith("D"))
            return new Color(0.3f, 0.8f, 0.4f); // 动车绿
        return Colors.Gray;
    }

    /// <summary>
    /// 更新所有列车位置和状态
    /// </summary>
    private void UpdateTrains(float delta)
    {
        if (GameTime.IsPaused) return;

        int currentTimeSeconds = GameTime.GetTimeOfDaySeconds();

        foreach (var train in trains)
        {
            UpdateSingleTrain(train, currentTimeSeconds);
            UpdateTrainVisual(train);
        }
    }

    /// <summary>
    /// 更新单个列车
    /// </summary>
    private void UpdateSingleTrain(Train train, int currentTimeSeconds)
    {
        if (train.Schedule == null || train.Schedule.Entries.Count == 0) return;
        if (train.State == TrainState.Arrived) return;

        // 找到当前应该执行的时刻表条目
        var entries = train.Schedule.Entries;

        // 找到当前时间段：在哪两个事件之间
        int prevDepartIndex = -1;
        int nextArriveIndex = -1;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Event == ScheduleEventType.Departure && entries[i].TimeInSeconds <= currentTimeSeconds)
            {
                prevDepartIndex = i;
            }
        }

        if (prevDepartIndex >= 0)
        {
            // 找下一个到达事件
            for (int i = prevDepartIndex + 1; i < entries.Count; i++)
            {
                if (entries[i].Event == ScheduleEventType.Arrival)
                {
                    nextArriveIndex = i;
                    break;
                }
            }
        }

        // 获取起点和终点站场
        Station fromStation = null;
        Station toStation = null;

        if (prevDepartIndex >= 0)
        {
            string fromStationName = entries[prevDepartIndex].Station;
            fromStation = network.Stations.Find(s => s.Name == fromStationName);
        }

        if (nextArriveIndex >= 0)
        {
            string toStationName = entries[nextArriveIndex].Station;
            toStation = network.Stations.Find(s => s.Name == toStationName);
        }

        // 计算位置
        if (fromStation != null && toStation != null && prevDepartIndex >= 0 && nextArriveIndex >= 0)
        {
            int departTime = entries[prevDepartIndex].TimeInSeconds;
            int arriveTime = entries[nextArriveIndex].TimeInSeconds;

            if (arriveTime > departTime)
            {
                float progress = (float)(currentTimeSeconds - departTime) / (arriveTime - departTime);
                progress = Mathf.Clamp(progress, 0f, 1f);

                if (progress >= 1f)
                {
                    // 到达停车位置：停在站台中央，加上停车偏移防止重叠
                    train.PositionX = toStation.BoundsX + toStation.BoundsWidth / 2 + train.ParkingOffset;
                    train.PositionY = toStation.BoundsY + toStation.BoundsHeight / 2;
                    train.MoveToNextEntry();
                }
                else
                {
                    // 使用路径进行移动（如果有路径）
                    if (train.CurrentPath != null && train.CurrentPath.Count > 0)
                    {
                        UpdateTrainPositionOnPath(train, progress);
                    }
                    else
                    {
                        // 回退：简单线性插值站场中心位置
                        var fromPos = fromStation.Bounds.GetCenter();
                        var toPos = toStation.Bounds.GetCenter();

                        train.PositionX = Mathf.Lerp(fromPos.X, toPos.X, progress);
                        train.PositionY = Mathf.Lerp(fromPos.Y, toPos.Y, progress);
                    }

                    train.State = TrainState.Running;
                }
            }
        }
        else if (fromStation != null && toStation == null)
        {
            // 已到达终点 - 停在站台中央
            train.PositionX = fromStation.BoundsX + fromStation.BoundsWidth / 2 + train.ParkingOffset;
            train.PositionY = fromStation.BoundsY + fromStation.BoundsHeight / 2;
            train.State = TrainState.Arrived;
        }
        else if (fromStation == null && entries.Count > 0)
        {
            // 还未发车 - 显示在始发站的站台中央
            string firstStation = entries[0].Station;
            var station = network.Stations.Find(s => s.Name == firstStation);
            if (station != null)
            {
                // 停在站台中央，加上停车偏移防止重叠
                train.PositionX = station.BoundsX + station.BoundsWidth / 2 + train.ParkingOffset;
                train.PositionY = station.BoundsY + station.BoundsHeight / 2;
                train.State = TrainState.WaitingToDepart;
            }
        }
    }

    /// <summary>
    /// 根据路径更新列车位置
    /// </summary>
    /// <param name="train">列车</param>
    /// <param name="totalProgress">总进度（0-1，整个行程）</param>
    private void UpdateTrainPositionOnPath(Train train, float totalProgress)
    {
        if (train.CurrentPath == null || train.CurrentPath.Count == 0)
            return;

        // 计算路径总长度
        float totalLength = 0f;
        var edgeLengths = new List<float>();

        foreach (var edgeId in train.CurrentPath)
        {
            var edge = network.GetEdge(edgeId);
            if (edge != null)
            {
                edgeLengths.Add(edge.Length);
                totalLength += edge.Length;
            }
            else
            {
                edgeLengths.Add(0f);
            }
        }

        if (totalLength <= 0f)
            return;

        // 计算当前应该在路径上的距离
        float targetDistance = totalProgress * totalLength;

        // 找到当前所在的边
        float accumulatedDistance = 0f;
        int currentEdgeIndex = 0;
        float edgeProgress = 0f;

        for (int i = 0; i < train.CurrentPath.Count; i++)
        {
            if (accumulatedDistance + edgeLengths[i] >= targetDistance)
            {
                currentEdgeIndex = i;
                float remainingDistance = targetDistance - accumulatedDistance;
                edgeProgress = edgeLengths[i] > 0 ? remainingDistance / edgeLengths[i] : 0f;
                break;
            }
            accumulatedDistance += edgeLengths[i];
            currentEdgeIndex = i;
            edgeProgress = 1f;
        }

        // 更新列车的路径状态
        train.CurrentPathEdgeIndex = currentEdgeIndex;
        train.CurrentEdgeProgress = edgeProgress;

        // 如果已到达路径终点，停在最后一个节点
        if (currentEdgeIndex >= train.CurrentPath.Count || (currentEdgeIndex == train.CurrentPath.Count - 1 && edgeProgress >= 1f))
        {
            // 获取最后一条边的终点节点
            if (train.CurrentPath.Count > 0)
            {
                var lastEdgeId = train.CurrentPath[train.CurrentPath.Count - 1];
                var lastEdge = network.GetEdge(lastEdgeId);
                if (lastEdge != null)
                {
                    var lastNode = network.GetNode(lastEdge.ToNode);
                    bool fromToDir = DetermineEdgeDirection(train, train.CurrentPath.Count - 1);
                    
                    if (lastNode != null)
                    {
                        if (fromToDir)
                        {
                            train.PositionX = lastNode.X;
                            train.PositionY = lastNode.Y;
                        }
                        else
                        {
                            var fromNode = network.GetNode(lastEdge.FromNode);
                            if (fromNode != null)
                            {
                                train.PositionX = fromNode.X;
                                train.PositionY = fromNode.Y;
                            }
                        }
                    }
                }
            }
            return;
        }

        // 获取当前边并计算位置
        if (currentEdgeIndex < train.CurrentPath.Count)
        {
            var edgeId = train.CurrentPath[currentEdgeIndex];
            var edge = network.GetEdge(edgeId);

            if (edge != null)
            {
                var fromNode = network.GetNode(edge.FromNode);
                var toNode = network.GetNode(edge.ToNode);

                if (fromNode != null && toNode != null)
                {
                    // 确定边的行进方向
                    // 需要根据上一条边来确定从哪个节点进入当前边
                    bool fromToDir = DetermineEdgeDirection(train, currentEdgeIndex);

                    if (fromToDir)
                    {
                        train.PositionX = Mathf.Lerp(fromNode.X, toNode.X, edgeProgress);
                        train.PositionY = Mathf.Lerp(fromNode.Y, toNode.Y, edgeProgress);
                    }
                    else
                    {
                        train.PositionX = Mathf.Lerp(toNode.X, fromNode.X, edgeProgress);
                        train.PositionY = Mathf.Lerp(toNode.Y, fromNode.Y, edgeProgress);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 确定列车在边上的行进方向
    /// </summary>
    /// <param name="train">列车</param>
    /// <param name="edgeIndex">边索引</param>
    /// <returns>true = FromNode → ToNode, false = ToNode → FromNode</returns>
    private bool DetermineEdgeDirection(Train train, int edgeIndex)
    {
        if (train.CurrentPath == null || edgeIndex >= train.CurrentPath.Count)
            return true;

        var currentEdgeId = train.CurrentPath[edgeIndex];
        var currentEdge = network.GetEdge(currentEdgeId);
        if (currentEdge == null) return true;

        // 如果是第一条边，根据起始节点判断
        if (edgeIndex == 0)
        {
            // 如果有起始节点ID，用它判断
            if (!string.IsNullOrEmpty(train.CurrentNodeId))
            {
                return train.CurrentNodeId == currentEdge.FromNode;
            }
            // 否则根据行驶方向判断
            var fromNode = network.GetNode(currentEdge.FromNode);
            var toNode = network.GetNode(currentEdge.ToNode);
            if (fromNode != null && toNode != null)
            {
                // 下行（X增大）时，从X较小的节点出发
                if (train.IsDownbound)
                    return fromNode.X < toNode.X;
                else
                    return fromNode.X > toNode.X;
            }
            return true;
        }

        // 非第一条边，查看上一条边的共享节点
        var prevEdgeId = train.CurrentPath[edgeIndex - 1];
        var prevEdge = network.GetEdge(prevEdgeId);
        if (prevEdge == null) return true;

        // 找到两条边的共享节点（列车的进入节点）
        string entryNode = null;
        if (prevEdge.FromNode == currentEdge.FromNode || prevEdge.ToNode == currentEdge.FromNode)
            entryNode = currentEdge.FromNode;
        else if (prevEdge.FromNode == currentEdge.ToNode || prevEdge.ToNode == currentEdge.ToNode)
            entryNode = currentEdge.ToNode;

        // 如果进入节点是 FromNode，则方向为 FromNode → ToNode
        return entryNode == currentEdge.FromNode;
    }

    // 转向架到车厢中心的距离（转向架位置 = 车厢中心 ± BogieOffset）
    private const float BogieOffset = 8f;

    /// <summary>
    /// 根据路径距离计算轨道上的点位置
    /// </summary>
    /// <param name="train">列车</param>
    /// <param name="distance">从路径起点的距离</param>
    /// <param name="totalLength">路径总长度</param>
    /// <param name="edgeLengths">每条边的长度列表</param>
    /// <returns>(位置X, 位置Y)</returns>
    private (float x, float y) GetPointOnPath(Train train, float distance, float totalLength, List<float> edgeLengths)
    {
        if (train.CurrentPath == null || train.CurrentPath.Count == 0)
            return (train.PositionX, train.PositionY);

        // 限制距离范围
        distance = Mathf.Clamp(distance, 0f, totalLength);

        // 找到当前所在的边
        float accumulatedDistance = 0f;
        int edgeIndex = 0;
        float edgeProgress = 0f;

        for (int i = 0; i < train.CurrentPath.Count; i++)
        {
            if (accumulatedDistance + edgeLengths[i] >= distance)
            {
                edgeIndex = i;
                float remainingDistance = distance - accumulatedDistance;
                edgeProgress = edgeLengths[i] > 0 ? remainingDistance / edgeLengths[i] : 0f;
                break;
            }
            accumulatedDistance += edgeLengths[i];
            edgeIndex = i;
            edgeProgress = 1f;
        }

        // 获取当前边并计算位置
        if (edgeIndex < train.CurrentPath.Count)
        {
            var edgeId = train.CurrentPath[edgeIndex];
            var edge = network.GetEdge(edgeId);

            if (edge != null)
            {
                var fromNode = network.GetNode(edge.FromNode);
                var toNode = network.GetNode(edge.ToNode);

                if (fromNode != null && toNode != null)
                {
                    bool fromToDir = DetermineEdgeDirection(train, edgeIndex);
                    
                    float startX = fromToDir ? fromNode.X : toNode.X;
                    float startY = fromToDir ? fromNode.Y : toNode.Y;
                    float endX = fromToDir ? toNode.X : fromNode.X;
                    float endY = fromToDir ? toNode.Y : fromNode.Y;

                    float posX = Mathf.Lerp(startX, endX, edgeProgress);
                    float posY = Mathf.Lerp(startY, endY, edgeProgress);
                    return (posX, posY);
                }
            }
        }

        return (train.PositionX, train.PositionY);
    }

    /// <summary>
    /// 使用转向架原理计算车厢位置和角度
    /// 前后两个转向架分别采样轨道上的点，两点连线确定车厢朝向
    /// </summary>
    /// <param name="train">列车</param>
    /// <param name="carriageCenterDistance">车厢中心在路径上的距离</param>
    /// <param name="totalLength">路径总长度</param>
    /// <param name="edgeLengths">每条边的长度列表</param>
    /// <returns>(中心X, 中心Y, 角度)</returns>
    private (float x, float y, float angle) GetCarriageTransform(Train train, float carriageCenterDistance, float totalLength, List<float> edgeLengths)
    {
        // 计算前后转向架在轨道上的位置
        float frontBogieDistance = carriageCenterDistance + BogieOffset;  // 前转向架（车头方向）
        float rearBogieDistance = carriageCenterDistance - BogieOffset;   // 后转向架（车尾方向）
        
        // 在轨道上采样两个点
        var (frontX, frontY) = GetPointOnPath(train, frontBogieDistance, totalLength, edgeLengths);
        var (rearX, rearY) = GetPointOnPath(train, rearBogieDistance, totalLength, edgeLengths);
        
        // 车厢中心 = 两个转向架的中点
        float centerX = (frontX + rearX) / 2f;
        float centerY = (frontY + rearY) / 2f;
        
        // 车厢角度 = 从后转向架指向前转向架的方向
        float angle = Mathf.Atan2(frontY - rearY, frontX - rearX);
        
        return (centerX, centerY, angle);
    }

    /// <summary>
    /// 更新列车可视化 - 使用转向架原理让每节车厢独立跟随轨道曲线
    /// </summary>
    private void UpdateTrainVisual(Train train)
    {
        if (!trainCarriages.TryGetValue(train.TrainId, out var carriages)) return;

        // 根据状态调整透明度
        float alpha = train.State switch
        {
            TrainState.WaitingToDepart => 0.6f,
            TrainState.Arrived => 0.4f,
            _ => 1.0f
        };

        // 如果没有路径，所有车厢简单排列
        if (train.CurrentPath == null || train.CurrentPath.Count == 0)
        {
            float angle = train.IsDownbound ? 0f : Mathf.Pi;
            for (int i = 0; i < carriages.Count; i++)
            {
                var carriage = carriages[i];
                float offsetX = i * (CarriageLength + CarriageGap);
                
                carriage.Position = new Godot.Vector2(
                    train.PositionX - offsetX * Mathf.Cos(angle),
                    train.PositionY - offsetX * Mathf.Sin(angle)
                );
                carriage.Rotation = angle;
                carriage.Color = new Color(train.TrainColor, alpha);
                carriage.Visible = true;
            }
            return;
        }

        // 计算路径总长度和每条边的长度
        float totalLength = 0f;
        var edgeLengths = new List<float>();
        foreach (var edgeId in train.CurrentPath)
        {
            var edge = network.GetEdge(edgeId);
            float len = edge?.Length ?? 0f;
            edgeLengths.Add(len);
            totalLength += len;
        }

        if (totalLength <= 0f) return;

        // 计算列车头部（第一节车厢前转向架）在路径上的距离
        // 注意：不再使用 InitialPathOffset，位置完全由路径状态决定
        float headDistance = 0f;
        for (int i = 0; i < train.CurrentPathEdgeIndex && i < edgeLengths.Count; i++)
        {
            headDistance += edgeLengths[i];
        }
        if (train.CurrentPathEdgeIndex < edgeLengths.Count)
        {
            headDistance += train.CurrentEdgeProgress * edgeLengths[train.CurrentPathEdgeIndex];
        }

        // 为每节车厢使用转向架原理计算位置和角度
        for (int i = 0; i < carriages.Count; i++)
        {
            var carriage = carriages[i];
            
            // 计算该车厢中心在路径上的距离
            // 第一节车厢的中心位置 = headDistance - BogieOffset（头部是前转向架位置）
            // 后续车厢依次往后偏移
            float carriageCenterDistance = headDistance - BogieOffset - i * (CarriageLength + CarriageGap);
            
            // 使用转向架原理获取车厢的位置和角度
            var (centerX, centerY, angle) = GetCarriageTransform(train, carriageCenterDistance, totalLength, edgeLengths);
            
            // 将中心坐标转换为左上角坐标（考虑旋转）
            float offsetX = CarriageLength / 2;
            float offsetY = CarriageWidth / 2;
            float posX = centerX - offsetX * Mathf.Cos(angle) + offsetY * Mathf.Sin(angle);
            float posY = centerY - offsetX * Mathf.Sin(angle) - offsetY * Mathf.Cos(angle);
            
            carriage.Position = new Godot.Vector2(posX, posY);
            carriage.Rotation = angle;
            carriage.Color = new Color(train.TrainColor, alpha);
            carriage.Visible = true;
        }
    }

    [DebugGUI("Schedules", Opening = true)]
    public static void RenderScheduleGUI()
    {
        if (Instance == null) return;

        ImGui.Text($"Schedules: {Instance.schedules.Count} | Active Trains: {Instance.trains.Count}");
        ImGui.Separator();

        // 时刻表列表
        if (ImGui.CollapsingHeader("Available Schedules", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var schedule in Instance.schedules)
            {
                bool isActive = Instance.trains.Exists(t => t.TrainId == schedule.TrainId);
                
                ImGui.PushID(schedule.TrainId);
                
                // 显示车次信息
                string direction = schedule.IsDownbound ? "↓" : "↑";
                string info = $"{schedule.TrainId} {direction}";
                
                if (schedule.Entries.Count > 0)
                {
                    var first = schedule.Entries[0];
                    var last = schedule.Entries[^1];
                    info += $" | {first.Station} → {last.Station}";
                    info += $" | {first.Time} - {last.Time}";
                }

                if (isActive)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.3f, 0.8f, 0.3f, 1f), info);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Remove"))
                    {
                        var train = Instance.trains.Find(t => t.TrainId == schedule.TrainId);
                        if (train != null)
                        {
                            Instance.trains.Remove(train);
                            if (Instance.trainCarriages.TryGetValue(train.TrainId, out var carriages))
                            {
                                foreach (var carriage in carriages)
                                {
                                    carriage.QueueFree();
                                }
                                Instance.trainCarriages.Remove(train.TrainId);
                            }
                        }
                    }
                }
                else
                {
                    ImGui.Text(info);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Add"))
                    {
                        Instance.AddTrainFromSchedule(schedule);
                    }
                }

                // 展开显示详细时刻表
                if (ImGui.TreeNode($"Details##{schedule.TrainId}"))
                {
                    ImGui.Columns(4, "schedule_columns", true);
                    ImGui.Text("Time"); ImGui.NextColumn();
                    ImGui.Text("Station"); ImGui.NextColumn();
                    ImGui.Text("Track"); ImGui.NextColumn();
                    ImGui.Text("Event"); ImGui.NextColumn();
                    ImGui.Separator();

                    foreach (var entry in schedule.Entries)
                    {
                        ImGui.Text(entry.Time); ImGui.NextColumn();
                        ImGui.Text(entry.Station); ImGui.NextColumn();
                        ImGui.Text(entry.Track.ToString()); ImGui.NextColumn();
                        ImGui.Text(entry.Event == ScheduleEventType.Arrival ? "到达" : "出发"); ImGui.NextColumn();
                    }

                    ImGui.Columns(1);
                    ImGui.TreePop();
                }

                ImGui.PopID();
            }
        }

        ImGui.Separator();

        // 活跃列车状态
        if (Instance.trains.Count > 0 && ImGui.CollapsingHeader("Active Trains", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var train in Instance.trains)
            {
                var entry = train.GetCurrentEntry();
                string status = train.State switch
                {
                    TrainState.WaitingToDepart => "等待发车",
                    TrainState.Running => "运行中",
                    TrainState.Stopped => "停站中",
                    TrainState.Arrived => "已到达",
                    _ => "未知"
                };

                string direction = train.IsDownbound ? "↓下行" : "↑上行";

                ImGui.TextColored(
                    new System.Numerics.Vector4(train.TrainColor.R, train.TrainColor.G, train.TrainColor.B, 1f),
                    $"{train.TrainId} ({direction}): {status}"
                );

                if (entry != null)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"@ {entry.Station}");
                }

                // 显示路径信息
                if (train.CurrentPath != null && train.CurrentPath.Count > 0)
                {
                    ImGui.Indent();
                    ImGui.TextDisabled($"路径: {train.CurrentPath.Count} 边, 当前边 {train.CurrentPathEdgeIndex + 1}/{train.CurrentPath.Count}, 进度 {train.CurrentEdgeProgress:P0}");
                    ImGui.Unindent();
                }
            }
        }

        ImGui.Separator();

        // 按钮区域
        if (ImGui.Button("Reload Schedules"))
        {
            Instance.LoadAllSchedules();
        }

        ImGui.SameLine();

        // 一键创建所有列车按钮
        if (ImGui.Button("Create All Trains"))
        {
            int count = Instance.CreateAllTrainsFromSchedules(true);
            GD.Print($"Auto-created {count} trains from schedules");
        }

        ImGui.SameLine();

        // 清除所有列车按钮
        if (ImGui.Button("Clear All Trains"))
        {
            Instance.ClearAllTrains();
        }

        // 显示统计信息
        var (total, withPath, withoutPath) = Instance.GetTrainStatistics();
        ImGui.TextDisabled($"Stats: {total} trains, {withPath} with path, {withoutPath} without path");
    }

    #endregion

    public RailwayNetwork GetNetwork() => network;
}
