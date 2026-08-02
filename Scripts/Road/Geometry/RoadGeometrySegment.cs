using Godot;
using System;
using System.Collections.Generic;

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

public readonly record struct RoadGeometryClosestPoint(
    float Parameter,
    Vector2 Position,
    float DistanceSquared)
{
    public float Distance => Mathf.Sqrt(DistanceSquared);
}

public enum RoadGeometryPointLocation
{
    Start,
    Interior,
    End,
}

public readonly record struct RoadGeometryPointHit(
    float Parameter,
    Vector2 Position,
    float DistanceSquared,
    RoadGeometryPointLocation Location)
{
    public float Distance => Mathf.Sqrt(DistanceSquared);
}

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

    public virtual RoadGeometryClosestPoint FindClosestPoint(Vector2 point, float tolerance = 1e-3f)
    {
        EnsureClosestPointArguments(point, tolerance);

        RoadGeometryClosestPoint best = CreateClosestPointCandidate(point, ParameterStart);
        best = ChooseCloser(best, CreateClosestPointCandidate(point, ParameterEnd));
        best = ChooseCloser(best, CreateClosestPointCandidate(point, 0.5f));

        var queue = new PriorityQueue<ClosestPointSearchInterval, float>();
        queue.Enqueue(
            new ClosestPointSearchInterval(this, ParameterStart, ParameterEnd),
            DistanceSquaredToBounds(point, Bounds));

        while (queue.TryDequeue(out ClosestPointSearchInterval interval, out float lowerBoundSquared))
        {
            float remainingDistanceGap = best.Distance - Mathf.Sqrt(lowerBoundSquared);
            if (remainingDistanceGap <= tolerance)
                break;

            const float localMidpoint = 0.5f;
            float globalMidpoint = (interval.ParameterStart + interval.ParameterEnd) * 0.5f;
            RoadGeometrySplit split = interval.Geometry.Split(localMidpoint);

            var before = new ClosestPointSearchInterval(
                split.Before, interval.ParameterStart, globalMidpoint);
            var after = new ClosestPointSearchInterval(
                split.After, globalMidpoint, interval.ParameterEnd);
            best = ChooseCloser(best, CreateClosestPointCandidate(point, before.Midpoint));
            best = ChooseCloser(best, CreateClosestPointCandidate(point, after.Midpoint));

            float beforeLowerBound = DistanceSquaredToBounds(point, split.Before.Bounds);
            float afterLowerBound = DistanceSquaredToBounds(point, split.After.Bounds);
            if (beforeLowerBound <= best.DistanceSquared)
                queue.Enqueue(before, beforeLowerBound);
            if (afterLowerBound <= best.DistanceSquared)
                queue.Enqueue(after, afterLowerBound);
        }

        return best;
    }

    public bool TryFindPointOnGeometry(
        Vector2 point,
        out RoadGeometryPointHit hit,
        float distanceTolerance = 1e-3f,
        float endpointParameterTolerance = 1e-4f)
    {
        EnsureClosestPointArguments(point, distanceTolerance);
        if (!float.IsFinite(endpointParameterTolerance) ||
            endpointParameterTolerance < 0f ||
            endpointParameterTolerance >= 0.5f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endpointParameterTolerance),
                endpointParameterTolerance,
                "Endpoint parameter tolerance must be finite and in [0, 0.5)."
            );
        }

        float searchTolerance = Mathf.Max(
            Mathf.Min(distanceTolerance * 0.01f, 1e-4f),
            float.Epsilon);
        RoadGeometryClosestPoint closest = FindClosestPoint(point, searchTolerance);
        if (closest.DistanceSquared > distanceTolerance * distanceTolerance)
        {
            hit = default;
            return false;
        }

        RoadGeometryPointLocation location = closest.Parameter <= endpointParameterTolerance
            ? RoadGeometryPointLocation.Start
            : closest.Parameter >= ParameterEnd - endpointParameterTolerance
                ? RoadGeometryPointLocation.End
                : RoadGeometryPointLocation.Interior;
        hit = new RoadGeometryPointHit(
            closest.Parameter,
            closest.Position,
            closest.DistanceSquared,
            location);
        return true;
    }

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

    protected static void EnsureClosestPointArguments(Vector2 point, float tolerance)
    {
        if (!IsFinite(point))
            throw new ArgumentException("Point must contain finite coordinates.", nameof(point));
        if (!float.IsFinite(tolerance) || tolerance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be positive and finite.");
    }

    protected RoadGeometryClosestPoint CreateClosestPointCandidate(Vector2 point, float parameter)
    {
        Vector2 position = GetPosition(parameter);
        return new RoadGeometryClosestPoint(parameter, position, position.DistanceSquaredTo(point));
    }

    protected static RoadGeometryClosestPoint ChooseCloser(
        RoadGeometryClosestPoint current,
        RoadGeometryClosestPoint candidate)
    {
        if (candidate.DistanceSquared < current.DistanceSquared ||
            (Mathf.IsEqualApprox(candidate.DistanceSquared, current.DistanceSquared) &&
             candidate.Parameter < current.Parameter))
            return candidate;
        return current;
    }

    private static float DistanceSquaredToBounds(Vector2 point, Rect2 bounds)
    {
        Vector2 end = bounds.End;
        float closestX = Mathf.Clamp(point.X, bounds.Position.X, end.X);
        float closestY = Mathf.Clamp(point.Y, bounds.Position.Y, end.Y);
        return point.DistanceSquaredTo(new Vector2(closestX, closestY));
    }

    private readonly record struct ClosestPointSearchInterval(
        RoadGeometrySegment Geometry,
        float ParameterStart,
        float ParameterEnd)
    {
        public float Midpoint => (ParameterStart + ParameterEnd) * 0.5f;
    }
}
