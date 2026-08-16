using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// 一条 Edge 的 ribbon 网格数据：顶点、三角形索引与逐顶点颜色。
/// 这是 V3 道路 mesh 的纯数据基础，不直接创建 Godot 资源。
/// </summary>
public sealed record RoadRibbonMeshData(
    IReadOnlyList<Vector2> Vertices,
    IReadOnlyList<int> Indices,
    IReadOnlyList<Color> Colors)
{
    public bool IsValid =>
        Vertices.Count >= 4 &&
        Vertices.Count % 2 == 0 &&
        Indices.Count > 0 &&
        Indices.Count % 3 == 0 &&
        Colors.Count == Vertices.Count &&
        Vertices.All(vertex => vertex.IsFinite()) &&
        Colors.All(color =>
            float.IsFinite(color.R) &&
            float.IsFinite(color.G) &&
            float.IsFinite(color.B) &&
            float.IsFinite(color.A));

    public IReadOnlyList<Vector2> ToOutlineVertices()
    {
        if (Vertices.Count < 4 || Vertices.Count % 2 != 0)
            return [];

        int sampleCount = Vertices.Count / 2;
        var outline = new List<Vector2>(Vertices.Count);
        for (int index = 0; index < sampleCount; index++)
            outline.Add(Vertices[index * 2]);
        for (int index = sampleCount - 1; index >= 0; index--)
            outline.Add(Vertices[index * 2 + 1]);
        return outline;
    }
}
