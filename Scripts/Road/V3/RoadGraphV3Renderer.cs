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
    private IReadOnlyList<RoadCapMeshData> _cachedCaps = [];
    private IReadOnlyList<RoadSemanticJoinMeshData> _cachedSemanticJoins = [];
    private MeshInstance2D _meshLayer = null!;

    public MeshInstance2D? MeshLayer => _meshLayer;
    public ArrayMesh? CurrentMesh => _meshLayer?.Mesh as ArrayMesh;
    public bool IsMeshReady => CurrentMesh is not null && MeshVertexCount > 0;

    public int MeshVertexCount =>
        _cachedMeshes.Sum(mesh => mesh.Vertices.Count) +
        _cachedPatches.Sum(patch => patch.Outline.Count) +
        _cachedCaps.Sum(cap => cap.Outline.Count) +
        _cachedSemanticJoins.Sum(join => join.Vertices.Count);

    public int MeshIndexCount =>
        _cachedMeshes.Sum(mesh => mesh.Indices.Count) +
        _cachedPatches.Sum(patch => Math.Max(0, patch.Outline.Count - 2) * 3) +
        _cachedCaps.Sum(cap => Math.Max(0, cap.Outline.Count - 2) * 3) +
        _cachedSemanticJoins.Sum(join => join.Indices.Count);

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
        _cachedCaps = plan.CapMeshes;
        _cachedSemanticJoins = plan.SemanticJoinMeshes;
        _lastToken = plan.Snapshot.Token;
        RebuildMesh();
    }

    public void ResetMesh()
    {
        _cachedMeshes = [];
        _cachedPatches = [];
        _cachedCaps = [];
        _cachedSemanticJoins = [];
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
        _cachedCaps = system.Application.BuildDefaultCapMeshes();
        _cachedSemanticJoins = system.Application.BuildDefaultSemanticJoinMeshes();
        RebuildMesh();
    }

    public bool TryPreflight(RoadPresentationFullReset plan, out RoadGraphV3RendererPreparedSwap? swap)
    {
        swap = null;
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsValid || !plan.HasMeshData)
            return false;

        ArrayMesh? mesh = BuildMesh(plan.RibbonMeshes, plan.JunctionPatches, plan.CapMeshes, plan.SemanticJoinMeshes);
        if (mesh is null)
            return false;

        swap = new RoadGraphV3RendererPreparedSwap(
            plan.RibbonMeshes,
            plan.JunctionPatches,
            plan.CapMeshes,
            plan.SemanticJoinMeshes,
            plan.Snapshot.Token,
            mesh);
        return true;
    }

    public void ApplyPreparedSwap(RoadGraphV3RendererPreparedSwap swap)
    {
        ArgumentNullException.ThrowIfNull(swap);
        _cachedMeshes = swap.RibbonMeshes;
        _cachedPatches = swap.JunctionPatches;
        _cachedCaps = swap.CapMeshes;
        _cachedSemanticJoins = swap.SemanticJoinMeshes;
        _lastToken = swap.Token;
        _meshLayer.Mesh = swap.Mesh;
    }

    private static ArrayMesh? BuildMesh(
        IReadOnlyList<RoadRibbonMeshData> ribbonMeshes,
        IReadOnlyList<RoadJunctionPatchData> junctionPatches,
        IReadOnlyList<RoadCapMeshData> capMeshes,
        IReadOnlyList<RoadSemanticJoinMeshData> semanticJoinMeshes)
    {
        var vertices = new List<Vector2>();
        var colors = new List<Color>();
        var indices = new List<int>();

        foreach (RoadRibbonMeshData ribbon in ribbonMeshes)
        {
            int baseVertex = vertices.Count;
            vertices.AddRange(ribbon.Vertices);
            colors.AddRange(ribbon.Colors);
            foreach (int index in ribbon.Indices)
                indices.Add(baseVertex + index);
        }

        foreach (RoadJunctionPatchData patch in junctionPatches)
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

        foreach (RoadCapMeshData cap in capMeshes)
        {
            if (cap.Outline.Count < 3)
                continue;

            int baseVertex = vertices.Count;
            vertices.AddRange(cap.Outline);
            for (int index = 0; index < cap.Outline.Count; index++)
                colors.Add(cap.Color);

            for (int index = 1; index < cap.Outline.Count - 1; index++)
            {
                indices.Add(baseVertex);
                indices.Add(baseVertex + index);
                indices.Add(baseVertex + index + 1);
            }
        }

        foreach (RoadSemanticJoinMeshData join in semanticJoinMeshes)
        {
            int baseVertex = vertices.Count;
            vertices.AddRange(join.Vertices);
            colors.AddRange(join.Colors);
            foreach (int index in join.Indices)
                indices.Add(baseVertex + index);
        }

        if (vertices.Count == 0 || indices.Count == 0)
            return null;

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private void RebuildMesh()
    {
        _meshLayer.Mesh = BuildMesh(_cachedMeshes, _cachedPatches, _cachedCaps, _cachedSemanticJoins);
    }
}

/// <summary>
/// Load Preflight 阶段构建好的隐藏道路 mesh 交换计划；non-yield commit 内只做引用赋值。
/// </summary>
public sealed class RoadGraphV3RendererPreparedSwap
{
    public IReadOnlyList<RoadRibbonMeshData> RibbonMeshes { get; }
    public IReadOnlyList<RoadJunctionPatchData> JunctionPatches { get; }
    public IReadOnlyList<RoadCapMeshData> CapMeshes { get; }
    public IReadOnlyList<RoadSemanticJoinMeshData> SemanticJoinMeshes { get; }
    public GraphStateToken Token { get; }
    public ArrayMesh Mesh { get; }

    public RoadGraphV3RendererPreparedSwap(
        IReadOnlyList<RoadRibbonMeshData> ribbonMeshes,
        IReadOnlyList<RoadJunctionPatchData> junctionPatches,
        IReadOnlyList<RoadCapMeshData> capMeshes,
        IReadOnlyList<RoadSemanticJoinMeshData> semanticJoinMeshes,
        GraphStateToken token,
        ArrayMesh mesh)
    {
        RibbonMeshes = ribbonMeshes ?? throw new ArgumentNullException(nameof(ribbonMeshes));
        JunctionPatches = junctionPatches ?? throw new ArgumentNullException(nameof(junctionPatches));
        CapMeshes = capMeshes ?? throw new ArgumentNullException(nameof(capMeshes));
        SemanticJoinMeshes = semanticJoinMeshes ?? throw new ArgumentNullException(nameof(semanticJoinMeshes));
        Token = token;
        Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
    }
}
