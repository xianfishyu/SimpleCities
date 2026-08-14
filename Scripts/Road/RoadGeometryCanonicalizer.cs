using Godot;
using System;
using System.Collections.Generic;

internal readonly record struct RoadGeometryCanonicalizationResult(
    IReadOnlyList<RoadGeometrySegment> GeometrySegments,
    bool Changed);

internal static class RoadGeometryCanonicalizer
{
    internal static IReadOnlyList<RoadGeometrySegment> ReanchorChain(
        IReadOnlyList<RoadGeometrySegment> geometrySegments,
        Vector2 start,
        Vector2 end)
    {
        ArgumentNullException.ThrowIfNull(geometrySegments);
        if (geometrySegments.Count == 0)
            throw new ArgumentException("A geometry chain must contain at least one segment.", nameof(geometrySegments));

        var anchored = new RoadGeometrySegment[geometrySegments.Count];
        for (int index = 0; index < anchored.Length; index++)
        {
            RoadGeometrySegment source = geometrySegments[index]
                ?? throw new ArgumentException(
                    "A geometry chain cannot contain null segments.",
                    nameof(geometrySegments));
            Vector2 anchoredStart = index == 0 ? start : source.Start;
            Vector2 anchoredEnd = index == anchored.Length - 1 ? end : source.End;
            anchored[index] = ReanchorSegment(source, anchoredStart, anchoredEnd);
        }

        return Array.AsReadOnly(anchored);
    }

    internal static RoadGeometryCanonicalizationResult Canonicalize(
        IReadOnlyList<RoadGeometrySegment> geometrySegments)
    {
        ArgumentNullException.ThrowIfNull(geometrySegments);
        if (geometrySegments.Count == 0)
            throw new ArgumentException("A geometry chain must contain at least one segment.", nameof(geometrySegments));

        var canonical = new List<RoadGeometrySegment>(geometrySegments.Count);
        bool changed = false;
        for (int index = 0; index < geometrySegments.Count; index++)
        {
            RoadGeometrySegment source = geometrySegments[index]
                ?? throw new ArgumentException("A geometry chain cannot contain null segments.", nameof(geometrySegments));
            RoadGeometrySegment current = CanonicalizeSegment(source, out bool segmentChanged);
            changed |= segmentChanged;

            if (canonical.Count > 0 && !RoadExactPredicates.SameBits(canonical[^1].End, current.Start))
                throw new ArgumentException("Geometry segment endpoints must match bit-for-bit.", nameof(geometrySegments));

            if (canonical.Count > 0 &&
                canonical[^1] is LineRoadGeometrySegment previousLine &&
                current is LineRoadGeometrySegment currentLine &&
                RoadExactPredicates.CanMergeForwardLines(previousLine, currentLine))
            {
                canonical[^1] = new LineRoadGeometrySegment(previousLine.Start, currentLine.End);
                changed = true;
                continue;
            }

            canonical.Add(current);
        }

        return new RoadGeometryCanonicalizationResult(canonical.AsReadOnly(), changed);
    }

    internal static RoadGeometrySegment CanonicalizeSegment(
        RoadGeometrySegment source,
        out bool changed)
    {
        changed = HasNonCanonicalValue(source);
        if (!changed)
            return source;

        Vector2 Point(Vector2 value) => RoadNumericPolicy.Canonicalize(value);
        float Scalar(float value) => RoadNumericPolicy.Canonicalize(value);

        return source switch
        {
            LineRoadGeometrySegment line =>
                new LineRoadGeometrySegment(Point(line.Start), Point(line.End)),
            CubicBezierRoadGeometrySegment cubic =>
                new CubicBezierRoadGeometrySegment(
                    Point(cubic.Start), Point(cubic.Control1), Point(cubic.Control2), Point(cubic.End)),
            CubicHermiteRoadGeometrySegment hermite =>
                new CubicHermiteRoadGeometrySegment(
                    Point(hermite.Start), Point(hermite.StartTangent),
                    Point(hermite.End), Point(hermite.EndTangent)),
            CircularArcRoadGeometrySegment arc =>
                CircularArcRoadGeometrySegment.CreateAnchored(
                    Point(arc.Center), Scalar(arc.Radius),
                    RoadGeometryDirection.NormalizePeriodicAngle(arc.StartAngle),
                    Scalar(arc.SweepAngle),
                    Point(arc.Start), Point(arc.End),
                    RoadGeometryDirection.NormalizePeriodicAngle(arc.EndAngle)),
            ClothoidRoadGeometrySegment clothoid =>
                ClothoidRoadGeometrySegment.CreateAnchored(
                    Point(clothoid.Start),
                    RoadGeometryDirection.NormalizePeriodicAngle(clothoid.StartHeading),
                    Scalar(clothoid.StartCurvature), Scalar(clothoid.EndCurvature),
                    Scalar(clothoid.ArcLength), Point(clothoid.End),
                    RoadGeometryDirection.NormalizePeriodicAngle(clothoid.ReverseStartHeading)),
            RationalQuadraticRoadGeometrySegment rational =>
                new RationalQuadraticRoadGeometrySegment(
                    Point(rational.Start), Scalar(rational.StartWeight),
                    Point(rational.Control), Scalar(rational.ControlWeight),
                    Point(rational.End), Scalar(rational.EndWeight)),
            _ => throw new NotSupportedException($"Unsupported road geometry type: {source.GetType().Name}."),
        };
    }

