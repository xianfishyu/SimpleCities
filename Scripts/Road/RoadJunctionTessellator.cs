using Clipper2Lib;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

internal readonly record struct RoadJunctionIncidence(
    int NodeID,
    int EdgeID,
    EdgeEndpoint Endpoint,
    Vector2 OutwardDirection,
    RoadTypeStyleDefinition Style,
    RoadLocation Location)
{
    internal float HalfWidth => Style.Width * 0.5f;
}

internal readonly record struct RoadJunctionTriangle(
    RoadJunctionIncidence Incidence,
    int SectorOrder,
    float CenterlineLength,
    Vector2 NodePosition,
    Vector2 A,
    Vector2 B,
    Vector2 C);

internal static class RoadJunctionTessellator
{
    internal const long QuantizationScale = RoadNumericPolicy.JunctionPatchQuantizationScale;
    internal const float MaximumQuantizationError = 0.5f / QuantizationScale;
    internal const float MiterLimit = 4f;

    internal static RoadJunctionTriangle[] Tessellate(
        Vector2 nodePosition,
        IReadOnlyCollection<RoadJunctionIncidence> sourceIncidences)
    {
        if (!nodePosition.IsFinite())
            throw new ArgumentException("A road junction requires a finite Node position.", nameof(nodePosition));
        ArgumentNullException.ThrowIfNull(sourceIncidences);
        if (sourceIncidences.Count < 3)
        {
            throw new ArgumentException(
                "A road junction patch requires at least three endpoint incidences.",
                nameof(sourceIncidences));
        }

        DirectionGroup[] groups = CreateDirectionGroups(sourceIncidences);
        if (groups.Length == 1)
            return TessellateSingleDirection(nodePosition, groups[0]);

        // Split each adjacent gap once so neighboring owner sectors reuse the same boundary.
        GapBoundary[] gaps = new GapBoundary[groups.Length];
        for (int index = 0; index < groups.Length; index++)
        {
            gaps[index] = CreateGapBoundary(
                nodePosition,
                groups[index],
                groups[(index + 1) % groups.Length]);
        }

        var result = new List<RoadJunctionTriangle>();
        for (int index = 0; index < groups.Length; index++)
        {
            DirectionGroup group = groups[index];
            GapBoundary previous = gaps[(index + gaps.Length - 1) % gaps.Length];
            GapBoundary next = gaps[index];
            var sector = new List<Vector2>(
                1 + previous.SecondOwnerBoundary.Length + next.FirstOwnerBoundary.Length)
            {
                nodePosition,
            };
            sector.AddRange(previous.SecondOwnerBoundary);
            sector.AddRange(next.FirstOwnerBoundary);
            AppendTriangulatedSector(
                result,
                group,
                index,
                sector,
                nodePosition);
        }

        return result.ToArray();
    }

    private static DirectionGroup[] CreateDirectionGroups(
        IReadOnlyCollection<RoadJunctionIncidence> sourceIncidences)
    {
        RoadJunctionIncidence[] incidences = sourceIncidences
            .Select(ValidateAndNormalize)
            .OrderBy(incidence => incidence, DirectionComparer.Instance)
            .ToArray();
        int nodeID = incidences[0].NodeID;
        if (incidences.Any(incidence => incidence.NodeID != nodeID))
            throw new ArgumentException("Road junction incidences must belong to one Node.");

        var groups = new List<DirectionGroup>(incidences.Length);
        foreach (RoadJunctionIncidence incidence in incidences)
        {
            if (groups.Count == 0 ||
                !HaveSameDirection(groups[^1].Direction, incidence.OutwardDirection))
            {
                groups.Add(new DirectionGroup(
                    incidence,
                    incidence.OutwardDirection,
                    incidence.HalfWidth));
                continue;
            }

            groups[^1].HalfWidth = MathF.Max(groups[^1].HalfWidth, incidence.HalfWidth);
        }
        return groups.ToArray();
    }

