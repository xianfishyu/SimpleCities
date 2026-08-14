using Godot;
using System;
using System.Collections.Generic;

internal enum RoadNumericError
{
    None,
    NonFinite,
    CoordinateOutOfRange,
    ControlParameterOutOfRange,
    GeometryLengthExceeded,
    EdgeLengthExceeded,
    GraphLengthExceeded,
}

internal static class RoadNumericPolicy
{
    internal const float NodeSnapRadius = 0.5f;
    internal const float IntersectionClusterEpsilon = 1e-3f;
    internal const float MaximumIntersectionClusterDiameter = 1e-3f;

    internal const float MaximumCoordinateMagnitude = 1_000_000f;
    internal const long JunctionPatchQuantizationScale = 1_024;
    internal const float MinimumDisplayRoadWidth = 4f / JunctionPatchQuantizationScale;
    internal const float MaximumDisplayRoadWidth = MaximumCoordinateMagnitude;
    internal const float MaximumVectorComponentMagnitude = 4_000_000f;
    internal const float MaximumRadius = 1_000_000f;
    internal const float MaximumAngleMagnitude = 65_536f;
    internal const float MaximumCurvatureMagnitude = 1_024f;
    internal const float MinimumRationalWeight = 1e-6f;
    internal const float MaximumRationalWeight = 1_000_000f;

    internal const double MaximumGeometryLength = 8_000_000d;
    internal const double MaximumEdgeLength = 64_000_000d;
    internal const double MaximumGraphLength = 1_000_000_000_000d;

    internal static float Canonicalize(float value) => value == 0f ? 0f : value;

    internal static Vector2 Canonicalize(Vector2 value) =>
        new(Canonicalize(value.X), Canonicalize(value.Y));

    internal static bool HasCanonicalZero(float value) =>
        value != 0f || BitConverter.SingleToInt32Bits(value) == 0;

    internal static bool IsWithinCoordinateRange(Vector2 value) =>
        IsFiniteAndWithin(value.X, MaximumCoordinateMagnitude) &&
        IsFiniteAndWithin(value.Y, MaximumCoordinateMagnitude);

    internal static double DistanceSquared(Vector2 first, Vector2 second)
    {
        double dx = (double)first.X - second.X;
        double dy = (double)first.Y - second.Y;
        return dx * dx + dy * dy;
    }

    internal static RoadNumericError ValidateGeometry(RoadGeometrySegment geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        RoadNumericError parameterError = geometry switch
        {
            LineRoadGeometrySegment line => ValidatePoints(line.Start, line.End),
            CubicBezierRoadGeometrySegment cubic => ValidatePoints(
                cubic.Start, cubic.Control1, cubic.Control2, cubic.End),
            CubicHermiteRoadGeometrySegment hermite => FirstError(
                ValidatePoints(hermite.Start, hermite.End),
                ValidateVectors(hermite.StartTangent, hermite.EndTangent)),
            CircularArcRoadGeometrySegment arc => FirstError(
                ValidateArc(arc),
                ValidatePoints(arc.Start, arc.End, arc.Center)),
            ClothoidRoadGeometrySegment clothoid => FirstError(
                ValidatePoints(clothoid.Start, clothoid.End),
                ValidateClothoid(clothoid)),
            RationalQuadraticRoadGeometrySegment rational => FirstError(
                ValidatePoints(rational.Start, rational.Control, rational.End),
                ValidateWeights(rational.StartWeight, rational.ControlWeight, rational.EndWeight)),
            _ => RoadNumericError.ControlParameterOutOfRange,
        };
        if (parameterError != RoadNumericError.None)
            return parameterError;

        if (!IsWithinCoordinateRange(geometry.Bounds.Position) ||
            !IsWithinCoordinateRange(geometry.Bounds.End))
        {
            return RoadNumericError.CoordinateOutOfRange;
        }

        double length = geometry.Length;
        if (!double.IsFinite(length))
            return RoadNumericError.NonFinite;
        return length <= MaximumGeometryLength
            ? RoadNumericError.None
            : RoadNumericError.GeometryLengthExceeded;
    }

