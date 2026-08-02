using Godot;

namespace SimpleCities.Tests;

public sealed class ClothoidRoadGeometrySegmentTests
{
    [Fact]
    public void ExposesLinearCurvatureAndArcLengthContract()
    {
        var segment = new ClothoidRoadGeometrySegment(
            new Vector2(2f, -3f), 0.25f, -0.04f, 0.12f, 20f);

        Assert.Equal(RoadGeometryKind.Clothoid, segment.Kind);
        Assert.Equal(20f, segment.Length);
        Assert.Equal(-0.04f, segment.GetCurvature(0f));
        Assert.Equal(0.04f, segment.GetCurvature(0.5f), 6);
        Assert.Equal(0.12f, segment.GetCurvature(1f));
        Assert.Equal(segment.Start, segment.GetPosition(0f));
        Assert.Equal(segment.End, segment.GetPosition(1f));
        AssertVectorApproximatelyEqual(
            new Vector2(Mathf.Cos(0.25f), Mathf.Sin(0.25f)),
            segment.GetUnitTangent(0f));
        Assert.True(segment.Bounds.HasPoint(segment.GetPosition(0.25f)));
        Assert.True(segment.Bounds.HasPoint(segment.GetPosition(0.75f)));
    }

    [Fact]
    public void ZeroCurvatureIsAnExactLine()
    {
        var segment = new ClothoidRoadGeometrySegment(Vector2.One, Mathf.Pi / 2f, 0f, 0f, 8f);

        AssertVectorApproximatelyEqual(new Vector2(1f, 5f), segment.GetPosition(0.5f));
        AssertVectorApproximatelyEqual(new Vector2(1f, 9f), segment.End);
        AssertVectorApproximatelyEqual(Vector2.Down, segment.GetUnitTangent(0.75f));
    }

    [Fact]
    public void ConstantCurvatureMatchesEquivalentCircularArc()
    {
        const float curvature = 0.2f;
        const float length = 7f;
        var clothoid = new ClothoidRoadGeometrySegment(Vector2.Zero, 0f, curvature, curvature, length);
        var arc = new CircularArcRoadGeometrySegment(
            new Vector2(0f, 1f / curvature), 1f / curvature, -Mathf.Pi / 2f, curvature * length);

        foreach (float parameter in new[] { 0f, 0.2f, 0.5f, 0.8f, 1f })
        {
            AssertVectorApproximatelyEqual(arc.GetPosition(parameter), clothoid.GetPosition(parameter));
            AssertVectorApproximatelyEqual(arc.GetUnitTangent(parameter), clothoid.GetUnitTangent(parameter));
        }
    }

    [Fact]
    public void SmallCurvatureRateAccumulatesOverLongArcLength()
    {
        var segment = new ClothoidRoadGeometrySegment(Vector2.Zero, 0f, 0f, 1e-6f, 100f);

        Assert.True(segment.End.Y > 0.001f);
        AssertVectorApproximatelyEqual(
            new Vector2(Mathf.Cos(0.00005f), Mathf.Sin(0.00005f)),
            segment.GetUnitTangent(1f));
    }

    [Fact]
    public void SplitPreservesCurvatureHeadingAndPositionSemantics()
    {
        var source = new ClothoidRoadGeometrySegment(
            new Vector2(-4f, 3f), -0.3f, -0.08f, 0.18f, 24f);
        const float splitParameter = 0.4f;

        RoadGeometrySplit split = source.Split(splitParameter);

        var before = Assert.IsType<ClothoidRoadGeometrySegment>(split.Before);
        var after = Assert.IsType<ClothoidRoadGeometrySegment>(split.After);
        Assert.Equal(source.GetCurvature(splitParameter), before.EndCurvature, 5);
        Assert.Equal(before.EndCurvature, after.StartCurvature);
        AssertVectorApproximatelyEqual(source.GetPosition(splitParameter), before.End);
        AssertVectorApproximatelyEqual(before.End, after.Start);
        foreach (float local in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            AssertVectorApproximatelyEqual(source.GetPosition(local * splitParameter), before.GetPosition(local));
            AssertVectorApproximatelyEqual(
                source.GetPosition(splitParameter + local * (1f - splitParameter)),
                after.GetPosition(local));
        }
        Assert.Equal(source.Length, before.Length + after.Length, 5);
    }

    [Fact]
    public void JsonRoundTripPreservesClothoidParametersAndGeometry()
    {
        var source = new ClothoidRoadGeometrySegment(
            new Vector2(3f, -7f), 0.6f, 0.01f, 0.09f, 30f);

        string json = RoadGeometrySerializer.Serialize(source);
        RoadGeometryDeserializationResult result = RoadGeometrySerializer.Deserialize(json);

        Assert.True(result.Success);
        var restored = Assert.IsType<ClothoidRoadGeometrySegment>(result.Geometry);
        Assert.Equal(source.Start, restored.Start);
        Assert.Equal(source.StartHeading, restored.StartHeading);
        Assert.Equal(source.StartCurvature, restored.StartCurvature);
        Assert.Equal(source.EndCurvature, restored.EndCurvature);
        Assert.Equal(source.ArcLength, restored.ArcLength);
        AssertVectorApproximatelyEqual(source.End, restored.End);
        Assert.Contains("\"kind\": \"clothoid\"", json);
    }

    [Fact]
    public void MissingOrMixedParametersAreRejectedWithoutGeometry()
    {
        var missing = ValidData();
        missing.ArcLength = null;
        var mixed = ValidData();
        mixed.Radius = 3f;

        AssertFailure(missing, RoadGeometryDataError.MissingRequiredParameter);
        AssertFailure(mixed, RoadGeometryDataError.UnexpectedParameter);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidArcLengthIsRejected(float length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClothoidRoadGeometrySegment(Vector2.Zero, 0f, 0f, 0.1f, length));
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    public void QueryOutsideParameterDomainIsRejected(float parameter)
    {
        var segment = new ClothoidRoadGeometrySegment(Vector2.Zero, 0f, 0f, 0.1f, 10f);

        Assert.Throws<ArgumentOutOfRangeException>(() => segment.GetPosition(parameter));
        Assert.Throws<ArgumentOutOfRangeException>(() => segment.GetUnitTangent(parameter));
        Assert.Throws<ArgumentOutOfRangeException>(() => segment.GetCurvature(parameter));
    }

    private static RoadGeometryData ValidData() => new()
    {
        Version = RoadGeometryData.CurrentVersion,
        Kind = RoadGeometryData.ClothoidKind,
        Start = new RoadGeometryPointData(Vector2.Zero),
        StartHeading = 0f,
        StartCurvature = 0f,
        EndCurvature = 0.1f,
        ArcLength = 12f,
    };

    private static void AssertFailure(RoadGeometryData data, RoadGeometryDataError expectedError)
    {
        RoadGeometryDeserializationResult result = RoadGeometrySerializer.FromData(data);
        Assert.False(result.Success);
        Assert.Null(result.Geometry);
        Assert.Equal(expectedError, result.Error);
    }

    private static void AssertVectorApproximatelyEqual(Vector2 expected, Vector2 actual) =>
        Assert.InRange(actual.DistanceTo(expected), 0f, 5e-4f);
}
