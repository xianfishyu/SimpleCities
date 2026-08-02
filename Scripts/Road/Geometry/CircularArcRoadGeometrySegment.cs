using Godot;
using System;

public sealed class CircularArcRoadGeometrySegment : RoadGeometrySegment
{
    private const float SweepEpsilon = 1e-6f;

    public override RoadGeometryKind Kind => RoadGeometryKind.CircularArc;
    public Vector2 Center { get; }
    public float Radius { get; }
    public float StartAngle { get; }
    public float SweepAngle { get; }
    public override Vector2 Start => PositionAtAngle(StartAngle);
    public override Vector2 End => PositionAtAngle(StartAngle + SweepAngle);
    public override float Length => Radius * Mathf.Abs(SweepAngle);
    public override Rect2 Bounds { get; }

    public CircularArcRoadGeometrySegment(
        Vector2 center,
        float radius,
        float startAngle,
        float sweepAngle)
    {
        if (!IsFinite(center))
            throw new ArgumentException("Center must contain finite coordinates.", nameof(center));
        if (!float.IsFinite(radius) || radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be positive and finite.");
        if (!float.IsFinite(startAngle))
            throw new ArgumentOutOfRangeException(nameof(startAngle), startAngle, "StartAngle must be finite.");
        if (!float.IsFinite(sweepAngle) || Mathf.Abs(sweepAngle) <= SweepEpsilon || Mathf.Abs(sweepAngle) > Mathf.Tau)
            throw new ArgumentOutOfRangeException(nameof(sweepAngle), sweepAngle, "SweepAngle must be finite, non-zero, and no greater than one revolution.");

        Center = center;
        Radius = radius;
        StartAngle = startAngle;
        SweepAngle = sweepAngle;
        Bounds = ComputeBounds();
    }

    public override Vector2 GetPosition(float parameter)
    {
        EnsureParameterInDomain(parameter);
        return PositionAtAngle(StartAngle + SweepAngle * parameter);
    }

    public override Vector2 GetUnitTangent(float parameter)
    {
        EnsureParameterInDomain(parameter);
        float angle = StartAngle + SweepAngle * parameter;
        float direction = Mathf.Sign(SweepAngle);
        return direction * new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
    }

    public override RoadGeometrySplit Split(float parameter)
    {
        EnsureInteriorParameter(parameter);
        float beforeSweep = SweepAngle * parameter;
        return new RoadGeometrySplit(
            new CircularArcRoadGeometrySegment(Center, Radius, StartAngle, beforeSweep),
            new CircularArcRoadGeometrySegment(Center, Radius, StartAngle + beforeSweep, SweepAngle - beforeSweep));
    }

    public override RoadGeometryClosestPoint FindClosestPoint(Vector2 point, float tolerance = 1e-3f)
    {
        EnsureClosestPointArguments(point, tolerance);
        Vector2 offset = point - Center;
        if (offset == Vector2.Zero)
            return CreateClosestPointCandidate(point, ParameterStart);

        float angle = Mathf.Atan2(offset.Y, offset.X);
        float directedDelta = SweepAngle > 0f
            ? Mathf.PosMod(angle - StartAngle, Mathf.Tau)
            : Mathf.PosMod(StartAngle - angle, Mathf.Tau);
        if (directedDelta <= Mathf.Abs(SweepAngle) + SweepEpsilon)
        {
            float parameter = Mathf.Clamp(directedDelta / Mathf.Abs(SweepAngle), 0f, 1f);
            return CreateClosestPointCandidate(point, parameter);
        }

        return ChooseCloser(
            CreateClosestPointCandidate(point, ParameterStart),
            CreateClosestPointCandidate(point, ParameterEnd));
    }

    private Rect2 ComputeBounds()
    {
        Vector2 start = Start;
        Vector2 end = End;
        float minX = Mathf.Min(start.X, end.X);
        float maxX = Mathf.Max(start.X, end.X);
        float minY = Mathf.Min(start.Y, end.Y);
        float maxY = Mathf.Max(start.Y, end.Y);

        foreach (float angle in new[] { 0f, Mathf.Pi * 0.5f, Mathf.Pi, Mathf.Pi * 1.5f })
        {
            if (!ContainsAngle(angle)) continue;
            Vector2 point = PositionAtAngle(angle);
            minX = Mathf.Min(minX, point.X);
            maxX = Mathf.Max(maxX, point.X);
            minY = Mathf.Min(minY, point.Y);
            maxY = Mathf.Max(maxY, point.Y);
        }

        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }

    private bool ContainsAngle(float angle)
    {
        float directedDelta = SweepAngle > 0f
            ? Mathf.PosMod(angle - StartAngle, Mathf.Tau)
            : Mathf.PosMod(StartAngle - angle, Mathf.Tau);
        return directedDelta <= Mathf.Abs(SweepAngle) + SweepEpsilon;
    }

    private Vector2 PositionAtAngle(float angle) =>
        Center + Radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
}
