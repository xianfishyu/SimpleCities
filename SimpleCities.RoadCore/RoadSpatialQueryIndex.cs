namespace SimpleCities.RoadCore;

public sealed record RoadQueryFragment(EdgeId Edge, int GeometryIndex, RoadPoint Start, RoadPoint End,
    double StartParameter, double EndParameter, bool OwnsStart, bool OwnsEnd);
public sealed record RoadQueryHit(RoadLocation Location, RoadQueryFragment Fragment, double Distance);

/// <summary>由不可变快照重建的中心线查询；精确检查只使用局部有界片段。</summary>
public sealed class RoadSpatialQueryIndex
{
    private readonly RoadSnapshot _snapshot;
    private readonly SpatialQueryIndex<RoadQueryFragment> _index;
    public RoadSpatialQueryIndex(RoadSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;
        var fragments = new List<SpatialFragment<RoadQueryFragment>>();
        foreach (RoadEdge edge in snapshot.Edges)
        {
            RoadSnapshotQueryData.Boundary[] vertices = snapshot.QueryData.Vertices(edge.Id);
            for (int geometry = 0; geometry < vertices.Length - 1; geometry++)
            {
                var a = vertices[geometry]; var b = vertices[geometry + 1];
                int count = Math.Max(1, (int)Math.Ceiling(a.Point.DistanceTo(b.Point) / 100));
                for (int part = 0; part < count; part++)
                {
                    double start = (double)part / count, end = (double)(part + 1) / count;
                    RoadPoint first = Interpolate(a.Point, b.Point, start), last = Interpolate(a.Point, b.Point, end);
                    var fragment = new RoadQueryFragment(edge.Id, geometry, first, last,
                        a.Parameter + (b.Parameter - a.Parameter) * start,
                        a.Parameter + (b.Parameter - a.Parameter) * end,
                        geometry == 0 && part == 0, geometry == vertices.Length - 2 && part == count - 1);
                    SpatialBounds bounds = SpatialBounds.Between(first, last);
                    // Conservative broad-phase padding only; it never changes exact geometry decisions.
                    fragments.Add(new(fragment, new(bounds.MinX - 1e-9, bounds.MinY - 1e-9,
                        bounds.MaxX + 1e-9, bounds.MaxY + 1e-9)));
                }
            }
        }
        _index = new(snapshot.Token, fragments);
    }

    public SpatialQueryResult<RoadQueryHit> QueryNearby(RoadPoint point, double radius, SpatialQueryBudget? budget = null)
    {
        if (!point.IsFinite || !double.IsFinite(radius) || radius < 0)
            return new(_snapshot.Token, SpatialQueryStatus.InvalidParameters, [], default, "查询位置或半径非法");
        SpatialQueryBudget limits = budget ?? SpatialQueryBudget.Default;
        SpatialQueryResult<RoadQueryFragment> candidates = _index.QueryBounds(
            new(point.X - radius, point.Y - radius, point.X + radius, point.Y + radius), limits);
        if (candidates.Status != SpatialQueryStatus.Ready)
            return new(candidates.Source, candidates.Status, [], candidates.Metrics, candidates.Reason);
        var hits = new List<RoadQueryHit>();
        int exact = 0;
        foreach (RoadQueryFragment fragment in candidates.Results)
        {
            if (exact >= limits.MaxExactTests)
                return new(candidates.Source, SpatialQueryStatus.BudgetExceeded, [], candidates.Metrics with { ExactGeometryTests = exact }, "空间查询精确检查预算已耗尽");
            exact++;
            SourceSegment source = ReadSource(fragment);
            double dx = source.End.X - source.Start.X, dy = source.End.Y - source.Start.Y;
            double px = point.X - source.Start.X, py = point.Y - source.Start.Y;
            double projection = (px * dx + py * dy) / (dx * dx + dy * dy);
            double fraction = Math.Clamp(projection, source.StartFraction, source.EndFraction);
            double distance = fraction == projection ? Math.Abs(px * dy - py * dx) / Math.Sqrt(dx * dx + dy * dy)
                : point.DistanceTo(Interpolate(source.Start, source.End, fraction));
            if (distance > radius) continue;
            double parameter = source.Parameter(fraction);
            hits.Add(new(new(_snapshot.Token, fragment.Edge, parameter), fragment, distance));
        }
        return new(_snapshot.Token, SpatialQueryStatus.Ready, hits.AsReadOnly(),
            candidates.Metrics with { ExactGeometryTests = exact, Hits = hits.Select(hit => hit.Location.Edge).Distinct().Count() }, "");
    }

