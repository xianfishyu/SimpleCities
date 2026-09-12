namespace SimpleCities.RoadCore;

public sealed record RoadProfile(RoadProfileId Id, double WidthMetres, byte Red, byte Green, byte Blue);

/// <summary>目录版本1的显示属性，不表达车道数或通行容量。</summary>
public static class RoadProfiles
{
    public static IReadOnlyList<RoadProfile> All { get; } = Array.AsReadOnly(new[]
    {
        new RoadProfile(RoadProfileId.Dirt, 8, 159, 118, 82),
        new RoadProfile(RoadProfileId.Street, 12, 112, 136, 150),
        new RoadProfile(RoadProfileId.Arterial, 24, 84, 139, 155),
        new RoadProfile(RoadProfileId.Highway, 32, 182, 153, 93),
    });

    public static RoadProfile Get(RoadProfileId id) => All.Single(profile => profile.Id == id);
}

/// <summary>一个凸表面片及其在规范道路边上的弧长来源；端帽投影截断，连接片使用零长区间。</summary>
public sealed class RoadSurfacePiece
{
    internal RoadSurfacePiece(RoadEdge edge, RoadPoint start, RoadPoint end,
        double startParameter, double endParameter, RoadPoint[] corners, NodeId? junctionNode = null)
    {
        Edge = edge;
        Start = start;
        End = end;
        StartParameter = startParameter;
        EndParameter = endParameter;
        Corners = Array.AsReadOnly(corners);
        JunctionNode = junctionNode;
    }
    public RoadEdge Edge { get; }
    public RoadPoint Start { get; }
    public RoadPoint End { get; }
    public double StartParameter { get; }
    public double EndParameter { get; }
    public IReadOnlyList<RoadPoint> Corners { get; }
    public NodeId? JunctionNode { get; }
}

/// <summary>后台生成的纯数值表面；Godot显式转换一次，绘制和命中共享转换结果。</summary>
public sealed class RoadSurfaceData
{
    internal RoadSurfaceData(IReadOnlyList<RoadNode> nodes, IReadOnlyList<RoadEdge> edges, IEnumerable<RoadSurfacePiece> pieces)
    {
        Nodes = Array.AsReadOnly(nodes.ToArray());
        Edges = Array.AsReadOnly(edges.ToArray());
        Pieces = Array.AsReadOnly(pieces.ToArray());
    }
    public IReadOnlyList<RoadNode> Nodes { get; }
    public IReadOnlyList<RoadEdge> Edges { get; }
    public IReadOnlyList<RoadSurfacePiece> Pieces { get; }
}

public static class RoadPresentation
{
    private sealed record EndConnection(RoadEdge Edge, RoadEndRole Role)
    {
        public RoadPoint Next => Role == RoadEndRole.Start ? Edge.Points[1] : Edge.Points[^2];
        public double Parameter => Role == RoadEndRole.Start ? 0 : 1;
    }

