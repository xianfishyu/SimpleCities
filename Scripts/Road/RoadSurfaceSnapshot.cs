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
    internal bool OwnsLocationEnd { get; }
    internal RoadLocation? FixedLocation { get; }

    internal RoadSurfaceTriangle(
        RoadSurfaceOwner owner,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 centerlineStart,
        Vector2 centerlineEnd,
        RoadLocation? locationStart,
        RoadLocation? locationEnd,
        bool ownsLocationEnd = true,
        RoadLocation? fixedLocation = null)
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
        if (fixedLocation.HasValue && locationStart.HasValue)
        {
            throw new ArgumentException(
                "A road surface triangle cannot combine an interval and a fixed location.");
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
            if (start.Parameter >= end.Parameter)
            {
                throw new ArgumentException(
                    "A road surface triangle location interval must be increasing.");
            }
        }
        if (fixedLocation is RoadLocation canonicalLocation)
        {
            ValidateLocation(
                owner,
                canonicalLocation,
                nameof(fixedLocation));
        }

        Owner = owner;
        A = a;
        B = b;
        C = c;
        CenterlineStart = centerlineStart;
        CenterlineEnd = centerlineEnd;
        LocationStart = locationStart;
        LocationEnd = locationEnd;
        OwnsLocationEnd = ownsLocationEnd;
        FixedLocation = fixedLocation;
    }

    internal RoadLocation? InterpolateLocation(float parameter)
    {
        if (FixedLocation is RoadLocation fixedLocation)
            return fixedLocation;
        if (LocationStart is not RoadLocation start ||
            LocationEnd is not RoadLocation end)
        {
            return null;
        }
        if (!OwnsLocationEnd && parameter >= 1f)
            return null;

        return new RoadLocation(
            start.EdgeID,
            start.GeometryIndex,
            Mathf.Lerp(start.Parameter, end.Parameter, parameter));
    }

    internal static void ValidateLocation(
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

    internal static void ValidatePoint(Vector2 point, string parameterName)
    {
        if (!point.IsFinite())
            throw new ArgumentException("Road surface coordinates must be finite.", parameterName);
    }

    private static double Cross(Vector2 first, Vector2 second) =>
        (double)first.X * second.Y - (double)first.Y * second.X;
}

internal readonly record struct RoadSurfaceDisc
{
    internal RoadSurfaceOwner Owner { get; }
    internal Vector2 Center { get; }
    internal float Radius { get; }
    internal Vector2 CenterlineStart { get; }
    internal Vector2 CenterlineEnd { get; }
    internal RoadLocation? Location { get; }

    internal RoadSurfaceDisc(
        RoadSurfaceOwner owner,
        Vector2 center,
        float radius,
        Vector2 centerlineStart,
        Vector2 centerlineEnd,
        RoadLocation? location)
    {
        if (owner.Kind != RoadSurfaceOwnerKind.TerminalCap)
        {
            throw new ArgumentException(
                "A road surface disc currently represents a terminal cap.",
                nameof(owner));
        }
        RoadSurfaceTriangle.ValidatePoint(center, nameof(center));
        RoadSurfaceTriangle.ValidatePoint(centerlineStart, nameof(centerlineStart));
        RoadSurfaceTriangle.ValidatePoint(centerlineEnd, nameof(centerlineEnd));
        if (!float.IsFinite(radius) || radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(center.X - radius) ||
            !float.IsFinite(center.X + radius) ||
            !float.IsFinite(center.Y - radius) ||
            !float.IsFinite(center.Y + radius))
        {
            throw new ArgumentOutOfRangeException(
                nameof(radius),
                "A road surface disc must have finite bounds.");
        }
        if (centerlineStart == centerlineEnd)
            throw new ArgumentException("A road surface disc must have a non-degenerate centerline.");
        if (location is RoadLocation canonicalLocation)
        {
            RoadSurfaceTriangle.ValidateLocation(
                owner,
                canonicalLocation,
                nameof(location));
        }

        Owner = owner;
        Center = center;
        Radius = radius;
        CenterlineStart = centerlineStart;
        CenterlineEnd = centerlineEnd;
        Location = location;
    }
}

internal enum RoadSurfacePrimitiveKind
{
    Triangle,
    Disc,
}

