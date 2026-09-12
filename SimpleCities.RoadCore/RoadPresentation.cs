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

/// <summary>后台生成的纯数值表面；Godot显式转换一次，绘制和命中共享转换结果。</summary>
public sealed class RoadSurfaceData
{
    internal RoadSurfaceData(RoadEdge edge, RoadPoint start, RoadPoint end, RoadPoint[] corners)
    {
        Edge = edge;
        Start = start;
        End = end;
        Corners = Array.AsReadOnly(corners);
    }
    public RoadEdge Edge { get; }
    public RoadPoint Start { get; }
    public RoadPoint End { get; }
    public IReadOnlyList<RoadPoint> Corners { get; }
}

public static class RoadPresentation
{
    public static RoadSurfaceData? Prepare(IReadOnlyList<RoadNode> nodes, IReadOnlyList<RoadEdge> edges)
    {
        if (edges.Count == 0) return null;
        RoadEdge edge = edges.Single();
        RoadPoint a = nodes.Single(node => node.Id == edge.Start).Position;
        RoadPoint b = nodes.Single(node => node.Id == edge.End).Position;
        double length = a.DistanceTo(b);
        double half = RoadProfiles.Get(edge.Profile).WidthMetres / 2;
        double nx = -(b.Y - a.Y) / length * half;
        double ny = (b.X - a.X) / length * half;
        if (!double.IsFinite(nx) || !double.IsFinite(ny))
            throw new InvalidOperationException("Cannot prepare a zero-length or non-finite road surface.");
        return new RoadSurfaceData(edge, a, b,
            [new(a.X + nx, a.Y + ny), new(b.X + nx, b.Y + ny), new(b.X - nx, b.Y - ny), new(a.X - nx, a.Y - ny)]);
    }
}
