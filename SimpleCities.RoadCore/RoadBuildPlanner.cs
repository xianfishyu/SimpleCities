namespace SimpleCities.RoadCore;

/// <summary>在私有内容中切分点交叉并恢复规范边，完成验证后才产生可发布计划。</summary>
internal static class RoadBuildPlanner
{
    private sealed record Piece(EdgeId? Id, NodeId Start, NodeId End, RoadProfileId Profile, List<RoadPoint> Points);

    internal static RoadBuildResult Plan(RoadNetwork owner, RoadSnapshot source, RoadBuildRequest request,
        CancellationToken cancellationToken)
    {
        var draftCuts = new HashSet<RoadPoint> { request.Start, request.End };
        var edgeCuts = new Dictionary<EdgeId, HashSet<RoadPoint>>();
        foreach (RoadEdge edge in source.Edges)
        {
            var cuts = new HashSet<RoadPoint> { edge.Points[0], edge.Points[^1] };
            for (int i = 1; i < edge.Points.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RoadPoint? intersection = Intersect(request.Start, request.End, edge.Points[i - 1], edge.Points[i]);
                if (!intersection.HasValue) continue;
                if (!source.Map.IsPrimaryPoint(intersection.Value))
                    throw new InvalidDataException("格心路口尚未接入，请在主格点交叉或接入道路");
                cuts.Add(intersection.Value);
                draftCuts.Add(intersection.Value);
            }
            edgeCuts.Add(edge.Id, cuts);
        }

        long nextNode = source.NextNodeId, nextEdge = source.NextEdgeId;
        var nodes = source.Nodes.ToDictionary(node => node.Position);
        // 端点优先，再沿草稿顺序分配交点身份；不依赖集合的枚举顺序。
        IEnumerable<RoadPoint> allocationOrder = new[] { request.Start, request.End }.Concat(draftCuts
            .Where(point => point != request.Start && point != request.End)
            .OrderBy(point => Parameter(point, request.Start, request.End)));
        foreach (RoadPoint position in allocationOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!nodes.ContainsKey(position))
                nodes.Add(position, new RoadNode(new NodeId(Allocate(ref nextNode)), position));
        }
        var pieces = new List<Piece>();
        foreach (RoadEdge edge in source.Edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Split(edge.Id, edge.Profile, edge.Points, edgeCuts[edge.Id], nodes, pieces, cancellationToken);
        }
        Split(null, request.Profile, [request.Start, request.End], draftCuts, nodes, pieces, cancellationToken);
        Normalize(nodes, pieces, cancellationToken);

