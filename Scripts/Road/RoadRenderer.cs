using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class RoadRenderer : Node2D
{
    [Export] public RoadConfig Config { get; set; } = null!;

    private RoadGraph? _network;

    // Edge.ID → Line2D 节点映射
    private readonly Dictionary<int, Line2D> _edgeLines = new();

    // 交叉口渲染层（放在所有 Line2D 之后，保证渲染在最上层）
    private Node2D _junctionLayer = null!;

    // 施工预览
    private Vector2[] _previewPoints = [];
    public Vector2[] PreviewPoints
    {
        get => (Vector2[])_previewPoints.Clone();
        set => _previewPoints = value == null ? [] : (Vector2[])value.Clone();
    }

    public int GetPreviewPointCount() => _previewPoints.Length;

    public Vector2 GetPreviewPoint(int index) => _previewPoints[index];

    private int[] _removalPreviewEdgeIDs = [];
    public int[] RemovalPreviewEdgeIDs
    {
        get => (int[])_removalPreviewEdgeIDs.Clone();
        set => _removalPreviewEdgeIDs = value == null ? [] : value.Distinct().Order().ToArray();
    }

    public Rect2? RemovalSelectionBounds { get; set; }

    public int GetRemovalPreviewEdgeCount() => _removalPreviewEdgeIDs.Length;

    /// <summary>拆除工具悬停的 Edge ID（null = 未悬停在任何 Edge 上）</summary>
    public int? HoveredEdgeID { get; set; }

    public override void _Ready()
    {
        if (Config == null)
        {
            GD.PushError("RoadRenderer: Config (RoadConfig resource) is not assigned in the scene.");
            Config = new RoadConfig();
        }

        // 交叉口层：最后添加，渲染在所有 Line2D 之上
        _junctionLayer = new Node2D();
        AddChild(_junctionLayer);
        _junctionLayer.Draw += OnDrawJunctions;
    }

    public void SetGraph(RoadGraph graph)
    {
        if (_network != null)
        {
            _network.EdgeAdded -= OnEdgeAdded;
            _network.EdgeRemoved -= OnEdgeRemoved;
            _network.GraphCleared -= OnGraphCleared;
        }
        _network = graph;
        _network.EdgeAdded += OnEdgeAdded;
        _network.EdgeRemoved += OnEdgeRemoved;
        _network.GraphCleared += OnGraphCleared;
    }

    // ── 整网重载（存档加载后） ──

    private void OnGraphCleared()
    {
        // 清除所有现有 Line2D 节点
        foreach (var line in _edgeLines.Values)
            line.QueueFree();
        _edgeLines.Clear();

        // 重建所有 Edge 的 Line2D
        if (_network == null) return;
        foreach (var edge in _network.GetAllEdges())
            CreateEdgeLine(edge);

        _junctionLayer.QueueRedraw();
    }

    // ── Edge 增删 → Line2D 同步 ──

    private void OnEdgeAdded(GraphEdge edge)
    {
        CreateEdgeLine(edge);
        _junctionLayer.QueueRedraw();
    }

    private void CreateEdgeLine(GraphEdge edge)
    {
        if (_network == null) return;

        var nodeA = _network.GetNode(edge.NodeA);
        var nodeB = _network.GetNode(edge.NodeB);
        if (nodeA == null || nodeB == null) return;

        // 构建点序列：NodeA → Points → NodeB
        var edgePoints = edge.Points;
        var points = new Vector2[2 + edgePoints.Length];
        points[0] = nodeA.Position;
        for (int i = 0; i < edgePoints.Length; i++)
            points[i + 1] = edgePoints[i];
        points[^1] = nodeB.Position;

        var line = new Line2D
        {
            Points = points,
            Width = Config.RoadWidth,
            DefaultColor = Config.RoadColor,
            JointMode = Line2D.LineJointMode.Sharp,
            BeginCapMode = Line2D.LineCapMode.None,
            EndCapMode = Line2D.LineCapMode.None,
        };

        // 插入到 JunctionLayer 之前
        AddChild(line);
        MoveChild(line, _junctionLayer.GetIndex());
        _edgeLines[edge.ID] = line;
    }

    private void OnEdgeRemoved(GraphEdge edge)
    {
        if (_edgeLines.TryGetValue(edge.ID, out var line))
        {
            line.QueueFree();
            _edgeLines.Remove(edge.ID);
        }
        _junctionLayer.QueueRedraw();
    }

    // ── 交叉口绘制 ──

    private void OnDrawJunctions()
    {
        if (_network == null) return;

        foreach (var node in _network.GetAllNodes())
        {
            if (node.EdgeCount >= 2)
            {
                _junctionLayer.DrawCircle(node.Position, Config.JunctionRadius, Config.JunctionColor);
            }
            else if (node.EdgeCount == 1 && Config.EndpointRadius > 0f)
            {
                _junctionLayer.DrawCircle(node.Position, Config.EndpointRadius, Config.EndpointColor);
            }
        }
    }

    // ── RoadRenderer._Draw() 只画施工预览 ──

    public override void _Draw()
    {
        foreach (int edgeID in _removalPreviewEdgeIDs)
            DrawEdgeHighlight(edgeID);

        if (RemovalSelectionBounds is { } bounds && bounds.Size.X > 0f && bounds.Size.Y > 0f)
            DrawRect(bounds, Config.HoverHighlightColor, false, 2f);

        if (HoveredEdgeID.HasValue && _network != null)
            DrawEdgeHighlight(HoveredEdgeID.Value);

        for (int index = 1; index < _previewPoints.Length; index++)
        {
            Vector2 from = _previewPoints[index - 1];
            Vector2 to = _previewPoints[index];
            if (from != to)
                DrawDashedLine(from, to, new Color(1, 1, 1, 0.5f));
        }
    }

    private void DrawEdgeHighlight(int edgeID)
    {
        GraphEdge? edge = _network?.GetEdge(edgeID);
        if (edge == null || _network == null)
            return;

        GraphNode? nodeA = _network.GetNode(edge.NodeA);
        GraphNode? nodeB = _network.GetNode(edge.NodeB);
        if (nodeA == null || nodeB == null)
            return;

        Vector2[] edgePoints = edge.Points;
        var points = new Vector2[2 + edgePoints.Length];
        points[0] = nodeA.Position;
        for (int index = 0; index < edgePoints.Length; index++)
            points[index + 1] = edgePoints[index];
        points[^1] = nodeB.Position;
        DrawPolyline(points, Config.HoverHighlightColor, Config.HoverHighlightWidth);
        DrawCircle(nodeA.Position, Config.JunctionRadius * 1.3f, Config.HoverHighlightColor);
        DrawCircle(nodeB.Position, Config.JunctionRadius * 1.3f, Config.HoverHighlightColor);
    }

    // ── 虚线工具 ──

    private void DrawDashedLine(Vector2 from, Vector2 to, Color color, float width = 2f, float dashLength = 6f)
    {
        Vector2 delta = to - from;
        float total = delta.Length();
        if (total < 0.01f) return;

        Vector2 dir = delta / total;
        float drawn = 0f;
        bool draw = true;

        while (drawn < total)
        {
            float seg = Mathf.Min(dashLength, total - drawn);
            if (draw)
            {
                Vector2 start = from + dir * drawn;
                Vector2 end = start + dir * seg;
                DrawLine(start, end, color, width);
            }
            drawn += seg;
            draw = !draw;
        }
    }
}
