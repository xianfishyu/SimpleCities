using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGeometryPointLocationTests
{
    public static TheoryData<RoadGeometrySegment> NativeGeometryCases => new()
    {
        new LineRoadGeometrySegment(Vector2.Zero, new Vector2(12f, 2f)),
        new CubicBezierRoadGeometrySegment(
            new Vector2(20f, 0f), new Vector2(20f, 12f),
            new Vector2(32f, 12f), new Vector2(32f, 0f)),
        new CubicHermiteRoadGeometrySegment(
            new Vector2(40f, 0f), new Vector2(2f, 14f),
            new Vector2(52f, 1f), new Vector2(3f, -12f)),
        new CircularArcRoadGeometrySegment(new Vector2(66f, 0f), 6f, Mathf.Pi, Mathf.Pi),
        new ClothoidRoadGeometrySegment(new Vector2(80f, 0f), 0.2f, 0f, 0.12f, 12f),
        new RationalQuadraticRoadGeometrySegment(
            new Vector2(100f, 0f), 1f, new Vector2(106f, 12f), 0.7f,
            new Vector2(112f, 1f), 1.1f),
    };

    [Theory]
    [MemberData(nameof(NativeGeometryCases))]
    public void TryFindPointOnGeometry_LocatesInteriorOfEveryNativeGeometry(
        RoadGeometrySegment geometry)
    {
        Vector2 point = geometry.GetPosition(0.37f);

        bool found = geometry.TryFindPointOnGeometry(point, out RoadGeometryPointHit hit, 1e-4f);

        Assert.True(found);
        Assert.Equal(RoadGeometryPointLocation.Interior, hit.Location);
        Assert.InRange(hit.Distance, 0f, 1e-4f);
        Assert.InRange(Mathf.Abs(hit.Parameter - 0.37f), 0f, 2e-3f);
        Assert.InRange(hit.Position.DistanceTo(point), 0f, 1e-4f);
    }

    [Fact]
    public void TryFindPointOnGeometry_ClassifiesBothEndpointsAndInterior()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f));

        Assert.True(line.TryFindPointOnGeometry(line.Start, out RoadGeometryPointHit start));
        Assert.True(line.TryFindPointOnGeometry(new Vector2(5f, 0f), out RoadGeometryPointHit interior));
        Assert.True(line.TryFindPointOnGeometry(line.End, out RoadGeometryPointHit end));

        Assert.Equal(RoadGeometryPointLocation.Start, start.Location);
        Assert.Equal(0f, start.Parameter);
        Assert.Equal(RoadGeometryPointLocation.Interior, interior.Location);
        Assert.Equal(0.5f, interior.Parameter, 6);
        Assert.Equal(RoadGeometryPointLocation.End, end.Location);
        Assert.Equal(1f, end.Parameter);
    }

    [Fact]
    public void TryFindPointOnGeometry_UsesInclusiveDistanceTolerance()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f));

        bool boundary = line.TryFindPointOnGeometry(
            new Vector2(5f, 0.25f), out RoadGeometryPointHit hit, 0.25f);
        bool outside = line.TryFindPointOnGeometry(
            new Vector2(5f, 0.2501f), out _, 0.25f);

        Assert.True(boundary);
        Assert.Equal(0.25f, hit.Distance, 6);
        Assert.False(outside);
    }

    [Fact]
    public void TryFindPointOnGeometry_EndpointParameterToleranceControlsClassification()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f));
        Vector2 point = line.GetPosition(0.01f);

        Assert.True(line.TryFindPointOnGeometry(point, out RoadGeometryPointHit nearStart, 1e-3f, 0.02f));
        Assert.True(line.TryFindPointOnGeometry(point, out RoadGeometryPointHit interior, 1e-3f, 0.005f));

        Assert.Equal(RoadGeometryPointLocation.Start, nearStart.Location);
        Assert.Equal(RoadGeometryPointLocation.Interior, interior.Location);
    }

    [Theory]
    [InlineData(float.NaN, 0f, 1e-3f, 1e-4f)]
    [InlineData(float.PositiveInfinity, 0f, 1e-3f, 1e-4f)]
    [InlineData(0f, 0f, 0f, 1e-4f)]
    [InlineData(0f, 0f, float.NaN, 1e-4f)]
    [InlineData(0f, 0f, 1e-3f, -1f)]
    [InlineData(0f, 0f, 1e-3f, 0.5f)]
    public void TryFindPointOnGeometry_InvalidArgumentsAreRejected(
        float x,
        float y,
        float distanceTolerance,
        float endpointParameterTolerance)
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, Vector2.One);

        Assert.ThrowsAny<ArgumentException>(() => line.TryFindPointOnGeometry(
            new Vector2(x, y), out _, distanceTolerance, endpointParameterTolerance));
    }
}
