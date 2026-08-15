using Godot;
using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// 为 line primitive 生成参数区间 query fragment：按 uniform grid bucket 边界切分，
/// 避免整条斜线 AABB 污染无关 bucket。曲线 fragment 策略后续再补。
/// </summary>
public static class RoadQueryFragmentBuilder
{
    public static IReadOnlyList<RoadQueryFragment> BuildLineFragments(
        int edgeID,
        int geometryIndex,
        LineRoadGeometrySegment line,
        float bucketSize)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!float.IsFinite(bucketSize) || bucketSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(bucketSize), "Bucket size must be finite and positive.");

        var cuts = new List<float> { 0f, 1f };
        AddBoundaryCrossings(cuts, line.Start.X, line.End.X, bucketSize);
        AddBoundaryCrossings(cuts, line.Start.Y, line.End.Y, bucketSize);
        cuts.Sort();

        var unique = new List<float>(cuts.Count);
        foreach (float cut in cuts)
        {
            if (unique.Count == 0 || cut - unique[^1] > 1e-6f)
                unique.Add(cut);
        }

        var fragments = new List<RoadQueryFragment>(unique.Count - 1);
        for (int index = 0; index < unique.Count - 1; index++)
        {
            float start = unique[index];
            float end = unique[index + 1];
            if (end - start <= 0f)
                continue;

            Vector2 startPoint = line.GetPosition(start);
            Vector2 endPoint = line.GetPosition(end);
            Rect2 bounds = BoundsOf(startPoint, endPoint);
            fragments.Add(new RoadQueryFragment(
                edgeID,
                geometryIndex,
                fragments.Count,
                start,
                end,
                bounds));
        }

        return fragments;
    }

    private static void AddBoundaryCrossings(List<float> cuts, float start, float end, float bucketSize)
    {
        if (end == start)
            return;

        float min = MathF.Min(start, end);
        float max = MathF.Max(start, end);
        int firstBucket = (int)MathF.Floor(min / bucketSize) + 1;
        int lastBucket = (int)MathF.Floor(max / bucketSize);
        for (int bucket = firstBucket; bucket <= lastBucket; bucket++)
        {
            float boundary = bucket * bucketSize;
            float parameter = (boundary - start) / (end - start);
            if (parameter > 0f && parameter < 1f)
                cuts.Add(parameter);
        }
    }

    private static Rect2 BoundsOf(Vector2 a, Vector2 b)
    {
        Vector2 min = new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y));
        Vector2 max = new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
        return new Rect2(min, max - min);
    }
}
