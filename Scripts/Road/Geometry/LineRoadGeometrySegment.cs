using Godot;
using System;

public sealed class LineRoadGeometrySegment : RoadGeometrySegment
{
    private readonly Vector2 _unitTangent;

    public override RoadGeometryKind Kind => RoadGeometryKind.Line;
    public override Vector2 Start { get; }
    public override Vector2 End { get; }
    public override float Length { get; }
    public override Rect2 Bounds { get; }

    public LineRoadGeometrySegment(Vector2 start, Vector2 end)
    {
        if (!IsFinite(start))
            throw new ArgumentException("Start must contain finite coordinates.", nameof(start));
        if (!IsFinite(end))
            throw new ArgumentException("End must contain finite coordinates.", nameof(end));

        Vector2 displacement = end - start;
        float length = displacement.Length();
        if (!float.IsFinite(length) || length <= 0f)
            throw new ArgumentException("A line segment must have positive finite length.", nameof(end));

        Start = start;
        End = end;
        Length = length;
        _unitTangent = displacement / length;

        Vector2 minimum = new(Mathf.Min(start.X, end.X), Mathf.Min(start.Y, end.Y));
        Vector2 maximum = new(Mathf.Max(start.X, end.X), Mathf.Max(start.Y, end.Y));
        Bounds = new Rect2(minimum, maximum - minimum);
    }

    public override Vector2 GetPosition(float parameter)
    {
        EnsureParameterInDomain(parameter);
        return Start.Lerp(End, parameter);
    }

    public override Vector2 GetUnitTangent(float parameter)
    {
        EnsureParameterInDomain(parameter);
        return _unitTangent;
    }

    public override RoadGeometrySplit Split(float parameter)
    {
        EnsureInteriorParameter(parameter);
        Vector2 splitPoint = GetPosition(parameter);
        return new RoadGeometrySplit(
            new LineRoadGeometrySegment(Start, splitPoint),
            new LineRoadGeometrySegment(splitPoint, End));
    }

    public override RoadGeometryClosestPoint FindClosestPoint(Vector2 point, float tolerance = 1e-3f)
    {
        EnsureClosestPointArguments(point, tolerance);
        Vector2 displacement = End - Start;
        float parameter = Mathf.Clamp((point - Start).Dot(displacement) / displacement.LengthSquared(), 0f, 1f);
        return CreateClosestPointCandidate(point, parameter);
    }
}
