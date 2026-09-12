namespace SimpleCities.RoadCore;

/// <summary>建造与格段编辑共用的私有切分和规范化操作，不发布活动状态。</summary>
internal static class RoadMutationDraft
{
    internal sealed record Piece(EdgeId? Id, NodeId Start, NodeId End, RoadProfileId Profile, List<RoadPoint> Points);

    internal static RoadPlan Freeze(RoadNetwork owner, RoadSnapshot source, Dictionary<RoadPoint, RoadNode> nodes,
        List<Piece> pieces, long nextNode, long nextEdge, CancellationToken cancellationToken)
    {
        var edges = new List<RoadEdge>(pieces.Count);
        foreach (Piece piece in pieces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EdgeId id = piece.Id ?? new EdgeId(Allocate(ref nextEdge));
            bool forward = piece.Start.Value < piece.End.Value ||
                (piece.Start == piece.End && RoadTopology.IsCanonicalLoopDirection(piece.Points));
            edges.Add(forward
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
        return new RoadPlan(owner, source, target, checked((int)(nextNode - source.NextNodeId)),
            checked((int)(nextEdge - source.NextEdgeId)));
    }

    internal static long Allocate(ref long next)
    {
        if (next >= long.MaxValue - 1) throw new InvalidDataException("道路身份或版本已耗尽");
        return next++;
    }

    internal static void Split(EdgeId? originalId, RoadProfileId profile, IReadOnlyList<RoadPoint> points,
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

    internal static void Normalize(Dictionary<RoadPoint, RoadNode> nodes, List<Piece> pieces,
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
            // Higher IDs disappear first, so an isolated degree-two ring retains its
            // smallest existing NodeId as the rooted seam. A true junction is never
            // removable, regardless of its ID or the former seam's ID.
            RoadNode? removable = nodes.Values.OrderByDescending(node => node.Id.Value).FirstOrDefault(node => incident[node.Id].Count == 2 &&
                !ReferenceEquals(incident[node.Id][0], incident[node.Id][1]) &&
                incident[node.Id][0].Profile == incident[node.Id][1].Profile);
            if (removable is null) return;
            Piece a = incident[removable.Id][0], b = incident[removable.Id][1];
            NodeId start = a.Start == removable.Id ? a.End : a.Start;
            NodeId end = b.Start == removable.Id ? b.End : b.Start;
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

    internal static double Parameter(RoadPoint p, RoadPoint a, RoadPoint b) =>
        a.X != b.X ? (p.X - a.X) / (b.X - a.X) : (p.Y - a.Y) / (b.Y - a.Y);

    internal static bool OnSegment(RoadPoint p, RoadPoint a, RoadPoint b) =>
        Cross(p.X - a.X, p.Y - a.Y, b.X - a.X, b.Y - a.Y) == 0 &&
        p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X) &&
        p.Y >= Math.Min(a.Y, b.Y) && p.Y <= Math.Max(a.Y, b.Y);

    internal static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;

}
