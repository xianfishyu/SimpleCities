using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// 一个 junction patch 的纯数据：以节点为中心、按入射方向生成的轮廓多边形。
/// 这是 V3 混合宽度 junction 填补的网格数据基础。
/// </summary>
public sealed record RoadJunctionPatchData(
    int NodeID,
    IReadOnlyList<Vector2> Outline,
    Color Color)
{
    public bool IsValid =>
        NodeID >= 0 &&
        Outline.Count >= 3 &&
        Outline.All(point => point.IsFinite()) &&
        float.IsFinite(Color.R) &&
        float.IsFinite(Color.G) &&
        float.IsFinite(Color.B) &&
        float.IsFinite(Color.A);
}
