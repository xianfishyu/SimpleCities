using Godot;
using System.Collections.Generic;

public partial class RoadRenderer : Node2D
{
    [Export] public RoadConfig Config { get; set; } = null!;

    private RoadNetwork? _network;

    // Segment.ID → Line2D 节点映射
    private readonly Dictionary<int, Line2D> _segmentLines = new();

    // 交叉口渲染层（放在所有 Line2D 之后，保证渲染在最上层）
    private Node2D _junctionLayer = null!;

    // 施工预览
    public Vector2? PreviewFrom { get; set; }
    public Vector2? PreviewTo { get; set; }

    /// <summary>拆除工具悬停的 Segment ID（null = 未悬停在任何 Segment 上）</summary>
    public int? HoveredSegmentID { get; set; }

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

    public void SetNetwork(RoadNetwork network)
    {
        if (_network != null)
        {
            _network.SegmentAdded -= OnSegmentAdded;
            _network.SegmentRemoved -= OnSegmentRemoved;
            _network.NetworkReloaded -= OnNetworkReloaded;
        }
        _network = network;
        _network.SegmentAdded += OnSegmentAdded;
        _network.SegmentRemoved += OnSegmentRemoved;
        _network.NetworkReloaded += OnNetworkReloaded;
    }

    // ── 整网重载（存档加载后） ──

    private void OnNetworkReloaded()
    {
        // 清除所有现有 Line2D 节点
        foreach (var line in _segmentLines.Values)
            line.QueueFree();
        _segmentLines.Clear();

        // 重建所有 Segment 的 Line2D
        if (_network == null) return;
        foreach (var seg in _network.GetAllSegments())
            CreateSegmentLine(seg);

        _junctionLayer.QueueRedraw();
    }

    // ── Segment 增删 → Line2D 同步 ──

    private void OnSegmentAdded(Segment seg)
    {
        CreateSegmentLine(seg);
        _junctionLayer.QueueRedraw();
    }

    private void CreateSegmentLine(Segment seg)
    {
        if (_network == null) return;

        var fromJ = _network.GetJunction(seg.FromJunctionID);
        var toJ = _network.GetJunction(seg.ToJunctionID);
        if (fromJ == null || toJ == null) return;

        // 构建点序列：FromJunction → Waypoints → ToJunction
        var points = new Vector2[2 + seg.Waypoints.Length];
        points[0] = fromJ.Position;
        for (int i = 0; i < seg.Waypoints.Length; i++)
            points[i + 1] = seg.Waypoints[i];
        points[^1] = toJ.Position;

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
        _segmentLines[seg.ID] = line;
    }

    private void OnSegmentRemoved(Segment seg)
    {
        if (_segmentLines.TryGetValue(seg.ID, out var line))
        {
            line.QueueFree();
            _segmentLines.Remove(seg.ID);
        }
        _junctionLayer.QueueRedraw();
    }

    // ── 交叉口绘制 ──

    private void OnDrawJunctions()
    {
        if (_network == null) return;

        // ConnectionCount == 1 → 端点（细小灰色圆，区别于路面色）
        // ConnectionCount >= 2 → 真路口（明显的高亮色圆，让 T 字 / 十字 / 转弯点视觉可辨）
        // 注：合并阶段已把"对向直通"的 ConnectionCount==2 节点降级回 waypoint，
        //     所以剩下 ConnectionCount==2 的 Junction 一定是"非对向"的转弯点（Curve 类型），仍当真路口画。
        foreach (var junction in _network.GetAllJunctions())
        {
            if (junction.ConnectionCount >= 2)
            {
                _junctionLayer.DrawCircle(junction.Position, Config.JunctionRadius, Config.JunctionColor);
            }
            else if (junction.ConnectionCount == 1 && Config.EndpointRadius > 0f)
            {
                _junctionLayer.DrawCircle(junction.Position, Config.EndpointRadius, Config.EndpointColor);
            }
        }
    }

    // ── RoadRenderer._Draw() 只画施工预览 ──

    public override void _Draw()
    {
        // 拆除工具悬停高亮：画在预览虚线之上
        if (HoveredSegmentID.HasValue && _network != null)
        {
            var seg = _network.GetSegment(HoveredSegmentID.Value);
            if (seg != null)
            {
                var fj = _network.GetJunction(seg.FromJunctionID);
                var tj = _network.GetJunction(seg.ToJunctionID);
                if (fj != null && tj != null)
                {
                    var points = new Vector2[2 + seg.Waypoints.Length];
                    points[0] = fj.Position;
                    for (int i = 0; i < seg.Waypoints.Length; i++)
                        points[i + 1] = seg.Waypoints[i];
                    points[^1] = tj.Position;
                    DrawPolyline(points, Config.HoverHighlightColor, Config.HoverHighlightWidth);
                    // 同时高亮端点
                    DrawCircle(fj.Position, Config.JunctionRadius * 1.3f, Config.HoverHighlightColor);
                    DrawCircle(tj.Position, Config.JunctionRadius * 1.3f, Config.HoverHighlightColor);
                }
            }
        }

        if (PreviewFrom.HasValue && PreviewTo.HasValue && PreviewFrom.Value != PreviewTo.Value)
        {
            DrawDashedLine(PreviewFrom.Value, PreviewTo.Value, new Color(1, 1, 1, 0.5f));
        }
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
