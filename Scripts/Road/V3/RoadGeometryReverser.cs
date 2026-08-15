using Godot;
using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// 六类原生几何的权威反向契约。反向必须保留原生类型、轨迹、长度和参数化语义，
/// 绝不通过显示采样点重建几何。
/// </summary>
public static class RoadGeometryReverser
{
    public static RoadGeometrySegment Reverse(RoadGeometrySegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);

        return segment switch
        {
            LineRoadGeometrySegment line => ReverseLine(line),
            CubicBezierRoadGeometrySegment bezier => ReverseBezier(bezier),
            CubicHermiteRoadGeometrySegment hermite => ReverseHermite(hermite),
            CircularArcRoadGeometrySegment arc => ReverseArc(arc),
            ClothoidRoadGeometrySegment clothoid => ReverseClothoid(clothoid),
            RationalQuadraticRoadGeometrySegment rational => ReverseRational(rational),
            _ => throw new NotSupportedException($"Unsupported geometry kind: {segment.Kind}."),
        };
    }

    private static LineRoadGeometrySegment ReverseLine(LineRoadGeometrySegment segment) =>
        new(segment.End, segment.Start);

    private static CubicBezierRoadGeometrySegment ReverseBezier(CubicBezierRoadGeometrySegment segment) =>
        new(segment.End, segment.Control2, segment.Control1, segment.Start);

    private static CubicHermiteRoadGeometrySegment ReverseHermite(CubicHermiteRoadGeometrySegment segment) =>
        new(segment.End, -segment.EndTangent, segment.Start, -segment.StartTangent);

    private static CircularArcRoadGeometrySegment ReverseArc(CircularArcRoadGeometrySegment segment)
    {
        float startAngle = RoadNumericPolicy.NormalizeStartAngle(segment.StartAngle + segment.SweepAngle);
        return new CircularArcRoadGeometrySegment(
            segment.Center,
            segment.Radius,
            startAngle,
            -segment.SweepAngle);
    }

    private static ClothoidRoadGeometrySegment ReverseClothoid(ClothoidRoadGeometrySegment segment)
    {
        float endHeading = segment.StartHeading +
            0.5f * (segment.StartCurvature + segment.EndCurvature) * segment.ArcLength;
        float reversedStartHeading = RoadNumericPolicy.NormalizeStartAngle(endHeading + Mathf.Pi);
        return new ClothoidRoadGeometrySegment(
            segment.End,
            reversedStartHeading,
            -segment.EndCurvature,
            -segment.StartCurvature,
            segment.ArcLength);
    }

    private static RationalQuadraticRoadGeometrySegment ReverseRational(
        RationalQuadraticRoadGeometrySegment segment) =>
        new(
            segment.End,
            segment.EndWeight,
            segment.Control,
            segment.ControlWeight,
            segment.Start,
            segment.StartWeight);
}