    private static RoadGeometrySegment ReanchorSegment(
        RoadGeometrySegment source,
        Vector2 start,
        Vector2 end)
    {
        if (RoadExactPredicates.SameBits(source.Start, start) &&
            RoadExactPredicates.SameBits(source.End, end))
        {
            return source;
        }

        Vector2 startDelta = start - source.Start;
        Vector2 endDelta = end - source.End;
        return source switch
        {
            LineRoadGeometrySegment =>
                new LineRoadGeometrySegment(start, end),
            CubicBezierRoadGeometrySegment cubic =>
                new CubicBezierRoadGeometrySegment(
                    start,
                    cubic.Control1 + startDelta,
                    cubic.Control2 + endDelta,
                    end),
            CubicHermiteRoadGeometrySegment hermite =>
                new CubicHermiteRoadGeometrySegment(
                    start,
                    hermite.StartTangent,
                    end,
                    hermite.EndTangent),
            CircularArcRoadGeometrySegment arc =>
                CircularArcRoadGeometrySegment.CreateAnchored(
                    RoadExactPredicates.SameBits(startDelta, endDelta)
                        ? arc.Center + startDelta
                        : arc.Center,
                    arc.Radius,
                    arc.StartAngle,
                    arc.SweepAngle,
                    start,
                    end,
                    arc.EndAngle),
            ClothoidRoadGeometrySegment clothoid =>
                ClothoidRoadGeometrySegment.CreateAnchored(
                    start,
                    clothoid.StartHeading,
                    clothoid.StartCurvature,
                    clothoid.EndCurvature,
                    clothoid.ArcLength,
                    end,
                    clothoid.ReverseStartHeading),
            RationalQuadraticRoadGeometrySegment rational =>
                new RationalQuadraticRoadGeometrySegment(
                    start,
                    rational.StartWeight,
                    rational.Control + (startDelta + endDelta) * 0.5f,
                    rational.ControlWeight,
                    end,
                    rational.EndWeight),
            _ => throw new NotSupportedException(
                $"Unsupported road geometry type: {source.GetType().Name}."),
        };
    }

    private static bool HasNonCanonicalValue(RoadGeometrySegment geometry)
    {
        bool NegativeScalar(float value) =>
            value == 0f && BitConverter.SingleToInt32Bits(value) < 0;
        bool NegativePoint(Vector2 value) =>
            NegativeScalar(value.X) || NegativeScalar(value.Y);

        return geometry switch
        {
            LineRoadGeometrySegment line => NegativePoint(line.Start) || NegativePoint(line.End),
            CubicBezierRoadGeometrySegment cubic =>
                NegativePoint(cubic.Start) || NegativePoint(cubic.Control1) ||
                NegativePoint(cubic.Control2) || NegativePoint(cubic.End),
            CubicHermiteRoadGeometrySegment hermite =>
                NegativePoint(hermite.Start) || NegativePoint(hermite.StartTangent) ||
                NegativePoint(hermite.End) || NegativePoint(hermite.EndTangent),
            CircularArcRoadGeometrySegment arc =>
                NegativePoint(arc.Start) || NegativePoint(arc.End) || NegativePoint(arc.Center) ||
                NegativeScalar(arc.Radius) ||
                NegativeScalar(arc.StartAngle) || NegativeScalar(arc.SweepAngle) ||
                NegativeScalar(arc.EndAngle) ||
                arc.StartAngle != RoadGeometryDirection.NormalizePeriodicAngle(arc.StartAngle) ||
                arc.EndAngle != RoadGeometryDirection.NormalizePeriodicAngle(arc.EndAngle),
            ClothoidRoadGeometrySegment clothoid =>
                NegativePoint(clothoid.Start) || NegativePoint(clothoid.End) ||
                NegativeScalar(clothoid.StartHeading) ||
                NegativeScalar(clothoid.StartCurvature) || NegativeScalar(clothoid.EndCurvature) ||
                NegativeScalar(clothoid.ArcLength) ||
                clothoid.StartHeading != RoadGeometryDirection.NormalizePeriodicAngle(
                    clothoid.StartHeading) ||
                clothoid.ReverseStartHeading != RoadGeometryDirection.NormalizePeriodicAngle(
                    clothoid.ReverseStartHeading),
            RationalQuadraticRoadGeometrySegment rational =>
                NegativePoint(rational.Start) || NegativeScalar(rational.StartWeight) ||
                NegativePoint(rational.Control) || NegativeScalar(rational.ControlWeight) ||
                NegativePoint(rational.End) || NegativeScalar(rational.EndWeight),
            _ => false,
        };
    }
}
