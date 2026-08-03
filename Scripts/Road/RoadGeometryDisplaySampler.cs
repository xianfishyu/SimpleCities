using Godot;
using System;
using System.Collections.Generic;

/// <summary>把权威道路几何细分为确定的显示折线，不修改原始几何。</summary>
public static class RoadGeometryDisplaySampler
{
    public const float DefaultTolerance = 0.25f;
    public const int MaxSubdivisionDepth = 16;

    public static Vector2[] SampleSegment(
        RoadGeometrySegment geometry,
        float tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ValidateTolerance(tolerance);

        var points = new List<Vector2> { geometry.Start };
        AppendSegment(geometry, tolerance, depth: 0, points);
        points[^1] = geometry.End;
        return points.ToArray();
    }

    public static Vector2[] SampleSegments(
        IEnumerable<RoadGeometrySegment?> geometries,
        float tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(geometries);
        ValidateTolerance(tolerance);

        var points = new List<Vector2>();
        foreach (RoadGeometrySegment? geometry in geometries)
        {
            if (geometry == null)
                throw new ArgumentException("Display geometry segments cannot contain null.", nameof(geometries));

            if (points.Count == 0)
            {
                points.Add(geometry.Start);
            }
            else if (points[^1] != geometry.Start)
            {
                throw new ArgumentException("Display geometry segments must form a continuous path.", nameof(geometries));
            }

            AppendSegment(geometry, tolerance, depth: 0, points);
            points[^1] = geometry.End;
        }

        return points.ToArray();
    }

    private static void AppendSegment(
        RoadGeometrySegment geometry,
        float tolerance,
        int depth,
        List<Vector2> points)
    {
        if (geometry.Kind == RoadGeometryKind.Line ||
            depth >= MaxSubdivisionDepth ||
            IsFlatEnough(geometry, tolerance))
        {
            points.Add(geometry.End);
            return;
        }

        RoadGeometrySplit split = geometry.Split(0.5f);
        AppendSegment(split.Before, tolerance, depth + 1, points);
        AppendSegment(split.After, tolerance, depth + 1, points);
    }

    private static bool IsFlatEnough(RoadGeometrySegment geometry, float tolerance)
    {
        Vector2 start = geometry.Start;
        Vector2 end = geometry.End;
        float toleranceSquared = tolerance * tolerance;
        if (DistanceSquaredToSegment(geometry.GetPosition(0.25f), start, end) > toleranceSquared ||
            DistanceSquaredToSegment(geometry.GetPosition(0.5f), start, end) > toleranceSquared ||
            DistanceSquaredToSegment(geometry.GetPosition(0.75f), start, end) > toleranceSquared)
            return false;

        float chordLength = start.DistanceTo(end);
        return geometry.Length - chordLength <= tolerance;
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        float lengthSquared = delta.LengthSquared();
        if (lengthSquared == 0f)
            return point.DistanceSquaredTo(start);

        float parameter = Mathf.Clamp((point - start).Dot(delta) / lengthSquared, 0f, 1f);
        return point.DistanceSquaredTo(start + parameter * delta);
    }

    private static void ValidateTolerance(float tolerance)
    {
        if (!float.IsFinite(tolerance) || tolerance <= 0f)
            throw new ArgumentOutOfRangeException(
                nameof(tolerance),
                tolerance,
                "Display tolerance must be positive and finite.");
    }
}