    public static RoadSurfaceData? Prepare(IReadOnlyList<RoadNode> nodes, IReadOnlyList<RoadEdge> edges, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (edges.Count == 0) return null;
        var incident = nodes.ToDictionary(node => node.Id, _ => new List<EndConnection>());
        foreach (RoadEdge edge in edges)
        {
            incident[edge.Start].Add(new EndConnection(edge, RoadEndRole.Start));
            incident[edge.End].Add(new EndConnection(edge, RoadEndRole.End));
        }
        var pieces = new List<RoadSurfacePiece>();
        var joins = new List<RoadSurfacePiece>();
        foreach (RoadEdge edge in edges)
        {
            double total = edge.Length;
            if (!double.IsFinite(total) || total <= 0)
                throw new InvalidOperationException("Cannot prepare a zero-length or non-finite road surface.");
            double half = RoadProfiles.Get(edge.Profile).WidthMetres / 2;
            double distance = 0;
            for (int i = 1; i < edge.Points.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RoadPoint a = edge.Points[i - 1], b = edge.Points[i];
                double length = a.DistanceTo(b);
                if (!double.IsFinite(length) || length <= 0)
                    throw new InvalidOperationException("Cannot prepare a zero-length or non-finite road segment.");
                double tx = (b.X - a.X) / length, ty = (b.Y - a.Y) / length;
                RoadPoint normal = new(-ty * half, tx * half);
                RoadPoint capStart = i == 1 && incident[edge.Start].Count == 1 ? new(a.X - tx * half, a.Y - ty * half) : a;
                RoadPoint capEnd = i == edge.Points.Count - 1 && incident[edge.End].Count == 1 ? new(b.X + tx * half, b.Y + ty * half) : b;
                double startParameter = distance / total;
                distance += length;
                double endParameter = i == edge.Points.Count - 1 ? 1 : distance / total;
                pieces.Add(new RoadSurfacePiece(edge, a, b, startParameter, endParameter,
                    [Add(capStart, normal), Add(capEnd, normal), Subtract(capEnd, normal), Subtract(capStart, normal)]));
                if (i < edge.Points.Count - 1)
                    AddJoin(joins, b, a, edge.Points[i + 1], edge, edge, endParameter, endParameter);
            }
        }
        foreach (RoadNode node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<EndConnection> connected = incident[node.Id];
            if (connected.Count >= 3)
            {
                AddJunction(joins, node, connected);
                continue;
            }
            if (connected.Count != 2) continue;
            EndConnection a = connected[0], b = connected[1];
            AddJoin(joins, node.Position, a.Next, b.Next, a.Edge, b.Edge, a.Parameter, b.Parameter);
        }
        pieces.AddRange(joins);
        return new RoadSurfaceData(nodes, edges, pieces);
    }

    private sealed record JunctionCorner(RoadPoint Point, RoadEdge Edge, double Parameter);

