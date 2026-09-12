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
    }

    internal RoadSnapshot Snapshot { get; }
    internal ArrayMesh? Mesh { get; }
    private Piece[] Pieces { get; }

    internal static V4RoadDisplay Prepare(RoadSnapshot snapshot, RoadSurfaceData? surface)
    {
        if (surface is null)
        {
            if (snapshot.EdgeCount != 0) throw new InvalidOperationException("Missing V4 road surface.");
            return new V4RoadDisplay(snapshot, null, []);
        }
        if (!surface.Edges.SequenceEqual(snapshot.Edges) || !surface.Nodes.SequenceEqual(snapshot.Nodes) || surface.Pieces.Count == 0)
            throw new InvalidOperationException("V4 surface does not match its target snapshot.");
        var pieces = new List<Piece>();
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var indices = new List<int>();
        foreach (RoadSurfacePiece source in surface.Pieces)
        {
            Vector2[] polygon = source.Corners.Select(ToVector).ToArray();
            if (polygon.Length is not (3 or 4)) throw new InvalidOperationException("Invalid V4 road surface polygon.");
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

    private static Vector2 ToVector(RoadPoint point)
    {
        var converted = new Vector2((float)point.X, (float)point.Y);
        if (!converted.IsFinite()) throw new InvalidOperationException("V4 display conversion is non-finite.");
        return converted;
    }

    internal HitResult? Hit(Vector2 world)
    {
        if (Mesh is null || !world.IsFinite()) return null;
        // Last drawn polygon owns the visible surface where differently coloured pieces overlap.
        for (int i = Pieces.Length - 1; i >= 0; i--)
        {
            Piece piece = Pieces[i];
            if (!Geometry2D.IsPointInPolygon(world, piece.Polygon)) continue;
            Vector2 direction = piece.End - piece.Start;
            double fraction = direction.LengthSquared() == 0 ? 0 : Mathf.Clamp((world - piece.Start).Dot(direction) / direction.LengthSquared(), 0, 1);
            double parameter = piece.StartParameter + fraction * (piece.EndParameter - piece.StartParameter);
            return new HitResult(new CoreRoadLocation(Snapshot.Token, piece.Edge, parameter), piece.JunctionNode);
        }
        return null;
    }

    internal RoadGridSpan? PeekSpan(Vector2 world)
    {
        HitResult? hit = Hit(world);
        // A junction patch has several incidences. Only the visible branch outside it selects a span.
        return hit is HitResult picked && picked.JunctionNode is null
            ? RoadSpanQuery.Pick(Snapshot, picked.Location) : null;
    }

    internal IReadOnlyList<RoadGridSpan> TraceSpans(Vector2 from, Vector2 to)
    {
        var result = new List<RoadGridSpan>();
        var seen = new HashSet<RoadSpanKey>();
        if (!from.IsFinite() || !to.IsFinite()) return result;
        if (from == to)
        {
            Add(from);
            return result;
        }

        // Partition the pointer segment at every visible polygon boundary and grid-span boundary.
        // This captures every crossed span even when an input event traverses the entire map.
        var breaks = new List<double> { 0, 1 };
        Vector2 travel = to - from;
        foreach (Piece piece in Pieces)
        {
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
        Add(from);
        for (int i = 1; i < breaks.Count; i++)
        {
            double left = breaks[i - 1], right = breaks[i];
            if (right > left) Add(from + travel * (float)((left + right) / 2));
        }
        Add(to);
        return result;

        void Add(Vector2 point)
        {
            RoadGridSpan? span = PeekSpan(point);
            if (span is not null && seen.Add(span.Key)) result.Add(span);
        }
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
        foreach (Piece piece in Pieces)
        {
            if (piece.Edge != location.Edge || location.Parameter < piece.StartParameter || location.Parameter > piece.EndParameter) continue;
            double range = piece.EndParameter - piece.StartParameter;
            return range == 0 ? piece.Start : piece.Start.Lerp(piece.End, (float)((location.Parameter - piece.StartParameter) / range));
        }
        throw new InvalidOperationException("V4 surface location does not belong to the displayed road.");
    }
    public void Dispose() => Mesh?.Dispose();
}
