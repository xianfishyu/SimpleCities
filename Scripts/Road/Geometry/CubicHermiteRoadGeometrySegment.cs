using Godot;
using System;

public sealed class CubicHermiteRoadGeometrySegment : RoadGeometrySegment
{
    private readonly CubicBezierRoadGeometrySegment _bezier;

    public override RoadGeometryKind Kind => RoadGeometryKind.CubicHermiteSpline;
    public override Vector2 Start { get; }
    public Vector2 StartTangent { get; }
    public override Vector2 End { get; }
    public Vector2 EndTangent { get; }
    public override float Length => _bezier.Length;
    public override Rect2 Bounds => _bezier.Bounds;

    public CubicHermiteRoadGeometrySegment(
        Vector2 start,
        Vector2 startTangent,
        Vector2 end,
        Vector2 endTangent)
    {
        if (!IsFinite(start))
            throw new ArgumentException("Start must contain finite coordinates.", nameof(start));
        if (!IsFinite(startTangent))
            throw new ArgumentException("StartTangent must contain finite coordinates.", nameof(startTangent));
        if (!IsFinite(end))
            throw new ArgumentException("End must contain finite coordinates.", nameof(end));
        if (!IsFinite(endTangent))
            throw new ArgumentException("EndTangent must contain finite coordinates.", nameof(endTangent));

        Start = start;
        StartTangent = startTangent;
        End = end;
        EndTangent = endTangent;
        _bezier = new CubicBezierRoadGeometrySegment(
            start,
            start + startTangent / 3f,
            end - endTangent / 3f,
            end);
    }

    public override Vector2 GetPosition(float parameter) => _bezier.GetPosition(parameter);

    public override Vector2 GetUnitTangent(float parameter) => _bezier.GetUnitTangent(parameter);

    public override RoadGeometrySplit Split(float parameter)
    {
        EnsureInteriorParameter(parameter);
        RoadGeometrySplit bezierSplit = _bezier.Split(parameter);
        var before = (CubicBezierRoadGeometrySegment)bezierSplit.Before;
        var after = (CubicBezierRoadGeometrySegment)bezierSplit.After;
        return new RoadGeometrySplit(FromBezier(before), FromBezier(after));
    }

    private static CubicHermiteRoadGeometrySegment FromBezier(CubicBezierRoadGeometrySegment segment) =>
        new(
            segment.Start,
            3f * (segment.Control1 - segment.Start),
            segment.End,
            3f * (segment.End - segment.Control2));
}
