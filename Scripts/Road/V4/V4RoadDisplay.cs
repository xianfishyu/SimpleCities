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
        double StartParameter, double EndParameter);

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
            pieces.Add(new Piece(source.Edge.Id, polygon, start, end, source.StartParameter, source.EndParameter));
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

    internal CoreRoadLocation? Hit(Vector2 world)
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
            return new CoreRoadLocation(Snapshot.Token, piece.Edge, parameter);
        }
        return null;
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
