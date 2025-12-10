using Godot;
using System.Collections.Generic;
using ImGuiNET;
using System.Numerics;

/// <summary>
/// 铁路网络编辑器 - 在游戏内编辑轨道网络
/// </summary>
public partial class RailwayNetworkEditor : Node2D
{
    [Export] public string NetworkPath = "res://Railwaydata/Networks/default.json";
    [Export] public float NodeRadius = 3f;
    [Export] public float TrackWidth = 1.435f;
    [Export] public Color NodeColor = Colors.Red;
    [Export] public Color EdgeColor = Colors.Black;
    [Export] public Color MainLineColor = Colors.DarkBlue;
    [Export] public Color CrossoverColor = Colors.DarkGreen;
    [Export] public Color SelectedColor = Colors.Yellow;
    [Export] public Color PlatformNodeColor = Colors.Orange;
    [Export] public Color SwitchNodeColor = Colors.Purple;

    private RailwayNetwork network;
    private string selectedNodeId = null;
    private string selectedEdgeId = null;
    private bool isAddingNode = false;
    private bool isAddingEdge = false;
    private string edgeStartNode = null;
    private bool showEditor = true;

    // 渲染节点
    private Dictionary<string, ColorRect> nodeVisuals = new();
    private Dictionary<string, Line2D> edgeVisuals = new();

    public override void _Ready()
    {
        LoadNetwork();
        RenderNetwork();

        // 注册调试GUI
        DebugGUI.RegisterDebugRender("Railway Network Editor", RenderEditorGUI, true);
    }

    public override void _Process(double delta)
    {
    }

