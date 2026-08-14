using Godot;
using System;

public sealed class CircularArcRoadGeometrySegment : RoadGeometrySegment
{
    private const float SweepEpsilon = 1e-6f;

    private readonly Vector2 _start;
    private readonly Vector2 _end;
    private readonly float _evaluationStartAngle;

    public override RoadGeometryKind Kind => RoadGeometryKind.CircularArc;
    public Vector2 Center { get; }
    public float Radius { get; }
    public float StartAngle { get; }
    public float EndAngle { get; }
    public float SweepAngle { get; }
    public bool IsFullTurn =>
        BitConverter.SingleToInt32Bits(SweepAngle) == BitConverter.SingleToInt32Bits(Mathf.Tau) ||
        BitConverter.SingleToInt32Bits(SweepAngle) == BitConverter.SingleToInt32Bits(-Mathf.Tau);
    public override Vector2 Start => _start;
    public override Vector2 End => _end;
    public override float Length => Radius * Mathf.Abs(SweepAngle);
    public override Rect2 Bounds { get; }

    public CircularArcRoadGeometrySegment(
        Vector2 center,
        float radius,
        float startAngle,
        float sweepAngle)
        : this(center, radius, startAngle, sweepAngle, null, null)
    {
    }

    private CircularArcRoadGeometrySegment(
        Vector2 center,
        float radius,
        float startAngle,
        float sweepAngle,
        Vector2? startAnchor,
        Vector2? endAnchor,
        float? endAngle = null)
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
        _evaluationStartAngle = RoadGeometryDirection.NormalizePeriodicAngle(StartAngle);
        EndAngle = endAngle ?? (IsFullTurn
            ? StartAngle
            : RoadGeometryDirection.NormalizePeriodicAngle(_evaluationStartAngle + SweepAngle));
        if (!float.IsFinite(EndAngle))
            throw new ArgumentOutOfRangeException(nameof(endAngle), endAngle, "EndAngle must be finite.");
        ValidateEndAngle();
        Vector2 derivedStart = PositionAtAngle(_evaluationStartAngle);
        Vector2 derivedEnd = IsFullTurn
            ? derivedStart
            : PositionAtAngle(EndAngle);
        _start = startAnchor ?? derivedStart;
        _end = endAnchor ?? (IsFullTurn ? _start : derivedEnd);
        ValidateAnchor(derivedStart, _start, nameof(startAnchor));
        ValidateAnchor(derivedEnd, _end, nameof(endAnchor));
        if (IsFullTurn && !RoadExactPredicates.SameBits(_start, _end))
            throw new ArgumentException("A full-turn arc must use one exact seam anchor.");
        Bounds = ComputeBounds();
    }

    internal static CircularArcRoadGeometrySegment CreateAnchored(
        Vector2 center,
        float radius,
        float startAngle,
        float sweepAngle,
        Vector2 start,
        Vector2 end,
        float? endAngle = null) =>
        new(center, radius, startAngle, sweepAngle, start, end, endAngle);

    public override Vector2 GetPosition(float parameter)
    {
        EnsureParameterInDomain(parameter);
        if (parameter == ParameterStart) return Start;
        if (parameter == ParameterEnd) return End;
        return PositionAtAngle(_evaluationStartAngle + SweepAngle * parameter);
    }

    public override Vector2 GetUnitTangent(float parameter)
    {
        EnsureParameterInDomain(parameter);
        float angle = parameter == ParameterEnd
            ? EndAngle
            : _evaluationStartAngle + SweepAngle * parameter;
        float direction = Mathf.Sign(SweepAngle);
        return direction * new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
    }

    public override RoadGeometrySplit Split(float parameter)
    {
        EnsureInteriorParameter(parameter);
        float beforeSweep = SweepAngle * parameter;
        Vector2 splitPoint = GetPosition(parameter);
        return new RoadGeometrySplit(
            CreateAnchored(
                Center,
                Radius,
                _evaluationStartAngle,
                beforeSweep,
                Start,
                splitPoint,
                _evaluationStartAngle + beforeSweep),
            CreateAnchored(
                Center,
                Radius,
                _evaluationStartAngle + beforeSweep,
                SweepAngle - beforeSweep,
                splitPoint,
                End,
                EndAngle));
    }

    public override RoadGeometrySegment Reverse() =>
        CreateAnchored(
            Center,
            Radius,
            EndAngle,
            -SweepAngle,
            End,
            Start,
            StartAngle);

    public override RoadGeometryClosestPoint FindClosestPoint(Vector2 point, float tolerance = 1e-3f)
    {
        EnsureClosestPointArguments(point, tolerance);
        Vector2 offset = point - Center;
        if (offset == Vector2.Zero)
            return CreateClosestPointCandidate(point, ParameterStart);

        float angle = Mathf.Atan2(offset.Y, offset.X);
        float directedDelta = SweepAngle > 0f
            ? Mathf.PosMod(angle - _evaluationStartAngle, Mathf.Tau)
            : Mathf.PosMod(_evaluationStartAngle - angle, Mathf.Tau);
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
            ? Mathf.PosMod(angle - _evaluationStartAngle, Mathf.Tau)
            : Mathf.PosMod(_evaluationStartAngle - angle, Mathf.Tau);
        return directedDelta <= Mathf.Abs(SweepAngle) + SweepEpsilon;
    }

    private Vector2 PositionAtAngle(float angle) =>
        Center + Radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

    private void ValidateAnchor(Vector2 derived, Vector2 anchor, string parameterName)
    {
        if (!IsFinite(anchor))
            throw new ArgumentException("Arc endpoint anchors must be finite.", parameterName);

        float tolerance = Mathf.Max(
            RoadNumericPolicy.MaximumIntersectionClusterDiameter,
            Radius * 2e-6f);
        if (derived.DistanceTo(anchor) > tolerance)
        {
            throw new ArgumentException(
                "Arc endpoint anchors must agree with the native parameters.",
                parameterName);
        }
    }

    private void ValidateEndAngle()
    {
        float expected = IsFullTurn
            ? RoadGeometryDirection.NormalizePeriodicAngle(StartAngle)
            : RoadGeometryDirection.NormalizePeriodicAngle(_evaluationStartAngle + SweepAngle);
        float actual = RoadGeometryDirection.NormalizePeriodicAngle(EndAngle);
        float difference = Mathf.PosMod(actual - expected + Mathf.Pi, Mathf.Tau) - Mathf.Pi;
        if (Mathf.Abs(difference) > 2e-6f)
            throw new ArgumentException("EndAngle does not agree with startAngle and sweepAngle.");
    }
}
