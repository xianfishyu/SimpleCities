using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGeometryCurveIntersectionTests
{
    public static TheoryData<RoadGeometrySegment> GeneralCurveCases => new()
    {
        new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 5f), new Vector2(7f, -3f), new Vector2(10f, 1f)),
        new CubicHermiteRoadGeometrySegment(
            new Vector2(20f, 0f), new Vector2(6f, 4f),
            new Vector2(28f, 2f), new Vector2(4f, -3f)),
        new CircularArcRoadGeometrySegment(new Vector2(38f, 0f), 5f, 0f, Mathf.Pi),
        new ClothoidRoadGeometrySegment(new Vector2(50f, 0f), 0.15f, 0f, 0.08f, 8f),
        new RationalQuadraticRoadGeometrySegment(
            new Vector2(65f, 0f), 1f, new Vector2(69f, 6f), 0.65f,
            new Vector2(74f, 1f), 1.2f),
    };

    public static TheoryData<RoadGeometrySegment, bool> PartialOverlapCases
    {
        get
        {
            var cases = new TheoryData<RoadGeometrySegment, bool>();
            foreach (RoadGeometrySegment geometry in GeneralCurveCases)
            {
                cases.Add(geometry, false);
                cases.Add(geometry, true);
            }
            return cases;
        }
    }

    [Fact]
    public void FindIntersections_BezierBezierInteriorCrossReturnsBothParameters()
    {
        var horizontal = LinearBezier(new Vector2(-5f, 0f), new Vector2(5f, 0f));
        var vertical = LinearBezier(new Vector2(0f, -5f), new Vector2(0f, 5f));

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindIntersections(horizontal, vertical, 1e-3f);

        RoadGeometryIntersection hit = Assert.Single(result.Intersections);
        Assert.False(result.HasOverlap);
        Assert.Equal(RoadGeometryIntersectionKind.Crossing, hit.Kind);
        Assert.InRange(Mathf.Abs(hit.FirstParameter - 0.5f), 0f, 2e-3f);
        Assert.InRange(Mathf.Abs(hit.SecondParameter - 0.5f), 0f, 2e-3f);
        Assert.InRange(hit.Position.Length(), 0f, 2e-3f);
    }

    [Fact]
    public void FindIntersections_BezierBezierFindsMultipleIntersections()
    {
        var baseline = LinearBezier(new Vector2(-1f, 0f), new Vector2(11f, 0f));
        var wave = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 8f), new Vector2(8f, -8f), new Vector2(10f, 0f));

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindIntersections(baseline, wave, 1e-3f);

        Assert.Equal(3, result.Intersections.Count);
    }

    [Fact]
    public void FindIntersections_BezierBezierInteriorTangencyIsSingleHit()
    {
        var baseline = LinearBezier(new Vector2(-1f, 1f), new Vector2(11f, 1f));
        var tangent = new CubicBezierRoadGeometrySegment(
            new Vector2(0f, 4f), new Vector2(3f, 0f),
            new Vector2(7f, 0f), new Vector2(10f, 4f));

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindIntersections(baseline, tangent, 1e-3f);

        RoadGeometryIntersection hit = Assert.Single(result.Intersections);
        Assert.Equal(RoadGeometryIntersectionKind.Tangent, hit.Kind);
        Assert.InRange(Mathf.Abs(hit.SecondParameter - 0.5f), 0f, 2e-3f);
    }

    [Fact]
    public void FindIntersections_FullCircularArcsReturnTwoCrossings()
    {
        var first = new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, Mathf.Tau);
        var second = new CircularArcRoadGeometrySegment(new Vector2(6f, 0f), 5f, 0f, Mathf.Tau);

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindIntersections(first, second, 1e-3f);

        Assert.Equal(2, result.Intersections.Count);
        Assert.All(result.Intersections, hit =>
            Assert.Equal(RoadGeometryIntersectionKind.Crossing, hit.Kind));
    }

    [Fact]
    public void FindIntersections_EndpointTouchAndSeparatedCurvesAreDistinguished()
    {
        var first = LinearBezier(Vector2.Zero, new Vector2(5f, 0f));
        var touching = LinearBezier(new Vector2(5f, 0f), new Vector2(8f, 3f));
        var separated = LinearBezier(new Vector2(0f, 3f), new Vector2(5f, 3f));

        RoadGeometryIntersectionResult touch =
            RoadGeometryIntersectionQuery.FindIntersections(first, touching);
        RoadGeometryIntersectionResult none =
            RoadGeometryIntersectionQuery.FindIntersections(first, separated);

        Assert.Equal(RoadGeometryIntersectionKind.EndpointTouch, Assert.Single(touch.Intersections).Kind);
        Assert.Empty(none.Intersections);
        Assert.False(none.HasOverlap);
    }

    [Fact]
    public void FindIntersections_IdenticalNativeCurveReportsOverlap()
    {
        var curve = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 4f), new Vector2(7f, -2f), new Vector2(10f, 1f));
        var identical = new CubicBezierRoadGeometrySegment(
            curve.Start, curve.Control1, curve.Control2, curve.End);

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindIntersections(curve, identical);

        Assert.True(result.HasOverlap);
        Assert.Empty(result.Intersections);
        RoadGeometryOverlap overlap = Assert.Single(result.Overlaps);
        Assert.Equal(0f, overlap.FirstParameterStart);
        Assert.Equal(1f, overlap.FirstParameterEnd);
        Assert.Equal(0f, overlap.SecondParameterAtFirstStart);
        Assert.Equal(1f, overlap.SecondParameterAtFirstEnd);
        Assert.True(overlap.SameDirection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FindIntersections_FullCircleOverlapPreservesDirection(bool reversed)
    {
        var circle = new CircularArcRoadGeometrySegment(
            new Vector2(3f, -2f), 5f, 0.4f, Mathf.Tau);
        RoadGeometrySegment other = reversed ? Reverse(circle) : new CircularArcRoadGeometrySegment(
            circle.Center, circle.Radius, circle.StartAngle, circle.SweepAngle);

        RoadGeometryOverlap overlap = Assert.Single(
            RoadGeometryIntersectionQuery.FindIntersections(circle, other).Overlaps);

        Assert.InRange(Mathf.Abs(overlap.FirstParameterStart), 0f, 2e-3f);
        Assert.InRange(Mathf.Abs(overlap.FirstParameterEnd - 1f), 0f, 2e-3f);
        Assert.InRange(
            Mathf.Abs(overlap.SecondParameterAtFirstStart - (reversed ? 1f : 0f)), 0f, 2e-3f);
        Assert.InRange(
            Mathf.Abs(overlap.SecondParameterAtFirstEnd - (reversed ? 0f : 1f)), 0f, 2e-3f);
        Assert.Equal(!reversed, overlap.SameDirection);
    }

    [Theory]
    [MemberData(nameof(GeneralCurveCases))]
    public void FindIntersections_ReversedNativeCurveReportsDescendingSecondParameters(
        RoadGeometrySegment curve)
    {
        RoadGeometrySegment reversed = Reverse(curve);

        RoadGeometryOverlap overlap = Assert.Single(
            RoadGeometryIntersectionQuery.FindIntersections(curve, reversed).Overlaps);

        Assert.Equal(0f, overlap.FirstParameterStart);
        Assert.Equal(1f, overlap.FirstParameterEnd);
        Assert.Equal(1f, overlap.SecondParameterAtFirstStart);
        Assert.Equal(0f, overlap.SecondParameterAtFirstEnd);
        Assert.False(overlap.SameDirection);
    }

    [Theory]
    [MemberData(nameof(PartialOverlapCases))]
    public void FindIntersections_PartialNativeOverlapReturnsBothParameterIntervals(
        RoadGeometrySegment curve,
        bool reversed)
    {
        RoadGeometrySegment tail = curve.Split(0.2f).After;
        RoadGeometrySegment partial = tail.Split(0.75f).Before;
        if (reversed)
            partial = Reverse(partial);

        RoadGeometryOverlap overlap = Assert.Single(
            RoadGeometryIntersectionQuery.FindIntersections(curve, partial, 1e-3f).Overlaps);

        Assert.InRange(Mathf.Abs(overlap.FirstParameterStart - 0.2f), 0f, 2e-3f);
        Assert.InRange(Mathf.Abs(overlap.FirstParameterEnd - 0.8f), 0f, 2e-3f);
        Assert.Equal(reversed ? 1f : 0f, overlap.SecondParameterAtFirstStart, 4);
        Assert.Equal(reversed ? 0f : 1f, overlap.SecondParameterAtFirstEnd, 4);
        Assert.Equal(!reversed, overlap.SameDirection);
    }

    [Theory]
    [MemberData(nameof(GeneralCurveCases))]
    public void FindIntersections_GeneralCurvePairFindsKnownInteriorParameter(
        RoadGeometrySegment geometry)
    {
        const float expectedParameter = 0.37f;
        Vector2 point = geometry.GetPosition(expectedParameter);
        Vector2 tangent = geometry.GetUnitTangent(expectedParameter);
        Vector2 normal = new(-tangent.Y, tangent.X);
        var crossingCurve = LinearBezier(point - normal * 5f, point + normal * 5f);

        RoadGeometryIntersectionResult result =
            RoadGeometryIntersectionQuery.FindIntersections(crossingCurve, geometry, 1e-3f);

        Assert.Contains(result.Intersections, hit =>
            Mathf.Abs(hit.SecondParameter - expectedParameter) <= 2e-3f &&
            hit.Position.DistanceTo(point) <= 2e-3f);
    }

    [Fact]
    public void FindIntersections_WhenSecondIsLinePreservesRequestedParameterOrder()
    {
        var curve = LinearBezier(new Vector2(-5f, 0f), new Vector2(5f, 0f));
        var line = new LineRoadGeometrySegment(new Vector2(0f, -5f), new Vector2(0f, 5f));

        RoadGeometryIntersection hit = Assert.Single(
            RoadGeometryIntersectionQuery.FindIntersections(curve, line).Intersections);

        Assert.InRange(Mathf.Abs(hit.FirstParameter - 0.5f), 0f, 2e-3f);
        Assert.Equal(0.5f, hit.SecondParameter, 5);
    }

    private static CubicBezierRoadGeometrySegment LinearBezier(Vector2 start, Vector2 end) =>
        new(start, start.Lerp(end, 1f / 3f), start.Lerp(end, 2f / 3f), end);

    private static RoadGeometrySegment Reverse(RoadGeometrySegment geometry) =>
        geometry switch
        {
            CubicBezierRoadGeometrySegment curve => new CubicBezierRoadGeometrySegment(
                curve.End, curve.Control2, curve.Control1, curve.Start),
            CubicHermiteRoadGeometrySegment curve => new CubicHermiteRoadGeometrySegment(
                curve.End, -curve.EndTangent, curve.Start, -curve.StartTangent),
            CircularArcRoadGeometrySegment curve => new CircularArcRoadGeometrySegment(
                curve.Center, curve.Radius, curve.StartAngle + curve.SweepAngle, -curve.SweepAngle),
            ClothoidRoadGeometrySegment curve => new ClothoidRoadGeometrySegment(
                curve.End,
                curve.GetUnitTangent(1f).Angle() + Mathf.Pi,
                -curve.EndCurvature,
                -curve.StartCurvature,
                curve.ArcLength),
            RationalQuadraticRoadGeometrySegment curve => new RationalQuadraticRoadGeometrySegment(
                curve.End, curve.EndWeight,
                curve.Control, curve.ControlWeight,
                curve.Start, curve.StartWeight),
            _ => throw new NotSupportedException(geometry.GetType().Name),
        };
}