    public override void _Input(InputEvent @event)
    {
        if (!showEditor) return;

        // 鼠标点击处理
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            Godot.Vector2 mousePos = GetGlobalMousePosition();

            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (isAddingNode)
                {
                    // 添加新节点
                    var node = network.AddNode(mousePos.X, mousePos.Y);
                    RenderNode(node);
                    isAddingNode = false;
                    GD.Print($"添加节点: {node.Id} at ({mousePos.X:F1}, {mousePos.Y:F1})");
                }
                else if (isAddingEdge)
                {
                    // 选择边的起点/终点
                    string clickedNode = FindNodeAtPosition(mousePos);
                    if (clickedNode != null)
                    {
                        if (edgeStartNode == null)
                        {
                            edgeStartNode = clickedNode;
                            GD.Print($"边起点: {edgeStartNode}");
                        }
                        else if (clickedNode != edgeStartNode)
                        {
                            var edge = network.AddEdge(edgeStartNode, clickedNode);
                            if (edge != null)
                            {
                                RenderEdge(edge);
                                GD.Print($"添加边: {edge.Id} ({edgeStartNode} -> {clickedNode})");
                            }
                            edgeStartNode = null;
                            isAddingEdge = false;
                        }
                    }
                }
                else
                {
                    // 选择节点或边
                    string clickedNode = FindNodeAtPosition(mousePos);
                    if (clickedNode != null)
                    {
                        selectedNodeId = clickedNode;
                        selectedEdgeId = null;
                        UpdateSelectionVisuals();
                    }
                    else
                    {
                        string clickedEdge = FindEdgeAtPosition(mousePos);
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
                }
            }
            else if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                // 取消当前操作
                isAddingNode = false;
                isAddingEdge = false;
                edgeStartNode = null;
            }
        }

        // 键盘快捷键
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Delete)
            {
                DeleteSelected();
            }
            else if (keyEvent.Keycode == Key.N && keyEvent.CtrlPressed)
            {
                isAddingNode = true;
                isAddingEdge = false;
            }
            else if (keyEvent.Keycode == Key.E && keyEvent.CtrlPressed)
            {
                isAddingEdge = true;
                isAddingNode = false;
                edgeStartNode = null;
            }
            else if (keyEvent.Keycode == Key.S && keyEvent.CtrlPressed)
            {
                SaveNetwork();
            }
        }
    }

    /// <summary>
    /// ImGui编辑器界面
    /// </summary>
    private void RenderEditorGUI()
    {
        ImGui.Text($"Network: {network?.Name ?? "Not Loaded"}");
        ImGui.Text($"Nodes: {network?.Nodes.Count ?? 0} | Edges: {network?.Edges.Count ?? 0}");
        ImGui.Separator();

        // Operation mode
        if (ImGui.Button(isAddingNode ? "Cancel Add Node" : "Add Node (Ctrl+N)"))
        {
            isAddingNode = !isAddingNode;
            isAddingEdge = false;
        }
        ImGui.SameLine();
        if (ImGui.Button(isAddingEdge ? "Cancel Add Edge" : "Add Edge (Ctrl+E)"))
        {
            isAddingEdge = !isAddingEdge;
            isAddingNode = false;
            edgeStartNode = null;
        }

        if (isAddingEdge && edgeStartNode != null)
        {
            ImGui.Text($"Start: {edgeStartNode}, click end node");
        }

        ImGui.Separator();

        // Selected node properties
        if (selectedNodeId != null)
        {
            var node = network.GetNode(selectedNodeId);
            if (node != null)
            {
                ImGui.Text($"Selected Node: {node.Id}");

                // Position editing
                var pos = new System.Numerics.Vector2(node.X, node.Y);
                if (ImGui.DragFloat2("Position", ref pos, 1f))
                {
                    node.X = pos.X;
                    node.Y = pos.Y;
                    UpdateNodeVisual(node);
                    UpdateConnectedEdgeVisuals(node.Id);
                }

                // Type selection
                int typeIndex = (int)node.Type;
                string[] typeNames = { "Endpoint", "Connection", "Switch", "Platform" };
                if (ImGui.Combo("Type", ref typeIndex, typeNames, typeNames.Length))
                {
                    node.Type = (RailwayNodeType)typeIndex;
                    UpdateNodeVisual(node);
                }

                if (node.Type == RailwayNodeType.Platform)
                {
                    string info = node.PlatformInfo ?? "";
                    if (ImGui.InputText("Platform Info", ref info, 100))
                    {
                        node.PlatformInfo = info;
                    }
                }

                if (ImGui.Button("Delete Node (Del)"))
                {
                    DeleteSelected();
                }
            }
        }

        // Selected edge properties
        if (selectedEdgeId != null)
        {
            var edge = network.GetEdge(selectedEdgeId);
            if (edge != null)
            {
                ImGui.Text($"Selected Edge: {edge.Id}");
                ImGui.Text($"From: {edge.FromNode} -> To: {edge.ToNode}");
                ImGui.Text($"Length: {edge.Length:F1}");

                bool isMainLine = edge.IsMainLine;
                if (ImGui.Checkbox("Main Line", ref isMainLine))
                {
                    edge.IsMainLine = isMainLine;
                    UpdateEdgeVisual(edge);
                }

                bool isCrossover = edge.IsCrossover;
                if (ImGui.Checkbox("Crossover", ref isCrossover))
                {
                    edge.IsCrossover = isCrossover;
                    UpdateEdgeVisual(edge);
                }

                float speedLimit = edge.SpeedLimit;
                if (ImGui.DragFloat("Speed Limit (km/h)", ref speedLimit, 5f, 0f, 350f))
                {
                    edge.SpeedLimit = speedLimit;
                }

                if (ImGui.Button("Delete Edge (Del)"))
                {
                    DeleteSelected();
                }
            }
        }

        ImGui.Separator();

        // File operations
        if (ImGui.Button("Save (Ctrl+S)"))
        {
            SaveNetwork();
        }
        ImGui.SameLine();
        if (ImGui.Button("Reload"))
        {
            LoadNetwork();
            ClearVisuals();
            RenderNetwork();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            network = new RailwayNetwork { Name = "New Network" };
            ClearVisuals();
        }
    }

    /// <summary>
    /// 加载网络
    /// </summary>
    private void LoadNetwork()
    {
        try
        {
            network = RailwayNetwork.LoadFromFile(NetworkPath);
            GD.Print($"已加载网络: {network.Name}");
        }
        catch
        {
            GD.Print("创建新网络");
            network = new RailwayNetwork { Name = "新网络" };
        }
    }

    /// <summary>
    /// 保存网络
    /// </summary>
    private void SaveNetwork()
    {
        network.SaveToFile(NetworkPath);
        GD.Print($"已保存网络到: {NetworkPath}");
    }

    /// <summary>
    /// 渲染整个网络
    /// </summary>
    private void RenderNetwork()
    {
        foreach (var edge in network.Edges)
        {
            RenderEdge(edge);
        }
        foreach (var node in network.Nodes)
        {
            RenderNode(node);
        }
    }

    /// <summary>
    /// 渲染节点
    /// </summary>
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
        visual.ZIndex = 5;
        AddChild(visual);
        nodeVisuals[node.Id] = visual;
    }

    /// <summary>
    /// 渲染边
    /// </summary>
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
        line.AddPoint(new Godot.Vector2(fromNode.X, fromNode.Y));
        line.AddPoint(new Godot.Vector2(toNode.X, toNode.Y));
        line.Width = TrackWidth;
        line.DefaultColor = GetEdgeColor(edge);
        line.ZIndex = 0;
        AddChild(line);
        edgeVisuals[edge.Id] = line;
    }

    /// <summary>
    /// 更新节点可视化
    /// </summary>
    private void UpdateNodeVisual(RailwayNode node)
    {
        if (!nodeVisuals.TryGetValue(node.Id, out var visual)) return;
        visual.Position = new Godot.Vector2(node.X - NodeRadius, node.Y - NodeRadius);
        visual.Color = GetNodeColor(node);
    }

    /// <summary>
    /// 更新边可视化
    /// </summary>
    private void UpdateEdgeVisual(RailwayEdge edge)
    {
        if (!edgeVisuals.TryGetValue(edge.Id, out var line)) return;

        var fromNode = network.GetNode(edge.FromNode);
        var toNode = network.GetNode(edge.ToNode);
        if (fromNode == null || toNode == null) return;

        line.ClearPoints();
        line.AddPoint(new Godot.Vector2(fromNode.X, fromNode.Y));
        line.AddPoint(new Godot.Vector2(toNode.X, toNode.Y));
        line.DefaultColor = GetEdgeColor(edge);
    }

    /// <summary>
    /// 更新与节点相连的边
    /// </summary>
    private void UpdateConnectedEdgeVisuals(string nodeId)
    {
        foreach (var edgeId in network.GetConnectedEdges(nodeId))
        {
            var edge = network.GetEdge(edgeId);
            if (edge != null)
            {
                UpdateEdgeVisual(edge);
                // 更新长度
                var from = network.GetNode(edge.FromNode);
                var to = network.GetNode(edge.ToNode);
                if (from != null && to != null)
                {
                    edge.Length = from.Position.DistanceTo(to.Position);
                }
            }
        }
    }

    /// <summary>
    /// 清空所有可视化
    /// </summary>
    private void ClearVisuals()
    {
        foreach (var visual in nodeVisuals.Values)
            visual.QueueFree();
        foreach (var visual in edgeVisuals.Values)
            visual.QueueFree();
        nodeVisuals.Clear();
        edgeVisuals.Clear();
    }

    /// <summary>
    /// 更新选择可视化
    /// </summary>
    private void UpdateSelectionVisuals()
    {
        // 重置所有颜色
        foreach (var node in network.Nodes)
        {
            if (nodeVisuals.TryGetValue(node.Id, out var visual))
            {
                visual.Color = node.Id == selectedNodeId ? SelectedColor : GetNodeColor(node);
            }
        }
        foreach (var edge in network.Edges)
        {
            if (edgeVisuals.TryGetValue(edge.Id, out var line))
            {
                line.DefaultColor = edge.Id == selectedEdgeId ? SelectedColor : GetEdgeColor(edge);
            }
        }
    }

    /// <summary>
    /// 获取节点颜色
    /// </summary>
    private Color GetNodeColor(RailwayNode node)
    {
        return node.Type switch
        {
            RailwayNodeType.Platform => PlatformNodeColor,
            RailwayNodeType.Switch => SwitchNodeColor,
            _ => NodeColor
        };
    }

    /// <summary>
    /// 获取边颜色
    /// </summary>
    private Color GetEdgeColor(RailwayEdge edge)
    {
        if (edge.IsMainLine) return MainLineColor;
        if (edge.IsCrossover) return CrossoverColor;
        return EdgeColor;
    }

    /// <summary>
    /// 在位置查找节点
    /// </summary>
    private string FindNodeAtPosition(Godot.Vector2 pos)
    {
        float threshold = NodeRadius * 3;
        foreach (var node in network.Nodes)
        {
            if (node.Position.DistanceTo(pos) < threshold)
                return node.Id;
        }
        return null;
    }

    /// <summary>
    /// 在位置查找边
    /// </summary>
    private string FindEdgeAtPosition(Godot.Vector2 pos)
    {
        float threshold = TrackWidth * 3;
        foreach (var edge in network.Edges)
        {
            var from = network.GetNode(edge.FromNode);
            var to = network.GetNode(edge.ToNode);
            if (from == null || to == null) continue;

            // 点到线段的距离
            float dist = PointToSegmentDistance(pos, from.Position, to.Position);
            if (dist < threshold)
                return edge.Id;
        }
        return null;
    }

    /// <summary>
    /// 计算点到线段的距离
    /// </summary>
    private float PointToSegmentDistance(Godot.Vector2 p, Godot.Vector2 a, Godot.Vector2 b)
    {
        var ab = b - a;
        var ap = p - a;
        float t = Mathf.Clamp(ap.Dot(ab) / ab.LengthSquared(), 0f, 1f);
        var closest = a + ab * t;
        return p.DistanceTo(closest);
    }

    /// <summary>
    /// 删除选中的元素
    /// </summary>
    private void DeleteSelected()
    {
        if (selectedNodeId != null)
        {
            // 先删除相关边的可视化
            foreach (var edgeId in network.GetConnectedEdges(selectedNodeId))
            {
                if (edgeVisuals.TryGetValue(edgeId, out var line))
                {
                    line.QueueFree();
                    edgeVisuals.Remove(edgeId);
                }
            }

            // 删除节点可视化
            if (nodeVisuals.TryGetValue(selectedNodeId, out var visual))
            {
                visual.QueueFree();
                nodeVisuals.Remove(selectedNodeId);
            }

            network.RemoveNode(selectedNodeId);
            GD.Print($"删除节点: {selectedNodeId}");
            selectedNodeId = null;
        }
        else if (selectedEdgeId != null)
        {
            if (edgeVisuals.TryGetValue(selectedEdgeId, out var line))
            {
                line.QueueFree();
                edgeVisuals.Remove(selectedEdgeId);
            }

            network.RemoveEdge(selectedEdgeId);
            GD.Print($"删除边: {selectedEdgeId}");
            selectedEdgeId = null;
        }
    }

    /// <summary>
    /// 获取当前网络
    /// </summary>
    public RailwayNetwork GetNetwork() => network;
}