    private static RoadJunctionIncidence ValidateAndNormalize(
        RoadJunctionIncidence incidence)
    {
        if (incidence.NodeID < 0)
            throw new ArgumentOutOfRangeException(nameof(incidence), "A junction Node ID cannot be negative.");
        if (incidence.EdgeID < 0)
            throw new ArgumentOutOfRangeException(nameof(incidence), "A junction Edge ID cannot be negative.");
        if (!Enum.IsDefined(incidence.Endpoint))
            throw new ArgumentOutOfRangeException(nameof(incidence), "A junction endpoint role is invalid.");
        if (!incidence.OutwardDirection.IsFinite() || incidence.OutwardDirection.IsZeroApprox())
            throw new ArgumentException("A junction incidence direction must be finite and non-zero.");
        if (!RoadTypeStyle.TryValidate(incidence.Style, out string styleError))
            throw new ArgumentException($"A junction incidence style is invalid: {styleError}");
        if (incidence.Location.EdgeID != incidence.EdgeID ||
            incidence.Location.GeometryIndex < 0 ||
            !float.IsFinite(incidence.Location.Parameter))
        {
            throw new ArgumentException(
                "A junction incidence location must be a finite location on its owner Edge.");
        }
        float endpointParameter = incidence.Endpoint == EdgeEndpoint.A
            ? RoadGeometrySegment.ParameterStart
            : RoadGeometrySegment.ParameterEnd;
        if (incidence.Location.Parameter != endpointParameter ||
            (incidence.Endpoint == EdgeEndpoint.A && incidence.Location.GeometryIndex != 0))
        {
            throw new ArgumentException(
                "A junction incidence location must identify its canonical endpoint.");
        }

        return incidence with
        {
            OutwardDirection = incidence.OutwardDirection.Normalized(),
        };
    }

    private static RoadJunctionTriangle[] TessellateSingleDirection(
        Vector2 nodePosition,
        DirectionGroup group)
    {
        Vector2 normal = LeftNormal(group.Direction);
        Vector2 left = nodePosition + normal * group.HalfWidth;
        Vector2 tip = nodePosition - group.Direction * group.HalfWidth;
        Vector2 right = nodePosition - normal * group.HalfWidth;
        var result = new List<RoadJunctionTriangle>(2);
        AppendTriangulatedSector(
            result,
            group,
            sectorOrder: 0,
            [left, tip, right],
            nodePosition);
        return result.ToArray();
    }

    private static GapBoundary CreateGapBoundary(
        Vector2 nodePosition,
        DirectionGroup first,
        DirectionGroup second)
    {
        Vector2 firstLeft = nodePosition + LeftNormal(first.Direction) * first.HalfWidth;
        Vector2 secondRight = nodePosition - LeftNormal(second.Direction) * second.HalfWidth;
        int orientation = RoadExactPredicates.Orient2DSign(
            Vector2.Zero,
            first.Direction,
            second.Direction);
        if (orientation > 0 &&
            TryCreateBoundedMiter(
                nodePosition,
                firstLeft,
                first.Direction,
                secondRight,
                second.Direction,
                MathF.Max(first.HalfWidth, second.HalfWidth),
                out Vector2 miter))
        {
            return SplitBoundary([miter]);
        }

        if (orientation == 0 &&
            RoadExactPredicates.DotSign(first.Direction, second.Direction) < 0)
        {
            Vector2 farther = firstLeft.DistanceSquaredTo(nodePosition) >=
                              secondRight.DistanceSquaredTo(nodePosition)
                ? firstLeft
                : secondRight;
            return SplitBoundary([farther]);
        }

        if (orientation < 0)
        {
            Vector2 firstOffset = firstLeft - nodePosition;
            Vector2 secondOffset = secondRight - nodePosition;
            Vector2 fallbackDirection = firstOffset + secondOffset;
            if (fallbackDirection.LengthSquared() == 0f)
                fallbackDirection = -(first.Direction + second.Direction);
            if (!fallbackDirection.IsFinite() || fallbackDirection.LengthSquared() == 0f)
                fallbackDirection = -first.Direction;
            Vector2 tip = nodePosition +
                          fallbackDirection.Normalized() *
                          MathF.Max(first.HalfWidth, second.HalfWidth);
            return SplitBoundary([firstLeft, tip, secondRight]);
        }

        return SplitBoundary([firstLeft, secondRight]);
    }

    private static bool TryCreateBoundedMiter(
        Vector2 nodePosition,
        Vector2 firstOrigin,
        Vector2 firstDirection,
        Vector2 secondOrigin,
        Vector2 secondDirection,
        float maximumHalfWidth,
        out Vector2 intersection)
    {
        intersection = Vector2.Zero;
        double denominator = Cross(firstDirection, secondDirection);
        if (denominator == 0d)
            return false;

        Vector2 delta = secondOrigin - firstOrigin;
        double firstParameter = Cross(delta, secondDirection) / denominator;
        double secondParameter = Cross(delta, firstDirection) / denominator;
        if (!double.IsFinite(firstParameter) ||
            !double.IsFinite(secondParameter) ||
            firstParameter < 0d ||
            secondParameter < 0d)
        {
            return false;
        }

        double x = firstOrigin.X + (double)firstDirection.X * firstParameter;
        double y = firstOrigin.Y + (double)firstDirection.Y * firstParameter;
        if (!double.IsFinite(x) || !double.IsFinite(y) ||
            x < float.MinValue || x > float.MaxValue ||
            y < float.MinValue || y > float.MaxValue)
        {
            return false;
        }

        intersection = new Vector2((float)x, (float)y);
        double maximumMiter = maximumHalfWidth * MiterLimit;
        return intersection.IsFinite() &&
               RoadNumericPolicy.DistanceSquared(intersection, nodePosition) <=
               maximumMiter * maximumMiter;
    }

