using Godot;
using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// geometry 层 canonicalizer：只合并可无损合并的相邻同类 primitive。
/// 当前实现 line 按 exact-sign 契约合并；其他原生曲线保留原样，绝不采样降级。
/// 输出同时把 line 端点中的 -0 规范为 +0。
/// </summary>
public static class RoadGeometryCanonicalizer
{
    public static IReadOnlyList<RoadGeometrySegment> Canonicalize(
        IReadOnlyList<RoadGeometrySegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var result = new List<RoadGeometrySegment>(segments.Count);
        foreach (RoadGeometrySegment segment in segments)
        {
            ArgumentNullException.ThrowIfNull(segment);
            if (result.Count == 0)
            {
                result.Add(NormalizeSegment(segment));
                continue;
            }

            if (result[^1] is LineRoadGeometrySegment previous &&
                segment is LineRoadGeometrySegment next &&
                ExactLinePredicates.CanMergeLineSegments(previous, next))
            {
                result[^1] = new LineRoadGeometrySegment(
                    RoadNumericPolicy.NormalizeVector(previous.Start),
                    RoadNumericPolicy.NormalizeVector(next.End));
            }
            else
            {
                result.Add(NormalizeSegment(segment));
            }
        }

        return result;
    }

    private static RoadGeometrySegment NormalizeSegment(RoadGeometrySegment segment) =>
        segment is LineRoadGeometrySegment line
            ? new LineRoadGeometrySegment(
                RoadNumericPolicy.NormalizeVector(line.Start),
                RoadNumericPolicy.NormalizeVector(line.End))
            : segment;
}
