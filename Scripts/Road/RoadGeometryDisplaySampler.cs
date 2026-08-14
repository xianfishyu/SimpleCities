using Godot;
using System;
using System.Collections.Generic;

internal readonly record struct RoadGeometryDisplaySpan
{
    internal int GeometryIndex { get; }
    internal float ParameterStart { get; }
    internal float ParameterEnd { get; }

    internal RoadGeometryDisplaySpan(
        int geometryIndex,
        float parameterStart,
        float parameterEnd)
    {
        if (geometryIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(geometryIndex));
        if (!float.IsFinite(parameterStart) ||
            !float.IsFinite(parameterEnd) ||
            parameterStart < RoadGeometrySegment.ParameterStart ||
            parameterEnd > RoadGeometrySegment.ParameterEnd ||
            parameterStart >= parameterEnd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameterStart),
                "Display span parameters must form a finite increasing interval in [0, 1].");
        }

        GeometryIndex = geometryIndex;
        ParameterStart = parameterStart;
        ParameterEnd = parameterEnd;
    }
}

internal sealed class RoadGeometryDisplayPath
{
    internal RoadGeometryDisplayPath(
        Vector2[] points,
        RoadGeometryDisplaySpan[] spans)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(spans);
        if (spans.Length != Math.Max(0, points.Length - 1))
        {
            throw new ArgumentException(
                "Every display point interval must have exactly one source span.",
                nameof(spans));
        }

        Points = points;
        Spans = spans;
    }

    internal Vector2[] Points { get; }
    internal RoadGeometryDisplaySpan[] Spans { get; }
}

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

        return SamplePath([geometry], tolerance).Points;
    }

    public static Vector2[] SampleSegments(
        IEnumerable<RoadGeometrySegment?> geometries,
        float tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(geometries);
        ValidateTolerance(tolerance);
        var materialized = new List<RoadGeometrySegment>();
        foreach (RoadGeometrySegment? geometry in geometries)
        {
            if (geometry == null)
                throw new ArgumentException("Display geometry segments cannot contain null.", nameof(geometries));
            materialized.Add(geometry);
        }

        return SamplePath(materialized, tolerance).Points;
    }

    internal static RoadGeometryDisplayPath SamplePath(
        IReadOnlyList<RoadGeometrySegment> geometries,
        float tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(geometries);
        ValidateTolerance(tolerance);

        var points = new List<Vector2>();
        var spans = new List<RoadGeometryDisplaySpan>();
        for (int geometryIndex = 0; geometryIndex < geometries.Count; geometryIndex++)
        {
            RoadGeometrySegment? geometry = geometries[geometryIndex];
            if (geometry == null)
            {
                throw new ArgumentException(
                    "Display geometry segments cannot contain null.",
                    nameof(geometries));
            }

            if (points.Count == 0)
            {
                points.Add(geometry.Start);
            }
            else if (points[^1] != geometry.Start)
            {
                throw new ArgumentException("Display geometry segments must form a continuous path.", nameof(geometries));
            }

            AppendSegment(
                geometry,
                tolerance,
                depth: 0,
                geometryIndex,
                RoadGeometrySegment.ParameterStart,
                RoadGeometrySegment.ParameterEnd,
                points,
                spans);
            points[^1] = geometry.End;
        }

        return new RoadGeometryDisplayPath(points.ToArray(), spans.ToArray());
    }

    private static void AppendSegment(
        RoadGeometrySegment geometry,
        float tolerance,
        int depth,
        int geometryIndex,
        float parameterStart,
        float parameterEnd,
        List<Vector2> points,
        List<RoadGeometryDisplaySpan> spans)
    {
        if (geometry.Kind == RoadGeometryKind.Line ||
            depth >= MaxSubdivisionDepth ||
            IsFlatEnough(geometry, tolerance))
        {
            points.Add(geometry.End);
            spans.Add(new RoadGeometryDisplaySpan(
                geometryIndex,
                parameterStart,
                parameterEnd));
            return;
        }

        RoadGeometrySplit split = geometry.Split(0.5f);
        float parameterMidpoint = (parameterStart + parameterEnd) * 0.5f;
        AppendSegment(
            split.Before,
            tolerance,
            depth + 1,
            geometryIndex,
            parameterStart,
            parameterMidpoint,
            points,
            spans);
        AppendSegment(
            split.After,
            tolerance,
            depth + 1,
            geometryIndex,
            parameterMidpoint,
            parameterEnd,
            points,
            spans);
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
