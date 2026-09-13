using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SimpleCities.RoadCore;
using CoreRoadLocation = SimpleCities.RoadCore.RoadLocation;

/// <summary>一次预检中生成的引擎资源和表面坐标，发布后由View独占。</summary>
internal sealed class V4RoadDisplay : IDisposable
{
    private sealed record Piece(EdgeId Edge, Vector2[] Polygon, Vector2 Start, Vector2 End,
        double StartParameter, double EndParameter, NodeId? JunctionNode);
    internal readonly record struct HitResult(CoreRoadLocation Location, NodeId? JunctionNode);

    private V4RoadDisplay(RoadSnapshot snapshot, ArrayMesh? mesh, Piece[] pieces)
    {
        Snapshot = snapshot;
        Mesh = mesh;
        Pieces = pieces;
        _piecesByEdge = pieces.Where(piece => piece.Start != piece.End).GroupBy(piece => piece.Edge)
            .ToDictionary(group => group.Key, group => group.OrderBy(piece => piece.StartParameter).ToArray());
        var fragments = new List<SpatialFragment<int>>();
        for (int index = 0; index < pieces.Length; index++)
        {
            Piece piece = pieces[index];
            int count = piece.Polygon.Length == 4 ? Math.Max(1,
                (int)Math.Ceiling(Math.Max(piece.Polygon[0].DistanceTo(piece.Polygon[1]), piece.Polygon[3].DistanceTo(piece.Polygon[2])) / 100)) : 1;
            for (int part = 0; part < count; part++)
            {
                Vector2[] corners = count == 1 ? piece.Polygon : [
                    piece.Polygon[0].Lerp(piece.Polygon[1], (float)part / count),
                    piece.Polygon[0].Lerp(piece.Polygon[1], (float)(part + 1) / count),
                    piece.Polygon[3].Lerp(piece.Polygon[2], (float)(part + 1) / count),
                    piece.Polygon[3].Lerp(piece.Polygon[2], (float)part / count)];
                // Broad-phase padding covers float interpolation at fragment seams; exact picking still uses the drawn polygon.
                fragments.Add(new(index, new(corners.Min(point => point.X) - 0.001, corners.Min(point => point.Y) - 0.001,
                    corners.Max(point => point.X) + 0.001, corners.Max(point => point.Y) + 0.001)));
            }
        }
        _index = new(snapshot.Token, fragments);
    }

    internal RoadSnapshot Snapshot { get; }
    internal ArrayMesh? Mesh { get; }
    private Piece[] Pieces { get; }
    private readonly SpatialQueryIndex<int> _index;
    private readonly Dictionary<EdgeId, Piece[]> _piecesByEdge;

    private readonly record struct SurfaceSpan(EdgeId Edge, double Start, double End);

    /// <summary>Bounds follow the current preparer: one ribbon per segment, four triangles per bend,
    /// and at most 4d hull sides split by d ownership rays at a degree-d junction.</summary>
    private sealed class SurfacePreflight
    {
        private readonly RoadSnapshot _snapshot;
        private readonly Dictionary<SurfaceSpan, (RoadPoint Start, RoadPoint End)> _ribbons = [];
        private readonly Dictionary<(EdgeId Edge, double Parameter), RoadPoint> _boundaries = [];
        private readonly Dictionary<NodeId, int> _degrees = [];
        private readonly double _maximumWidth = RoadProfiles.All.Max(profile => profile.WidthMetres);
        internal long MaximumVertices { get; }
        internal long MaximumIndices { get; }

