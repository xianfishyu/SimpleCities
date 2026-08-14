using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

internal enum RoadSurfaceOwnerKind
{
    EdgeRibbon,
    TerminalCap,
    SemanticJoin,
    JunctionPatch,
}

internal readonly record struct RoadSurfaceOwner
{
    internal RoadSurfaceOwnerKind Kind { get; }
    internal int EdgeID { get; }
    internal int? NodeID { get; }
    internal EdgeEndpoint? Endpoint { get; }
    internal int SectorOrder { get; }

    private RoadSurfaceOwner(
        RoadSurfaceOwnerKind kind,
        int edgeID,
        int? nodeID,
        EdgeEndpoint? endpoint,
        int sectorOrder)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (edgeID < 0)
            throw new ArgumentOutOfRangeException(nameof(edgeID));
        if (nodeID is < 0)
            throw new ArgumentOutOfRangeException(nameof(nodeID));
        if (endpoint.HasValue && !Enum.IsDefined(endpoint.Value))
            throw new ArgumentOutOfRangeException(nameof(endpoint));
        if (sectorOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(sectorOrder));
        if (kind == RoadSurfaceOwnerKind.EdgeRibbon &&
            (nodeID.HasValue || endpoint.HasValue || sectorOrder != 0))
        {
            throw new ArgumentException(
                "An Edge ribbon owner cannot carry a Node incidence or sector order.");
        }
        if (kind != RoadSurfaceOwnerKind.EdgeRibbon &&
            (!nodeID.HasValue || !endpoint.HasValue))
        {
            throw new ArgumentException(
                "A cap, join, or junction owner must identify its Node incidence.");
        }

        Kind = kind;
        EdgeID = edgeID;
        NodeID = nodeID;
        Endpoint = endpoint;
        SectorOrder = sectorOrder;
    }

    internal static RoadSurfaceOwner EdgeRibbon(int edgeID) => new(
        RoadSurfaceOwnerKind.EdgeRibbon,
        edgeID,
        nodeID: null,
        endpoint: null,
        sectorOrder: 0);

    internal static RoadSurfaceOwner TerminalCap(
        int edgeID,
        int nodeID,
        EdgeEndpoint endpoint) => new(
            RoadSurfaceOwnerKind.TerminalCap,
            edgeID,
            nodeID,
            endpoint,
            sectorOrder: 0);

    internal static RoadSurfaceOwner SemanticJoin(
        int edgeID,
        int nodeID,
        EdgeEndpoint endpoint,
        int sectorOrder) => new(
            RoadSurfaceOwnerKind.SemanticJoin,
            edgeID,
            nodeID,
            endpoint,
            sectorOrder);

    internal static RoadSurfaceOwner JunctionPatch(
        int edgeID,
        int nodeID,
        EdgeEndpoint endpoint,
        int sectorOrder) => new(
            RoadSurfaceOwnerKind.JunctionPatch,
            edgeID,
            nodeID,
            endpoint,
            sectorOrder);
}

