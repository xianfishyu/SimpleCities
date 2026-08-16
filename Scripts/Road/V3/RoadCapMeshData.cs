using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// 一个 degree-1 端点的端帽网格数据：以节点中心为 fan 原点、沿外法线方向生成半圆轮廓。
/// </summary>
public sealed record RoadCapMeshData(
    int NodeID,
    int EdgeID,
    EdgeEndpoint Endpoint,
    IReadOnlyList<Vector2> Outline,
    Color Color)
{
    public bool IsValid =>
        NodeID >= 0 &&
        EdgeID >= 0 &&
        Outline.Count >= 3 &&
        Outline.All(point => point.IsFinite()) &&
        float.IsFinite(Color.R) &&
        float.IsFinite(Color.G) &&
        float.IsFinite(Color.B) &&
        float.IsFinite(Color.A);
}
