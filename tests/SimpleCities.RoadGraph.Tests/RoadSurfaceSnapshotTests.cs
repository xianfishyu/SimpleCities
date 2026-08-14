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
}
