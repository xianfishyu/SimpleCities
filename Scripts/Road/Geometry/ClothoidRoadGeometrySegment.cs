using Godot;
using System;

public sealed class ClothoidRoadGeometrySegment : RoadGeometrySegment
{
    private const int MaxIntegrationDepth = 20;
    private const float RelativePositionTolerance = 1e-5f;

    private readonly float _curvatureRate;
    private readonly Vector2 _end;

    public override RoadGeometryKind Kind => RoadGeometryKind.Clothoid;
    public override Vector2 Start { get; }
    public float StartHeading { get; }
    public float StartCurvature { get; }
    public float EndCurvature { get; }
    public float ArcLength { get; }
    public override Vector2 End => _end;
    public override float Length => ArcLength;
    public override Rect2 Bounds { get; }

    public ClothoidRoadGeometrySegment(
        Vector2 start,
        float startHeading,
        float startCurvature,
        float endCurvature,
        float arcLength)
    {
        if (!IsFinite(start))
            throw new ArgumentException("Start must contain finite coordinates.", nameof(start));
        if (!float.IsFinite(startHeading))
            throw new ArgumentOutOfRangeException(nameof(startHeading), startHeading, "StartHeading must be finite.");
        if (!float.IsFinite(startCurvature))
            throw new ArgumentOutOfRangeException(nameof(startCurvature), startCurvature, "StartCurvature must be finite.");
        if (!float.IsFinite(endCurvature))
            throw new ArgumentOutOfRangeException(nameof(endCurvature), endCurvature, "EndCurvature must be finite.");
        if (!float.IsFinite(arcLength) || arcLength <= 0f)
            throw new ArgumentOutOfRangeException(nameof(arcLength), arcLength, "ArcLength must be positive and finite.");

        Start = start;
        StartHeading = startHeading;
        StartCurvature = startCurvature;
        EndCurvature = endCurvature;
        ArcLength = arcLength;
        _curvatureRate = (endCurvature - startCurvature) / arcLength;
        if (!float.IsFinite(_curvatureRate) || !float.IsFinite(GetHeadingAtArcLength(arcLength)))
            throw new ArgumentException("Clothoid parameters produce non-finite curvature or heading.");

        _end = Start + IntegrateDisplacement(arcLength);
        if (!IsFinite(_end))
            throw new ArgumentException("Clothoid parameters produce a non-finite endpoint.");

        Vector2 extent = Vector2.One * arcLength;
        Bounds = new Rect2(Start - extent, extent * 2f);
        if (!IsFinite(Bounds.Position) || !IsFinite(Bounds.End))
            throw new ArgumentException("Clothoid parameters produce non-finite bounds.");
    }

    public float GetCurvature(float parameter)
    {
        EnsureParameterInDomain(parameter);
        return Mathf.Lerp(StartCurvature, EndCurvature, parameter);
    }

    public override Vector2 GetPosition(float parameter)
    {
        EnsureParameterInDomain(parameter);
        if (parameter == ParameterStart) return Start;
        if (parameter == ParameterEnd) return End;
        return Start + IntegrateDisplacement(ArcLength * parameter);
    }

    public override Vector2 GetUnitTangent(float parameter)
    {
        EnsureParameterInDomain(parameter);
        float heading = GetHeadingAtArcLength(ArcLength * parameter);
        return new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
    }

    public override RoadGeometrySplit Split(float parameter)
    {
        EnsureInteriorParameter(parameter);
        float beforeLength = ArcLength * parameter;
        float splitCurvature = GetCurvature(parameter);
        Vector2 splitPosition = GetPosition(parameter);
        float splitHeading = GetHeadingAtArcLength(beforeLength);
        return new RoadGeometrySplit(
            new ClothoidRoadGeometrySegment(
                Start,
                StartHeading,
                StartCurvature,
                splitCurvature,
                beforeLength),
            new ClothoidRoadGeometrySegment(
                splitPosition,
                splitHeading,
                splitCurvature,
                EndCurvature,
                ArcLength - beforeLength));
    }

    private float GetHeadingAtArcLength(float distance) =>
        StartHeading + StartCurvature * distance + 0.5f * _curvatureRate * distance * distance;

    private Vector2 IntegrateDisplacement(float distance)
    {
        if (EndCurvature == StartCurvature)
            return IntegrateConstantCurvature(distance);

        Vector2 startValue = DirectionAtArcLength(0f);
        Vector2 middleValue = DirectionAtArcLength(distance * 0.5f);
        Vector2 endValue = DirectionAtArcLength(distance);
        Vector2 whole = Simpson(0f, distance, startValue, middleValue, endValue);
        float tolerance = RelativePositionTolerance * Mathf.Max(1f, distance);
        return IntegrateAdaptive(0f, distance, startValue, middleValue, endValue, whole, tolerance, 0);
    }

    private Vector2 IntegrateConstantCurvature(float distance)
    {
        if (StartCurvature == 0f)
            return distance * new Vector2(Mathf.Cos(StartHeading), Mathf.Sin(StartHeading));

        float endHeading = StartHeading + StartCurvature * distance;
        return new Vector2(
            (Mathf.Sin(endHeading) - Mathf.Sin(StartHeading)) / StartCurvature,
            (Mathf.Cos(StartHeading) - Mathf.Cos(endHeading)) / StartCurvature);
    }

    private Vector2 IntegrateAdaptive(
        float start,
        float end,
        Vector2 startValue,
        Vector2 middleValue,
        Vector2 endValue,
        Vector2 whole,
        float tolerance,
        int depth)
    {
        float middle = (start + end) * 0.5f;
        float leftMiddle = (start + middle) * 0.5f;
        float rightMiddle = (middle + end) * 0.5f;
        Vector2 leftMiddleValue = DirectionAtArcLength(leftMiddle);
        Vector2 rightMiddleValue = DirectionAtArcLength(rightMiddle);
        Vector2 left = Simpson(start, middle, startValue, leftMiddleValue, middleValue);
        Vector2 right = Simpson(middle, end, middleValue, rightMiddleValue, endValue);
        Vector2 refined = left + right;
        Vector2 correction = (refined - whole) / 15f;

        if (depth >= MaxIntegrationDepth || correction.Length() <= tolerance)
            return refined + correction;

        float childTolerance = tolerance * 0.5f;
        return IntegrateAdaptive(
                   start, middle, startValue, leftMiddleValue, middleValue,
                   left, childTolerance, depth + 1) +
               IntegrateAdaptive(
                   middle, end, middleValue, rightMiddleValue, endValue,
                   right, childTolerance, depth + 1);
    }

    private Vector2 DirectionAtArcLength(float distance)
    {
        float heading = GetHeadingAtArcLength(distance);
        return new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
    }

    private static Vector2 Simpson(
        float start,
        float end,
        Vector2 startValue,
        Vector2 middleValue,
        Vector2 endValue) =>
        (end - start) * (startValue + 4f * middleValue + endValue) / 6f;
}