    private static GapBoundary SplitBoundary(Vector2[] points)
    {
        if (points.Length == 0)
            throw new ArgumentException("A junction gap boundary cannot be empty.", nameof(points));
        if (points.Length == 1)
            return new GapBoundary([points[0]], [points[0]]);

        double totalLength = 0d;
        for (int index = 1; index < points.Length; index++)
            totalLength += points[index - 1].DistanceTo(points[index]);
        if (!double.IsFinite(totalLength) || totalLength <= 0d)
            return new GapBoundary([points[0]], [points[^1]]);

        double splitDistance = totalLength * 0.5d;
        double consumed = 0d;
        for (int index = 1; index < points.Length; index++)
        {
            Vector2 start = points[index - 1];
            Vector2 end = points[index];
            double segmentLength = start.DistanceTo(end);
            if (consumed + segmentLength < splitDistance)
            {
                consumed += segmentLength;
                continue;
            }

            float parameter = segmentLength == 0d
                ? 0f
                : (float)((splitDistance - consumed) / segmentLength);
            Vector2 split = start.Lerp(end, parameter);
            var firstOwner = new List<Vector2>(index + 1);
            for (int pointIndex = 0; pointIndex < index; pointIndex++)
                firstOwner.Add(points[pointIndex]);
            firstOwner.Add(split);

            var secondOwner = new List<Vector2>(points.Length - index + 1)
            {
                split,
            };
            for (int pointIndex = index; pointIndex < points.Length; pointIndex++)
                secondOwner.Add(points[pointIndex]);
            return new GapBoundary(firstOwner.ToArray(), secondOwner.ToArray());
        }

        return new GapBoundary(points, [points[^1]]);
    }

    private static void AppendTriangulatedSector(
        List<RoadJunctionTriangle> destination,
        DirectionGroup group,
        int sectorOrder,
        IReadOnlyList<Vector2> sectorPoints,
        Vector2 nodePosition)
    {
        Path64 path = QuantizePath(sectorPoints);
        if (path.Count < 3)
            return;
        Vector2 quantizedNodePosition = QuantizePoint(nodePosition);

        // Reflex sectors can self-touch; integer union canonicalizes them before triangulation.
        Paths64 polygons = Clipper.Union(new Paths64 { path }, FillRule.NonZero);
        if (polygons.Count == 0)
            return;
        TriangulateResult triangulateResult = Clipper.Triangulate(
            polygons,
            out Paths64 quantizedTriangles,
            useDelaunay: false);
        if (triangulateResult == TriangulateResult.noPolygons)
            return;
        if (triangulateResult != TriangulateResult.success)
        {
            throw new InvalidOperationException(
                $"Clipper2 could not triangulate a road junction sector: {triangulateResult}.");
        }

        RoadJunctionTriangle[] triangles = quantizedTriangles
            .Select(path => CreateTriangle(
                path,
                group,
                sectorOrder,
                quantizedNodePosition))
            .Where(triangle => triangle.HasValue)
            .Select(triangle => triangle!.Value)
            .OrderBy(triangle => triangle, TriangleComparer.Instance)
            .ToArray();
        destination.AddRange(triangles);
    }

    private static Path64 QuantizePath(IReadOnlyList<Vector2> points)
    {
        var path = new Path64(points.Count);
        foreach (Vector2 point in points)
        {
            if (!point.IsFinite())
                throw new InvalidOperationException("A junction sector contains a non-finite point.");
            var quantized = new Point64(
                QuantizeCoordinate(point.X),
                QuantizeCoordinate(point.Y));
            if (path.Count == 0 || path[^1] != quantized)
                path.Add(quantized);
        }
        if (path.Count > 1 && path[0] == path[^1])
            path.RemoveAt(path.Count - 1);
        return path;
    }