    public SpatialQueryResult<RoadQueryHit> QueryBounds(SpatialBounds bounds, SpatialQueryBudget? budget = null)
    {
        SpatialQueryBudget limits = budget ?? SpatialQueryBudget.Default;
        return Refine(_index.QueryBounds(bounds, limits), limits, fragment =>
        {
            SourceSegment source = ReadSource(fragment);
            double start = source.StartFraction, end = source.EndFraction;
            if (!ClipAxis(source.Start.X, source.End.X - source.Start.X, bounds.MinX, bounds.MaxX, ref start, ref end) ||
                !ClipAxis(source.Start.Y, source.End.Y - source.Start.Y, bounds.MinY, bounds.MaxY, ref start, ref end)) return null;
            return source.Parameter(start);
        });
    }

    public SpatialQueryResult<RoadQueryHit> QuerySegment(RoadPoint from, RoadPoint to, SpatialQueryBudget? budget = null)
    {
        SpatialQueryBudget limits = budget ?? SpatialQueryBudget.Default;
        return Refine(_index.QuerySegment(from, to, limits), limits, fragment =>
        {
            SourceSegment source = ReadSource(fragment);
            double rx = source.End.X - source.Start.X, ry = source.End.Y - source.Start.Y;
            double sx = to.X - from.X, sy = to.Y - from.Y;
            double qx = from.X - source.Start.X, qy = from.Y - source.Start.Y;
            double denominator = rx * sy - ry * sx;
            if (denominator == 0)
            {
                if (qx * ry - qy * rx != 0) return null;
                double lengthSquared = rx * rx + ry * ry;
                double first = (qx * rx + qy * ry) / lengthSquared;
                double last = first + (sx * rx + sy * ry) / lengthSquared;
                double start = Math.Max(source.StartFraction, Math.Min(first, last)), end = Math.Min(source.EndFraction, Math.Max(first, last));
                return start <= end ? source.Parameter(start) : null;
            }
            double alongFragment = (qx * sy - qy * sx) / denominator;
            double alongQuery = (qx * ry - qy * rx) / denominator;
            return alongFragment >= source.StartFraction && alongFragment <= source.EndFraction && alongQuery >= 0 && alongQuery <= 1
                ? source.Parameter(alongFragment) : null;
        });
    }

    private SpatialQueryResult<RoadQueryHit> Refine(SpatialQueryResult<RoadQueryFragment> candidates,
        SpatialQueryBudget budget, Func<RoadQueryFragment, double?> intersect)
    {
        if (candidates.Status != SpatialQueryStatus.Ready)
            return new(candidates.Source, candidates.Status, [], candidates.Metrics, candidates.Reason);
        var hits = new List<RoadQueryHit>();
        int exact = 0;
        foreach (RoadQueryFragment fragment in candidates.Results)
        {
            if (exact >= budget.MaxExactTests)
                return new(candidates.Source, SpatialQueryStatus.BudgetExceeded, [],
                    candidates.Metrics with { ExactGeometryTests = exact }, "空间查询精确检查预算已耗尽");
            exact++;
            if (intersect(fragment) is not double parameter) continue;
            hits.Add(new(new(_snapshot.Token, fragment.Edge, parameter), fragment, 0));
        }
        return new(_snapshot.Token, SpatialQueryStatus.Ready, hits.AsReadOnly(),
            candidates.Metrics with { ExactGeometryTests = exact, Hits = hits.Select(hit => hit.Location.Edge).Distinct().Count() }, "");
    }

    private readonly record struct SourceSegment(RoadPoint Start, RoadPoint End, double StartParameter, double EndParameter,
        double StartFraction, double EndFraction)
    {
        internal double Parameter(double fraction) => StartParameter + (EndParameter - StartParameter) * fraction;
    }

    private SourceSegment ReadSource(RoadQueryFragment fragment)
    {
        // The fragment is an indexing detail: exact queries address two authoritative grid points
        // directly, then restrict their parameter interval. No full canonical edge is enumerated.
        RoadSnapshotQueryData.Boundary[] vertices = _snapshot.QueryData.Vertices(fragment.Edge);
        var a = vertices[fragment.GeometryIndex]; var b = vertices[fragment.GeometryIndex + 1];
        return new(a.Point, b.Point, a.Parameter, b.Parameter,
            (fragment.StartParameter - a.Parameter) / (b.Parameter - a.Parameter),
            (fragment.EndParameter - a.Parameter) / (b.Parameter - a.Parameter));
    }

    private static bool ClipAxis(double origin, double direction, double minimum, double maximum, ref double start, ref double end)
    {
        if (direction == 0) return origin >= minimum && origin <= maximum;
        double first = (minimum - origin) / direction, last = (maximum - origin) / direction;
        start = Math.Max(start, Math.Min(first, last));
        end = Math.Min(end, Math.Max(first, last));
        return start <= end;
    }

    private static RoadPoint Interpolate(RoadPoint start, RoadPoint end, double fraction) =>
        new(start.X + (end.X - start.X) * fraction, start.Y + (end.Y - start.Y) * fraction);
}
