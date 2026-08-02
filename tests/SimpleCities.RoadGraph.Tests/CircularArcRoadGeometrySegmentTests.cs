using Godot;

namespace SimpleCities.Tests;

public sealed class CircularArcRoadGeometrySegmentTests
{
    [Fact]
    public void CounterClockwiseQuarterArcHasExactNativeContract()
    {
        var arc = new CircularArcRoadGeometrySegment(new Vector2(2f, 3f), 4f, 0f, Mathf.Pi / 2f);

        Assert.Equal(RoadGeometryKind.CircularArc, arc.Kind);
        AssertVectorApproximatelyEqual(new Vector2(6f, 3f), arc.Start);
        AssertVectorApproximatelyEqual(new Vector2(2f, 7f), arc.End);
        AssertVectorApproximatelyEqual(new Vector2(2f + 2f * Mathf.Sqrt(2f), 3f + 2f * Mathf.Sqrt(2f)), arc.GetPosition(0.5f));
        AssertVectorApproximatelyEqual(Vector2.Down, arc.GetUnitTangent(0f));
        Assert.Equal(2f * Mathf.Pi, arc.Length, 5);
        AssertRectApproximatelyEqual(new Rect2(2f, 3f, 4f, 4f), arc.Bounds);
    }

    [Fact]
    public void ClockwiseArcUsesSignedSweepAndIncludesAxisExtrema()
    {
        var arc = new CircularArcRoadGeometrySegment(Vector2.Zero, 3f, Mathf.Pi / 2f, -Mathf.Pi);

        AssertVectorApproximatelyEqual(new Vector2(0f, 3f), arc.Start);
        AssertVectorApproximatelyEqual(new Vector2(0f, -3f), arc.End);
        AssertVectorApproximatelyEqual(Vector2.Right, arc.GetUnitTangent(0f));
        AssertRectApproximatelyEqual(new Rect2(0f, -3f, 3f, 6f), arc.Bounds);
    }

    [Fact]
    public void FullRevolutionBoundsCoverTheCircle()
    {
        var arc = new CircularArcRoadGeometrySegment(new Vector2(-1f, 2f), 5f, 0.37f, Mathf.Tau);

        AssertRectApproximatelyEqual(new Rect2(-6f, -3f, 10f, 10f), arc.Bounds);
        Assert.Equal(5f * Mathf.Tau, arc.Length, 5);
    }

    [Fact]
    public void SplitPreservesCircleDirectionAndParameterization()
    {
        var source = new CircularArcRoadGeometrySegment(new Vector2(4f, -2f), 7f, 0.4f, -4.2f);
        const float splitParameter = 0.3f;

        RoadGeometrySplit split = source.Split(splitParameter);

        var before = Assert.IsType<CircularArcRoadGeometrySegment>(split.Before);
        var after = Assert.IsType<CircularArcRoadGeometrySegment>(split.After);
        Assert.Equal(source.Center, before.Center);
        Assert.Equal(source.Radius, after.Radius);
        Assert.Equal(source.SweepAngle * splitParameter, before.SweepAngle);
        Assert.Equal(source.SweepAngle * (1f - splitParameter), after.SweepAngle, 5);
        foreach (float local in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            AssertVectorApproximatelyEqual(source.GetPosition(local * splitParameter), before.GetPosition(local));
            AssertVectorApproximatelyEqual(
                source.GetPosition(splitParameter + local * (1f - splitParameter)),
                after.GetPosition(local));
        }
        Assert.Equal(source.Length, before.Length + after.Length, 4);
    }

    [Fact]
    public void JsonRoundTripPreservesNativeArcParameters()
    {
        var source = new CircularArcRoadGeometrySegment(new Vector2(-3f, 6f), 8f, -0.7f, 2.4f);

        string json = RoadGeometrySerializer.Serialize(source);
        RoadGeometryDeserializationResult result = RoadGeometrySerializer.Deserialize(json);

        Assert.True(result.Success);
        var restored = Assert.IsType<CircularArcRoadGeometrySegment>(result.Geometry);
        Assert.Equal(source.Center, restored.Center);
        Assert.Equal(source.Radius, restored.Radius);
        Assert.Equal(source.StartAngle, restored.StartAngle);
        Assert.Equal(source.SweepAngle, restored.SweepAngle);
        Assert.Contains("\"kind\": \"circularArc\"", json);
    }

    [Fact]
    public void MissingOrMixedParametersAreRejectedWithoutGeometry()
    {
        var missing = ValidData();
        missing.Radius = null;
        var mixed = ValidData();
        mixed.Start = new RoadGeometryPointData(Vector2.Zero);

        AssertFailure(missing, RoadGeometryDataError.MissingRequiredParameter);
        AssertFailure(mixed, RoadGeometryDataError.UnexpectedParameter);
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    public void QueryOutsideParameterDomainIsRejected(float parameter)
    {
        var arc = new CircularArcRoadGeometrySegment(Vector2.Zero, 2f, 0f, 1f);

        Assert.Throws<ArgumentOutOfRangeException>(() => arc.GetPosition(parameter));
        Assert.Throws<ArgumentOutOfRangeException>(() => arc.GetUnitTangent(parameter));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    public void SplitOutsideOpenParameterDomainIsRejected(float parameter)
    {
        var arc = new CircularArcRoadGeometrySegment(Vector2.Zero, 2f, 0f, 1f);

        Assert.Throws<ArgumentOutOfRangeException>(() => arc.Split(parameter));
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(-1f, 1f)]
    [InlineData(1f, 0f)]
    [InlineData(1f, 0.0000001f)]
    [InlineData(1f, 6.4f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(1f, float.NaN)]
    public void InvalidRadiusOrSweepIsRejected(float radius, float sweep)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CircularArcRoadGeometrySegment(Vector2.Zero, radius, 0f, sweep));
    }

    private static RoadGeometryData ValidData() => new()
    {
        Version = RoadGeometryData.CurrentVersion,
        Kind = RoadGeometryData.CircularArcKind,
        Center = new RoadGeometryPointData(Vector2.Zero),
        Radius = 4f,
        StartAngle = 0f,
        SweepAngle = 1f,
    };

    private static void AssertFailure(RoadGeometryData data, RoadGeometryDataError expectedError)
    {
        RoadGeometryDeserializationResult result = RoadGeometrySerializer.FromData(data);
        Assert.False(result.Success);
        Assert.Null(result.Geometry);
        Assert.Equal(expectedError, result.Error);
    }

    private static void AssertVectorApproximatelyEqual(Vector2 expected, Vector2 actual) =>
        Assert.InRange(actual.DistanceTo(expected), 0f, 2e-5f);

    private static void AssertRectApproximatelyEqual(Rect2 expected, Rect2 actual)
    {
        AssertVectorApproximatelyEqual(expected.Position, actual.Position);
        AssertVectorApproximatelyEqual(expected.Size, actual.Size);
    }
}
