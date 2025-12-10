using Godot;
using System.Collections.Generic;
using ImGuiNET;
using System.Numerics;

/// <summary>
/// 铁路网络编辑器（重构版）- 使用管理器类解耦
/// </summary>
public partial class RailwayNetworkEditorRefactored : Node2D
{
    [ExportGroup("File Settings")]
    [Export] public string NetworkPath = "res://Railwaydata/Networks/default.json";

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

    public override void _Ready()
    {
        // 初始化配置
        config = CreateConfig();
        state = new EditorState();

        // 加载网络
        LoadNetwork();

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

        // 注册调试GUI
        DebugGUI.RegisterDebugRender("Railway Editor (Refactored)", RenderEditorGUI, true);
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

    private void RenderEditorGUI()
    {
        ImGui.Text($"Network: {network?.Name ?? "None"}");
        ImGui.Text($"Nodes: {network?.Nodes.Count ?? 0} | Edges: {network?.Edges.Count ?? 0} | Stations: {network?.Stations.Count ?? 0}");
        ImGui.Separator();

        // 编辑模式
        ImGui.Text("Edit Mode:");
        int modeIndex = (int)state.CurrentMode;
        string[] modeNames = { "Select [1]", "Draw Track [2]", "Draw Crossover [3]", "Draw Station [4]" };
        for (int i = 0; i < modeNames.Length; i++)
        {
            if (ImGui.RadioButton(modeNames[i], modeIndex == i)) state.CurrentMode = (EditMode)i;
            if (i < modeNames.Length - 1) ImGui.SameLine();
        }

        ImGui.Separator();

        // 轨道类型
        ImGui.Text("Track Type:");
        string[] trackTypeNames = { "Main Line [Q]", "Arrival/Departure [W]", "Main+Platform [E]", "Crossover [R]" };
        int trackTypeIndex = (int)state.CurrentTrackType;
        if (ImGui.Combo("##TrackType", ref trackTypeIndex, trackTypeNames, trackTypeNames.Length))
            state.CurrentTrackType = (TrackType)trackTypeIndex;

        ImGui.Separator();

        // 网格设置
        bool snapToGrid = config.SnapToGrid;
        ImGui.Checkbox("Snap to Grid [G]", ref snapToGrid);
        config.SnapToGrid = snapToGrid;

        ImGui.Separator();

        // 调试选项
        bool showCollisionBoxes = state.ShowCollisionBoxes;
        ImGui.Checkbox("Show Collision Boxes", ref showCollisionBoxes);
        state.ShowCollisionBoxes = showCollisionBoxes;

        bool showStationBorders = state.ShowStationBorders;
        if (ImGui.Checkbox("Show Station Borders", ref showStationBorders))
        {
            state.ShowStationBorders = showStationBorders;
            stationVisuals.UpdateAllStationVisuals();
        }

        // 鼠标位置
        var mousePos = GetGlobalMousePosition();
        ImGui.Text($"Mouse: ({mousePos.X:F0}, {mousePos.Y:F0})");
        ImGui.Text($"Hovered: {state.HoveredNodeId ?? state.HoveredEdgeId ?? state.HoveredStationId ?? "None"}");

        ImGui.Separator();

        // 选中对象属性
        RenderSelectedObjectProperties();

        ImGui.Separator();

        // 文件操作
        if (ImGui.Button("Save [Ctrl+S]")) SaveNetwork();
        ImGui.SameLine();
        if (ImGui.Button("Reload"))
        {
            LoadNetwork();
            nodeEdgeVisuals = new NodeEdgeVisualsManager(this, config, state, network);
            stationVisuals = new StationVisualsManager(this, config, state, network);
            ClearVisuals();
            RenderNetwork();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear All"))
        {
            network = new RailwayNetwork { Name = "New Network" };
            nodeEdgeVisuals = new NodeEdgeVisualsManager(this, config, state, network);
            stationVisuals = new StationVisualsManager(this, config, state, network);
            ClearVisuals();
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

    public RailwayNetwork GetNetwork() => network;
}
