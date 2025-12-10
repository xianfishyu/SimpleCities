using Godot;
using System.Collections.Generic;

/// <summary>
/// 节点和边的可视化管理器
/// </summary>
public class NodeEdgeVisualsManager
{
    private readonly Node2D parent;
    private readonly EditorConfig config;
    private readonly EditorState state;
    private readonly RailwayNetwork network;

    private readonly Dictionary<string, ColorRect> nodeVisuals = new();
    private readonly Dictionary<string, Line2D> edgeVisuals = new();
    private readonly Dictionary<string, Line2D> collisionBoxes = new();
    private readonly Dictionary<string, Line2D> edgeCollisionBoxes = new();

    public NodeEdgeVisualsManager(Node2D parent, EditorConfig config, EditorState state, RailwayNetwork network)
    {
        this.parent = parent;
        this.config = config;
        this.state = state;
        this.network = network;
    }

    /// <summary>
    /// 更新网络引用（重新加载时调用）
    /// </summary>
    public void SetNetwork(RailwayNetwork newNetwork)
    {
        // 注意：需要通过反射或其他方式更新，这里简化处理
    }

    /// <summary>
    /// 渲染所有节点和边
    /// </summary>
    public void RenderAll()
    {
        foreach (var edge in network.Edges)
            RenderEdge(edge);
        foreach (var node in network.Nodes)
            RenderNode(node);
    }

    /// <summary>
    /// 渲染单个节点
    /// </summary>
    public void RenderNode(RailwayNode node)
    {
        if (nodeVisuals.ContainsKey(node.Id))
        {
            UpdateNodeVisual(node);
            return;
        }

        var visual = new ColorRect();
        visual.Size = new Vector2(config.NodeRadius * 2, config.NodeRadius * 2);
        visual.Position = new Vector2(node.X - config.NodeRadius, node.Y - config.NodeRadius);
        visual.Color = config.GetNodeColor(node.Type);
        visual.ZIndex = 10;
        parent.AddChild(visual);
        nodeVisuals[node.Id] = visual;

        if (state.ShowCollisionBoxes)
            RenderCollisionBox(node);
    }

    /// <summary>
    /// 渲染节点碰撞箱
    /// </summary>
    private void RenderCollisionBox(RailwayNode node)
    {
        if (collisionBoxes.ContainsKey(node.Id))
            return;

        var collisionBox = new Line2D();
        var rect = new Rect2(
            new Vector2(node.X - config.NodeRadius, node.Y - config.NodeRadius),
            new Vector2(config.NodeRadius * 2, config.NodeRadius * 2)
        );

        collisionBox.AddPoint(rect.Position);
        collisionBox.AddPoint(new Vector2(rect.Position.X + rect.Size.X, rect.Position.Y));
        collisionBox.AddPoint(new Vector2(rect.Position.X + rect.Size.X, rect.Position.Y + rect.Size.Y));
        collisionBox.AddPoint(new Vector2(rect.Position.X, rect.Position.Y + rect.Size.Y));
        collisionBox.AddPoint(rect.Position);

        collisionBox.Width = 1f;
        collisionBox.DefaultColor = new Color(0, 1, 1, 0.5f);
        collisionBox.ZIndex = 5;
        parent.AddChild(collisionBox);
        collisionBoxes[node.Id] = collisionBox;
    }

    /// <summary>
    /// 渲染单个边
    /// </summary>
    public void RenderEdge(RailwayEdge edge)
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
        line.Width = config.TrackWidth;
        line.DefaultColor = config.GetTrackColor(edge.TrackType);
        line.ZIndex = 0;
        parent.AddChild(line);
        edgeVisuals[edge.Id] = line;

