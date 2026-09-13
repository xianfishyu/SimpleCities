namespace SimpleCities.RoadCore;

/// <summary>米字网格路网的规范性与显式资源上限，包含 rooted 自环和不同路径平行边。</summary>
internal static class RoadTopology
{
    internal const int MaximumNodes = 32768;
    internal const int MaximumEdges = 16384;
    internal const int MaximumPoints = 131072;

    internal static bool IsForwardCollinear(RoadPoint a, RoadPoint b, RoadPoint c) =>
        Cross(b.X - a.X, b.Y - a.Y, c.X - b.X, c.Y - b.Y) == 0 &&
        (b.X - a.X) * (c.X - b.X) + (b.Y - a.Y) * (c.Y - b.Y) > 0;

    internal static bool IsCanonicalLoopDirection(IReadOnlyList<RoadPoint> points)
    {
        for (int i = 1; i < points.Count - 1; i++)
        {
            RoadPoint forward = points[i], reverse = points[points.Count - 1 - i];
            int comparison = forward.X.CompareTo(reverse.X);
            if (comparison == 0) comparison = forward.Y.CompareTo(reverse.Y);
            if (comparison != 0) return comparison < 0;
        }
        return true;
    }

    internal static void Validate(MapDefinition map, IReadOnlyList<RoadNode> nodes, IReadOnlyList<RoadEdge> edges, long nextNodeId, long nextEdgeId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (nextNodeId <= 0 || nextNodeId == long.MaxValue || nextEdgeId <= 0 || nextEdgeId == long.MaxValue)
            throw new InvalidDataException("道路身份水位无效");
        if (nodes.Count > MaximumNodes || edges.Count > MaximumEdges || edges.Sum(edge => edge.Points.Count) > MaximumPoints)
            throw new InvalidDataException("超过当前道路路网的资源上限");
        if (nodes.Count == 0 && edges.Count == 0) return;
        var byId = new Dictionary<NodeId, RoadNode>();
        var positions = new HashSet<RoadPoint>();
        long lastId = 0;
        foreach (RoadNode node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Id.Value <= lastId || node.Id.Value >= nextNodeId || !map.IsBuildPoint(node.Position) || !positions.Add(node.Position))
                throw new InvalidDataException("节点身份、排序、水位或主格点/格心位置无效");
            byId.Add(node.Id, node);
            lastId = node.Id.Value;
        }
        var incident = nodes.ToDictionary(node => node.Id, _ => new List<RoadEdge>());
        var segments = new List<Segment>();
        lastId = 0;
        foreach (RoadEdge edge in edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (edge.Id.Value <= lastId || edge.Id.Value >= nextEdgeId || edge.Start.Value > edge.End.Value || !edge.Profile.IsValid ||
                !byId.TryGetValue(edge.Start, out RoadNode? start) || !byId.TryGetValue(edge.End, out RoadNode? end) ||
                edge.Points.Count < 2 || edge.Points[0] != start.Position || edge.Points[^1] != end.Position)
                throw new InvalidDataException("道路身份、端点、方向或类型无效");
            if (edge.Start == edge.End && !IsCanonicalLoopDirection(edge.Points))
                throw new InvalidDataException("自环必须在既定 seam 上采用规范方向");
            lastId = edge.Id.Value;
            incident[edge.Start].Add(edge);
            incident[edge.End].Add(edge);
            for (int i = 0; i < edge.Points.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!map.IsBuildPoint(edge.Points[i])) throw new InvalidDataException("道路折点不在地图内的主格点或格心上");
                if (i == 0) continue;
                RoadPoint a = edge.Points[i - 1], b = edge.Points[i];
                if (!map.IsBuildSegment(a, b)) throw new InvalidDataException("道路格段必须具有正长度，主格点沿八方向，格心仅沿对角方向");
                if (i >= 2 && IsForwardCollinear(edge.Points[i - 2], a, b)) throw new InvalidDataException("道路含多余共线折点");
                segments.Add(new Segment(edge.Id, i, a, b, i == 1 ? edge.Start : null, i == edge.Points.Count - 1 ? edge.End : null));
            }
        }
        foreach (List<RoadEdge> connected in incident.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (connected.Count < 1 || (connected.Count == 2 && connected[0].Profile == connected[1].Profile &&
                !ReferenceEquals(connected[0], connected[1])))
                throw new InvalidDataException("结构节点必须是道路端点、真实路口或必要的道路类型分界");
        }
        ValidateIntersections(map, segments, cancellationToken);
    }

    private static void ValidateIntersections(MapDefinition map, IReadOnlyList<Segment> segments, CancellationToken cancellationToken)
    {
        // Under the validated square-eight rules every possible intersection is
        // on the half-cell lattice. Enumerating closed segments includes endpoint
        // touches and overlaps, while a long diagonal costs length, not AABB area.
        // This completeness argument does not apply to future free-angle curves.
        var atPoint = new Dictionary<(int X, int Y), List<int>>();
        double halfCell = map.CellSizeMetres / 2d;
        var checkedCandidates = new HashSet<int>();
        for (int i = 0; i < segments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Segment segment = segments[i];
            int x = (int)(segment.A.X / halfCell), y = (int)(segment.A.Y / halfCell);
            int endX = (int)(segment.B.X / halfCell), endY = (int)(segment.B.Y / halfCell);
            int dx = Math.Sign(endX - x), dy = Math.Sign(endY - y);
            int steps = Math.Max(Math.Abs(endX - x), Math.Abs(endY - y));
            checkedCandidates.Clear();
            for (int step = 0; step <= steps; step++, x += dx, y += dy)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!atPoint.TryGetValue((x, y), out List<int>? candidates))
                {
                    candidates = new List<int>();
                    atPoint.Add((x, y), candidates);
                }
                foreach (int candidate in candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (checkedCandidates.Add(candidate)) ValidatePair(segments[candidate], segment);
                }
                candidates.Add(i);
            }
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
        if (!adjacent && !joined) throw new InvalidDataException("道路交叉或接入位置缺少共同结构节点");
    }
}
