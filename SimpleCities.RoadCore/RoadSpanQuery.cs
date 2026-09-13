using Boundary = SimpleCities.RoadCore.RoadSnapshotQueryData.Boundary;

namespace SimpleCities.RoadCore;

public readonly record struct RoadSpanRange(double StartParameter, double EndParameter);
public readonly record struct RoadSpanKey(RoadStateToken Source, EdgeId Edge,
    double StartParameter, double EndParameter, bool WrapsSeam);

/// <summary>一个可选道路格段的版本绑定区间和有序中心链；闭环接缝可由两个区间表达。</summary>
public sealed class RoadGridSpan
{
    internal RoadGridSpan(RoadStateToken source, EdgeId edge,
        IEnumerable<RoadSpanRange> ranges, IEnumerable<RoadPoint> points)
    {
        Source = source;
        Edge = edge;
        Ranges = Array.AsReadOnly(ranges.ToArray());
        Points = Array.AsReadOnly(points.ToArray());
        Key = new(source, edge, Ranges[0].StartParameter, Ranges[^1].EndParameter, Ranges.Count == 2);
    }

    public RoadStateToken Source { get; }
    public EdgeId Edge { get; }
    public IReadOnlyList<RoadSpanRange> Ranges { get; }
    public IReadOnlyList<RoadPoint> Points { get; }
    public RoadSpanKey Key { get; }
}

public static class RoadSpanQuery
{
    /// <summary>定位命中折线片后只求相邻主格边界，保留普通格心转折，不依赖渲染表面。</summary>
    public static RoadGridSpan? Pick(RoadSnapshot snapshot, RoadLocation location)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (location.Source != snapshot.Token || !double.IsFinite(location.Parameter) ||
            location.Parameter < 0 || location.Parameter > 1) return null;
        RoadEdge? edge = snapshot.FindEdge(location.Edge);
        if (edge is null) return null;
        if ((location.Parameter == 0 && snapshot.QueryData.Degree(edge.Start) >= 3) ||
            (location.Parameter == 1 && snapshot.QueryData.Degree(edge.End) >= 3)) return null;

        Boundary[] vertices = snapshot.QueryData.Vertices(edge.Id);
        int segment = RoadSnapshotQueryData.Segment(vertices, location.Parameter);
        Boundary start = FindPrimary(snapshot.Map, vertices, segment, location.Parameter, backwards: true) ?? vertices[0];
        Boundary end = FindPrimary(snapshot.Map, vertices, segment, location.Parameter, backwards: false) ?? vertices[^1];

        bool transparentSeam = edge.Start == edge.End && snapshot.QueryData.Degree(edge.Start) == 2 &&
            !snapshot.Map.IsPrimaryPoint(edge.Points[0]);
        if (transparentSeam && (start.Parameter == 0 || end.Parameter == 1))
        {
            Boundary tail = FindPrimary(snapshot.Map, vertices, vertices.Length - 2, 1, backwards: true) ?? vertices[0];
            Boundary head = FindPrimary(snapshot.Map, vertices, 0, 0, backwards: false) ?? vertices[^1];
            return new(snapshot.Token, edge.Id,
                [new(tail.Parameter, 1), new(0, head.Parameter)],
                Slice(tail, vertices[^1], vertices).Concat(Slice(vertices[0], head, vertices).Skip(1)));
        }
        return new(snapshot.Token, edge.Id, [new(start.Parameter, end.Parameter)], Slice(start, end, vertices));
    }

    private static Boundary? FindPrimary(MapDefinition map, Boundary[] vertices, int segment,
        double parameter, bool backwards)
    {
        for (int i = segment; i >= 0 && i < vertices.Length - 1; i += backwards ? -1 : 1)
        {
            Boundary a = vertices[i], b = vertices[i + 1];
            bool useX = a.Point.X != b.Point.X;
            double from = useX ? a.Point.X : a.Point.Y, to = useX ? b.Point.X : b.Point.Y;
            double fraction = Math.Clamp((parameter - a.Parameter) / (b.Parameter - a.Parameter), 0, 1);
            double grid = Math.Floor((from + (to - from) * fraction) / map.CellSizeMetres);
            Boundary? nearest = null;
            // Test adjacent grid coordinates against their actual source parameters. This also
            // handles an exactly-hit boundary whose inverse interpolation rounded to either side.
            for (int offset = -1; offset <= 2; offset++)
            {
                double coordinate = (grid + offset) * map.CellSizeMetres;
                double along = (coordinate - from) / (to - from);
                if (along < 0 || along > 1) continue;
                double candidateParameter = along == 0 ? a.Parameter : along == 1 ? b.Parameter :
                    a.Parameter + (b.Parameter - a.Parameter) * along;
                if (backwards ? candidateParameter > parameter || candidateParameter == 1 : candidateParameter <= parameter)
                    continue;
                RoadPoint point = useX
                    ? new(coordinate, a.Point.Y + Math.Sign(b.Point.Y - a.Point.Y) * Math.Abs(coordinate - from))
                    : new(a.Point.X, coordinate);
                if (!map.IsPrimaryPoint(point)) continue;
                if (nearest is null || (backwards ? candidateParameter > nearest.Value.Parameter : candidateParameter < nearest.Value.Parameter))
                    nearest = new(candidateParameter, point);
            }
            if (nearest is not null) return nearest;
        }
        return null;
    }

    private static IEnumerable<RoadPoint> Slice(Boundary start, Boundary end, Boundary[] vertices)
    {
        yield return start.Point;
        int first = RoadSnapshotQueryData.Segment(vertices, start.Parameter) + 1;
        for (int i = first; i < vertices.Length && vertices[i].Parameter < end.Parameter; i++)
            if (vertices[i].Parameter > start.Parameter) yield return vertices[i].Point;
        yield return end.Point;
    }


}
