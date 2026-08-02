using Godot;

namespace SimpleCities.Tests;

public sealed class RationalQuadraticRoadGeometrySegmentTests
{
    [Fact]
    public void QuarterCirclePreservesConicGeometry()
    {
        float middleWeight = Mathf.Sqrt(0.5f);
        var segment = new RationalQuadraticRoadGeometrySegment(
            Vector2.Right, 1f, Vector2.One, middleWeight, Vector2.Down, 1f);

        Assert.Equal(RoadGeometryKind.RationalQuadratic, segment.Kind);
        AssertVectorApproximatelyEqual(Vector2.Right, segment.Start);
        AssertVectorApproximatelyEqual(Vector2.Down, segment.End);
        AssertVectorApproximatelyEqual(new Vector2(middleWeight, middleWeight), segment.GetPosition(0.5f));
        AssertVectorApproximatelyEqual(Vector2.Down, segment.GetUnitTangent(0f));
        AssertVectorApproximatelyEqual(Vector2.Left, segment.GetUnitTangent(1f));
        Assert.Equal(Mathf.Pi / 2f, segment.Length, 3);
        AssertRectApproximatelyEqual(new Rect2(0f, 0f, 1f, 1f), segment.Bounds);
    }

    [Fact]
    public void EqualWeightsMatchPolynomialQuadraticBezier()
    {
        var segment = new RationalQuadraticRoadGeometrySegment(
            Vector2.Zero, 1f, new Vector2(3f, 6f), 1f, new Vector2(8f, 0f), 1f);

        AssertVectorApproximatelyEqual(new Vector2(3.5f, 3f), segment.GetPosition(0.5f));
        Assert.True(segment.Length > segment.Start.DistanceTo(segment.End));
        AssertRectApproximatelyEqual(new Rect2(0f, 0f, 8f, 3f), segment.Bounds);
    }

    [Fact]
    public void ScalingAllWeightsLeavesGeometryUnchanged()
    {
        var source = new RationalQuadraticRoadGeometrySegment(
            new Vector2(-2f, 1f), 1f, new Vector2(4f, 7f), 0.6f, new Vector2(9f, -1f), 1.4f);
        var scaled = new RationalQuadraticRoadGeometrySegment(
            source.Start, 5f, source.Control, 3f, source.End, 7f);

        foreach (float parameter in new[] { 0f, 0.2f, 0.5f, 0.8f, 1f })
        {
            AssertVectorApproximatelyEqual(source.GetPosition(parameter), scaled.GetPosition(parameter));
            AssertVectorApproximatelyEqual(source.GetUnitTangent(parameter), scaled.GetUnitTangent(parameter));
        }
        Assert.Equal(source.Length, scaled.Length, 4);
    }

    [Fact]
    public void SplitPreservesRationalTypeAndParameterization()
    {
        var source = new RationalQuadraticRoadGeometrySegment(
            new Vector2(-4f, 2f), 1.2f,
            new Vector2(3f, 11f), 0.45f,
            new Vector2(12f, -3f), 2f);
        const float splitParameter = 0.3f;

        RoadGeometrySplit split = source.Split(splitParameter);

        var before = Assert.IsType<RationalQuadraticRoadGeometrySegment>(split.Before);
        var after = Assert.IsType<RationalQuadraticRoadGeometrySegment>(split.After);
        AssertVectorApproximatelyEqual(source.GetPosition(splitParameter), before.End);
        AssertVectorApproximatelyEqual(before.End, after.Start);
        Assert.True(before.StartWeight > 0f && before.ControlWeight > 0f && before.EndWeight > 0f);
        Assert.True(after.StartWeight > 0f && after.ControlWeight > 0f && after.EndWeight > 0f);
        foreach (float local in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            AssertVectorApproximatelyEqual(source.GetPosition(local * splitParameter), before.GetPosition(local));
            AssertVectorApproximatelyEqual(
                source.GetPosition(splitParameter + local * (1f - splitParameter)),
                after.GetPosition(local));
        }
        Assert.Equal(source.Length, before.Length + after.Length, 3);
    }

    [Fact]
    public void JsonRoundTripPreservesHomogeneousControls()
    {
        var source = new RationalQuadraticRoadGeometrySegment(
            new Vector2(-3f, 4f), 0.8f,
            new Vector2(2f, 9f), 1.7f,
            new Vector2(10f, -2f), 1.1f);

        string json = RoadGeometrySerializer.Serialize(source);
        RoadGeometryDeserializationResult result = RoadGeometrySerializer.Deserialize(json);

        Assert.True(result.Success);
        var restored = Assert.IsType<RationalQuadraticRoadGeometrySegment>(result.Geometry);
        Assert.Equal(source.Start, restored.Start);
        Assert.Equal(source.StartWeight, restored.StartWeight);
        Assert.Equal(source.Control, restored.Control);
        Assert.Equal(source.ControlWeight, restored.ControlWeight);
        Assert.Equal(source.End, restored.End);
        Assert.Equal(source.EndWeight, restored.EndWeight);
        Assert.Contains("\"kind\": \"rationalQuadratic\"", json);
    }

    [Fact]
    public void MissingOrMixedParametersAreRejectedWithoutGeometry()
    {
        var missing = ValidData();
        missing.ControlWeight = null;
        var mixed = ValidData();
        mixed.Control2 = new RoadGeometryPointData(Vector2.One);

        AssertFailure(missing, RoadGeometryDataError.MissingRequiredParameter);
        AssertFailure(mixed, RoadGeometryDataError.UnexpectedParameter);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NaN)]
    public void InvalidWeightsAreRejected(float weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RationalQuadraticRoadGeometrySegment(
                Vector2.Zero, 1f, Vector2.One, weight, Vector2.Right * 2f, 1f));
    }

    [Fact]
    public void ConstantGeometryIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new RationalQuadraticRoadGeometrySegment(
                Vector2.One, 1f, Vector2.One, 2f, Vector2.One, 3f));
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    public void QueryOutsideParameterDomainIsRejected(float parameter)
    {
        var segment = new RationalQuadraticRoadGeometrySegment(
            Vector2.Zero, 1f, Vector2.One, 1f, Vector2.Right * 2f, 1f);

        Assert.Throws<ArgumentOutOfRangeException>(() => segment.GetPosition(parameter));
        Assert.Throws<ArgumentOutOfRangeException>(() => segment.GetUnitTangent(parameter));
    }

    private static RoadGeometryData ValidData() => new()
    {
        Version = RoadGeometryData.CurrentVersion,
        Kind = RoadGeometryData.RationalQuadraticKind,
        Start = new RoadGeometryPointData(Vector2.Zero),
        StartWeight = 1f,
        Control1 = new RoadGeometryPointData(Vector2.One),
        ControlWeight = 0.7f,
        End = new RoadGeometryPointData(Vector2.Right * 2f),
        EndWeight = 1f,
    };

    private static void AssertFailure(RoadGeometryData data, RoadGeometryDataError expectedError)
    {
        RoadGeometryDeserializationResult result = RoadGeometrySerializer.FromData(data);
        Assert.False(result.Success);
        Assert.Null(result.Geometry);
        Assert.Equal(expectedError, result.Error);
    }

    private static void AssertVectorApproximatelyEqual(Vector2 expected, Vector2 actual) =>
        Assert.InRange(actual.DistanceTo(expected), 0f, 5e-5f);

    private static void AssertRectApproximatelyEqual(Rect2 expected, Rect2 actual)
    {
        AssertVectorApproximatelyEqual(expected.Position, actual.Position);
        AssertVectorApproximatelyEqual(expected.Size, actual.Size);
    }
}