        if (state.ShowCollisionBoxes)
            RenderEdgeCollisionBox(edge, fromNode.Position, toNode.Position);
    }

    /// <summary>
    /// 渲染边碰撞箱
    /// </summary>
    private void RenderEdgeCollisionBox(RailwayEdge edge, Vector2 from, Vector2 to)
    {
        if (edgeCollisionBoxes.ContainsKey(edge.Id))
            return;

        float threshold = 3f;
        var collisionBox = new Line2D();
        var dir = (to - from).Normalized();
        var perp = new Vector2(-dir.Y, dir.X) * threshold;

        collisionBox.AddPoint(from + perp);
        collisionBox.AddPoint(to + perp);
        collisionBox.AddPoint(to - perp);
        collisionBox.AddPoint(from - perp);
        collisionBox.AddPoint(from + perp);

        collisionBox.Width = 1f;
        collisionBox.DefaultColor = new Color(1, 1, 0, 0.3f);
        collisionBox.ZIndex = 4;
        parent.AddChild(collisionBox);
        edgeCollisionBoxes[edge.Id] = collisionBox;
    }

    /// <summary>
    /// 更新节点可视化
    /// </summary>
    public void UpdateNodeVisual(RailwayNode node)
    {
        if (!nodeVisuals.TryGetValue(node.Id, out var visual)) return;

        visual.Position = new Vector2(node.X - config.NodeRadius, node.Y - config.NodeRadius);
        visual.Color = node.Id == state.SelectedNodeId ? config.SelectedColor :
                       node.Id == state.HoveredNodeId ? config.HoverColor : config.GetNodeColor(node.Type);

        if (state.ShowCollisionBoxes && collisionBoxes.TryGetValue(node.Id, out var collisionBox))
        {
            var rect = new Rect2(
                new Vector2(node.X - config.NodeRadius, node.Y - config.NodeRadius),
                new Vector2(config.NodeRadius * 2, config.NodeRadius * 2)
            );

            collisionBox.ClearPoints();
            collisionBox.AddPoint(rect.Position);
            collisionBox.AddPoint(new Vector2(rect.Position.X + rect.Size.X, rect.Position.Y));
            collisionBox.AddPoint(new Vector2(rect.Position.X + rect.Size.X, rect.Position.Y + rect.Size.Y));
            collisionBox.AddPoint(new Vector2(rect.Position.X, rect.Position.Y + rect.Size.Y));
            collisionBox.AddPoint(rect.Position);
        }
    }

    /// <summary>
    /// 更新边可视化
    /// </summary>
    public void UpdateEdgeVisual(RailwayEdge edge)
    {
        if (!edgeVisuals.TryGetValue(edge.Id, out var line)) return;

        var fromNode = network.GetNode(edge.FromNode);
        var toNode = network.GetNode(edge.ToNode);
        if (fromNode == null || toNode == null) return;

        line.ClearPoints();
        line.AddPoint(fromNode.Position);
        line.AddPoint(toNode.Position);
        line.DefaultColor = edge.Id == state.SelectedEdgeId ? config.SelectedColor :
                            edge.Id == state.HoveredEdgeId ? config.HoverColor : config.GetTrackColor(edge.TrackType);

        if (state.ShowCollisionBoxes)
        {
            if (edgeCollisionBoxes.TryGetValue(edge.Id, out var collisionBox))
            {
                collisionBox.QueueFree();
                edgeCollisionBoxes.Remove(edge.Id);
            }
            RenderEdgeCollisionBox(edge, fromNode.Position, toNode.Position);
        }
    }

    /// <summary>
    /// 更新与节点连接的所有边
    /// </summary>
    public void UpdateConnectedEdgeVisuals(string nodeId)
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

    /// <summary>
    /// 移除节点可视化
    /// </summary>
    public void RemoveNodeVisual(string nodeId)
    {
        if (nodeVisuals.TryGetValue(nodeId, out var visual))
        {
            visual.QueueFree();
            nodeVisuals.Remove(nodeId);
        }
        if (collisionBoxes.TryGetValue(nodeId, out var collisionBox))
        {
            collisionBox.QueueFree();
            collisionBoxes.Remove(nodeId);
        }
    }

    /// <summary>
    /// 移除边可视化
    /// </summary>
    public void RemoveEdgeVisual(string edgeId)
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

    /// <summary>
    /// 更新所有选择状态的可视化
    /// </summary>
    public void UpdateSelectionVisuals()
    {
        foreach (var node in network.Nodes)
            UpdateNodeVisual(node);
        foreach (var edge in network.Edges)
            UpdateEdgeVisual(edge);
    }

    /// <summary>
    /// 更新碰撞箱（用于实时更新）
    /// </summary>
    public void UpdateCollisionBoxes()
    {
        if (!state.ShowCollisionBoxes) return;

        foreach (var nodeId in nodeVisuals.Keys)
        {
            var node = network.GetNode(nodeId);
            if (node != null && collisionBoxes.TryGetValue(nodeId, out var collisionBox))
            {
                var rect = new Rect2(
                    new Vector2(node.X - config.NodeRadius, node.Y - config.NodeRadius),
                    new Vector2(config.NodeRadius * 2, config.NodeRadius * 2)
                );

                collisionBox.ClearPoints();
                collisionBox.AddPoint(rect.Position);
                collisionBox.AddPoint(new Vector2(rect.Position.X + rect.Size.X, rect.Position.Y));
                collisionBox.AddPoint(new Vector2(rect.Position.X + rect.Size.X, rect.Position.Y + rect.Size.Y));
                collisionBox.AddPoint(new Vector2(rect.Position.X, rect.Position.Y + rect.Size.Y));
                collisionBox.AddPoint(rect.Position);
            }
        }

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
                    var perp = new Vector2(-dir.Y, dir.X) * threshold;

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

    /// <summary>
    /// 清除所有可视化
    /// </summary>
    public void Clear()
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

    /// <summary>
    /// 查找位置处的节点
    /// </summary>
    public string FindNodeAtPosition(Vector2 pos)
    {
        foreach (var node in network.Nodes)
        {
            var rect = new Rect2(
                new Vector2(node.X - config.NodeRadius, node.Y - config.NodeRadius),
                new Vector2(config.NodeRadius * 2, config.NodeRadius * 2)
            );
            if (rect.HasPoint(pos))
                return node.Id;
        }
        return null;
    }

    /// <summary>
    /// 查找位置处的边
    /// </summary>
    public string FindEdgeAtPosition(Vector2 pos)
    {
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

    /// <summary>
    /// 计算点到线段的距离
    /// </summary>
    public static float PointToSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var ap = p - a;
        float t = Mathf.Clamp(ap.Dot(ab) / ab.LengthSquared(), 0f, 1f);
        var closest = a + ab * t;
        return p.DistanceTo(closest);
    }
}