        internal SurfacePreflight(RoadSnapshot snapshot, int pieceCount)
        {
            _snapshot = snapshot;
            long joins = 0;
            foreach (RoadEdge edge in snapshot.Edges)
            {
                _degrees[edge.Start] = _degrees.GetValueOrDefault(edge.Start) + 1;
                _degrees[edge.End] = _degrees.GetValueOrDefault(edge.End) + 1;
                joins += 4L * (edge.Points.Count - 2);
                double length = edge.Length, distance = 0, startParameter = 0;
                _boundaries.Add((edge.Id, 0), edge.Points[0]);
                for (int i = 1; i < edge.Points.Count; i++)
                {
                    distance += edge.Points[i - 1].DistanceTo(edge.Points[i]);
                    double endParameter = i == edge.Points.Count - 1 ? 1 : distance / length;
                    _ribbons.Add(new(edge.Id, startParameter, endParameter), (edge.Points[i - 1], edge.Points[i]));
                    _boundaries.Add((edge.Id, endParameter), edge.Points[i]);
                    startParameter = endParameter;
                }
            }
            foreach (int degree in _degrees.Values)
                joins += degree >= 3 ? 4L * degree * (degree + 1) : degree == 2 ? 4 : 0;
            MaximumVertices = 4L * _ribbons.Count + 3 * joins;
            MaximumIndices = 6L * _ribbons.Count + 3 * joins;
            if (pieceCount < _ribbons.Count || pieceCount > _ribbons.Count + joins ||
                MaximumVertices > int.MaxValue || MaximumIndices > int.MaxValue)
                throw new InvalidOperationException("V4 road surface exceeds its target resource budget.");
        }

        internal void Validate(RoadSurfacePiece source)
        {
            if (!ReferenceEquals(_snapshot.FindEdge(source.Edge.Id), source.Edge) ||
                !double.IsFinite(source.StartParameter) || !double.IsFinite(source.EndParameter) ||
                source.StartParameter < 0 || source.EndParameter > 1 || source.StartParameter > source.EndParameter ||
                !_boundaries.TryGetValue((source.Edge.Id, source.StartParameter), out RoadPoint start) || start != source.Start ||
                !_boundaries.TryGetValue((source.Edge.Id, source.EndParameter), out RoadPoint end) || end != source.End)
                throw new InvalidOperationException("V4 road surface owner or location is invalid.");
            if (source.StartParameter != source.EndParameter)
            {
                if (source.Corners.Count != 4 || source.JunctionNode is not null ||
                    !_ribbons.Remove(new(source.Edge.Id, source.StartParameter, source.EndParameter), out var span) ||
                    span.Start != start || span.End != end)
                    throw new InvalidOperationException("V4 road ribbon coverage is invalid.");
            }
            else
            {
                NodeId? endpoint = source.StartParameter == 0 ? source.Edge.Start : source.StartParameter == 1 ? source.Edge.End : null;
                if (source.Corners.Count != 3 ||
                    (source.JunctionNode is NodeId junction && (endpoint != junction || _degrees.GetValueOrDefault(junction) < 3)) ||
                    (source.JunctionNode is null && endpoint is NodeId node && _degrees.GetValueOrDefault(node) != 2))
                    throw new InvalidOperationException("V4 road join ownership is invalid.");
            }
            // Caps and junction mouths can extend outside the map, but never farther than
            // the largest catalog width from their source segment. This also bounds indexing.
            foreach (RoadPoint corner in source.Corners)
                if (!corner.IsFinite || corner.X < Math.Min(start.X, end.X) - _maximumWidth ||
                    corner.X > Math.Max(start.X, end.X) + _maximumWidth ||
                    corner.Y < Math.Min(start.Y, end.Y) - _maximumWidth || corner.Y > Math.Max(start.Y, end.Y) + _maximumWidth)
                    throw new InvalidOperationException("V4 road surface vertex is outside its source envelope.");
        }

        internal void Complete()
        {
            if (_ribbons.Count != 0) throw new InvalidOperationException("V4 road surface is missing a ribbon.");
        }
    }