internal readonly record struct RoadSurfacePrimitive
{
    private readonly RoadSurfaceTriangle _triangle;
    private readonly RoadSurfaceDisc _disc;

    private RoadSurfacePrimitive(RoadSurfaceTriangle triangle)
    {
        Kind = RoadSurfacePrimitiveKind.Triangle;
        _triangle = triangle;
        _disc = default;
    }

    private RoadSurfacePrimitive(RoadSurfaceDisc disc)
    {
        Kind = RoadSurfacePrimitiveKind.Disc;
        _triangle = default;
        _disc = disc;
    }

    internal RoadSurfacePrimitiveKind Kind { get; }

    internal RoadSurfaceTriangle Triangle => Kind == RoadSurfacePrimitiveKind.Triangle
        ? _triangle
        : throw new InvalidOperationException("The road surface primitive is not a triangle.");

    internal RoadSurfaceDisc Disc => Kind == RoadSurfacePrimitiveKind.Disc
        ? _disc
        : throw new InvalidOperationException("The road surface primitive is not a disc.");

    internal RoadSurfaceOwner Owner => Kind switch
    {
        RoadSurfacePrimitiveKind.Triangle => _triangle.Owner,
        RoadSurfacePrimitiveKind.Disc => _disc.Owner,
        _ => throw new InvalidOperationException("The road surface primitive kind is invalid."),
    };

    internal Vector2 CenterlineStart => Kind switch
    {
        RoadSurfacePrimitiveKind.Triangle => _triangle.CenterlineStart,
        RoadSurfacePrimitiveKind.Disc => _disc.CenterlineStart,
        _ => throw new InvalidOperationException("The road surface primitive kind is invalid."),
    };

    internal Vector2 CenterlineEnd => Kind switch
    {
        RoadSurfacePrimitiveKind.Triangle => _triangle.CenterlineEnd,
        RoadSurfacePrimitiveKind.Disc => _disc.CenterlineEnd,
        _ => throw new InvalidOperationException("The road surface primitive kind is invalid."),
    };

    internal RoadLocation? InterpolateLocation(float parameter) => Kind switch
    {
        RoadSurfacePrimitiveKind.Triangle => _triangle.InterpolateLocation(parameter),
        RoadSurfacePrimitiveKind.Disc => _disc.Location,
        _ => throw new InvalidOperationException("The road surface primitive kind is invalid."),
    };

    internal static RoadSurfacePrimitive FromTriangle(RoadSurfaceTriangle triangle) =>
        new(triangle);

    internal static RoadSurfacePrimitive FromDisc(RoadSurfaceDisc disc) =>
        new(disc);
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

internal readonly record struct RoadSurfaceQueryMetrics(
    int IndexNodeVisitCount,
    int PrimitiveCandidateCount,
    int ExactPrimitiveTestCount);

internal sealed class RoadSurfaceSnapshot
{
    private const int SpatialLeafCapacity = 8;
    private const int MaximumSpatialTraversalDepth = 64;

    private readonly PreparedData _prepared;

    internal RoadSurfaceSnapshot(
        RoadRenderToken renderToken,
        IReadOnlyCollection<RoadSurfaceTriangle> primitives) :
        this(renderToken, Prepare(primitives))
    {
    }

    internal RoadSurfaceSnapshot(
        RoadRenderToken renderToken,
        IReadOnlyCollection<RoadSurfaceTriangle> triangles,
        IReadOnlyCollection<RoadSurfaceDisc> discs) :
        this(renderToken, Prepare(triangles, discs))
    {
    }

    internal RoadSurfaceSnapshot(
        RoadRenderToken renderToken,
        PreparedData prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        RenderToken = renderToken;
        _prepared = prepared;
    }

    internal RoadRenderToken RenderToken { get; }
    internal int PrimitiveCount => _prepared.PrimitiveCount;
    internal int TriangleCount => _prepared.TriangleCount;
    internal int DiscCount => _prepared.DiscCount;

    internal RoadSurfacePrimitive GetPrimitive(int index) => _prepared.GetPrimitive(index);

    internal static PreparedData Prepare(
        IReadOnlyCollection<RoadSurfaceTriangle> primitives) =>
        PreparedData.Create(primitives);

    internal static PreparedData Prepare(
        IReadOnlyCollection<RoadSurfaceTriangle> triangles,
        IReadOnlyCollection<RoadSurfaceDisc> discs) =>
        PreparedData.Create(triangles, discs);

    internal RoadSurfaceHit? FindClosest(
        Vector2 position,
        float maxSurfaceDistance) =>
        FindClosest(position, maxSurfaceDistance, out _);

    internal RoadSurfaceHit? FindClosest(
        Vector2 position,
        float maxSurfaceDistance,
        out RoadSurfaceQueryMetrics metrics)
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
        int nodeVisitCount = 0;
        int primitiveCandidateCount = 0;
        int exactPrimitiveTestCount = 0;
        Span<int> nodeStack = stackalloc int[MaximumSpatialTraversalDepth];
        int stackCount = 0;
        if (_prepared.SpatialRootIndex >= 0)
            nodeStack[stackCount++] = _prepared.SpatialRootIndex;

        while (stackCount > 0)
        {
            int nodeIndex = nodeStack[--stackCount];
            nodeVisitCount++;
            RoadSurfaceSpatialNode node = _prepared.GetSpatialNode(nodeIndex);
            double distanceLimitSquared = best?.SurfaceDistanceSquared ?? maximumDistanceSquared;
            if (node.Bounds.DistanceSquared(position) > distanceLimitSquared)
                continue;

            if (!node.IsLeaf)
            {
                if (stackCount > nodeStack.Length - 2)
                {
                    throw new InvalidOperationException(
                        "Road surface spatial index exceeded its balanced traversal bound.");
                }
                nodeStack[stackCount++] = node.RightChildIndex;
                nodeStack[stackCount++] = node.LeftChildIndex;
                continue;
            }

            for (int offset = 0; offset < node.PrimitiveCount; offset++)
            {
                primitiveCandidateCount++;
                int index = _prepared.GetSpatialPrimitiveIndex(node.PrimitiveStart + offset);
                distanceLimitSquared = best?.SurfaceDistanceSquared ?? maximumDistanceSquared;
                if (_prepared.GetPrimitiveBounds(index).DistanceSquared(position) > distanceLimitSquared)
                    continue;

                exactPrimitiveTestCount++;
                RoadSurfacePrimitive primitive = _prepared.GetPrimitive(index);
                double surfaceDistanceSquared = DistanceSquaredToPrimitive(position, primitive);
                if (surfaceDistanceSquared > maximumDistanceSquared)
                    continue;

                (double centerlineDistanceSquared, float parameter) =
                    DistanceSquaredToSegment(
                        position,
                        primitive.CenterlineStart,
                        primitive.CenterlineEnd);
                var candidate = new SurfaceCandidate(
                    primitive,
                    index,
                    surfaceDistanceSquared,
                    centerlineDistanceSquared,
                    primitive.InterpolateLocation(parameter));
                if (best is null || Compare(candidate, best.Value) < 0)
                    best = candidate;
            }
        }

        metrics = new RoadSurfaceQueryMetrics(
            nodeVisitCount,
            primitiveCandidateCount,
            exactPrimitiveTestCount);

        if (best is not SurfaceCandidate selected)
            return null;

        RoadSurfaceOwner owner = selected.Primitive.Owner;
        return new RoadSurfaceHit(
            RenderToken,
            owner.Kind,
            owner.NodeID,
            owner.EdgeID,
            owner.Endpoint,
            (float)Math.Sqrt(selected.SurfaceDistanceSquared),
            (float)Math.Sqrt(selected.CenterlineDistanceSquared),
            selected.Location);
    }

    internal int[] FindEdgeIDsIntersecting(Rect2 bounds) =>
        FindEdgeIDsIntersecting(bounds, out _);

    internal int[] FindEdgeIDsIntersecting(
        Rect2 bounds,
        out RoadSurfaceQueryMetrics metrics)
    {
        if (!bounds.Position.IsFinite() || !bounds.Size.IsFinite())
            throw new ArgumentException("Road surface query bounds must be finite.", nameof(bounds));
        if (bounds.Size.X < 0f || bounds.Size.Y < 0f)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Road surface query size cannot be negative.");

        var edgeIDs = new HashSet<int>();
        int nodeVisitCount = 0;
        int primitiveCandidateCount = 0;
        int exactPrimitiveTestCount = 0;
        Span<int> nodeStack = stackalloc int[MaximumSpatialTraversalDepth];
        int stackCount = 0;
        if (_prepared.SpatialRootIndex >= 0)
            nodeStack[stackCount++] = _prepared.SpatialRootIndex;

        while (stackCount > 0)
        {
            int nodeIndex = nodeStack[--stackCount];
            nodeVisitCount++;
            RoadSurfaceSpatialNode node = _prepared.GetSpatialNode(nodeIndex);
            if (!node.Bounds.Intersects(bounds))
                continue;

            if (!node.IsLeaf)
            {
                if (stackCount > nodeStack.Length - 2)
                {
                    throw new InvalidOperationException(
                        "Road surface spatial index exceeded its balanced traversal bound.");
                }
                nodeStack[stackCount++] = node.RightChildIndex;
                nodeStack[stackCount++] = node.LeftChildIndex;
                continue;
            }

            for (int offset = 0; offset < node.PrimitiveCount; offset++)
            {
                primitiveCandidateCount++;
                int primitiveIndex = _prepared.GetSpatialPrimitiveIndex(node.PrimitiveStart + offset);
                if (!_prepared.GetPrimitiveBounds(primitiveIndex).Intersects(bounds))
                    continue;

                exactPrimitiveTestCount++;
                RoadSurfacePrimitive primitive = _prepared.GetPrimitive(primitiveIndex);
                if (PrimitiveIntersectsRect(primitive, bounds))
                    edgeIDs.Add(primitive.Owner.EdgeID);
            }
        }

        metrics = new RoadSurfaceQueryMetrics(
            nodeVisitCount,
            primitiveCandidateCount,
            exactPrimitiveTestCount);
        return edgeIDs.Order().ToArray();
    }

    private static RoadSurfaceSpatialIndex BuildSpatialIndex(
        RoadSurfaceBounds[] primitiveBounds)
    {
        int primitiveCount = primitiveBounds.Length;
        if (primitiveCount == 0)
        {
            return new RoadSurfaceSpatialIndex(
                primitiveBounds,
                [],
                [],
                RootIndex: -1);
        }

        var primitiveIndices = new int[primitiveCount];
        for (int index = 0; index < primitiveCount; index++)
            primitiveIndices[index] = index;

        var partitionBuffer = new int[primitiveCount];
        var nodes = new List<RoadSurfaceSpatialNode>(
            Math.Max(1, primitiveCount / SpatialLeafCapacity * 2));
        int rootIndex = BuildSpatialNode(
            primitiveBounds,
            primitiveIndices,
            partitionBuffer,
            nodes,
            start: 0,
            primitiveCount);
        return new RoadSurfaceSpatialIndex(
            primitiveBounds,
            nodes.ToArray(),
            primitiveIndices,
            rootIndex);
    }

    private static int BuildSpatialNode(
        IReadOnlyList<RoadSurfaceBounds> primitiveBounds,
        int[] primitiveIndices,
        int[] partitionBuffer,
        List<RoadSurfaceSpatialNode> nodes,
        int start,
        int count)
    {
        int nodeIndex = nodes.Count;
        nodes.Add(default);

        RoadSurfaceBounds bounds = primitiveBounds[primitiveIndices[start]];
        double minimumCenterX = bounds.CenterX;
        double maximumCenterX = bounds.CenterX;
        double minimumCenterY = bounds.CenterY;
        double maximumCenterY = bounds.CenterY;
        for (int offset = 1; offset < count; offset++)
        {
            RoadSurfaceBounds current = primitiveBounds[primitiveIndices[start + offset]];
            bounds = RoadSurfaceBounds.Combine(bounds, current);
            minimumCenterX = Math.Min(minimumCenterX, current.CenterX);
            maximumCenterX = Math.Max(maximumCenterX, current.CenterX);
            minimumCenterY = Math.Min(minimumCenterY, current.CenterY);
            maximumCenterY = Math.Max(maximumCenterY, current.CenterY);
        }

        if (count <= SpatialLeafCapacity)
        {
            nodes[nodeIndex] = RoadSurfaceSpatialNode.Leaf(bounds, start, count);
            return nodeIndex;
        }

        bool splitAlongX = maximumCenterX - minimumCenterX >=
                           maximumCenterY - minimumCenterY;
        double minimumCenter = splitAlongX ? minimumCenterX : minimumCenterY;
        double maximumCenter = splitAlongX ? maximumCenterX : maximumCenterY;
        int leftCount;
        if (minimumCenter == maximumCenter)
        {
            leftCount = count / 2;
        }
        else
        {
            double split = (minimumCenter + maximumCenter) * 0.5d;
            leftCount = 0;
            for (int offset = 0; offset < count; offset++)
            {
                RoadSurfaceBounds current = primitiveBounds[primitiveIndices[start + offset]];
                double center = splitAlongX ? current.CenterX : current.CenterY;
                if (center < split)
                    leftCount++;
            }

            int minimumBalancedCount = count / 3;
            if (leftCount < minimumBalancedCount ||
                leftCount > count - minimumBalancedCount)
            {
                Array.Sort(
                    primitiveIndices,
                    start,
                    count,
                    new RoadSurfacePrimitiveIndexComparer(
                        primitiveBounds,
                        splitAlongX));
                leftCount = count / 2;
            }
            else
            {
                int leftWrite = start;
                int rightWrite = start + leftCount;
                for (int offset = 0; offset < count; offset++)
                {
                    int primitiveIndex = primitiveIndices[start + offset];
                    RoadSurfaceBounds current = primitiveBounds[primitiveIndex];
                    double center = splitAlongX ? current.CenterX : current.CenterY;
                    partitionBuffer[center < split ? leftWrite++ : rightWrite++] = primitiveIndex;
                }
                Array.Copy(partitionBuffer, start, primitiveIndices, start, count);
            }
        }

        if (leftCount <= 0 || leftCount >= count)
            throw new InvalidOperationException("Road surface spatial partition did not make progress.");

        int leftChildIndex = BuildSpatialNode(
            primitiveBounds,
            primitiveIndices,
            partitionBuffer,
            nodes,
            start,
            leftCount);
        int rightChildIndex = BuildSpatialNode(
            primitiveBounds,
            primitiveIndices,
            partitionBuffer,
            nodes,
            start + leftCount,
            count - leftCount);
        nodes[nodeIndex] = RoadSurfaceSpatialNode.Branch(
            bounds,
            leftChildIndex,
            rightChildIndex);
        return nodeIndex;
    }

    private static int Compare(SurfaceCandidate left, SurfaceCandidate right)
    {
        int comparison = left.SurfaceDistanceSquared.CompareTo(right.SurfaceDistanceSquared);
        if (comparison != 0)
            return comparison;
        comparison = left.CenterlineDistanceSquared.CompareTo(right.CenterlineDistanceSquared);
        if (comparison != 0)
            return comparison;
        comparison = left.Primitive.Owner.SectorOrder.CompareTo(right.Primitive.Owner.SectorOrder);
        if (comparison != 0)
            return comparison;
        comparison = left.Primitive.Owner.EdgeID.CompareTo(right.Primitive.Owner.EdgeID);
        if (comparison != 0)
            return comparison;
        comparison = left.Primitive.Owner.Kind.CompareTo(right.Primitive.Owner.Kind);
        if (comparison != 0)
            return comparison;
        comparison = Nullable.Compare(left.Primitive.Owner.NodeID, right.Primitive.Owner.NodeID);
        if (comparison != 0)
            return comparison;
        comparison = Nullable.Compare(left.Primitive.Owner.Endpoint, right.Primitive.Owner.Endpoint);
        if (comparison != 0)
            return comparison;
        if (left.Location.HasValue != right.Location.HasValue)
            return left.Location.HasValue ? -1 : 1;
        return left.PrimitiveIndex.CompareTo(right.PrimitiveIndex);
    }

    private static double DistanceSquaredToPrimitive(
        Vector2 point,
        RoadSurfacePrimitive primitive) => primitive.Kind switch
    {
        RoadSurfacePrimitiveKind.Triangle =>
            DistanceSquaredToTriangle(point, primitive.Triangle),
        RoadSurfacePrimitiveKind.Disc =>
            DistanceSquaredToDisc(point, primitive.Disc),
        _ => throw new InvalidOperationException("The road surface primitive kind is invalid."),
    };

    private static double DistanceSquaredToDisc(
        Vector2 point,
        RoadSurfaceDisc disc)
    {
        double centerDistance = Math.Sqrt(
            RoadNumericPolicy.DistanceSquared(point, disc.Center));
        double surfaceDistance = Math.Max(0d, centerDistance - disc.Radius);
        return surfaceDistance * surfaceDistance;
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

    private static bool PrimitiveIntersectsRect(
        RoadSurfacePrimitive primitive,
        Rect2 bounds) => primitive.Kind switch
    {
        RoadSurfacePrimitiveKind.Triangle =>
            TriangleIntersectsRect(primitive.Triangle, bounds),
        RoadSurfacePrimitiveKind.Disc =>
            DiscIntersectsRect(primitive.Disc, bounds),
        _ => throw new InvalidOperationException("The road surface primitive kind is invalid."),
    };

    private static bool DiscIntersectsRect(
        RoadSurfaceDisc disc,
        Rect2 bounds)
    {
        Vector2 end = bounds.End;
        double nearestX = Math.Clamp(
            (double)disc.Center.X,
            bounds.Position.X,
            end.X);
        double nearestY = Math.Clamp(
            (double)disc.Center.Y,
            bounds.Position.Y,
            end.Y);
        double offsetX = disc.Center.X - nearestX;
        double offsetY = disc.Center.Y - nearestY;
        return offsetX * offsetX + offsetY * offsetY <=
               (double)disc.Radius * disc.Radius;
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

    internal sealed class PreparedData
    {
        private readonly RoadSurfaceTriangle[] _triangles;
        private readonly RoadSurfaceDisc[] _discs;
        private readonly RoadSurfaceBounds[] _primitiveBounds;
        private readonly RoadSurfaceSpatialNode[] _spatialNodes;
        private readonly int[] _spatialPrimitiveIndices;
        private readonly int _spatialRootIndex;

        private PreparedData(
            RoadSurfaceTriangle[] triangles,
            RoadSurfaceDisc[] discs,
            RoadSurfaceBounds[] primitiveBounds,
            RoadSurfaceSpatialNode[] spatialNodes,
            int[] spatialPrimitiveIndices,
            int spatialRootIndex)
        {
            _triangles = triangles;
            _discs = discs;
            _primitiveBounds = primitiveBounds;
            _spatialNodes = spatialNodes;
            _spatialPrimitiveIndices = spatialPrimitiveIndices;
            _spatialRootIndex = spatialRootIndex;
        }

        internal static PreparedData Create(
            IReadOnlyCollection<RoadSurfaceTriangle> primitives)
            => Create(primitives, Array.Empty<RoadSurfaceDisc>());

        internal static PreparedData Create(
            IReadOnlyCollection<RoadSurfaceTriangle> triangles,
            IReadOnlyCollection<RoadSurfaceDisc> discs)
        {
            ArgumentNullException.ThrowIfNull(triangles);
            ArgumentNullException.ThrowIfNull(discs);
            RoadSurfaceTriangle[] triangleCopy = triangles.ToArray();
            RoadSurfaceDisc[] discCopy = discs.ToArray();
            var primitiveBounds = new RoadSurfaceBounds[
                triangleCopy.Length + discCopy.Length];
            for (int index = 0; index < triangleCopy.Length; index++)
            {
                primitiveBounds[index] = RoadSurfaceBounds.FromTriangle(
                    triangleCopy[index]);
            }
            for (int index = 0; index < discCopy.Length; index++)
            {
                primitiveBounds[triangleCopy.Length + index] =
                    RoadSurfaceBounds.FromDisc(discCopy[index]);
            }

            RoadSurfaceSpatialIndex spatialIndex = BuildSpatialIndex(primitiveBounds);
            return new PreparedData(
                triangleCopy,
                discCopy,
                spatialIndex.PrimitiveBounds,
                spatialIndex.Nodes,
                spatialIndex.PrimitiveIndices,
                spatialIndex.RootIndex);
        }

        internal int PrimitiveCount => _triangles.Length + _discs.Length;
        internal int TriangleCount => _triangles.Length;
        internal int DiscCount => _discs.Length;
        internal int SpatialRootIndex => _spatialRootIndex;

        internal RoadSurfacePrimitive GetPrimitive(int index)
        {
            if ((uint)index < (uint)_triangles.Length)
                return RoadSurfacePrimitive.FromTriangle(_triangles[index]);

            int discIndex = index - _triangles.Length;
            if ((uint)discIndex < (uint)_discs.Length)
                return RoadSurfacePrimitive.FromDisc(_discs[discIndex]);

            throw new ArgumentOutOfRangeException(nameof(index));
        }

        internal RoadSurfaceBounds GetPrimitiveBounds(int index) =>
            _primitiveBounds[index];

        internal RoadSurfaceSpatialNode GetSpatialNode(int index) =>
            _spatialNodes[index];

        internal int GetSpatialPrimitiveIndex(int index) =>
            _spatialPrimitiveIndices[index];
    }

    private readonly record struct RoadSurfaceSpatialIndex(
        RoadSurfaceBounds[] PrimitiveBounds,
        RoadSurfaceSpatialNode[] Nodes,
        int[] PrimitiveIndices,
        int RootIndex);

    internal readonly record struct RoadSurfaceSpatialNode(
        RoadSurfaceBounds Bounds,
        int LeftChildIndex,
        int RightChildIndex,
        int PrimitiveStart,
        int PrimitiveCount)
    {
        internal bool IsLeaf => PrimitiveCount > 0;

        internal static RoadSurfaceSpatialNode Leaf(
            RoadSurfaceBounds bounds,
            int primitiveStart,
            int primitiveCount) => new(
            bounds,
            LeftChildIndex: -1,
            RightChildIndex: -1,
            primitiveStart,
            primitiveCount);

        internal static RoadSurfaceSpatialNode Branch(
            RoadSurfaceBounds bounds,
            int leftChildIndex,
            int rightChildIndex) => new(
            bounds,
            leftChildIndex,
            rightChildIndex,
            PrimitiveStart: 0,
            PrimitiveCount: 0);
    }

    internal readonly record struct RoadSurfaceBounds(
        float MinimumX,
        float MaximumX,
        float MinimumY,
        float MaximumY)
    {
        internal double CenterX => ((double)MinimumX + MaximumX) * 0.5d;
        internal double CenterY => ((double)MinimumY + MaximumY) * 0.5d;

        internal static RoadSurfaceBounds FromTriangle(RoadSurfaceTriangle triangle) => new(
            MathF.Min(triangle.A.X, MathF.Min(triangle.B.X, triangle.C.X)),
            MathF.Max(triangle.A.X, MathF.Max(triangle.B.X, triangle.C.X)),
            MathF.Min(triangle.A.Y, MathF.Min(triangle.B.Y, triangle.C.Y)),
            MathF.Max(triangle.A.Y, MathF.Max(triangle.B.Y, triangle.C.Y)));

        internal static RoadSurfaceBounds FromDisc(RoadSurfaceDisc disc) => new(
            disc.Center.X - disc.Radius,
            disc.Center.X + disc.Radius,
            disc.Center.Y - disc.Radius,
            disc.Center.Y + disc.Radius);

        internal static RoadSurfaceBounds Combine(
            RoadSurfaceBounds first,
            RoadSurfaceBounds second) => new(
            MathF.Min(first.MinimumX, second.MinimumX),
            MathF.Max(first.MaximumX, second.MaximumX),
            MathF.Min(first.MinimumY, second.MinimumY),
            MathF.Max(first.MaximumY, second.MaximumY));

        internal double DistanceSquared(Vector2 point)
        {
            double offsetX = point.X < MinimumX
                ? (double)MinimumX - point.X
                : point.X > MaximumX
                    ? (double)point.X - MaximumX
                    : 0d;
            double offsetY = point.Y < MinimumY
                ? (double)MinimumY - point.Y
                : point.Y > MaximumY
                    ? (double)point.Y - MaximumY
                    : 0d;
            return offsetX * offsetX + offsetY * offsetY;
        }

        internal bool Intersects(Rect2 bounds)
        {
            double queryMaximumX = (double)bounds.Position.X + bounds.Size.X;
            double queryMaximumY = (double)bounds.Position.Y + bounds.Size.Y;
            return MaximumX >= bounds.Position.X &&
                   MinimumX <= queryMaximumX &&
                   MaximumY >= bounds.Position.Y &&
                   MinimumY <= queryMaximumY;
        }
    }

    private sealed class RoadSurfacePrimitiveIndexComparer(
        IReadOnlyList<RoadSurfaceBounds> primitiveBounds,
        bool compareX) : IComparer<int>
    {
        public int Compare(int left, int right)
        {
            double leftCenter = compareX
                ? primitiveBounds[left].CenterX
                : primitiveBounds[left].CenterY;
            double rightCenter = compareX
                ? primitiveBounds[right].CenterX
                : primitiveBounds[right].CenterY;
            int comparison = leftCenter.CompareTo(rightCenter);
            return comparison != 0 ? comparison : left.CompareTo(right);
        }
    }

    private readonly record struct SurfaceCandidate(
        RoadSurfacePrimitive Primitive,
        int PrimitiveIndex,
        double SurfaceDistanceSquared,
        double CenterlineDistanceSquared,
        RoadLocation? Location);
}
