using Godot;
using System.Collections.Generic;
using ImGuiNET;
using System.Numerics;

/// <summary>
/// 编辑模式
/// </summary>
public enum EditMode
{
    Select,              // 选择模式 - 点击选中，拖拽移动，按Delete删除
    DrawTrack,           // 绘制轨道 - 连续点击添加节点和边
    DrawCrossover        // 绘制渡线 - 点击两个节点连接
}

/// <summary>
/// 铁路网络编辑器 - 更人性化的编辑体验
/// </summary>
public partial class RailwayNetworkEditor : Node2D
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

    [ExportGroup("Track Colors")]
    [Export] public Color MainLineColor = new Color(0.1f, 0.1f, 0.5f);           // 深蓝 - 正线
    [Export] public Color ArrivalDepartureColor = new Color(0.2f, 0.2f, 0.2f);   // 深灰 - 到发线
    [Export] public Color MainLineWithPlatformColor = new Color(0.3f, 0.1f, 0.5f);   // 紫色 - 正线兼到发线
    [Export] public Color CrossoverColor = new Color(0.1f, 0.4f, 0.1f);          // 绿色 - 渡线

    [ExportGroup("Node Colors")]
    [Export] public Color EndpointColor = new Color(1f, 0.2f, 0.2f);
    [Export] public Color ConnectionColor = Colors.SkyBlue;
    [Export] public Color SwitchColor = new Color(0.8f, 0.2f, 0.8f);
    [Export] public Color SelectedColor = new Color(1f, 1f, 0f);
    [Export] public Color HoverColor = new Color(0.5f, 1f, 0.5f);

    // 网络数据
    private RailwayNetwork network;

    // 编辑状态
    private EditMode currentMode = EditMode.Select;
    private TrackType currentTrackType = TrackType.ArrivalDeparture;
    private string selectedNodeId = null;
    private string selectedEdgeId = null;
    private string hoveredNodeId = null;
    private string hoveredEdgeId = null;
    private string drawStartNodeId = null;
    private bool isDragging = false;
    private Godot.Vector2 dragOffset;
    private bool showCollisionBoxes = false; // 调试用

    // 渲染对象
    private Dictionary<string, ColorRect> nodeVisuals = new();
    private Dictionary<string, Line2D> edgeVisuals = new();
    private Dictionary<string, Line2D> collisionBoxes = new(); // 节点碰撞箱调试
    private Dictionary<string, Line2D> edgeCollisionBoxes = new(); // 边碰撞箱调试

    // 预览线
    private Line2D previewLine;

    public override void _Ready()
    {
        LoadNetwork();
        RenderNetwork();

        // 创建预览线
        previewLine = new Line2D();
        previewLine.Width = TrackWidth;
        previewLine.DefaultColor = new Color(1, 1, 1, 0.5f);
        previewLine.ZIndex = 100;
        AddChild(previewLine);

        // 注册调试GUI
        DebugGUI.RegisterDebugRender("Railway Editor", RenderEditorGUI, true);
    }

    public override void _Process(double delta)
    {
        // 更新悬停状态
        UpdateHover();

        // 更新预览线
        UpdatePreviewLine();
        
        // 实时更新碰撞箱位置
        if (showCollisionBoxes)
        {
            foreach (var nodeId in nodeVisuals.Keys)
            {
                var node = network.GetNode(nodeId);
                if (node != null && collisionBoxes.TryGetValue(nodeId, out var collisionBox))
                {
                    var rect = new Rect2(
                        new Godot.Vector2(node.X - NodeRadius, node.Y - NodeRadius),
                        new Godot.Vector2(NodeRadius * 2, NodeRadius * 2)
                    );
                    
                    collisionBox.ClearPoints();
                    collisionBox.AddPoint(rect.Position);
                    collisionBox.AddPoint(new Godot.Vector2(rect.Position.X + rect.Size.X, rect.Position.Y));
                    collisionBox.AddPoint(new Godot.Vector2(rect.Position.X + rect.Size.X, rect.Position.Y + rect.Size.Y));
                    collisionBox.AddPoint(new Godot.Vector2(rect.Position.X, rect.Position.Y + rect.Size.Y));
                    collisionBox.AddPoint(rect.Position);
                }
            }
            
            // 实时更新边的碰撞箱
            foreach (var edgeId in edgeVisuals.Keys)
            {
                var edge = network.GetEdge(edgeId);
                if (edge != null)
                {
                    var fromNode = network.GetNode(edge.FromNode);
                    var toNode = network.GetNode(edge.ToNode);
                    if (fromNode != null && toNode != null && edgeCollisionBoxes.TryGetValue(edgeId, out var collisionBox))
                    {
                        float threshold = 3f;
                        var dir = (toNode.Position - fromNode.Position).Normalized();
                        var perp = new Godot.Vector2(-dir.Y, dir.X) * threshold;
                        
                        collisionBox.ClearPoints();
                        collisionBox.AddPoint(fromNode.Position + perp);
                        collisionBox.AddPoint(toNode.Position + perp);
                        collisionBox.AddPoint(toNode.Position - perp);
                        collisionBox.AddPoint(fromNode.Position - perp);
                        collisionBox.AddPoint(fromNode.Position + perp);
                    }
                }
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        // 如果 ImGui 正在捕获输入，不处理游戏内输入
        if (ImGui.GetIO().WantCaptureMouse && @event is InputEventMouse)
            return;
        if (ImGui.GetIO().WantCaptureKeyboard && @event is InputEventKey)
            return;

        Godot.Vector2 rawMousePos = GetGlobalMousePosition();
        Godot.Vector2 snappedMousePos = SnapToGrid ? SnapPosition(rawMousePos) : rawMousePos;

        // 鼠标移动 - 拖拽
        if (@event is InputEventMouseMotion motion && isDragging && selectedNodeId != null)
        {
            var node = network.GetNode(selectedNodeId);
            if (node != null)
            {
                node.X = snappedMousePos.X;
                node.Y = snappedMousePos.Y;
                UpdateNodeVisual(node);
                UpdateConnectedEdgeVisuals(node.Id);
            }
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
        {
            HandleKeyPress(keyEvent);
        }
    }

    private void HandleLeftMouseDown(Godot.Vector2 rawPos, Godot.Vector2 snappedPos)
    {
        switch (currentMode)
        {
            case EditMode.Select:
                // 选择或开始拖拽 - 使用原始位置
                var clickedNode = FindNodeAtPosition(rawPos);
                if (clickedNode != null)
                {
                    selectedNodeId = clickedNode;
                    selectedEdgeId = null;
                    isDragging = true;
                    UpdateSelectionVisuals();
                }
                else
                {
                    var clickedEdge = FindEdgeAtPosition(rawPos);
                    if (clickedEdge != null)
                    {
                        selectedEdgeId = clickedEdge;
                        selectedNodeId = null;
                        UpdateSelectionVisuals();
                    }
                    else
                    {
                        selectedNodeId = null;
                        selectedEdgeId = null;
                        UpdateSelectionVisuals();
                    }
                }
                break;

            case EditMode.DrawTrack:
                // 绘制轨道模式 - 使用原始位置查找，对齐位置创建
                var existingNode = FindNodeAtPosition(rawPos);
                if (existingNode != null)
                {
                    // 点击已有节点
                    if (drawStartNodeId == null)
                    {
                        drawStartNodeId = existingNode;
                    }
                    else if (existingNode != drawStartNodeId)
                    {
                        // 连接两个节点
                        CreateEdge(drawStartNodeId, existingNode);
                        drawStartNodeId = existingNode; // 继续从此节点绘制
                    }
                }
                else
                {
                    // 检查是否点击在现有边上
                    var edgeOnClick = FindEdgeAtPosition(rawPos);
                    if (edgeOnClick != null)
                    {
                        // 在边上插入新节点
                        InsertNodeOnEdge(edgeOnClick, snappedPos);
                    }
                    else
                    {
                        // 点击空白处 - 创建新节点（使用对齐位置）
                        var newNode = network.AddNode(snappedPos.X, snappedPos.Y, RailwayNodeType.Connection);
                        RenderNode(newNode);

                        if (drawStartNodeId != null)
                        {
                            CreateEdge(drawStartNodeId, newNode.Id);
                        }
                        drawStartNodeId = newNode.Id;
                    }
                }
                break;

            case EditMode.DrawCrossover:
                // 绘制渡线 - 必须点击已有节点（使用原始位置）
                var nodeForCrossover = FindNodeAtPosition(rawPos);
                if (nodeForCrossover != null)
                {
                    if (drawStartNodeId == null)
                    {
                        drawStartNodeId = nodeForCrossover;
                    }
                    else if (nodeForCrossover != drawStartNodeId)
                    {
                        var edge = network.AddEdge(drawStartNodeId, nodeForCrossover);
                        if (edge != null)
                        {
                            edge.TrackType = TrackType.Crossover;
                            RenderEdge(edge);
                        }
                        drawStartNodeId = null; // 渡线画完后重置
                    }
                }
                break;
        }
    }

    private void HandleLeftMouseUp()
    {
        isDragging = false;
    }

    private void HandleRightMouseDown()
    {
        // 右键取消当前操作
        drawStartNodeId = null;
        selectedNodeId = null;
        selectedEdgeId = null;
        UpdateSelectionVisuals();
    }

    private void HandleKeyPress(InputEventKey keyEvent)
    {
        // 模式切换快捷键
        if (keyEvent.Keycode == Key.Key1) currentMode = EditMode.Select;
        else if (keyEvent.Keycode == Key.Key2) currentMode = EditMode.DrawTrack;
        else if (keyEvent.Keycode == Key.Key3) currentMode = EditMode.DrawCrossover;

        // 轨道类型快捷键
        else if (keyEvent.Keycode == Key.Q) currentTrackType = TrackType.MainLine;
        else if (keyEvent.Keycode == Key.W) currentTrackType = TrackType.ArrivalDeparture;
        else if (keyEvent.Keycode == Key.E) currentTrackType = TrackType.MainLineWithPlatform;
        else if (keyEvent.Keycode == Key.R) currentTrackType = TrackType.Crossover;

        // 功能快捷键
        else if (keyEvent.Keycode == Key.Delete)
        {
            // 在任何模式下都可以按Delete删除选中的对象
            if (selectedNodeId != null)
                DeleteNode(selectedNodeId, autoConnect: false);
            else if (selectedEdgeId != null)
                DeleteEdge(selectedEdgeId);
        }
        else if (keyEvent.Keycode == Key.S && keyEvent.CtrlPressed)
            SaveNetwork();
        else if (keyEvent.Keycode == Key.G)
            SnapToGrid = !SnapToGrid;
        else if (keyEvent.Keycode == Key.Escape)
        {
            drawStartNodeId = null;
            currentMode = EditMode.Select;
        }
    }

    private void CreateEdge(string fromId, string toId)
    {
        var edge = network.AddEdge(fromId, toId);
        if (edge != null)
        {
            edge.TrackType = currentTrackType;
            RenderEdge(edge);
        }
    }

    /// <summary>
    /// 在已有的边上插入新节点，将边分为两段
    /// </summary>
    private void InsertNodeOnEdge(string edgeId, Godot.Vector2 newNodePos)
    {
        var edge = network.GetEdge(edgeId);
        if (edge == null) return;

        var fromNode = network.GetNode(edge.FromNode);
        var toNode = network.GetNode(edge.ToNode);
        if (fromNode == null || toNode == null) return;

        // 创建新节点
        var newNode = network.AddNode(newNodePos.X, newNodePos.Y, RailwayNodeType.Connection);
        RenderNode(newNode);

        // 删除原边的可视化
        if (edgeVisuals.TryGetValue(edgeId, out var line))
        {
            line.QueueFree();
            edgeVisuals.Remove(edgeId);
        }
        if (edgeCollisionBoxes.TryGetValue(edgeId, out var collisionBox))
        {
            collisionBox.QueueFree();
            edgeCollisionBoxes.Remove(edgeId);
        }

        // 删除原边
        network.RemoveEdge(edgeId);

        // 创建两条新边：从 → 新节点，新节点 → 到
        var edge1 = network.AddEdge(edge.FromNode, newNode.Id);
        if (edge1 != null)
        {
            edge1.TrackType = edge.TrackType;
            edge1.TrackNumber = edge.TrackNumber;
            RenderEdge(edge1);
        }

        var edge2 = network.AddEdge(newNode.Id, edge.ToNode);
        if (edge2 != null)
        {
            edge2.TrackType = edge.TrackType;
            edge2.TrackNumber = edge.TrackNumber;
            RenderEdge(edge2);
        }

        // 从新创建的节点继续绘制
        drawStartNodeId = newNode.Id;
    }

    private void DeleteNode(string nodeId, bool autoConnect = true)
    {
        var node = network.GetNode(nodeId);
        if (node == null) return;

        // 获取连接到该节点的所有边
        var connectedEdges = new List<string>(network.GetConnectedEdges(nodeId));
        
        // 仅在autoConnect为true时进行自动连接
        if (autoConnect)
        {
            // 收集前驱和后继节点
            var predecessors = new List<RailwayEdge>(); // 指向此节点的边
            var successors = new List<RailwayEdge>();   // 从此节点出发的边
            
            foreach (var edgeId in connectedEdges)
            {
                var edge = network.GetEdge(edgeId);
                if (edge == null) continue;
                
                if (edge.ToNode == nodeId)
                    predecessors.Add(edge);
                else if (edge.FromNode == nodeId)
                    successors.Add(edge);
            }
            
            
            // 自动连接前驱节点和后继节点
            if (predecessors.Count > 0 && successors.Count > 0)
            {
                // 对每个前驱节点，连接到每个后继节点
                foreach (var predEdge in predecessors)
                {
                    foreach (var succEdge in successors)
                    {
                        var newEdge = network.AddEdge(predEdge.FromNode, succEdge.ToNode);
                        if (newEdge != null)
                        {
                            // 继承轨道类型（优先使用前驱的类型）
                            newEdge.TrackType = predEdge.TrackType;
                            RenderEdge(newEdge);
                        }
                    }
                }
            }
        }
        
        // 删除相关边的可视化
        foreach (var edgeId in connectedEdges)
        {
            if (edgeVisuals.TryGetValue(edgeId, out var line))
            {
                line.QueueFree();
                edgeVisuals.Remove(edgeId);
            }
            if (edgeCollisionBoxes.TryGetValue(edgeId, out var collisionBox))
            {
                collisionBox.QueueFree();
                edgeCollisionBoxes.Remove(edgeId);
            }
        }

        // 删除节点可视化
        if (nodeVisuals.TryGetValue(nodeId, out var visual))
        {
            visual.QueueFree();
            nodeVisuals.Remove(nodeId);
        }
        if (collisionBoxes.TryGetValue(nodeId, out var nodeCollisionBox))
        {
            nodeCollisionBox.QueueFree();
            collisionBoxes.Remove(nodeId);
        }

        network.RemoveNode(nodeId);
        
        selectedNodeId = null;
        drawStartNodeId = null;
    }

    private void DeleteEdge(string edgeId)
    {
        var edge = network.GetEdge(edgeId);
        if (edge == null) return;
        
        if (edgeVisuals.TryGetValue(edgeId, out var line))
        {
            line.QueueFree();
            edgeVisuals.Remove(edgeId);
        }
        
        if (edgeCollisionBoxes.TryGetValue(edgeId, out var collisionBox))
        {
            collisionBox.QueueFree();
            edgeCollisionBoxes.Remove(edgeId);
        }

        network.RemoveEdge(edgeId);
        
        selectedEdgeId = null;
    }

    private Godot.Vector2 SnapPosition(Godot.Vector2 pos)
    {
        return new Godot.Vector2(
            Mathf.Round(pos.X / GridSize) * GridSize,
            Mathf.Round(pos.Y / GridSize) * GridSize
        );
    }

    private void UpdateHover()
    {
        var mousePos = GetGlobalMousePosition();
        hoveredNodeId = FindNodeAtPosition(mousePos);
        if (hoveredNodeId == null)
            hoveredEdgeId = FindEdgeAtPosition(mousePos);
        else
            hoveredEdgeId = null;
    }

    private void UpdatePreviewLine()
    {
        previewLine.ClearPoints();

        if (drawStartNodeId != null && (currentMode == EditMode.DrawTrack || currentMode == EditMode.DrawCrossover))
        {
            var startNode = network.GetNode(drawStartNodeId);
            if (startNode != null)
            {
                var mousePos = SnapToGrid ? SnapPosition(GetGlobalMousePosition()) : GetGlobalMousePosition();
                previewLine.AddPoint(startNode.Position);
                previewLine.AddPoint(mousePos);
                previewLine.DefaultColor = GetTrackColor(currentTrackType).Lightened(0.3f);
                previewLine.DefaultColor = new Color(previewLine.DefaultColor, 0.5f);
            }
        }
    }

    /// <summary>
    /// ImGui编辑器界面
    /// </summary>
    private void RenderEditorGUI()
    {
        // 网络信息
        ImGui.Text($"Network: {network?.Name ?? "None"}");
        ImGui.Text($"Nodes: {network?.Nodes.Count ?? 0} | Edges: {network?.Edges.Count ?? 0}");
        ImGui.Separator();

        // 编辑模式选择
        ImGui.Text("Edit Mode:");
        if (ImGui.RadioButton("Select [1]", currentMode == EditMode.Select)) currentMode = EditMode.Select;
        ImGui.SameLine();
        if (ImGui.RadioButton("Draw Track [2]", currentMode == EditMode.DrawTrack)) currentMode = EditMode.DrawTrack;
        ImGui.SameLine();
        if (ImGui.RadioButton("Draw Crossover [3]", currentMode == EditMode.DrawCrossover)) currentMode = EditMode.DrawCrossover;

        ImGui.Separator();

        // 轨道类型选择
        ImGui.Text("Track Type:");
        string[] trackTypeNames = { "Main Line [Q]", "Arrival/Departure [W]", "Main+Platform [E]", "Crossover [R]" };
        int trackTypeIndex = (int)currentTrackType;
        if (ImGui.Combo("##TrackType", ref trackTypeIndex, trackTypeNames, trackTypeNames.Length))
        {
            currentTrackType = (TrackType)trackTypeIndex;
        }

        // 网格设置
        ImGui.Separator();
        ImGui.Checkbox("Snap to Grid [G]", ref SnapToGrid);
        ImGui.SameLine();
        float gridSize = GridSize;
        ImGui.SetNextItemWidth(60);
        if (ImGui.DragFloat("Grid", ref gridSize, 1f, 5f, 50f))
        {
            GridSize = gridSize;
        }

        ImGui.Separator();

        // 调试选项
        ImGui.Checkbox("Show Collision Boxes", ref showCollisionBoxes);
        
        // 显示鼠标位置和检测结果
        var mousePos = GetGlobalMousePosition();
        ImGui.Text($"Mouse: ({mousePos.X:F0}, {mousePos.Y:F0})");
        ImGui.Text($"Hovered Node: {hoveredNodeId ?? "None"}");
        ImGui.Text($"Hovered Edge: {hoveredEdgeId ?? "None"}");
        
        ImGui.Separator();

        // 选中的节点/边属性
        if (selectedNodeId != null)
        {
            var node = network.GetNode(selectedNodeId);
            if (node != null)
            {
                ImGui.Text($"Node: {node.Id}");

                var pos = new System.Numerics.Vector2(node.X, node.Y);
                if (ImGui.DragFloat2("Position", ref pos, GridSize))
                {
                    node.X = pos.X;
                    node.Y = pos.Y;
                    UpdateNodeVisual(node);
                    UpdateConnectedEdgeVisuals(node.Id);
                }

                int typeIndex = (int)node.Type;
                string[] nodeTypeNames = { "Endpoint", "Connection", "Switch" };
                if (ImGui.Combo("Node Type", ref typeIndex, nodeTypeNames, nodeTypeNames.Length))
                {
                    node.Type = (RailwayNodeType)typeIndex;
                    UpdateNodeVisual(node);
                }
            }
        }

        if (selectedEdgeId != null)
        {
            var edge = network.GetEdge(selectedEdgeId);
            if (edge != null)
            {
                ImGui.Text($"Edge: {edge.Id}");
                ImGui.Text($"From: {edge.FromNode} -> {edge.ToNode}");
                ImGui.Text($"Length: {edge.Length:F1}m");

                int trackType = (int)edge.TrackType;
                if (ImGui.Combo("Track Type##Edge", ref trackType, trackTypeNames, trackTypeNames.Length))
                {
                    edge.TrackType = (TrackType)trackType;
                    UpdateEdgeVisual(edge);
                }

                int trackNum = edge.TrackNumber;
                if (ImGui.InputInt("Track Number", ref trackNum))
                {
                    edge.TrackNumber = trackNum;
                }
            }
        }

        ImGui.Separator();

        // 文件操作
        if (ImGui.Button("Save [Ctrl+S]"))
            SaveNetwork();
        ImGui.SameLine();
        if (ImGui.Button("Reload"))
        {
            LoadNetwork();
            ClearVisuals();
            RenderNetwork();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear All"))
        {
            network = new RailwayNetwork { Name = "New Network" };
            ClearVisuals();
        }

        // 模式提示
        ImGui.Separator();
        ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1f), GetModeHint());
    }

    private string GetModeHint()
    {
        return currentMode switch
        {
            EditMode.Select => "Click to select, drag to move nodes, press Delete to remove",
            EditMode.DrawTrack => "Click to place nodes and connect tracks. Right-click to cancel.",
            EditMode.DrawCrossover => "Click two nodes to connect with crossover",
            _ => ""
        };
    }

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
        foreach (var edge in network.Edges)
            RenderEdge(edge);
        foreach (var node in network.Nodes)
            RenderNode(node);
    }

    private void RenderNode(RailwayNode node)
    {
        if (nodeVisuals.ContainsKey(node.Id))
        {
            UpdateNodeVisual(node);
            return;
        }

        var visual = new ColorRect();
        visual.Size = new Godot.Vector2(NodeRadius * 2, NodeRadius * 2);
        visual.Position = new Godot.Vector2(node.X - NodeRadius, node.Y - NodeRadius);
        visual.Color = GetNodeColor(node);
        visual.ZIndex = 10;
        AddChild(visual);
        nodeVisuals[node.Id] = visual;

        // 绘制碰撞箱（调试用）
        if (showCollisionBoxes)
        {
            RenderCollisionBox(node);
        }
    }

    private void RenderCollisionBox(RailwayNode node)
    {
        if (collisionBoxes.ContainsKey(node.Id))
            return;

        // 碰撞箱大小与节点显示大小相同
        var collisionBox = new Line2D();
        
        // 绘制矩形边界 - 使用与节点相同的大小
        var rect = new Rect2(
            new Godot.Vector2(node.X - NodeRadius, node.Y - NodeRadius),
            new Godot.Vector2(NodeRadius * 2, NodeRadius * 2)
        );
        
        collisionBox.AddPoint(rect.Position);
        collisionBox.AddPoint(new Godot.Vector2(rect.Position.X + rect.Size.X, rect.Position.Y));
        collisionBox.AddPoint(new Godot.Vector2(rect.Position.X + rect.Size.X, rect.Position.Y + rect.Size.Y));
        collisionBox.AddPoint(new Godot.Vector2(rect.Position.X, rect.Position.Y + rect.Size.Y));
        collisionBox.AddPoint(rect.Position); // 闭合矩形
        
        collisionBox.Width = 1f;
        collisionBox.DefaultColor = new Color(0, 1, 1, 0.5f); // 青色半透明
        collisionBox.ZIndex = 5;
        AddChild(collisionBox);
        collisionBoxes[node.Id] = collisionBox;
    }

    private void RenderEdge(RailwayEdge edge)
    {
        if (edgeVisuals.ContainsKey(edge.Id))
        {
            UpdateEdgeVisual(edge);
            return;
        }

        var fromNode = network.GetNode(edge.FromNode);
        var toNode = network.GetNode(edge.ToNode);
        if (fromNode == null || toNode == null) return;

        var line = new Line2D();
        line.AddPoint(fromNode.Position);
        line.AddPoint(toNode.Position);
        line.Width = TrackWidth;
        line.DefaultColor = GetTrackColor(edge.TrackType);
        line.ZIndex = 0;
        AddChild(line);
        edgeVisuals[edge.Id] = line;

        // 绘制边的碰撞箱（调试用）
        if (showCollisionBoxes)
        {
            RenderEdgeCollisionBox(edge, fromNode.Position, toNode.Position);
        }
    }

    private void RenderEdgeCollisionBox(RailwayEdge edge, Godot.Vector2 from, Godot.Vector2 to)
    {
        if (edgeCollisionBoxes.ContainsKey(edge.Id))
            return;

        // 绘制边周围的碰撞检测区域 - 3像素宽度
        float threshold = 3f;
        
        var collisionBox = new Line2D();
        
        // 绘制从起点到终点的平行线（上下两侧）
        var dir = (to - from).Normalized();
        var perp = new Godot.Vector2(-dir.Y, dir.X) * threshold;
        
        collisionBox.AddPoint(from + perp);
        collisionBox.AddPoint(to + perp);
        collisionBox.AddPoint(to - perp);
        collisionBox.AddPoint(from - perp);
        collisionBox.AddPoint(from + perp); // 闭合矩形
        
        collisionBox.Width = 1f;
        collisionBox.DefaultColor = new Color(1, 1, 0, 0.3f); // 黄色半透明
        collisionBox.ZIndex = 4;
        AddChild(collisionBox);
        edgeCollisionBoxes[edge.Id] = collisionBox;
    }

    private void UpdateNodeVisual(RailwayNode node)
    {
        if (!nodeVisuals.TryGetValue(node.Id, out var visual)) return;
        visual.Position = new Godot.Vector2(node.X - NodeRadius, node.Y - NodeRadius);
        visual.Color = node.Id == selectedNodeId ? SelectedColor :
                       node.Id == hoveredNodeId ? HoverColor : GetNodeColor(node);
        
        // 更新碰撞箱位置
        if (showCollisionBoxes && collisionBoxes.TryGetValue(node.Id, out var collisionBox))
        {
            var rect = new Rect2(
                new Godot.Vector2(node.X - NodeRadius, node.Y - NodeRadius),
                new Godot.Vector2(NodeRadius * 2, NodeRadius * 2)
            );
            
            collisionBox.ClearPoints();
            collisionBox.AddPoint(rect.Position);
            collisionBox.AddPoint(new Godot.Vector2(rect.Position.X + rect.Size.X, rect.Position.Y));
            collisionBox.AddPoint(new Godot.Vector2(rect.Position.X + rect.Size.X, rect.Position.Y + rect.Size.Y));
            collisionBox.AddPoint(new Godot.Vector2(rect.Position.X, rect.Position.Y + rect.Size.Y));
            collisionBox.AddPoint(rect.Position);
        }
    }

    private void UpdateEdgeVisual(RailwayEdge edge)
    {
        if (!edgeVisuals.TryGetValue(edge.Id, out var line)) return;

        var fromNode = network.GetNode(edge.FromNode);
        var toNode = network.GetNode(edge.ToNode);
        if (fromNode == null || toNode == null) return;

        line.ClearPoints();
        line.AddPoint(fromNode.Position);
        line.AddPoint(toNode.Position);
        line.DefaultColor = edge.Id == selectedEdgeId ? SelectedColor :
                            edge.Id == hoveredEdgeId ? HoverColor : GetTrackColor(edge.TrackType);
        
        // 更新边的碰撞箱
        if (showCollisionBoxes)
        {
            if (edgeCollisionBoxes.TryGetValue(edge.Id, out var collisionBox))
            {
                collisionBox.QueueFree();
                edgeCollisionBoxes.Remove(edge.Id);
            }
            RenderEdgeCollisionBox(edge, fromNode.Position, toNode.Position);
        }
    }

    private void UpdateConnectedEdgeVisuals(string nodeId)
    {
        foreach (var edgeId in network.GetConnectedEdges(nodeId))
        {
            var edge = network.GetEdge(edgeId);
            if (edge != null)
            {
                UpdateEdgeVisual(edge);
                edge.Length = network.GetNode(edge.FromNode).Position
                    .DistanceTo(network.GetNode(edge.ToNode).Position);
            }
        }
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var node in network.Nodes)
            UpdateNodeVisual(node);
        foreach (var edge in network.Edges)
            UpdateEdgeVisual(edge);
    }

    private void ClearVisuals()
    {
        foreach (var v in nodeVisuals.Values) v.QueueFree();
        foreach (var v in edgeVisuals.Values) v.QueueFree();
        foreach (var v in collisionBoxes.Values) v.QueueFree();
        foreach (var v in edgeCollisionBoxes.Values) v.QueueFree();
        nodeVisuals.Clear();
        edgeVisuals.Clear();
        collisionBoxes.Clear();
        edgeCollisionBoxes.Clear();
    }

    private Color GetNodeColor(RailwayNode node)
    {
        return node.Type switch
        {
            RailwayNodeType.Endpoint => EndpointColor,
            RailwayNodeType.Switch => SwitchColor,
            _ => ConnectionColor
        };
    }

    private Color GetTrackColor(TrackType type)
    {
        return type switch
        {
            TrackType.MainLine => MainLineColor,
            TrackType.ArrivalDeparture => ArrivalDepartureColor,
            TrackType.MainLineWithPlatform => MainLineWithPlatformColor,
            TrackType.Crossover => CrossoverColor,
            _ => ArrivalDepartureColor
        };
    }

    private string FindNodeAtPosition(Godot.Vector2 pos)
    {
        // 使用矩形碰撞检测而不是距离，更准确 - 大小与节点显示大小相同
        foreach (var node in network.Nodes)
        {
            var rect = new Rect2(
                new Godot.Vector2(node.X - NodeRadius, node.Y - NodeRadius),
                new Godot.Vector2(NodeRadius * 2, NodeRadius * 2)
            );
            if (rect.HasPoint(pos))
                return node.Id;
        }
        return null;
    }

    private string FindEdgeAtPosition(Godot.Vector2 pos)
    {
        // 使用3像素宽度作为点击检测区域
        float threshold = 3f;
        foreach (var edge in network.Edges)
        {
            var from = network.GetNode(edge.FromNode);
            var to = network.GetNode(edge.ToNode);
            if (from == null || to == null) continue;

            float dist = PointToSegmentDistance(pos, from.Position, to.Position);
            if (dist < threshold)
                return edge.Id;
        }
        return null;
    }

    private float PointToSegmentDistance(Godot.Vector2 p, Godot.Vector2 a, Godot.Vector2 b)
    {
        var ab = b - a;
        var ap = p - a;
        float t = Mathf.Clamp(ap.Dot(ab) / ab.LengthSquared(), 0f, 1f);
        var closest = a + ab * t;
        return p.DistanceTo(closest);
    }

    #endregion

    /// <summary>
    /// 获取当前网络
    /// </summary>
    public RailwayNetwork GetNetwork() => network;
}