    internal static RoadNumericError ValidateGeometryChain(
        IReadOnlyList<RoadGeometrySegment> geometrySegments,
        double currentGraphLength,
        out double edgeLength,
        out double projectedGraphLength)
    {
        ArgumentNullException.ThrowIfNull(geometrySegments);
        edgeLength = 0d;
        projectedGraphLength = currentGraphLength;
        if (!double.IsFinite(currentGraphLength) || currentGraphLength < 0d)
            return RoadNumericError.NonFinite;

        foreach (RoadGeometrySegment geometry in geometrySegments)
        {
            RoadNumericError geometryError = ValidateGeometry(geometry);
            if (geometryError != RoadNumericError.None)
                return geometryError;

            edgeLength += geometry.Length;
            if (!double.IsFinite(edgeLength))
                return RoadNumericError.NonFinite;
            if (edgeLength > MaximumEdgeLength)
                return RoadNumericError.EdgeLengthExceeded;
        }

        projectedGraphLength += edgeLength;
        if (!double.IsFinite(projectedGraphLength))
            return RoadNumericError.NonFinite;
        return projectedGraphLength <= MaximumGraphLength
            ? RoadNumericError.None
            : RoadNumericError.GraphLengthExceeded;
    }

    private static RoadNumericError ValidatePoints(params Vector2[] points)
    {
        foreach (Vector2 point in points)
        {
            if (!point.IsFinite())
                return RoadNumericError.NonFinite;
            if (!IsWithinCoordinateRange(point))
                return RoadNumericError.CoordinateOutOfRange;
        }

        return RoadNumericError.None;
    }

    private static RoadNumericError ValidateVectors(params Vector2[] vectors)
    {
        foreach (Vector2 vector in vectors)
        {
            if (!vector.IsFinite())
                return RoadNumericError.NonFinite;
            if (!IsFiniteAndWithin(vector.X, MaximumVectorComponentMagnitude) ||
                !IsFiniteAndWithin(vector.Y, MaximumVectorComponentMagnitude))
            {
                return RoadNumericError.ControlParameterOutOfRange;
            }
        }

        return RoadNumericError.None;
    }

    private static RoadNumericError ValidateArc(CircularArcRoadGeometrySegment arc)
    {
        if (!float.IsFinite(arc.Radius) || !float.IsFinite(arc.StartAngle) ||
            !float.IsFinite(arc.EndAngle) || !float.IsFinite(arc.SweepAngle))
        {
            return RoadNumericError.NonFinite;
        }

        return arc.Radius <= MaximumRadius &&
               MathF.Abs(arc.StartAngle) <= MaximumAngleMagnitude &&
               MathF.Abs(arc.EndAngle) <= MaximumAngleMagnitude
            ? RoadNumericError.None
            : RoadNumericError.ControlParameterOutOfRange;
    }

    private static RoadNumericError ValidateClothoid(ClothoidRoadGeometrySegment clothoid)
    {
        if (!float.IsFinite(clothoid.StartHeading) ||
            !float.IsFinite(clothoid.ReverseStartHeading) ||
            !float.IsFinite(clothoid.StartCurvature) ||
            !float.IsFinite(clothoid.EndCurvature) ||
            !float.IsFinite(clothoid.ArcLength))
        {
            return RoadNumericError.NonFinite;
        }

        return MathF.Abs(clothoid.StartHeading) <= MaximumAngleMagnitude &&
               MathF.Abs(clothoid.ReverseStartHeading) <= MaximumAngleMagnitude &&
               MathF.Abs(clothoid.StartCurvature) <= MaximumCurvatureMagnitude &&
               MathF.Abs(clothoid.EndCurvature) <= MaximumCurvatureMagnitude
            ? RoadNumericError.None
            : RoadNumericError.ControlParameterOutOfRange;
    }

    private static RoadNumericError ValidateWeights(params float[] weights)
    {
        foreach (float weight in weights)
        {
            if (!float.IsFinite(weight))
                return RoadNumericError.NonFinite;
            if (weight < MinimumRationalWeight || weight > MaximumRationalWeight)
                return RoadNumericError.ControlParameterOutOfRange;
        }

        return RoadNumericError.None;
    }

    private static RoadNumericError FirstError(
        RoadNumericError first,
        RoadNumericError second) =>
        first != RoadNumericError.None ? first : second;

    private static bool IsFiniteAndWithin(float value, float magnitude) =>
        float.IsFinite(value) && MathF.Abs(value) <= magnitude;
}
