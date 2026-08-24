using Godot;

namespace SimpleCities.Tests;

public sealed class RoadSurfaceSnapshotTests
{
    [Fact]
    public void PointQueryUsesActualTriangleSurfaceAndCarriesRenderToken()
    {
        RoadRenderToken token = Token();
        RoadSurfaceTriangle[] triangles = Quad(edgeID: 7, y: 0f, halfWidth: 2f);
        var snapshot = new RoadSurfaceSnapshot(token, triangles);

        RoadSurfaceHit? nullableHit = snapshot.FindClosest(
            new Vector2(5f, 1.5f),
            maxSurfaceDistance: 0f);

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(nullableHit);
        Assert.Equal(token, hit.RenderToken);
        Assert.Equal(RoadSurfaceOwnerKind.EdgeRibbon, hit.OwnerKind);
        Assert.Equal(7, hit.EdgeID);
        Assert.Null(hit.NodeID);
        Assert.Null(hit.Endpoint);
        Assert.Equal(0f, hit.SurfaceDistance);
        Assert.Equal(1.5f, hit.CenterlineDistance, 5);
        Assert.Null(hit.Location);
    }

    [Fact]
    public void PointQueryIncludesExactDistanceBoundaryAndRejectsOutsideIt()
    {
        var snapshot = new RoadSurfaceSnapshot(Token(), Quad(7, y: 0f, halfWidth: 2f));

        RoadSurfaceHit boundary = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(5f, 3f), maxSurfaceDistance: 1f));

        Assert.Equal(1f, boundary.SurfaceDistance, 5);
        Assert.Null(snapshot.FindClosest(new Vector2(5f, 3f), maxSurfaceDistance: 0.999f));
    }

    [Fact]
    public void TerminalCapDiscUsesExactCircleSurfaceAndCarriesEndpointLocation()
    {
        RoadSurfaceDisc disc = TerminalCapDisc(
            edgeID: 7,
            nodeID: 3,
            endpoint: EdgeEndpoint.A,
            center: Vector2.Zero,
            radius: 2f,
            outwardDirection: Vector2.Left,
            location: new RoadLocation(7, 0, 0f));
        var snapshot = new RoadSurfaceSnapshot(
            Token(),
            Array.Empty<RoadSurfaceTriangle>(),
            [disc]);

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(-1.5f, 1f), maxSurfaceDistance: 0f));

        Assert.Equal(RoadSurfaceOwnerKind.TerminalCap, hit.OwnerKind);
        Assert.Equal(7, hit.EdgeID);
        Assert.Equal(3, hit.NodeID);
        Assert.Equal(EdgeEndpoint.A, hit.Endpoint);
        Assert.Equal(new RoadLocation(7, 0, 0f), hit.Location);
        Assert.Equal(0f, hit.SurfaceDistance);
        Assert.Null(snapshot.FindClosest(new Vector2(-2f, 2f), maxSurfaceDistance: 0f));
        Assert.Empty(snapshot.FindEdgeIDsIntersecting(new Rect2(1.9f, 1.9f, 0.05f, 0.05f)));
        Assert.Equal([7], snapshot.FindEdgeIDsIntersecting(new Rect2(-2f, -0.1f, 0.2f, 0.2f)));
    }

    [Fact]
    public void RibbonWinsStableTieInsideOverlappingTerminalCap()
    {
        RoadSurfaceTriangle[] ribbon = Quad(edgeID: 7, y: 0f, halfWidth: 2f);
        RoadSurfaceDisc disc = TerminalCapDisc(
            edgeID: 7,
            nodeID: 3,
            endpoint: EdgeEndpoint.A,
            center: Vector2.Zero,
            radius: 2f,
            outwardDirection: Vector2.Left,
            location: new RoadLocation(7, 0, 0f));
        var snapshot = new RoadSurfaceSnapshot(Token(), ribbon, [disc]);

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(Vector2.Zero, maxSurfaceDistance: 0f));

        Assert.Equal(RoadSurfaceOwnerKind.EdgeRibbon, hit.OwnerKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointQueryBreaksOverlappingTerminalCapTiesByNodeRegardlessOfEnumeration(
        bool reverseEnumeration)
    {
        RoadSurfaceDisc lowerNode = TerminalCapDisc(
            edgeID: 7,
            nodeID: 3,
            endpoint: EdgeEndpoint.A,
            center: new Vector2(-1f, 0f),
            radius: 2f,
            outwardDirection: Vector2.Left,
            location: new RoadLocation(7, 0, 0f));
        RoadSurfaceDisc higherNode = TerminalCapDisc(
            edgeID: 7,
            nodeID: 9,
            endpoint: EdgeEndpoint.B,
            center: new Vector2(1f, 0f),
            radius: 2f,
            outwardDirection: Vector2.Right,
            location: new RoadLocation(7, 0, 1f));
        RoadSurfaceDisc[] candidates = reverseEnumeration
            ? [higherNode, lowerNode]
            : [lowerNode, higherNode];
        var snapshot = new RoadSurfaceSnapshot(
            Token(),
            Array.Empty<RoadSurfaceTriangle>(),
            candidates);

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(Vector2.Zero, maxSurfaceDistance: 0f));

        Assert.Equal(RoadSurfaceOwnerKind.TerminalCap, hit.OwnerKind);
        Assert.Equal(7, hit.EdgeID);
        Assert.Equal(3, hit.NodeID);
        Assert.Equal(EdgeEndpoint.A, hit.Endpoint);
        Assert.Equal(new RoadLocation(7, 0, 0f), hit.Location);
        Assert.Equal(0f, hit.SurfaceDistance);
        Assert.Equal(1f, hit.CenterlineDistance);
    }

    [Fact]
    public void TerminalCapDiscRejectsInvalidRadiusAndBounds()
    {
        RoadSurfaceOwner owner = RoadSurfaceOwner.TerminalCap(
            edgeID: 7,
            nodeID: 3,
            endpoint: EdgeEndpoint.A);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RoadSurfaceDisc(
            owner: owner,
            center: Vector2.Zero,
            radius: 0f,
            centerlineStart: Vector2.Zero,
            centerlineEnd: Vector2.Left,
            location: new RoadLocation(7, 0, 0f)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoadSurfaceDisc(
            owner: owner,
            center: new Vector2(float.MaxValue, 0f),
            radius: float.MaxValue,
            centerlineStart: Vector2.Zero,
            centerlineEnd: Vector2.Left,
            location: new RoadLocation(7, 0, 0f)));
    }

    [Fact]
    public void SemanticJoinTriangleCarriesFixedCanonicalEndpointLocation()
    {
        var location = new RoadLocation(7, 0, 0f);
        var triangle = new RoadSurfaceTriangle(
            RoadSurfaceOwner.SemanticJoin(
                edgeID: 7,
                nodeID: 3,
                endpoint: EdgeEndpoint.A,
                sectorOrder: 1),
            Vector2.Zero,
            new Vector2(-4f, 0f),
            new Vector2(0f, -2f),
            centerlineStart: Vector2.Zero,
            centerlineEnd: Vector2.Right,
            locationStart: null,
            locationEnd: null,
            fixedLocation: location);
        var snapshot = new RoadSurfaceSnapshot(Token(), [triangle]);

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(-1f, -0.5f), maxSurfaceDistance: 0f));

        Assert.Equal(RoadSurfaceOwnerKind.SemanticJoin, hit.OwnerKind);
        Assert.Equal(7, hit.EdgeID);
        Assert.Equal(3, hit.NodeID);
        Assert.Equal(EdgeEndpoint.A, hit.Endpoint);
        Assert.Equal(location, hit.Location);
    }

    [Fact]
    public void SurfaceTriangleRejectsCombinedIntervalAndFixedLocation()
    {
        RoadSurfaceOwner owner = RoadSurfaceOwner.EdgeRibbon(edgeID: 7);

        Assert.Throws<ArgumentException>(() => new RoadSurfaceTriangle(
            owner,
            Vector2.Zero,
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            centerlineStart: Vector2.Zero,
            centerlineEnd: Vector2.Right,
            locationStart: new RoadLocation(7, 0, 0f),
            locationEnd: new RoadLocation(7, 0, 1f),
            fixedLocation: new RoadLocation(7, 0, 0f)));
    }

    [Fact]
    public void PointQueryBreaksVisualTiesByCenterlineThenStableOwner()
    {
        RoadSurfaceTriangle fartherCenterline = Triangle(
            edgeID: 1,
            new Vector2(0f, 0f),
            new Vector2(10f, 0f),
            new Vector2(0f, 10f),
            centerlineStart: new Vector2(0f, 4f),
            centerlineEnd: new Vector2(10f, 4f));
        RoadSurfaceTriangle nearerHigherID = Triangle(
            edgeID: 9,
            new Vector2(0f, 0f),
            new Vector2(10f, 0f),
            new Vector2(0f, 10f),
            centerlineStart: new Vector2(0f, 2f),
            centerlineEnd: new Vector2(10f, 2f));
        RoadSurfaceTriangle nearerLowerID = Triangle(
            edgeID: 3,
            new Vector2(0f, 0f),
            new Vector2(10f, 0f),
            new Vector2(0f, 10f),
            centerlineStart: new Vector2(0f, 2f),
            centerlineEnd: new Vector2(10f, 2f));
        var snapshot = new RoadSurfaceSnapshot(
            Token(),
            [fartherCenterline, nearerHigherID, nearerLowerID]);

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(2f, 2f), maxSurfaceDistance: 0f));

        Assert.Equal(3, hit.EdgeID);
        Assert.Equal(0f, hit.CenterlineDistance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointQueryBreaksJunctionTiesBySectorThenEdgeIDRegardlessOfEnumeration(
        bool reverseEnumeration)
    {
        RoadSurfaceTriangle lowerSectorHigherID = JunctionPatchTriangle(
            edgeID: 9,
            sectorOrder: 0);
        RoadSurfaceTriangle higherSectorLowerID = JunctionPatchTriangle(
            edgeID: 3,
            sectorOrder: 1);
        RoadSurfaceTriangle[] sectorCandidates = reverseEnumeration
            ? [higherSectorLowerID, lowerSectorHigherID]
            : [lowerSectorHigherID, higherSectorLowerID];
        var sectorSnapshot = new RoadSurfaceSnapshot(Token(), sectorCandidates);

        RoadSurfaceHit sectorHit = Assert.IsType<RoadSurfaceHit>(
            sectorSnapshot.FindClosest(new Vector2(2f, 2f), maxSurfaceDistance: 0f));

        Assert.Equal(RoadSurfaceOwnerKind.JunctionPatch, sectorHit.OwnerKind);
        Assert.Equal(9, sectorHit.EdgeID);
        Assert.Equal(20, sectorHit.NodeID);
        Assert.Equal(EdgeEndpoint.A, sectorHit.Endpoint);
        Assert.Equal(new RoadLocation(9, 0, 0f), sectorHit.Location);

        RoadSurfaceTriangle sameSectorLowerID = JunctionPatchTriangle(
            edgeID: 3,
            sectorOrder: 0);
        RoadSurfaceTriangle[] edgeCandidates = reverseEnumeration
            ? [lowerSectorHigherID, sameSectorLowerID]
            : [sameSectorLowerID, lowerSectorHigherID];
        var edgeSnapshot = new RoadSurfaceSnapshot(Token(), edgeCandidates);

        RoadSurfaceHit edgeHit = Assert.IsType<RoadSurfaceHit>(
            edgeSnapshot.FindClosest(new Vector2(2f, 2f), maxSurfaceDistance: 0f));

        Assert.Equal(RoadSurfaceOwnerKind.JunctionPatch, edgeHit.OwnerKind);
        Assert.Equal(3, edgeHit.EdgeID);
        Assert.Equal(new RoadLocation(3, 0, 0f), edgeHit.Location);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointQueryBreaksSelfLoopIncidenceTiesByEndpointRegardlessOfEnumeration(
        bool reverseEnumeration)
    {
        RoadSurfaceTriangle endpointA = JunctionPatchTriangle(
            edgeID: 7,
            sectorOrder: 0,
            endpoint: EdgeEndpoint.A,
            parameter: 0f);
        RoadSurfaceTriangle endpointB = JunctionPatchTriangle(
            edgeID: 7,
            sectorOrder: 0,
            endpoint: EdgeEndpoint.B,
            parameter: 1f);
        RoadSurfaceTriangle[] candidates = reverseEnumeration
            ? [endpointB, endpointA]
            : [endpointA, endpointB];
        var snapshot = new RoadSurfaceSnapshot(Token(), candidates);

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(2f, 2f), maxSurfaceDistance: 0f));

        Assert.Equal(RoadSurfaceOwnerKind.JunctionPatch, hit.OwnerKind);
        Assert.Equal(7, hit.EdgeID);
        Assert.Equal(20, hit.NodeID);
        Assert.Equal(EdgeEndpoint.A, hit.Endpoint);
        Assert.Equal(new RoadLocation(7, 0, 0f), hit.Location);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointQueryChoosesNearestCenterlineAcrossOverlappingWidthsRegardlessOfEnumeration(
        bool reverseEnumeration)
    {
        RoadSurfaceTriangle[] wide = Quad(edgeID: 3, y: 0f, halfWidth: 4f);
        RoadSurfaceTriangle[] narrow = Quad(edgeID: 9, y: 2f, halfWidth: 1f);
        RoadSurfaceTriangle[] triangles = reverseEnumeration
            ? [.. narrow.Reverse(), .. wide.Reverse()]
            : [.. wide, .. narrow];
        var snapshot = new RoadSurfaceSnapshot(Token(), triangles);

        RoadSurfaceHit overlapHit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(5f, 1.5f), maxSurfaceDistance: 0f));

        Assert.Equal(RoadSurfaceOwnerKind.EdgeRibbon, overlapHit.OwnerKind);
        Assert.Equal(9, overlapHit.EdgeID);
        Assert.Equal(0f, overlapHit.SurfaceDistance);
        Assert.Equal(0.5f, overlapHit.CenterlineDistance, 5);

        RoadSurfaceHit wideOnlyHit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(5f, -3.5f), maxSurfaceDistance: 0f));

        Assert.Equal(3, wideOnlyHit.EdgeID);
        Assert.Equal(0f, wideOnlyHit.SurfaceDistance);
        Assert.Equal(3.5f, wideOnlyHit.CenterlineDistance, 5);
    }

    [Fact]
    public void RectangleQueryUsesVisibleTrianglesAndReturnsSortedUniqueOwners()
    {
        RoadSurfaceTriangle[] first = Quad(edgeID: 9, y: 0f, halfWidth: 2f);
        RoadSurfaceTriangle[] second = Quad(edgeID: 3, y: 8f, halfWidth: 2f);
        var snapshot = new RoadSurfaceSnapshot(Token(), [.. first, .. second]);

        Assert.Equal(
            [9],
            snapshot.FindEdgeIDsIntersecting(new Rect2(4f, 1.5f, 2f, 1f)));
        Assert.Equal(
            [3, 9],
            snapshot.FindEdgeIDsIntersecting(new Rect2(4f, 1.5f, 2f, 8f)));
        Assert.Empty(snapshot.FindEdgeIDsIntersecting(new Rect2(20f, 20f, 1f, 1f)));
    }

    [Fact]
    public void SnapshotDefensivelyCopiesPrimitiveArray()
    {
        RoadSurfaceTriangle[] source = Quad(edgeID: 7, y: 0f, halfWidth: 2f);
        var snapshot = new RoadSurfaceSnapshot(Token(), source);
        source[0] = Quad(edgeID: 99, y: 20f, halfWidth: 1f)[0];

        Assert.Equal(7, snapshot.GetPrimitive(0).Owner.EdgeID);
        Assert.Equal(2, snapshot.PrimitiveCount);
    }

    [Fact]
    public void PointQueryInterpolatesCanonicalRoadLocation()
    {
        RoadSurfaceTriangle[] triangles = QuadWithLocations(
            edgeID: 7,
            geometryIndex: 3,
            startX: 0f,
            endX: 10f,
            parameterStart: 0.25f,
            parameterEnd: 0.75f,
            ownsLocationEnd: true);
        var snapshot = new RoadSurfaceSnapshot(Token(), triangles);

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(5f, 0f), maxSurfaceDistance: 0f));

        Assert.Equal(new RoadLocation(7, 3, 0.5f), hit.Location);
    }

    [Fact]
    public void PointQueryUsesHalfOpenLocationOwnershipAtGeometryJoin()
    {
        RoadSurfaceTriangle[] before = QuadWithLocations(
            edgeID: 7,
            geometryIndex: 0,
            startX: 0f,
            endX: 10f,
            parameterStart: 0f,
            parameterEnd: 1f,
            ownsLocationEnd: false);
        RoadSurfaceTriangle[] after = QuadWithLocations(
            edgeID: 7,
            geometryIndex: 1,
            startX: 10f,
            endX: 20f,
            parameterStart: 0f,
            parameterEnd: 1f,
            ownsLocationEnd: true);
        var snapshot = new RoadSurfaceSnapshot(Token(), [.. before, .. after]);

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(10f, 0f), maxSurfaceDistance: 0f));

        Assert.Equal(new RoadLocation(7, 1, 0f), hit.Location);
    }

    [Fact]
    public void PointQueryUsesSpatialIndexToBoundExactTriangleTests()
    {
        RoadSurfaceTriangle[] triangles = Enumerable.Range(0, 1_024)
            .SelectMany(index => Quad(index, y: index * 100f, halfWidth: 2f))
            .ToArray();
        var snapshot = new RoadSurfaceSnapshot(Token(), triangles);

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(snapshot.FindClosest(
            new Vector2(5f, 0f),
            maxSurfaceDistance: 0f,
            out RoadSurfaceQueryMetrics metrics));

        Assert.Equal(0, hit.EdgeID);
        Assert.InRange(metrics.IndexNodeVisitCount, 1, 64);
        Assert.InRange(metrics.PrimitiveCandidateCount, 2, 8);
        Assert.Equal(2, metrics.ExactPrimitiveTestCount);
        Assert.True(metrics.PrimitiveCandidateCount < snapshot.PrimitiveCount);
    }

    [Fact]
    public void RectangleQueryUsesSpatialIndexToBoundExactTriangleTests()
    {
        RoadSurfaceTriangle[] triangles = Enumerable.Range(0, 1_024)
            .SelectMany(index => Quad(index, y: index * 100f, halfWidth: 2f))
            .ToArray();
        var snapshot = new RoadSurfaceSnapshot(Token(), triangles);

        int[] edgeIDs = snapshot.FindEdgeIDsIntersecting(
            new Rect2(4f, -1f, 2f, 2f),
            out RoadSurfaceQueryMetrics metrics);

        Assert.Equal([0], edgeIDs);
        Assert.InRange(metrics.IndexNodeVisitCount, 1, 64);
        Assert.InRange(metrics.PrimitiveCandidateCount, 2, 8);
        Assert.Equal(2, metrics.ExactPrimitiveTestCount);
        Assert.True(metrics.PrimitiveCandidateCount < snapshot.PrimitiveCount);
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void PointQueryKeepsExactTriangleWorkLocalAtPerformanceContractScale(int edgeCount)
    {
        RoadSurfaceTriangle[] triangles = Enumerable.Range(0, edgeCount)
            .SelectMany(index => Quad(index, y: index * 100f, halfWidth: 2f))
            .ToArray();
        var snapshot = new RoadSurfaceSnapshot(Token(), triangles);
        int targetEdgeID = edgeCount / 2;

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(snapshot.FindClosest(
            new Vector2(5f, targetEdgeID * 100f),
            maxSurfaceDistance: 0f,
            out RoadSurfaceQueryMetrics metrics));

        Assert.Equal(targetEdgeID, hit.EdgeID);
        Assert.InRange(metrics.IndexNodeVisitCount, 1, 64);
        Assert.InRange(metrics.PrimitiveCandidateCount, 2, 8);
        Assert.Equal(2, metrics.ExactPrimitiveTestCount);
        Assert.True(metrics.PrimitiveCandidateCount < snapshot.PrimitiveCount);
    }

    [Theory]
    [InlineData(float.NaN, 0f, 1f)]
    [InlineData(float.PositiveInfinity, 0f, 1f)]
    [InlineData(0f, float.NegativeInfinity, 1f)]
    [InlineData(0f, 0f, -1f)]
    [InlineData(0f, 0f, float.NaN)]
    public void PointQueryRejectsInvalidInput(float x, float y, float radius)
    {
        var snapshot = new RoadSurfaceSnapshot(Token(), Quad(7, y: 0f, halfWidth: 2f));

        Assert.ThrowsAny<ArgumentException>(() =>
            snapshot.FindClosest(new Vector2(x, y), radius));
    }

    private static RoadRenderToken Token() => new(
        SceneGeneration: 1,
        GraphFacadeID: 2,
        GraphFacadeGeneration: 3,
        ChangeSequence: 4,
        RoadStyleRevision: 5,
        RenderRequestID: 6);

    private static RoadSurfaceTriangle[] Quad(int edgeID, float y, float halfWidth)
    {
        Vector2 startLeft = new(0f, y - halfWidth);
        Vector2 startRight = new(0f, y + halfWidth);
        Vector2 endLeft = new(10f, y - halfWidth);
        Vector2 endRight = new(10f, y + halfWidth);
        Vector2 centerlineStart = new(0f, y);
        Vector2 centerlineEnd = new(10f, y);
        return
        [
            Triangle(
                edgeID,
                startLeft,
                startRight,
                endLeft,
                centerlineStart,
                centerlineEnd),
            Triangle(
                edgeID,
                endLeft,
                startRight,
                endRight,
                centerlineStart,
                centerlineEnd),
        ];
    }

    private static RoadSurfaceTriangle Triangle(
        int edgeID,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 centerlineStart,
        Vector2 centerlineEnd) => new(
            RoadSurfaceOwner.EdgeRibbon(edgeID),
            a,
            b,
            c,
            centerlineStart,
            centerlineEnd,
            locationStart: null,
            locationEnd: null);

    private static RoadSurfaceTriangle JunctionPatchTriangle(
        int edgeID,
        int sectorOrder,
        EdgeEndpoint endpoint = EdgeEndpoint.A,
        float parameter = 0f) => new(
            RoadSurfaceOwner.JunctionPatch(
                edgeID,
                nodeID: 20,
                endpoint,
                sectorOrder: sectorOrder),
            Vector2.Zero,
            new Vector2(10f, 0f),
            new Vector2(0f, 10f),
            centerlineStart: Vector2.Zero,
            centerlineEnd: new Vector2(10f, 10f),
            locationStart: null,
            locationEnd: null,
            fixedLocation: new RoadLocation(edgeID, 0, parameter));

    private static RoadSurfaceTriangle[] QuadWithLocations(
        int edgeID,
        int geometryIndex,
        float startX,
        float endX,
        float parameterStart,
        float parameterEnd,
        bool ownsLocationEnd)
    {
        Vector2 startLeft = new(startX, -2f);
        Vector2 startRight = new(startX, 2f);
        Vector2 endLeft = new(endX, -2f);
        Vector2 endRight = new(endX, 2f);
        Vector2 centerlineStart = new(startX, 0f);
        Vector2 centerlineEnd = new(endX, 0f);
        RoadSurfaceOwner owner = RoadSurfaceOwner.EdgeRibbon(edgeID);
        var locationStart = new RoadLocation(edgeID, geometryIndex, parameterStart);
        var locationEnd = new RoadLocation(edgeID, geometryIndex, parameterEnd);
        return
        [
            new RoadSurfaceTriangle(
                owner,
                startLeft,
                startRight,
                endLeft,
                centerlineStart,
                centerlineEnd,
                locationStart,
                locationEnd,
                ownsLocationEnd),
            new RoadSurfaceTriangle(
                owner,
                endLeft,
                startRight,
                endRight,
                centerlineStart,
                centerlineEnd,
                locationStart,
                locationEnd,
                ownsLocationEnd),
        ];
    }

    private static RoadSurfaceDisc TerminalCapDisc(
        int edgeID,
        int nodeID,
        EdgeEndpoint endpoint,
        Vector2 center,
        float radius,
        Vector2 outwardDirection,
        RoadLocation location) => new(
        RoadSurfaceOwner.TerminalCap(edgeID, nodeID, endpoint),
        center,
        radius,
        center,
        center + outwardDirection * radius,
        location);
}