    internal static V4RoadDisplay Prepare(RoadSnapshot snapshot, RoadSurfaceData? surface)
    {
        if (surface is null)
        {
            if (snapshot.EdgeCount != 0) throw new InvalidOperationException("Missing V4 road surface.");
            return new V4RoadDisplay(snapshot, null, []);
        }
        if (!surface.Edges.SequenceEqual(snapshot.Edges) || !surface.Nodes.SequenceEqual(snapshot.Nodes) || surface.Pieces.Count == 0)
            throw new InvalidOperationException("V4 surface does not match its target snapshot.");
        var preflight = new SurfacePreflight(snapshot, surface.Pieces.Count);
        var pieces = new List<Piece>(surface.Pieces.Count);
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var indices = new List<int>();
        foreach (RoadSurfacePiece source in surface.Pieces)
        {
            preflight.Validate(source);
            Vector2[] polygon = source.Corners.Select(ToVector).ToArray();
            ValidatePolygon(polygon);
            Vector2 start = ToVector(source.Start), end = ToVector(source.End);
            pieces.Add(new Piece(source.Edge.Id, polygon, start, end, source.StartParameter, source.EndParameter, source.JunctionNode));
            RoadProfile profile = RoadProfiles.Get(source.Edge.Profile);
            var color = new Color(profile.Red / 255f, profile.Green / 255f, profile.Blue / 255f);
            int offset = vertices.Count;
            foreach (Vector2 point in polygon)
            {
                vertices.Add(new Vector3(point.X, point.Y, 0));
                colors.Add(color);
            }
            for (int i = 1; i < polygon.Length - 1; i++)
                indices.AddRange([offset, offset + i, offset + i + 1]);
        }
        preflight.Complete();
        if (vertices.Count != colors.Count || vertices.Count > preflight.MaximumVertices || indices.Count > preflight.MaximumIndices ||
            indices.Count % 3 != 0 || indices.Any(index => index < 0 || index >= vertices.Count))
            throw new InvalidOperationException("V4 road mesh arrays or resource budget are invalid.");
        var mesh = new ArrayMesh();
        try
        {
            using var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Godot.Mesh.ArrayType.Max);
            arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)Godot.Mesh.ArrayType.Color] = colors.ToArray();
            arrays[(int)Godot.Mesh.ArrayType.Index] = indices.ToArray();
            mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
            if (mesh.GetSurfaceCount() != 1 || mesh.SurfaceGetArrayLen(0) != vertices.Count || mesh.SurfaceGetArrayIndexLen(0) != indices.Count)
                throw new InvalidOperationException("V4 road mesh preflight failed.");
            return new V4RoadDisplay(snapshot, mesh, pieces.ToArray());
        }
        catch
        {
            mesh.Dispose();
            throw;
        }
    }

    private static void ValidatePolygon(Vector2[] polygon)
    {
        double winding = 0;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length], c = polygon[(i + 2) % polygon.Length];
            // Compute from the shared binary32 vertices, widening before arithmetic so large
            // world offsets do not hide a collapsed or concave triangle through cancellation.
            double cross = ((double)b.X - a.X) * ((double)c.Y - b.Y) - ((double)b.Y - a.Y) * ((double)c.X - b.X);
            if (cross == 0 || (winding != 0 && Math.Sign(cross) != winding))
                throw new InvalidOperationException("V4 road surface is degenerate or non-convex after display conversion.");
            winding = Math.Sign(cross);
        }
    }

    private static Vector2 ToVector(RoadPoint point)
    {
        var converted = new Vector2((float)point.X, (float)point.Y);
        if (!converted.IsFinite()) throw new InvalidOperationException("V4 display conversion is non-finite.");
        return converted;
    }

    internal SpatialQueryResult<HitResult> QueryHit(Vector2 world, SpatialQueryBudget? budget = null)
    {
        SpatialQueryBudget limits = budget ?? SpatialQueryBudget.Default;
        SpatialQueryResult<int> candidates = _index.QueryBounds(new(world.X, world.Y, world.X, world.Y), limits);
        if (candidates.Status != SpatialQueryStatus.Ready)
            return new(Snapshot.Token, candidates.Status, [], candidates.Metrics, candidates.Reason);
        int exact = 0;
        // Last drawn polygon owns the visible surface where differently coloured pieces overlap.
        foreach (int i in candidates.Results.Distinct().OrderDescending())
        {
            if (exact >= limits.MaxExactTests)
                return new(Snapshot.Token, SpatialQueryStatus.BudgetExceeded, [], candidates.Metrics with { ExactGeometryTests = exact }, "空间查询精确检查预算已耗尽");
            exact++;
            Piece piece = Pieces[i];
            if (!Geometry2D.IsPointInPolygon(world, piece.Polygon)) continue;
            Vector2 direction = piece.End - piece.Start;
            double fraction = direction.LengthSquared() == 0 ? 0 : Mathf.Clamp((world - piece.Start).Dot(direction) / direction.LengthSquared(), 0, 1);
            double parameter = piece.StartParameter + fraction * (piece.EndParameter - piece.StartParameter);
            return new(Snapshot.Token, SpatialQueryStatus.Ready,
                [new HitResult(new CoreRoadLocation(Snapshot.Token, piece.Edge, parameter), piece.JunctionNode)],
                candidates.Metrics with { ExactGeometryTests = exact, Hits = 1 }, "");
        }
        return new(Snapshot.Token, SpatialQueryStatus.Ready, [], candidates.Metrics with { ExactGeometryTests = exact }, "");
    }

    internal SpatialQueryResult<RoadGridSpan> QuerySpan(Vector2 world, SpatialQueryBudget? budget = null)
    {
        SpatialQueryResult<HitResult> result = QueryHit(world, budget);
        HitResult? hit = result.Results.Cast<HitResult?>().FirstOrDefault();
        // A junction patch has several incidences. Only the visible branch outside it selects a span.
        RoadGridSpan? span = hit is HitResult picked && picked.JunctionNode is null
            ? RoadSpanQuery.Pick(Snapshot, picked.Location) : null;
        return new(Snapshot.Token, result.Status, span is null ? [] : [span],
            result.Metrics with { Hits = span is null ? 0 : 1 }, result.Reason);
    }

    internal SpatialQueryResult<RoadGridSpan> QueryTraceSpans(Vector2 from, Vector2 to, SpatialQueryBudget? budget = null)
    {
        if (from == to) return QuerySpan(from, budget);
        SpatialQueryBudget limits = budget ?? SpatialQueryBudget.Default;
        SpatialQueryResult<int> candidates = _index.QuerySegment(new(from.X, from.Y), new(to.X, to.Y), limits);
        if (candidates.Status != SpatialQueryStatus.Ready)
            return new(Snapshot.Token, candidates.Status, [], candidates.Metrics, candidates.Reason);
        SpatialQueryMetrics metrics = candidates.Metrics;
        var result = new List<RoadGridSpan>();
        var seen = new HashSet<RoadSpanKey>();

        // Partition the pointer segment at every visible polygon boundary and grid-span boundary.
        // This captures every crossed span even when an input event traverses the entire map.
        var breaks = new List<double> { 0, 1 };
        Vector2 travel = to - from;
        foreach (int index in candidates.Results.Distinct())
        {
            if (metrics.ExactGeometryTests >= limits.MaxExactTests) return Exhausted();
            metrics = metrics with { ExactGeometryTests = metrics.ExactGeometryTests + 1 };
            Piece piece = Pieces[index];
            if (!Clip(piece.Polygon, from, travel, out double entry, out double exit)) continue;
            breaks.Add(entry);
            breaks.Add(exit);
            Vector2 axis = piece.End - piece.Start;
            double lengthSquared = axis.LengthSquared();
            if (lengthSquared == 0 || piece.JunctionNode is not null) continue;
            double fractionStart = (from - piece.Start).Dot(axis) / lengthSquared;
            double fractionDelta = travel.Dot(axis) / lengthSquared;
            if (fractionDelta == 0) continue;
            // All eight supported directions cross one main-grid line per span. For diagonals
            // either coordinate gives the same crossing, so use the dominant coordinate only.
            double origin = Math.Abs(axis.X) >= Math.Abs(axis.Y) ? piece.Start.X : piece.Start.Y;
            double extent = Math.Abs(axis.X) >= Math.Abs(axis.Y) ? axis.X : axis.Y;
            double startCoordinate = origin + extent * fractionStart;
            double coordinateDelta = extent * fractionDelta;
            double low = Math.Max(Math.Min(origin, origin + extent), Math.Min(startCoordinate + coordinateDelta * entry, startCoordinate + coordinateDelta * exit));
            double high = Math.Min(Math.Max(origin, origin + extent), Math.Max(startCoordinate + coordinateDelta * entry, startCoordinate + coordinateDelta * exit));
            int cell = Snapshot.Map.CellSizeMetres;
            for (int grid = (int)Math.Ceiling(low / cell); grid <= Math.Floor(high / cell); grid++)
            {
                double t = (grid * cell - startCoordinate) / coordinateDelta;
                if (t > entry && t < exit) breaks.Add(t);
            }
        }
        breaks.Sort();
        if (!Add(from)) return Exhausted();
        for (int i = 1; i < breaks.Count; i++)
        {
            double left = breaks[i - 1], right = breaks[i];
            if (right > left && !Add(from + travel * (float)((left + right) / 2))) return Exhausted();
        }
        if (!Add(to)) return Exhausted();
        return new(Snapshot.Token, SpatialQueryStatus.Ready, result.AsReadOnly(),
            metrics with { Hits = result.Select(span => span.Edge).Distinct().Count() }, "");

        bool Add(Vector2 point)
        {
            var remaining = new SpatialQueryBudget(limits.MaxBuckets - metrics.BucketsVisited,
                limits.MaxCandidates - metrics.FragmentCandidates, limits.MaxExactTests - metrics.ExactGeometryTests);
            if (!remaining.IsValid) return false;
            SpatialQueryResult<RoadGridSpan> picked = QuerySpan(point, remaining);
            metrics = new(metrics.BucketsVisited + picked.Metrics.BucketsVisited,
                metrics.FragmentCandidates + picked.Metrics.FragmentCandidates,
                metrics.ExactGeometryTests + picked.Metrics.ExactGeometryTests, 0, 0);
            if (picked.Status != SpatialQueryStatus.Ready) return false;
            RoadGridSpan? span = picked.Results.FirstOrDefault();
            if (span is not null && seen.Add(span.Key)) result.Add(span);
            return true;
        }

        SpatialQueryResult<RoadGridSpan> Exhausted() => new(Snapshot.Token, SpatialQueryStatus.BudgetExceeded,
            [], metrics with { Hits = 0 }, "拖选查询预算已耗尽，本次手势不可提交");
    }

    internal static bool SameSpan(RoadGridSpan left, RoadGridSpan right) => left.Key == right.Key;

    private static bool Clip(Vector2[] polygon, Vector2 from, Vector2 travel, out double entry, out double exit)
    {
        entry = 0;
        exit = 1;
        double area = 0;
        for (int i = 0; i < polygon.Length; i++) area += polygon[i].Cross(polygon[(i + 1) % polygon.Length]);
        double winding = Math.Sign(area);
        if (winding == 0) return false;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 start = polygon[i], side = polygon[(i + 1) % polygon.Length] - start;
            double value = winding * side.Cross(from - start);
            double slope = winding * side.Cross(travel);
            if (slope == 0)
            {
                if (value < 0) return false;
                continue;
            }
            double crossing = -value / slope;
            if (slope > 0) entry = Math.Max(entry, crossing);
            else exit = Math.Min(exit, crossing);
            if (entry > exit) return false;
        }
        return true;
    }

    internal Vector2 SurfaceCenter(CoreRoadLocation location)
    {
        if (location.Source != Snapshot.Token) throw new InvalidOperationException("V4 surface location is stale.");
        if (!_piecesByEdge.TryGetValue(location.Edge, out Piece[]? edgePieces))
            throw new InvalidOperationException("V4 surface location does not belong to the displayed road.");
        if (!double.IsFinite(location.Parameter) || location.Parameter < 0 || location.Parameter > 1)
            throw new InvalidOperationException("V4 surface parameter is invalid.");
        int low = 0, high = edgePieces.Length;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if (edgePieces[middle].StartParameter <= location.Parameter) low = middle;
            else high = middle;
        }
        Piece piece = edgePieces[low];
        double range = piece.EndParameter - piece.StartParameter;
        return range == 0 ? piece.Start : piece.Start.Lerp(piece.End, (float)((location.Parameter - piece.StartParameter) / range));
    }
    public void Dispose() => Mesh?.Dispose();
}