    private static long QuantizeCoordinate(float value)
    {
        double scaled = (double)value * QuantizationScale;
        if (!double.IsFinite(scaled) || scaled < long.MinValue || scaled > long.MaxValue)
            throw new InvalidOperationException("A junction coordinate exceeds the fixed quantization range.");
        return checked((long)Math.Round(scaled, MidpointRounding.ToEven));
    }

    private static Vector2 QuantizePoint(Vector2 point)
    {
        if (!point.IsFinite())
            throw new InvalidOperationException("A junction point must be finite before quantization.");
        return Dequantize(new Point64(
            QuantizeCoordinate(point.X),
            QuantizeCoordinate(point.Y)));
    }

    private static RoadJunctionTriangle? CreateTriangle(
        Path64 path,
        DirectionGroup group,
        int sectorOrder,
        Vector2 nodePosition)
    {
        if (path.Count != 3)
        {
            throw new InvalidOperationException(
                $"Clipper2 returned a junction triangle with {path.Count} vertices.");
        }

        Vector2 a = Dequantize(path[0]);
        Vector2 b = Dequantize(path[1]);
        Vector2 c = Dequantize(path[2]);
        double orientation = Cross(b - a, c - a);
        if (orientation == 0d)
            return null;
        if (orientation < 0d)
            (b, c) = (c, b);

        if (ComparePoint(b, a) < 0 && ComparePoint(b, c) <= 0)
            (a, b, c) = (b, c, a);
        else if (ComparePoint(c, a) < 0 && ComparePoint(c, b) < 0)
            (a, b, c) = (c, a, b);

        return new RoadJunctionTriangle(
            group.Owner,
            sectorOrder,
            group.HalfWidth,
            nodePosition,
            a,
            b,
            c);
    }

    private static Vector2 Dequantize(Point64 point) =>
        RoadNumericPolicy.Canonicalize(new Vector2(
            (float)((double)point.X / QuantizationScale),
            (float)((double)point.Y / QuantizationScale)));

    private static bool HaveSameDirection(Vector2 first, Vector2 second) =>
        RoadExactPredicates.Orient2DSign(Vector2.Zero, first, second) == 0 &&
        RoadExactPredicates.DotSign(first, second) > 0;

    private static Vector2 LeftNormal(Vector2 direction) =>
        new(-direction.Y, direction.X);

    private static double Cross(Vector2 first, Vector2 second) =>
        (double)first.X * second.Y - (double)first.Y * second.X;

    private static int DirectionHalf(Vector2 direction) =>
        direction.Y > 0f || (direction.Y == 0f && direction.X >= 0f) ? 0 : 1;

    private static int ComparePoint(Vector2 first, Vector2 second)
    {
        int comparison = first.X.CompareTo(second.X);
        return comparison != 0 ? comparison : first.Y.CompareTo(second.Y);
    }

    private sealed class DirectionGroup(
        RoadJunctionIncidence owner,
        Vector2 direction,
        float halfWidth)
    {
        internal RoadJunctionIncidence Owner { get; } = owner;
        internal Vector2 Direction { get; } = direction;
        internal float HalfWidth { get; set; } = halfWidth;
    }

    private sealed record GapBoundary(
        Vector2[] FirstOwnerBoundary,
        Vector2[] SecondOwnerBoundary);

    private sealed class DirectionComparer : IComparer<RoadJunctionIncidence>
    {
        internal static DirectionComparer Instance { get; } = new();

        public int Compare(RoadJunctionIncidence first, RoadJunctionIncidence second)
        {
            int comparison = DirectionHalf(first.OutwardDirection)
                .CompareTo(DirectionHalf(second.OutwardDirection));
            if (comparison != 0)
                return comparison;

            int orientation = RoadExactPredicates.Orient2DSign(
                Vector2.Zero,
                first.OutwardDirection,
                second.OutwardDirection);
            if (orientation != 0)
                return orientation > 0 ? -1 : 1;

            comparison = second.Style.RoadType.CompareTo(first.Style.RoadType);
            if (comparison != 0)
                return comparison;
            comparison = first.EdgeID.CompareTo(second.EdgeID);
            if (comparison != 0)
                return comparison;
            return first.Endpoint.CompareTo(second.Endpoint);
        }
    }

    private sealed class TriangleComparer : IComparer<RoadJunctionTriangle>
    {
        internal static TriangleComparer Instance { get; } = new();

        public int Compare(RoadJunctionTriangle first, RoadJunctionTriangle second)
        {
            int comparison = ComparePoint(first.A, second.A);
            if (comparison != 0)
                return comparison;
            comparison = ComparePoint(first.B, second.B);
            return comparison != 0 ? comparison : ComparePoint(first.C, second.C);
        }
    }
}
