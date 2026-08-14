using Godot;
using System;
using System.Collections.Generic;

internal static class RoadGeometryDirection
{
    internal static IReadOnlyList<RoadGeometrySegment> ReverseChain(
        IReadOnlyList<RoadGeometrySegment> geometrySegments)
    {
        ArgumentNullException.ThrowIfNull(geometrySegments);
        var reversed = new RoadGeometrySegment[geometrySegments.Count];
        for (int index = 0; index < geometrySegments.Count; index++)
        {
            RoadGeometrySegment geometry = geometrySegments[geometrySegments.Count - 1 - index]
                ?? throw new ArgumentException(
                    "A geometry chain cannot contain null segments.",
                    nameof(geometrySegments));
            reversed[index] = geometry.Reverse();
        }
        return Array.AsReadOnly(reversed);
    }

    internal static int CompareCanonicalKeys(
        IReadOnlyList<RoadGeometrySegment> first,
        IReadOnlyList<RoadGeometrySegment> second)
    {
        uint[] firstKey = CreateCanonicalKey(first);
        uint[] secondKey = CreateCanonicalKey(second);
        int count = Math.Min(firstKey.Length, secondKey.Length);
        for (int index = 0; index < count; index++)
        {
            int comparison = firstKey[index].CompareTo(secondKey[index]);
            if (comparison != 0)
                return comparison;
        }
        return firstKey.Length.CompareTo(secondKey.Length);
    }

    internal static float NormalizePeriodicAngle(float angle)
    {
        if (!float.IsFinite(angle))
            throw new ArgumentOutOfRangeException(nameof(angle), angle, "Angle must be finite.");
        if (angle >= 0f && angle < Mathf.Tau)
            return RoadNumericPolicy.Canonicalize(angle);

        float normalized = Mathf.PosMod(angle, Mathf.Tau);
        return normalized == Mathf.Tau
            ? 0f
            : RoadNumericPolicy.Canonicalize(normalized);
    }

    private static uint[] CreateCanonicalKey(IReadOnlyList<RoadGeometrySegment> geometrySegments)
    {
        ArgumentNullException.ThrowIfNull(geometrySegments);
        var tokens = new List<uint>(2 + geometrySegments.Count * 12)
        {
            1u,
            checked((uint)geometrySegments.Count),
        };
        foreach (RoadGeometrySegment geometry in geometrySegments)
        {
            ArgumentNullException.ThrowIfNull(geometry);
            tokens.Add(1u);
            tokens.Add(checked((uint)geometry.Kind));
            AppendGeometryTokens(tokens, geometry);
        }
        return [.. tokens];
    }

    private static void AppendGeometryTokens(List<uint> tokens, RoadGeometrySegment geometry)
    {
        void Scalar(float value) => tokens.Add(ToOrderedFloatKey(value));
        void Angle(float value) => tokens.Add(ToOrderedFloatKey(NormalizePeriodicAngle(value)));
        void Point(Vector2 value)
        {
            Scalar(value.X);
            Scalar(value.Y);
        }

        switch (geometry)
        {
            case LineRoadGeometrySegment line:
                Point(line.Start);
                Point(line.End);
                break;
            case CubicBezierRoadGeometrySegment cubic:
                Point(cubic.Start);
                Point(cubic.Control1);
                Point(cubic.Control2);
                Point(cubic.End);
                break;
            case CubicHermiteRoadGeometrySegment hermite:
                Point(hermite.Start);
                Point(hermite.StartTangent);
                Point(hermite.End);
                Point(hermite.EndTangent);
                break;
            case CircularArcRoadGeometrySegment arc:
                Point(arc.Start);
                Point(arc.End);
                Point(arc.Center);
                Scalar(arc.Radius);
                Angle(arc.StartAngle);
                Angle(arc.EndAngle);
                Scalar(arc.SweepAngle);
                break;
            case ClothoidRoadGeometrySegment clothoid:
                Point(clothoid.Start);
                Point(clothoid.End);
                Angle(clothoid.StartHeading);
                Angle(clothoid.ReverseStartHeading);
                Scalar(clothoid.StartCurvature);
                Scalar(clothoid.EndCurvature);
                Scalar(clothoid.ArcLength);
                break;
            case RationalQuadraticRoadGeometrySegment rational:
                Point(rational.Start);
                Scalar(rational.StartWeight);
                Point(rational.Control);
                Scalar(rational.ControlWeight);
                Point(rational.End);
                Scalar(rational.EndWeight);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported road geometry type: {geometry.GetType().Name}.");
        }
    }

    private static uint ToOrderedFloatKey(float value)
    {
        value = RoadNumericPolicy.Canonicalize(value);
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Key values must be finite.");
        uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
        return (bits & 0x8000_0000u) != 0 ? ~bits : bits ^ 0x8000_0000u;
    }
}
