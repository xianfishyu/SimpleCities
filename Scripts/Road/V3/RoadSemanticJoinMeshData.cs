using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// 一个 degree-2 且两侧 RoadType 不同的语义过渡 mesh：用四条 road-side 顶点构成四边形，
/// 两侧顶点分别使用各自 RoadType 颜色，形成简单宽度/颜色过渡。
/// </summary>
public sealed record RoadSemanticJoinMeshData(
    int NodeID,
    IReadOnlyList<Vector2> Vertices,
    IReadOnlyList<int> Indices,
    IReadOnlyList<Color> Colors)
{
    public bool IsValid =>
        NodeID >= 0 &&
        Vertices.Count >= 4 &&
        Vertices.Count == Colors.Count &&
        Indices.Count > 0 &&
        Indices.Count % 3 == 0 &&
        Vertices.All(vertex => vertex.IsFinite()) &&
        Colors.All(color =>
            float.IsFinite(color.R) &&
            float.IsFinite(color.G) &&
            float.IsFinite(color.B) &&
            float.IsFinite(color.A));
}
