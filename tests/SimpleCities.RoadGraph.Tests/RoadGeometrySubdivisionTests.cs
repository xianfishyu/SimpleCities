using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGeometrySubdivisionTests
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
    public void SplitAtParameters_PreservesTypeAndGlobalParameterMapping(
        RoadGeometrySegment geometry)
    {
        IReadOnlyList<RoadGeometrySubsegment> segments =
            RoadGeometrySubdivision.SplitAtParameters(geometry, [0.8f, 0.2f, 0.5f]);

        Assert.Equal(4, segments.Count);
        Assert.Equal(
            new[] { (0f, 0.2f), (0.2f, 0.5f), (0.5f, 0.8f), (0.8f, 1f) },
            segments.Select(segment => (segment.ParameterStart, segment.ParameterEnd)));
        foreach (RoadGeometrySubsegment segment in segments)
        {
            Assert.IsType(geometry.GetType(), segment.Geometry);
            Assert.InRange(segment.Geometry.Start.DistanceTo(
                geometry.GetPosition(segment.ParameterStart)), 0f, 2e-4f);
            Assert.InRange(segment.Geometry.End.DistanceTo(
                geometry.GetPosition(segment.ParameterEnd)), 0f, 2e-4f);
            foreach (float localParameter in new[] { 0.25f, 0.5f, 0.75f })
            {
                float globalParameter = Mathf.Lerp(
                    segment.ParameterStart, segment.ParameterEnd, localParameter);
                Assert.InRange(segment.Geometry.GetPosition(localParameter).DistanceTo(
                    geometry.GetPosition(globalParameter)), 0f, 3e-4f);
            }
        }
    }

    [Fact]
    public void SplitAtParameters_IgnoresEndpointsAndCoalescesToleranceDuplicates()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f));

        IReadOnlyList<RoadGeometrySubsegment> segments =
            RoadGeometrySubdivision.SplitAtParameters(
                line,
                [1f, 0.500004f, 0f, 0.5f],
                parameterTolerance: 1e-5f);

        Assert.Equal(2, segments.Count);
        Assert.Equal((0f, 0.5f), (segments[0].ParameterStart, segments[0].ParameterEnd));
        Assert.Equal((0.5f, 1f), (segments[1].ParameterStart, segments[1].ParameterEnd));
    }

    [Fact]
    public void SplitAtParameters_EmptyInputReturnsOriginalGeometryAsSingleRange()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f));

        RoadGeometrySubsegment segment = Assert.Single(
            RoadGeometrySubdivision.SplitAtParameters(line, []));

        Assert.Equal(0f, segment.ParameterStart);
        Assert.Equal(1f, segment.ParameterEnd);
        Assert.Same(line, segment.Geometry);
    }

    [Theory]
    [InlineData(float.NaN, 1e-5f)]
    [InlineData(float.PositiveInfinity, 1e-5f)]
    [InlineData(-0.1f, 1e-5f)]
    [InlineData(1.1f, 1e-5f)]
    [InlineData(0.5f, -1f)]
    [InlineData(0.5f, 0.5f)]
    public void SplitAtParameters_InvalidParametersAreRejected(
        float parameter,
        float parameterTolerance)
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, Vector2.One);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoadGeometrySubdivision.SplitAtParameters(
                line, [parameter], parameterTolerance));
    }
}