internal readonly record struct RoadSurfaceTriangle
{
    internal RoadSurfaceOwner Owner { get; }
    internal Vector2 A { get; }
    internal Vector2 B { get; }
    internal Vector2 C { get; }
    internal Vector2 CenterlineStart { get; }
    internal Vector2 CenterlineEnd { get; }
    internal RoadLocation? LocationStart { get; }
    internal RoadLocation? LocationEnd { get; }

    internal RoadSurfaceTriangle(
        RoadSurfaceOwner owner,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 centerlineStart,
        Vector2 centerlineEnd,
        RoadLocation? locationStart,
        RoadLocation? locationEnd)
    {
        ValidatePoint(a, nameof(a));
        ValidatePoint(b, nameof(b));
        ValidatePoint(c, nameof(c));
        ValidatePoint(centerlineStart, nameof(centerlineStart));
        ValidatePoint(centerlineEnd, nameof(centerlineEnd));
        if (Cross(b - a, c - a) == 0d)
            throw new ArgumentException("A road surface triangle cannot be degenerate.");
        if (centerlineStart == centerlineEnd)
            throw new ArgumentException("A road surface triangle must have a non-degenerate centerline.");
        if (locationStart.HasValue != locationEnd.HasValue)
        {
            throw new ArgumentException(
                "Road surface location endpoints must either both be present or both be absent.");
        }
        if (locationStart is RoadLocation start && locationEnd is RoadLocation end)
        {
            ValidateLocation(owner, start, nameof(locationStart));
            ValidateLocation(owner, end, nameof(locationEnd));
            if (start.GeometryIndex != end.GeometryIndex)
            {
                throw new ArgumentException(
                    "A road surface triangle cannot interpolate across geometry identities.");
            }
        }

        Owner = owner;
        A = a;
        B = b;
        C = c;
        CenterlineStart = centerlineStart;
        CenterlineEnd = centerlineEnd;
        LocationStart = locationStart;
        LocationEnd = locationEnd;
    }

    internal RoadLocation? InterpolateLocation(float parameter)
    {
        if (LocationStart is not RoadLocation start ||
            LocationEnd is not RoadLocation end)
        {
            return null;
        }

        return new RoadLocation(
            start.EdgeID,
            start.GeometryIndex,
            Mathf.Lerp(start.Parameter, end.Parameter, parameter));
    }

    private static void ValidateLocation(
        RoadSurfaceOwner owner,
        RoadLocation location,
        string parameterName)
    {
        if (location.EdgeID != owner.EdgeID ||
            location.GeometryIndex < 0 ||
            !float.IsFinite(location.Parameter) ||
            location.Parameter < RoadGeometrySegment.ParameterStart ||
            location.Parameter > RoadGeometrySegment.ParameterEnd)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidatePoint(Vector2 point, string parameterName)
    {
        if (!point.IsFinite())
            throw new ArgumentException("Road surface coordinates must be finite.", parameterName);
    }

    private static double Cross(Vector2 first, Vector2 second) =>
        (double)first.X * second.Y - (double)first.Y * second.X;
}

internal readonly record struct RoadSurfaceHit(
    RoadRenderToken RenderToken,
    RoadSurfaceOwnerKind OwnerKind,
    int? NodeID,
    int? EdgeID,
    EdgeEndpoint? Endpoint,
    float SurfaceDistance,
    float CenterlineDistance,
    RoadLocation? Location);

internal sealed class RoadSurfaceSnapshot
{
    private readonly RoadSurfaceTriangle[] _primitives;

    internal RoadSurfaceSnapshot(
        RoadRenderToken renderToken,
        IReadOnlyCollection<RoadSurfaceTriangle> primitives)
    {
        ArgumentNullException.ThrowIfNull(primitives);
        RenderToken = renderToken;
        _primitives = primitives.ToArray();
    }

    internal RoadRenderToken RenderToken { get; }
    internal int PrimitiveCount => _primitives.Length;

    internal RoadSurfaceTriangle GetPrimitive(int index) => _primitives[index];

    internal RoadSurfaceHit? FindClosest(
        Vector2 position,
        float maxSurfaceDistance)
    {
        if (!position.IsFinite())
            throw new ArgumentException("Road surface query position must be finite.", nameof(position));
        if (!float.IsFinite(maxSurfaceDistance) || maxSurfaceDistance < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSurfaceDistance),
                "Road surface query distance must be finite and non-negative.");
        }

        double maximumDistanceSquared = (double)maxSurfaceDistance * maxSurfaceDistance;
        SurfaceCandidate? best = null;
        for (int index = 0; index < _primitives.Length; index++)
        {
            RoadSurfaceTriangle triangle = _primitives[index];
            double surfaceDistanceSquared = DistanceSquaredToTriangle(position, triangle);
            if (surfaceDistanceSquared > maximumDistanceSquared)
                continue;

            (double centerlineDistanceSquared, float parameter) =
                DistanceSquaredToSegment(
                    position,
                    triangle.CenterlineStart,
                    triangle.CenterlineEnd);
            var candidate = new SurfaceCandidate(
                triangle,
                index,
                surfaceDistanceSquared,
                centerlineDistanceSquared,
                parameter);
            if (best is null || Compare(candidate, best.Value) < 0)
                best = candidate;
        }

        if (best is not SurfaceCandidate selected)
            return null;

        RoadSurfaceOwner owner = selected.Triangle.Owner;
        return new RoadSurfaceHit(
            RenderToken,
            owner.Kind,
            owner.NodeID,
            owner.EdgeID,
            owner.Endpoint,
            (float)Math.Sqrt(selected.SurfaceDistanceSquared),
            (float)Math.Sqrt(selected.CenterlineDistanceSquared),
            selected.Triangle.InterpolateLocation(selected.CenterlineParameter));
    }

    internal int[] FindEdgeIDsIntersecting(Rect2 bounds)
    {
        if (!bounds.Position.IsFinite() || !bounds.Size.IsFinite())
            throw new ArgumentException("Road surface query bounds must be finite.", nameof(bounds));
        if (bounds.Size.X < 0f || bounds.Size.Y < 0f)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Road surface query size cannot be negative.");

        var edgeIDs = new HashSet<int>();
        foreach (RoadSurfaceTriangle triangle in _primitives)
        {
            if (TriangleIntersectsRect(triangle, bounds))
                edgeIDs.Add(triangle.Owner.EdgeID);
        }
        return edgeIDs.Order().ToArray();
    }

    private static int Compare(SurfaceCandidate left, SurfaceCandidate right)
    {
        int comparison = left.SurfaceDistanceSquared.CompareTo(right.SurfaceDistanceSquared);
        if (comparison != 0)
            return comparison;
        comparison = left.CenterlineDistanceSquared.CompareTo(right.CenterlineDistanceSquared);
        if (comparison != 0)
            return comparison;
        comparison = left.Triangle.Owner.SectorOrder.CompareTo(right.Triangle.Owner.SectorOrder);
        if (comparison != 0)
            return comparison;
        comparison = left.Triangle.Owner.EdgeID.CompareTo(right.Triangle.Owner.EdgeID);
        if (comparison != 0)
            return comparison;
        comparison = left.Triangle.Owner.Kind.CompareTo(right.Triangle.Owner.Kind);
        if (comparison != 0)
            return comparison;
        comparison = Nullable.Compare(left.Triangle.Owner.NodeID, right.Triangle.Owner.NodeID);
        return comparison != 0 ? comparison : left.PrimitiveIndex.CompareTo(right.PrimitiveIndex);
    }

    private static double DistanceSquaredToTriangle(
        Vector2 point,
        RoadSurfaceTriangle triangle)
    {
        double first = Cross(triangle.B - triangle.A, point - triangle.A);
        double second = Cross(triangle.C - triangle.B, point - triangle.B);
        double third = Cross(triangle.A - triangle.C, point - triangle.C);
        bool hasNegative = first < 0d || second < 0d || third < 0d;
        bool hasPositive = first > 0d || second > 0d || third > 0d;
        if (!(hasNegative && hasPositive))
            return 0d;

        return Math.Min(
            DistanceSquaredToSegment(point, triangle.A, triangle.B).DistanceSquared,
            Math.Min(
                DistanceSquaredToSegment(point, triangle.B, triangle.C).DistanceSquared,
                DistanceSquaredToSegment(point, triangle.C, triangle.A).DistanceSquared));
    }

    private static (double DistanceSquared, float Parameter) DistanceSquaredToSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        double dx = (double)end.X - start.X;
        double dy = (double)end.Y - start.Y;
        double lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0d)
            return (RoadNumericPolicy.DistanceSquared(point, start), 0f);

        double parameter = Math.Clamp(
            (((double)point.X - start.X) * dx + ((double)point.Y - start.Y) * dy) /
            lengthSquared,
            0d,
            1d);
        double offsetX = point.X - (start.X + dx * parameter);
        double offsetY = point.Y - (start.Y + dy * parameter);
        return (offsetX * offsetX + offsetY * offsetY, (float)parameter);
    }

    private static bool TriangleIntersectsRect(
        RoadSurfaceTriangle triangle,
        Rect2 bounds)
    {
        Vector2 end = bounds.End;
        float minimumX = MathF.Min(triangle.A.X, MathF.Min(triangle.B.X, triangle.C.X));
        float maximumX = MathF.Max(triangle.A.X, MathF.Max(triangle.B.X, triangle.C.X));
        float minimumY = MathF.Min(triangle.A.Y, MathF.Min(triangle.B.Y, triangle.C.Y));
        float maximumY = MathF.Max(triangle.A.Y, MathF.Max(triangle.B.Y, triangle.C.Y));
        if (maximumX < bounds.Position.X || minimumX > end.X ||
            maximumY < bounds.Position.Y || minimumY > end.Y)
        {
            return false;
        }

        if (RectContainsInclusive(bounds, triangle.A) ||
            RectContainsInclusive(bounds, triangle.B) ||
            RectContainsInclusive(bounds, triangle.C))
        {
            return true;
        }

        Vector2 topLeft = bounds.Position;
        Vector2 topRight = new(end.X, bounds.Position.Y);
        Vector2 bottomRight = end;
        Vector2 bottomLeft = new(bounds.Position.X, end.Y);
        if (PointInTriangle(topLeft, triangle) ||
            PointInTriangle(topRight, triangle) ||
            PointInTriangle(bottomRight, triangle) ||
            PointInTriangle(bottomLeft, triangle))
        {
            return true;
        }

        return TriangleEdgeIntersectsRect(triangle.A, triangle.B, topLeft, topRight, bottomRight, bottomLeft) ||
               TriangleEdgeIntersectsRect(triangle.B, triangle.C, topLeft, topRight, bottomRight, bottomLeft) ||
               TriangleEdgeIntersectsRect(triangle.C, triangle.A, topLeft, topRight, bottomRight, bottomLeft);
    }

    private static bool TriangleEdgeIntersectsRect(
        Vector2 start,
        Vector2 end,
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight,
        Vector2 bottomLeft) =>
        SegmentsIntersect(start, end, topLeft, topRight) ||
        SegmentsIntersect(start, end, topRight, bottomRight) ||
        SegmentsIntersect(start, end, bottomRight, bottomLeft) ||
        SegmentsIntersect(start, end, bottomLeft, topLeft);

    private static bool PointInTriangle(Vector2 point, RoadSurfaceTriangle triangle)
    {
        double first = Cross(triangle.B - triangle.A, point - triangle.A);
        double second = Cross(triangle.C - triangle.B, point - triangle.B);
        double third = Cross(triangle.A - triangle.C, point - triangle.C);
        bool hasNegative = first < 0d || second < 0d || third < 0d;
        bool hasPositive = first > 0d || second > 0d || third > 0d;
        return !(hasNegative && hasPositive);
    }

    private static bool RectContainsInclusive(Rect2 bounds, Vector2 point) =>
        point.X >= bounds.Position.X && point.X <= bounds.End.X &&
        point.Y >= bounds.Position.Y && point.Y <= bounds.End.Y;

    private static bool SegmentsIntersect(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd)
    {
        double firstSide = Cross(firstEnd - firstStart, secondStart - firstStart);
        double secondSide = Cross(firstEnd - firstStart, secondEnd - firstStart);
        double thirdSide = Cross(secondEnd - secondStart, firstStart - secondStart);
        double fourthSide = Cross(secondEnd - secondStart, firstEnd - secondStart);
        if (((firstSide > 0d && secondSide < 0d) || (firstSide < 0d && secondSide > 0d)) &&
            ((thirdSide > 0d && fourthSide < 0d) || (thirdSide < 0d && fourthSide > 0d)))
        {
            return true;
        }

        return (firstSide == 0d && PointOnSegment(secondStart, firstStart, firstEnd)) ||
               (secondSide == 0d && PointOnSegment(secondEnd, firstStart, firstEnd)) ||
               (thirdSide == 0d && PointOnSegment(firstStart, secondStart, secondEnd)) ||
               (fourthSide == 0d && PointOnSegment(firstEnd, secondStart, secondEnd));
    }

    private static bool PointOnSegment(Vector2 point, Vector2 start, Vector2 end) =>
        point.X >= MathF.Min(start.X, end.X) && point.X <= MathF.Max(start.X, end.X) &&
        point.Y >= MathF.Min(start.Y, end.Y) && point.Y <= MathF.Max(start.Y, end.Y);

    private static double Cross(Vector2 first, Vector2 second) =>
        (double)first.X * second.Y - (double)first.Y * second.X;

    private readonly record struct SurfaceCandidate(
        RoadSurfaceTriangle Triangle,
        int PrimitiveIndex,
        double SurfaceDistanceSquared,
        double CenterlineDistanceSquared,
        float CenterlineParameter);
}
