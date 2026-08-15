using Godot;
using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// canonical RoadLocation：Edge 内限定到 geometry 与参数的权威命中位置。
/// </summary>
public readonly record struct RoadLocation(
    int EdgeID,
    int GeometryIndex,
    float Parameter);

/// <summary>
/// 空间索引的派生 query fragment；不持久化、不产生拓扑身份。
/// </summary>
public readonly record struct RoadQueryFragment(
    int EdgeID,
    int GeometryIndex,
    int FragmentIndex,
    float ParameterStart,
    float ParameterEnd,
    Rect2 ConservativeBounds);

/// <summary>
/// primitive join 与 self-loop seam 的边界所有权映射。
/// 非环 Edge 的最后一个 fragment 拥有最终 t=1；self-loop 的 (last,1) 映射为 (0,0)。
/// </summary>
public static class RoadQueryOwnership
{
    public static RoadLocation NormalizeBoundary(
        int edgeID,
        int geometryIndex,
        float parameter,
        int geometryCount,
        bool isSelfLoop)
    {
        if (geometryIndex < 0 || geometryIndex >= geometryCount)
            throw new ArgumentOutOfRangeException(nameof(geometryIndex), "Geometry index is out of range.");
        if (!float.IsFinite(parameter) || parameter < 0f || parameter > 1f)
            throw new ArgumentOutOfRangeException(nameof(parameter), "Parameter must be in [0, 1].");

        if (parameter == 1f)
        {
            if (isSelfLoop && geometryIndex == geometryCount - 1)
                return new RoadLocation(edgeID, 0, 0f);
            if (geometryIndex < geometryCount - 1)
                return new RoadLocation(edgeID, geometryIndex + 1, 0f);
            return new RoadLocation(edgeID, geometryIndex, 1f);
        }

        return new RoadLocation(edgeID, geometryIndex, parameter);
    }
}
