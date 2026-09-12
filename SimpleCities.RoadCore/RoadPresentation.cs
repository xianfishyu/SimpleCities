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
        double startParameter, double endParameter, RoadPoint[] corners)
    {
        Edge = edge;
        Start = start;
        End = end;
        StartParameter = startParameter;
        EndParameter = endParameter;
        Corners = Array.AsReadOnly(corners);
    }
    public RoadEdge Edge { get; }
    public RoadPoint Start { get; }
    public RoadPoint End { get; }
    public double StartParameter { get; }
    public double EndParameter { get; }
    public IReadOnlyList<RoadPoint> Corners { get; }
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
    public static RoadSurfaceData? Prepare(IReadOnlyList<RoadNode> nodes, IReadOnlyList<RoadEdge> edges, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (edges.Count == 0) return null;
        var incident = nodes.ToDictionary(node => node.Id, _ => new List<RoadEdge>());
        foreach (RoadEdge edge in edges)
        {
            incident[edge.Start].Add(edge);
            incident[edge.End].Add(edge);
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
            List<RoadEdge> connected = incident[node.Id];
            if (connected.Count != 2) continue;
            RoadEdge a = connected[0], b = connected[1];
            RoadPoint aNext = a.Start == node.Id ? a.Points[1] : a.Points[^2];
            RoadPoint bNext = b.Start == node.Id ? b.Points[1] : b.Points[^2];
            AddJoin(joins, node.Position, aNext, bNext, a, b, a.Start == node.Id ? 0 : 1, b.Start == node.Id ? 0 : 1);
        }
        pieces.AddRange(joins);
        return new RoadSurfaceData(nodes, edges, pieces);
    }

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