        var edges = new List<RoadEdge>(pieces.Count);
        foreach (Piece piece in pieces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EdgeId id = piece.Id ?? new EdgeId(Allocate(ref nextEdge));
            edges.Add(piece.Start.Value < piece.End.Value
                ? new RoadEdge(id, piece.Start, piece.End, piece.Profile, piece.Points)
                : new RoadEdge(id, piece.End, piece.Start, piece.Profile, piece.Points.AsEnumerable().Reverse()));
        }
        RoadNode[] orderedNodes = nodes.Values.OrderBy(node => node.Id.Value).ToArray();
        edges.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        RoadTopology.Validate(source.Map, orderedNodes, edges, nextNode, nextEdge, cancellationToken);
        RoadStateToken token = source.Token with
        {
            ContentRevision = source.Token.ContentRevision + 1,
            ChangeSequence = source.Token.ChangeSequence + 1,
        };
        var target = new RoadSnapshot(source.Map, token, nextNode, nextEdge, orderedNodes, edges);
        cancellationToken.ThrowIfCancellationRequested();
        return new RoadBuildResult(RoadBuildStatus.Ready,
            new RoadPlan(owner, source, target, checked((int)(nextNode - source.NextNodeId)), checked((int)(nextEdge - source.NextEdgeId))), "");
    }

    private static long Allocate(ref long next)
    {
        if (next >= long.MaxValue - 1) throw new InvalidDataException("道路身份或版本已耗尽");
        return next++;
    }

    private static void Split(EdgeId? originalId, RoadProfileId profile, IReadOnlyList<RoadPoint> points,
        HashSet<RoadPoint> cuts, Dictionary<RoadPoint, RoadNode> nodes, List<Piece> destination,
        CancellationToken cancellationToken)
    {
        var current = new List<RoadPoint> { points[0] };
        EdgeId? id = originalId;
        for (int i = 1; i < points.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RoadPoint a = points[i - 1], b = points[i];
            IEnumerable<RoadPoint> ordered = cuts.Where(point => point != a && OnSegment(point, a, b))
                .Append(b).Distinct().OrderBy(point => Parameter(point, a, b));
            foreach (RoadPoint point in ordered)
            {
                current.Add(point);
                if (!cuts.Contains(point)) continue;
                destination.Add(new Piece(id, nodes[current[0]].Id, nodes[point].Id, profile, current));
                id = null;
                current = [point];
            }
        }
    }

    private static void Normalize(Dictionary<RoadPoint, RoadNode> nodes, List<Piece> pieces,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var incident = nodes.Values.ToDictionary(node => node.Id, _ => new List<Piece>());
            foreach (Piece piece in pieces)
            {
                incident[piece.Start].Add(piece);
                incident[piece.End].Add(piece);
            }
            RoadNode? removable = nodes.Values.OrderBy(node => node.Id.Value).FirstOrDefault(node => incident[node.Id].Count == 2 &&
                incident[node.Id][0].Profile == incident[node.Id][1].Profile);
            if (removable is null) return;
            Piece a = incident[removable.Id][0], b = incident[removable.Id][1];
            if (ReferenceEquals(a, b)) throw new InvalidDataException("闭环道路尚未接入");
            NodeId start = a.Start == removable.Id ? a.End : a.Start;
            NodeId end = b.Start == removable.Id ? b.End : b.Start;
            if (start == end) throw new InvalidDataException("闭环道路尚未接入");
            IEnumerable<RoadPoint> first = a.End == removable.Id ? a.Points : a.Points.AsEnumerable().Reverse();
            IEnumerable<RoadPoint> second = b.Start == removable.Id ? b.Points : b.Points.AsEnumerable().Reverse();
            var joined = new List<RoadPoint>();
            foreach (RoadPoint point in first.Concat(second.Skip(1)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (joined.Count >= 2 && RoadTopology.IsForwardCollinear(joined[^2], joined[^1], point))
                    joined.RemoveAt(joined.Count - 1);
                joined.Add(point);
            }
            EdgeId? retained = a.Id.HasValue && b.Id.HasValue
                ? (a.Id.Value.Value < b.Id.Value.Value ? a.Id : b.Id) : a.Id ?? b.Id;
            pieces.Remove(a);
            pieces.Remove(b);
            pieces.Add(new Piece(retained, start, end, a.Profile, joined));
            nodes.Remove(removable.Position);
        }
    }

    private static double Parameter(RoadPoint p, RoadPoint a, RoadPoint b) =>
        a.X != b.X ? (p.X - a.X) / (b.X - a.X) : (p.Y - a.Y) / (b.Y - a.Y);

    private static bool OnSegment(RoadPoint p, RoadPoint a, RoadPoint b) =>
        Cross(p.X - a.X, p.Y - a.Y, b.X - a.X, b.Y - a.Y) == 0 &&
        p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X) &&
        p.Y >= Math.Min(a.Y, b.Y) && p.Y <= Math.Max(a.Y, b.Y);

    private static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;

    private static RoadPoint? Intersect(RoadPoint a, RoadPoint b, RoadPoint c, RoadPoint d)
    {
        double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y;
        double denominator = Cross(rx, ry, sx, sy);
        if (denominator == 0)
        {
            // 调用入口已优先拒绝正长度共线重叠，此处仅余端点触碰。
            foreach (RoadPoint point in new[] { a, b })
                if (OnSegment(point, c, d)) return point;
            return null;
        }
        double numerator = Cross(c.X - a.X, c.Y - a.Y, sx, sy);
        double other = Cross(c.X - a.X, c.Y - a.Y, rx, ry);
        if (denominator > 0
            ? numerator < 0 || numerator > denominator || other < 0 || other > denominator
            : numerator > 0 || numerator < denominator || other > 0 || other < denominator)
            return null;
        // 有界整数米坐标的行列式精确；先相乘相加后相除，避免 t 插值引入格点误差。
        return new RoadPoint((a.X * denominator + rx * numerator) / denominator,
            (a.Y * denominator + ry * numerator) / denominator);
    }
}
