namespace SimpleCities.RoadCore;

/// <summary>当前开放折线切片的规范性与显式资源上限；不接受尚未支持的交叉或闭环。</summary>
internal static class RoadTopology
{
    internal const int MaximumNodes = 512;
    internal const int MaximumEdges = 256;
    internal const int MaximumPoints = 2048;

    internal static bool IsForwardCollinear(RoadPoint a, RoadPoint b, RoadPoint c) =>
        Cross(b.X - a.X, b.Y - a.Y, c.X - b.X, c.Y - b.Y) == 0 &&
        (b.X - a.X) * (c.X - b.X) + (b.Y - a.Y) * (c.Y - b.Y) > 0;

    internal static void Validate(MapDefinition map, IReadOnlyList<RoadNode> nodes, IReadOnlyList<RoadEdge> edges, long nextNodeId, long nextEdgeId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (nextNodeId <= 0 || nextNodeId == long.MaxValue || nextEdgeId <= 0 || nextEdgeId == long.MaxValue)
            throw new InvalidDataException("道路身份水位无效");
        if (nodes.Count > MaximumNodes || edges.Count > MaximumEdges || edges.Sum(edge => edge.Points.Count) > MaximumPoints)
            throw new InvalidDataException("超过当前开放折线的资源上限");
        if (nodes.Count == 0 && edges.Count == 0) return;
        if (edges.Count != nodes.Count - 1 || edges.Count == 0)
            throw new InvalidDataException("当前只支持无分叉开放折线，不支持闭环");
        var byId = new Dictionary<NodeId, RoadNode>();
        var positions = new HashSet<RoadPoint>();
        long lastId = 0;
        foreach (RoadNode node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Id.Value <= lastId || node.Id.Value >= nextNodeId || !map.IsPrimaryPoint(node.Position) || !positions.Add(node.Position))
                throw new InvalidDataException("节点身份、排序、水位或主格点位置无效");
            byId.Add(node.Id, node);
            lastId = node.Id.Value;
        }
        var incident = nodes.ToDictionary(node => node.Id, _ => new List<RoadEdge>());
        var segments = new List<Segment>();
        lastId = 0;
        foreach (RoadEdge edge in edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (edge.Id.Value <= lastId || edge.Id.Value >= nextEdgeId || edge.Start.Value >= edge.End.Value || !edge.Profile.IsValid ||
                !byId.TryGetValue(edge.Start, out RoadNode? start) || !byId.TryGetValue(edge.End, out RoadNode? end) ||
                edge.Points.Count < 2 || edge.Points[0] != start.Position || edge.Points[^1] != end.Position)
                throw new InvalidDataException("道路身份、端点、方向或类型无效");
            lastId = edge.Id.Value;
            incident[edge.Start].Add(edge);
            incident[edge.End].Add(edge);
            for (int i = 0; i < edge.Points.Count; i++)
            {
                if (!map.IsPrimaryPoint(edge.Points[i])) throw new InvalidDataException("道路折点不在地图内的主格点上");
                if (i == 0) continue;
                RoadPoint a = edge.Points[i - 1], b = edge.Points[i];
                if (a == b || !map.IsEightDirection(a, b)) throw new InvalidDataException("道路格段必须具有正长度并沿八方向");
                if (i >= 2 && IsForwardCollinear(edge.Points[i - 2], a, b)) throw new InvalidDataException("道路含多余共线折点");
                segments.Add(new Segment(edge.Id, i, a, b, i == 1 ? edge.Start : null, i == edge.Points.Count - 1 ? edge.End : null));
            }
        }
        foreach (List<RoadEdge> connected in incident.Values)
            if (connected.Count is < 1 or > 2 || (connected.Count == 2 && connected[0].Profile == connected[1].Profile))
                throw new InvalidDataException("当前只支持开放端点和必要的道路类型分界");
        var visited = new HashSet<NodeId>();
        var pending = new Stack<NodeId>();
        pending.Push(nodes[0].Id);
        while (pending.TryPop(out NodeId id))
        {
            if (!visited.Add(id)) continue;
            foreach (RoadEdge edge in incident[id]) pending.Push(edge.Start == id ? edge.End : edge.Start);
        }
        if (visited.Count != nodes.Count) throw new InvalidDataException("当前道路必须形成一条连通开放折线");
        for (int i = 0; i < segments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int j = i + 1; j < segments.Count; j++)
                ValidatePair(segments[i], segments[j]);
        }
    }

    private sealed record Segment(EdgeId Edge, int Index, RoadPoint A, RoadPoint B, NodeId? Start, NodeId? End);
    private static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;
    private static void ValidatePair(Segment a, Segment b)
    {
        double rx = a.B.X - a.A.X, ry = a.B.Y - a.A.Y;
        double sx = b.B.X - b.A.X, sy = b.B.Y - b.A.Y;
        double qx = b.A.X - a.A.X, qy = b.A.Y - a.A.Y;
        double denominator = Cross(rx, ry, sx, sy);
        if (denominator == 0)
        {
            if (Cross(qx, qy, rx, ry) != 0) return;
            double a0 = rx != 0 ? a.A.X : a.A.Y, a1 = rx != 0 ? a.B.X : a.B.Y;
            double b0 = rx != 0 ? b.A.X : b.A.Y, b1 = rx != 0 ? b.B.X : b.B.Y;
            double lower = Math.Max(Math.Min(a0, a1), Math.Min(b0, b1));
            double upper = Math.Min(Math.Max(a0, a1), Math.Max(b0, b1));
            if (lower > upper) return;
            if (lower < upper) throw new InvalidDataException("道路中心线存在共线重叠");
        }
        else
        {
            double t = Cross(qx, qy, sx, sy) / denominator;
            double u = Cross(qx, qy, rx, ry) / denominator;
            if (t < 0 || t > 1 || u < 0 || u > 1) return;
        }
        bool adjacent = a.Edge == b.Edge && Math.Abs(a.Index - b.Index) == 1;
        bool joined = (a.A == b.A && a.Start.HasValue && a.Start == b.Start) ||
            (a.A == b.B && a.Start.HasValue && a.Start == b.End) ||
            (a.B == b.A && a.End.HasValue && a.End == b.Start) ||
            (a.B == b.B && a.End.HasValue && a.End == b.End);
        if (!adjacent && !joined) throw new InvalidDataException("道路相交或触及内部折点，路口功能尚未接入");
    }
}
