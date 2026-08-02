using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGeometryClosestPointTests
{
    public static TheoryData<RoadGeometrySegment> CurvedGeometry => new()
    {
        new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 5f), new Vector2(7f, -3f), new Vector2(10f, 1f)),
        new CubicHermiteRoadGeometrySegment(
            new Vector2(20f, 0f), new Vector2(6f, 4f),
            new Vector2(28f, 2f), new Vector2(4f, -3f)),
        new ClothoidRoadGeometrySegment(new Vector2(40f, 0f), 0.15f, 0f, 0.08f, 8f),
        new RationalQuadraticRoadGeometrySegment(
            new Vector2(55f, 0f), 1f, new Vector2(59f, 6f), 0.65f,
            new Vector2(64f, 1f), 1.2f),
    };

    [Fact]
    public void LineClosestPoint_UsesExactProjectionAndClampsToEndpoints()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f));

        RoadGeometryClosestPoint interior = line.FindClosestPoint(new Vector2(3f, 4f));
        RoadGeometryClosestPoint before = line.FindClosestPoint(new Vector2(-2f, 1f));
        RoadGeometryClosestPoint after = line.FindClosestPoint(new Vector2(12f, -3f));

        Assert.Equal(0.3f, interior.Parameter, 6);
        Assert.Equal(new Vector2(3f, 0f), interior.Position);
        Assert.Equal(16f, interior.DistanceSquared, 6);
        Assert.Equal(4f, interior.Distance, 6);
        Assert.Equal(0f, before.Parameter);
        Assert.Equal(line.Start, before.Position);
        Assert.Equal(1f, after.Parameter);
        Assert.Equal(line.End, after.Position);
    }

    [Fact]
    public void CircularArcClosestPoint_UsesRadialProjectionAndSweepEndpoints()
    {
        var arc = new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, Mathf.Pi);

        RoadGeometryClosestPoint radial = arc.FindClosestPoint(new Vector2(0f, 8f));
        RoadGeometryClosestPoint outsideSweep = arc.FindClosestPoint(new Vector2(0f, -8f));

        Assert.Equal(0.5f, radial.Parameter, 5);
        Assert.InRange(radial.Position.DistanceTo(new Vector2(0f, 5f)), 0f, 1e-5f);
        Assert.Equal(3f, radial.Distance, 5);
        Assert.Contains(outsideSweep.Parameter, new[] { 0f, 1f });
        Assert.Contains(outsideSweep.Position, new[] { arc.Start, arc.End });
    }

    [Fact]
    public void CircularArc_QueryAtCenterUsesStableLowerParameterTieBreak()
    {
        var arc = new CircularArcRoadGeometrySegment(new Vector2(3f, -2f), 4f, 0.7f, -Mathf.Pi);

        RoadGeometryClosestPoint closest = arc.FindClosestPoint(arc.Center);

        Assert.Equal(0f, closest.Parameter);
        Assert.Equal(arc.Start, closest.Position);
        Assert.Equal(arc.Radius, closest.Distance, 5);
    }

    [Theory]
    [MemberData(nameof(CurvedGeometry))]
    public void CurvedClosestPoint_ConvergesToExactOnCurveQuery(RoadGeometrySegment geometry)
    {
        const float expectedParameter = 0.37f;
        Vector2 query = geometry.GetPosition(expectedParameter);

        RoadGeometryClosestPoint closest = geometry.FindClosestPoint(query, 1e-4f);

        Assert.InRange(closest.Distance, 0f, 1e-4f);
        Assert.InRange(closest.Position.DistanceTo(query), 0f, 1e-4f);
        Assert.InRange(Mathf.Abs(closest.Parameter - expectedParameter), 0f, 2e-3f);
    }

    [Fact]
    public void CurvedClosestPoint_UsesCurveInteriorInsteadOfEndpointChord()
    {
        var cubic = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(0f, 8f), new Vector2(10f, 8f), new Vector2(10f, 0f));
        Vector2 query = cubic.GetPosition(0.5f) + new Vector2(0f, 1f);

        RoadGeometryClosestPoint closest = cubic.FindClosestPoint(query, 1e-4f);

        Assert.InRange(closest.Parameter, 0.42f, 0.58f);
        Assert.InRange(closest.Distance, 0.9f, 1.01f);
        Assert.True(closest.Distance < query.DistanceTo(new Vector2(5f, 0f)));
    }

    [Theory]
    [InlineData(float.NaN, 0f, 1e-3f)]
    [InlineData(float.PositiveInfinity, 0f, 1e-3f)]
    [InlineData(0f, 0f, 0f)]
    [InlineData(0f, 0f, -1f)]
    [InlineData(0f, 0f, float.NaN)]
    public void ClosestPoint_InvalidArgumentsAreRejected(float x, float y, float tolerance)
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, Vector2.One);
        var cubic = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, Vector2.Right, Vector2.Down, Vector2.One);

        Assert.ThrowsAny<ArgumentException>(() => line.FindClosestPoint(new Vector2(x, y), tolerance));
        Assert.ThrowsAny<ArgumentException>(() => cubic.FindClosestPoint(new Vector2(x, y), tolerance));
    }
}
