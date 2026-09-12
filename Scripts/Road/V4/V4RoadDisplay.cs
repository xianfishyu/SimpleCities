using Godot;
using System;
using System.Linq;
using SimpleCities.RoadCore;
using CoreRoadLocation = SimpleCities.RoadCore.RoadLocation;

/// <summary>一次预检中生成的引擎资源和表面坐标，发布后由View独占。</summary>
internal sealed class V4RoadDisplay : IDisposable
{
    private V4RoadDisplay(RoadSnapshot snapshot, ArrayMesh? mesh, Vector2[] polygon, Vector2 start, Vector2 end)
    {
        Snapshot = snapshot;
        Mesh = mesh;
        Polygon = polygon;
        Start = start;
        End = end;
    }

    internal RoadSnapshot Snapshot { get; }
    internal ArrayMesh? Mesh { get; }
    private Vector2[] Polygon { get; }
    private Vector2 Start { get; }
    private Vector2 End { get; }

    internal static V4RoadDisplay Prepare(RoadSnapshot snapshot, RoadSurfaceData? surface)
    {
        if (surface is null)
        {
            if (snapshot.EdgeCount != 0) throw new InvalidOperationException("Missing V4 road surface.");
            return new V4RoadDisplay(snapshot, null, [], default, default);
        }
        if (surface.Edge != snapshot.Edges.Single() ||
            surface.Start != snapshot.Nodes.Single(node => node.Id == surface.Edge.Start).Position ||
            surface.End != snapshot.Nodes.Single(node => node.Id == surface.Edge.End).Position)
            throw new InvalidOperationException("V4 surface does not match its target snapshot.");
        Vector2[] polygon = surface.Corners.Select(ToVector).ToArray();
        Vector2 start = ToVector(surface.Start);
        Vector2 end = ToVector(surface.End);
        RoadProfile profile = RoadProfiles.Get(surface.Edge.Profile);
        var color = new Color(profile.Red / 255f, profile.Green / 255f, profile.Blue / 255f);
        var mesh = new ArrayMesh();
        try
        {
            using var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Godot.Mesh.ArrayType.Max);
            arrays[(int)Godot.Mesh.ArrayType.Vertex] = polygon.Select(point => new Vector3(point.X, point.Y, 0)).ToArray();
            arrays[(int)Godot.Mesh.ArrayType.Color] = new[] { color, color, color, color };
            arrays[(int)Godot.Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
            if (mesh.GetSurfaceCount() != 1 || mesh.SurfaceGetArrayLen(0) != 4 || mesh.SurfaceGetArrayIndexLen(0) != 6)
                throw new InvalidOperationException("V4 road mesh preflight failed.");
            return new V4RoadDisplay(snapshot, mesh, polygon, start, end);
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
        if (Mesh is null || !world.IsFinite() || !Geometry2D.IsPointInPolygon(world, Polygon))
            return null;
        Vector2 direction = End - Start;
        double parameter = Mathf.Clamp((world - Start).Dot(direction) / direction.LengthSquared(), 0, 1);
        return new CoreRoadLocation(Snapshot.Token, Snapshot.Edges[0].Id, parameter);
    }

    internal Vector2 SurfaceCenter(double parameter) => Start.Lerp(End, (float)parameter);
    public void Dispose() => Mesh?.Dispose();
}
