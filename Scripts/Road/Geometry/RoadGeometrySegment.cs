using Godot;
using System;

public enum RoadGeometryKind
{
    Line,
    CubicBezier,
    CubicHermiteSpline,
    CircularArc,
    Clothoid,
    RationalQuadratic,
}

public readonly record struct RoadGeometrySplit(
    RoadGeometrySegment Before,
    RoadGeometrySegment After);

public abstract class RoadGeometrySegment
{
    public const float ParameterStart = 0f;
    public const float ParameterEnd = 1f;

    public abstract RoadGeometryKind Kind { get; }
    public abstract Vector2 Start { get; }
    public abstract Vector2 End { get; }
    public abstract float Length { get; }
    public abstract Rect2 Bounds { get; }

    public abstract Vector2 GetPosition(float parameter);
    public abstract Vector2 GetUnitTangent(float parameter);
    public abstract RoadGeometrySplit Split(float parameter);

    protected static void EnsureParameterInDomain(float parameter)
    {
        if (!float.IsFinite(parameter) || parameter < ParameterStart || parameter > ParameterEnd)
            throw new ArgumentOutOfRangeException(nameof(parameter), parameter, "Parameter must be finite and in [0, 1].");
    }

    protected static void EnsureInteriorParameter(float parameter)
    {
        EnsureParameterInDomain(parameter);
        if (parameter <= ParameterStart || parameter >= ParameterEnd)
            throw new ArgumentOutOfRangeException(nameof(parameter), parameter, "Split parameter must be in (0, 1).");
    }

    protected static bool IsFinite(Vector2 point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y);
}
