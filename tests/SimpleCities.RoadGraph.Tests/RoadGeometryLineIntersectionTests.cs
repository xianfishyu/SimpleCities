using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGeometryLineIntersectionTests
{
    public static TheoryData<RoadGeometrySegment> GeneralCurveCases => new()
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
    public void LineLine_InteriorCrossReturnsStableParameters()
    {
        var horizontal = new LineRoadGeometrySegment(new Vector2(-5f, 0f), new Vector2(5f, 0f));
        var vertical = new LineRoadGeometrySegment(new Vector2(0f, -4f), new Vector2(0f, 6f));

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindLineIntersections(horizontal, vertical);

        RoadGeometryIntersection hit = Assert.Single(result.Intersections);
        Assert.False(result.HasOverlap);
        Assert.Equal(RoadGeometryIntersectionKind.Crossing, hit.Kind);
        Assert.Equal(0.5f, hit.FirstParameter, 5);
        Assert.Equal(0.4f, hit.SecondParameter, 5);
        Assert.InRange(hit.Position.Length(), 0f, 1e-5f);
    }

    [Fact]
    public void LineLine_EndpointTouchAndOverlapAreDistinguished()
    {
        var first = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f));
        var touching = new LineRoadGeometrySegment(new Vector2(10f, 0f), new Vector2(14f, 0f));
        var overlapping = new LineRoadGeometrySegment(new Vector2(4f, 0f), new Vector2(12f, 0f));

        RoadGeometryIntersectionResult touch =
            RoadGeometryIntersectionQuery.FindLineIntersections(first, touching);
        RoadGeometryIntersectionResult overlap =
            RoadGeometryIntersectionQuery.FindLineIntersections(first, overlapping);

        Assert.Equal(RoadGeometryIntersectionKind.EndpointTouch, Assert.Single(touch.Intersections).Kind);
        Assert.False(touch.HasOverlap);
        Assert.Empty(overlap.Intersections);
        Assert.True(overlap.HasOverlap);
        RoadGeometryOverlap interval = Assert.Single(overlap.Overlaps);
        Assert.Equal(0.4f, interval.FirstParameterStart, 5);
        Assert.Equal(1f, interval.FirstParameterEnd);
        Assert.Equal(0f, interval.SecondParameterAtFirstStart, 5);
        Assert.Equal(0.75f, interval.SecondParameterAtFirstEnd, 5);
    }

    [Fact]
    public void LineLine_ParallelSeparatedReturnsNoIntersection()
    {
        var first = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f));
        var second = new LineRoadGeometrySegment(new Vector2(0f, 2f), new Vector2(10f, 2f));

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindLineIntersections(first, second);

        Assert.Empty(result.Intersections);
        Assert.False(result.HasOverlap);
    }

    [Fact]
    public void LineBezier_FindsMultipleInteriorCrossings()
    {
        var line = new LineRoadGeometrySegment(new Vector2(-1f, 0f), new Vector2(11f, 0f));
        var cubic = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 8f), new Vector2(8f, -8f), new Vector2(10f, 0f));

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindLineIntersections(line, cubic, 1e-3f);

        Assert.False(result.HasOverlap);
        Assert.Equal(3, result.Intersections.Count);
        Assert.All(result.Intersections, hit =>
            Assert.InRange(hit.Position.DistanceTo(line.GetPosition(hit.FirstParameter)), 0f, 1e-3f));
    }

    [Fact]
    public void LineBezier_InteriorTangencyIsClassified()
    {
        var line = new LineRoadGeometrySegment(new Vector2(-1f, 1f), new Vector2(11f, 1f));
        var cubic = new CubicBezierRoadGeometrySegment(
            new Vector2(0f, 4f), new Vector2(3f, 0f),
            new Vector2(7f, 0f), new Vector2(10f, 4f));

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindLineIntersections(line, cubic, 1e-3f);

        RoadGeometryIntersection hit = Assert.Single(result.Intersections);
        Assert.Equal(RoadGeometryIntersectionKind.Tangent, hit.Kind);
        Assert.InRange(Mathf.Abs(hit.SecondParameter - 0.5f), 0f, 2e-3f);
    }

    [Fact]
    public void LineArc_FindsTwoIntersectionsInParameterOrder()
    {
        var line = new LineRoadGeometrySegment(new Vector2(-6f, 0f), new Vector2(6f, 0f));
        var arc = new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, Mathf.Tau);

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindLineIntersections(line, arc, 1e-3f);

        Assert.Equal(2, result.Intersections.Count);
        Assert.True(result.Intersections[0].FirstParameter < result.Intersections[1].FirstParameter);
    }

    [Theory]
    [MemberData(nameof(GeneralCurveCases))]
    public void LineGeneralCurve_FindsKnownInteriorParameter(RoadGeometrySegment geometry)
    {
        const float expectedParameter = 0.37f;
        Vector2 point = geometry.GetPosition(expectedParameter);
        Vector2 tangent = geometry.GetUnitTangent(expectedParameter);
        Vector2 normal = new(-tangent.Y, tangent.X);
        var line = new LineRoadGeometrySegment(point - normal * 5f, point + normal * 5f);

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindLineIntersections(line, geometry, 1e-3f);

        Assert.Contains(result.Intersections, hit =>
            Mathf.Abs(hit.SecondParameter - expectedParameter) <= 2e-3f &&
            hit.Position.DistanceTo(point) <= 2e-3f);
    }

    [Theory]
    [InlineData(0f, 1e-4f)]
    [InlineData(float.NaN, 1e-4f)]
    [InlineData(1e-3f, -1f)]
    [InlineData(1e-3f, 0.5f)]
    [InlineData(1e-3f, float.PositiveInfinity)]
    public void FindLineIntersections_InvalidTolerancesAreRejected(
        float spatialTolerance,
        float endpointParameterTolerance)
    {
        var first = new LineRoadGeometrySegment(Vector2.Zero, Vector2.Right);
        var second = new LineRoadGeometrySegment(Vector2.Down, Vector2.Up);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoadGeometryIntersectionQuery.FindLineIntersections(
                first, second, spatialTolerance, endpointParameterTolerance));
    }
}
