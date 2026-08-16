using Godot;
using SimpleCities.Road.V3;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// V3 道路渲染器：仅在 GraphStateToken 变化时从 RoadGraphV3System 重建 ribbon/junction
/// ArrayMesh 缓存，并通过 MeshInstance2D 子节点绘制；不修改图数据。
/// </summary>
public partial class RoadGraphV3Renderer : Node2D
{
    [Export] public float DisplayTolerance { get; set; } = RoadGeometryDisplaySampler.DefaultTolerance;

    private GraphStateToken? _lastToken;
    private IReadOnlyList<RoadRibbonMeshData> _cachedMeshes = [];
    private IReadOnlyList<RoadJunctionPatchData> _cachedPatches = [];
    private MeshInstance2D _meshLayer = null!;

    public MeshInstance2D? MeshLayer => _meshLayer;

    public int MeshVertexCount =>
        _cachedMeshes.Sum(mesh => mesh.Vertices.Count) +
        _cachedPatches.Sum(patch => patch.Outline.Count);

    public int MeshIndexCount =>
        _cachedMeshes.Sum(mesh => mesh.Indices.Count) +
        _cachedPatches.Sum(patch => Math.Max(0, patch.Outline.Count - 2) * 3);

    public override void _Ready()
    {
        _meshLayer = new MeshInstance2D
        {
            ZIndex = -1,
        };
        AddChild(_meshLayer);
    }

    public void ApplyPresentationFullReset(RoadPresentationFullReset plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _cachedMeshes = plan.RibbonMeshes;
        _cachedPatches = plan.JunctionPatches;
        _lastToken = plan.Snapshot.Token;
        RebuildMesh();
    }

    public void ResetMesh()
    {
        _cachedMeshes = [];
        _cachedPatches = [];
        _lastToken = null;
        RebuildMesh();
    }

    public override void _Process(double delta)
    {
        RoadGraphV3System? system = RoadGraphV3System.Instance;
        if (system is null)
            return;

        GraphStateToken current = system.Controller.Facade.CurrentToken;
        if (_lastToken == current)
            return;

        _lastToken = current;
        _cachedMeshes = system.Application.BuildDefaultRibbonMeshes(DisplayTolerance);
        _cachedPatches = system.Application.BuildDefaultJunctionPatches();
        RebuildMesh();
    }

    private void RebuildMesh()
    {
        var vertices = new List<Vector2>();
        var colors = new List<Color>();
        var indices = new List<int>();

        foreach (RoadRibbonMeshData ribbon in _cachedMeshes)
        {
            int baseVertex = vertices.Count;
            vertices.AddRange(ribbon.Vertices);
            colors.AddRange(ribbon.Colors);
            foreach (int index in ribbon.Indices)
                indices.Add(baseVertex + index);
        }

        foreach (RoadJunctionPatchData patch in _cachedPatches)
        {
            if (patch.Outline.Count < 3)
                continue;

            int baseVertex = vertices.Count;
            vertices.AddRange(patch.Outline);
            for (int index = 0; index < patch.Outline.Count; index++)
                colors.Add(patch.Color);

            for (int index = 1; index < patch.Outline.Count - 1; index++)
            {
                indices.Add(baseVertex);
                indices.Add(baseVertex + index);
                indices.Add(baseVertex + index + 1);
            }
        }

        if (vertices.Count == 0 || indices.Count == 0)
        {
            _meshLayer.Mesh = null;
            return;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _meshLayer.Mesh = mesh;
    }
}