    private static void AddJunction(List<RoadSurfacePiece> pieces, RoadNode node, IReadOnlyList<EndConnection> connections)
    {
        RoadPoint center = node.Position;
        double reach = connections.Max(connection => RoadProfiles.Get(connection.Edge.Profile).WidthMetres / 2);
        var corners = new List<JunctionCorner>();
        foreach (EndConnection connection in connections)
        {
            RoadEdge edge = connection.Edge;
            RoadPoint next = connection.Next;
            double length = center.DistanceTo(next);
            double distance = Math.Min(reach, length / 2);
            RoadPoint mouth = new(center.X + (next.X - center.X) / length * distance,
                center.Y + (next.Y - center.Y) / length * distance);
            RoadPoint normal = Normal(center, next, RoadProfiles.Get(edge.Profile).WidthMetres / 2);
            double parameter = connection.Parameter;
            corners.Add(new(Add(center, normal), edge, parameter));
            corners.Add(new(Subtract(center, normal), edge, parameter));
            corners.Add(new(Add(mouth, normal), edge, parameter));
            corners.Add(new(Subtract(mouth, normal), edge, parameter));
        }

        // The convex envelope joins unequal-width mouths without unbounded miters.
        // Including both sides at the node also encloses its center for one-sided forks.
        JunctionCorner[] ordered = corners.OrderBy(corner => corner.Point.X).ThenBy(corner => corner.Point.Y)
            .ThenBy(corner => corner.Edge.Id.Value).ThenBy(corner => corner.Parameter)
            .DistinctBy(corner => corner.Point).ToArray();
        var lower = new List<JunctionCorner>();
        var upper = new List<JunctionCorner>();
        foreach (JunctionCorner corner in ordered) Append(lower, corner);
        foreach (JunctionCorner corner in ordered.Reverse()) Append(upper, corner);
        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        // Partition the existing envelope by angular end-role ownership. Several
        // mouths can contribute the same hull corner; choosing its first EdgeId
        // would erase another incidence (including the other end of a self-loop).
        var owners = connections.Select(connection =>
        {
            double length = center.DistanceTo(connection.Next);
            var outward = new RoadPoint((connection.Next.X - center.X) / length,
                (connection.Next.Y - center.Y) / length);
            double angle = Math.Atan2(outward.Y, outward.X);
            if (angle < 0) angle += Math.Tau;
            return (Connection: connection, Outward: outward, Angle: angle);
        }).OrderBy(owner => owner.Angle).ThenBy(owner => owner.Connection.Edge.Id.Value)
            .ThenBy(owner => owner.Connection.Role).ToArray();
        var boundaries = new List<RoadPoint>();
        for (int i = 0; i < owners.Length; i++)
        {
            double nextAngle = i + 1 == owners.Length ? owners[0].Angle + Math.Tau : owners[i + 1].Angle;
            double middleAngle = (owners[i].Angle + nextAngle) / 2;
            boundaries.Add(new RoadPoint(Math.Cos(middleAngle), Math.Sin(middleAngle)));
        }
        for (int i = 0; i < lower.Count; i++)
        {
            RoadPoint a = lower[i].Point, b = lower[(i + 1) % lower.Count].Point;
            if (Cross(center, a, b) == 0) continue;
            var cuts = new List<double> { 0, 1 };
            RoadPoint relative = Subtract(a, center), direction = Subtract(b, a);
            foreach (RoadPoint ray in boundaries)
            {
                double denominator = direction.X * ray.Y - direction.Y * ray.X;
                if (Math.Abs(denominator) < 1e-12) continue;
                double t = (relative.Y * ray.X - relative.X * ray.Y) / denominator;
                if (t <= 1e-12 || t >= 1 - 1e-12) continue;
                RoadPoint hit = new(relative.X + direction.X * t, relative.Y + direction.Y * t);
                if (hit.X * ray.X + hit.Y * ray.Y > 0) cuts.Add(t);
            }
            cuts.Sort();
            for (int j = 1; j < cuts.Count; j++)
            {
                RoadPoint start = new(a.X + direction.X * cuts[j - 1], a.Y + direction.Y * cuts[j - 1]);
                RoadPoint end = new(a.X + direction.X * cuts[j], a.Y + direction.Y * cuts[j]);
                if (Cross(center, start, end) == 0) continue;
                RoadPoint middle = new((start.X + end.X) / 2 - center.X, (start.Y + end.Y) / 2 - center.Y);
                EndConnection owner = owners.OrderByDescending(candidate => middle.X * candidate.Outward.X + middle.Y * candidate.Outward.Y)
                    .ThenBy(candidate => candidate.Connection.Edge.Id.Value).ThenBy(candidate => candidate.Connection.Role).First().Connection;
                pieces.Add(new RoadSurfacePiece(owner.Edge, center, center, owner.Parameter, owner.Parameter,
                    [center, start, end], node.Id));
            }
        }

        static void Append(List<JunctionCorner> hull, JunctionCorner corner)
        {
            while (hull.Count >= 2 && Cross(hull[^2].Point, hull[^1].Point, corner.Point) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(corner);
        }
    }

    private static double Cross(RoadPoint a, RoadPoint b, RoadPoint c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static void AddJoin(List<RoadSurfacePiece> pieces, RoadPoint center, RoadPoint aNext, RoadPoint bNext,
        RoadEdge a, RoadEdge b, double aParameter, double bParameter)
    {
        RoadPoint aNormal = Normal(center, aNext, RoadProfiles.Get(a.Profile).WidthMetres / 2);
        RoadPoint bNormal = Normal(center, bNext, RoadProfiles.Get(b.Profile).WidthMetres / 2);
        AddSide(Add(center, aNormal), Subtract(center, bNormal));
        AddSide(Subtract(center, aNormal), Add(center, bNormal));

        void AddSide(RoadPoint aCorner, RoadPoint bCorner)
        {
            double cross = (aCorner.X - center.X) * (bCorner.Y - center.Y) - (aCorner.Y - center.Y) * (bCorner.X - center.X);
            if (cross == 0) return;
            RoadPoint middle = new((aCorner.X + bCorner.X) / 2, (aCorner.Y + bCorner.Y) / 2);
            pieces.Add(new RoadSurfacePiece(a, center, center, aParameter, aParameter, [center, aCorner, middle]));
            pieces.Add(new RoadSurfacePiece(b, center, center, bParameter, bParameter, [center, middle, bCorner]));
        }
    }

    private static RoadPoint Normal(RoadPoint start, RoadPoint end, double half)
    {
        double length = start.DistanceTo(end);
        return new(-(end.Y - start.Y) / length * half, (end.X - start.X) / length * half);
    }
    private static RoadPoint Add(RoadPoint a, RoadPoint b) => new(a.X + b.X, a.Y + b.Y);
    private static RoadPoint Subtract(RoadPoint a, RoadPoint b) => new(a.X - b.X, a.Y - b.Y);
}
