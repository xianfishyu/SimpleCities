using Godot;

namespace SimpleCities.Tests;

public sealed class CubicHermiteRoadGeometrySegmentTests
{
    [Fact]
    public void ExposesNativeParametersAndEndpointTangents()
    {
        var segment = new CubicHermiteRoadGeometrySegment(
            new Vector2(-2f, 1f),
            new Vector2(6f, 3f),
            new Vector2(8f, 5f),
            new Vector2(3f, -6f));

        Assert.Equal(RoadGeometryKind.CubicHermiteSpline, segment.Kind);
        Assert.Equal(new Vector2(-2f, 1f), segment.Start);
        Assert.Equal(new Vector2(6f, 3f), segment.StartTangent);
        Assert.Equal(new Vector2(8f, 5f), segment.End);
        Assert.Equal(new Vector2(3f, -6f), segment.EndTangent);
        AssertVectorApproximatelyEqual(segment.StartTangent.Normalized(), segment.GetUnitTangent(0f));
        AssertVectorApproximatelyEqual(segment.EndTangent.Normalized(), segment.GetUnitTangent(1f));
    }

    [Fact]
    public void PositionUsesCubicHermiteBasis()
    {
        var segment = new CubicHermiteRoadGeometrySegment(
            Vector2.Zero,
            new Vector2(4f, 8f),
            new Vector2(8f, 0f),
            new Vector2(4f, -8f));

        Assert.Equal(Vector2.Zero, segment.GetPosition(0f));
        AssertVectorApproximatelyEqual(new Vector2(4f, 2f), segment.GetPosition(0.5f));
        Assert.Equal(new Vector2(8f, 0f), segment.GetPosition(1f));
        Assert.Equal(new Rect2(0f, 0f, 8f, 2f), segment.Bounds);
        Assert.True(segment.Length > segment.Start.DistanceTo(segment.End));
    }

    [Fact]
    public void SplitPreservesHermiteTypeAndOriginalParameterization()
    {
        var source = new CubicHermiteRoadGeometrySegment(
            new Vector2(-3f, 2f),
            new Vector2(9f, 12f),
            new Vector2(11f, -1f),
            new Vector2(6f, -9f));
        const float splitParameter = 0.35f;

        RoadGeometrySplit split = source.Split(splitParameter);

        var before = Assert.IsType<CubicHermiteRoadGeometrySegment>(split.Before);
        var after = Assert.IsType<CubicHermiteRoadGeometrySegment>(split.After);
        AssertVectorApproximatelyEqual(source.GetPosition(splitParameter), before.End);
        AssertVectorApproximatelyEqual(before.End, after.Start);
        AssertVectorApproximatelyEqual(source.StartTangent * splitParameter, before.StartTangent);
        AssertVectorApproximatelyEqual(source.EndTangent * (1f - splitParameter), after.EndTangent);
        foreach (float local in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            AssertVectorApproximatelyEqual(
                source.GetPosition(local * splitParameter),
                before.GetPosition(local));
            AssertVectorApproximatelyEqual(
                source.GetPosition(splitParameter + local * (1f - splitParameter)),
                after.GetPosition(local));
        }
        Assert.Equal(source.Length, before.Length + after.Length, 3);
    }

    [Fact]
    public void JsonRoundTripPreservesSplineParametersAndGeometry()
    {
        var source = new CubicHermiteRoadGeometrySegment(
            new Vector2(-5f, 2f),
            new Vector2(7f, 4f),
            new Vector2(9f, 3f),
            new Vector2(5f, -8f));

        string json = RoadGeometrySerializer.Serialize(source);
        RoadGeometryDeserializationResult result = RoadGeometrySerializer.Deserialize(json);

        Assert.True(result.Success);
        var restored = Assert.IsType<CubicHermiteRoadGeometrySegment>(result.Geometry);
        Assert.Equal(source.StartTangent, restored.StartTangent);
        Assert.Equal(source.EndTangent, restored.EndTangent);
        Assert.Equal(source.Bounds, restored.Bounds);
        Assert.Equal(source.Length, restored.Length);
        Assert.Contains("\"kind\": \"cubicHermite\"", json);
    }

    [Fact]
    public void MissingTangentOrBezierControlsAreRejectedWithoutGeometry()
    {
        var missingTangent = ValidData();
        missingTangent.EndTangent = null;
        var mixedControls = ValidData();
        mixedControls.Control1 = Point(1f, 1f);

        AssertFailure(missingTangent, RoadGeometryDataError.MissingRequiredParameter);
        AssertFailure(mixedControls, RoadGeometryDataError.UnexpectedParameter);
    }

    [Theory]
    [MemberData(nameof(InvalidParameters))]
    public void InvalidParametersAreRejected(
        Vector2 start,
        Vector2 startTangent,
        Vector2 end,
        Vector2 endTangent)
    {
        Assert.Throws<ArgumentException>(() =>
            new CubicHermiteRoadGeometrySegment(start, startTangent, end, endTangent));
    }

    public static TheoryData<Vector2, Vector2, Vector2, Vector2> InvalidParameters => new()
    {
        { new Vector2(float.NaN, 0f), Vector2.Right, Vector2.One, Vector2.Up },
        { Vector2.Zero, new Vector2(float.PositiveInfinity, 0f), Vector2.One, Vector2.Up },
        { Vector2.Zero, Vector2.Right, new Vector2(0f, float.NegativeInfinity), Vector2.Up },
        { Vector2.Zero, Vector2.Right, Vector2.One, new Vector2(float.NaN, 0f) },
        { Vector2.One, Vector2.Zero, Vector2.One, Vector2.Zero },
    };

    private static RoadGeometryData ValidData() => new()
    {
        Version = RoadGeometryData.CurrentVersion,
        Kind = RoadGeometryData.CubicHermiteKind,
        Start = Point(0f, 0f),
        StartTangent = Point(2f, 3f),
        End = Point(6f, 1f),
        EndTangent = Point(4f, -2f),
    };

    private static RoadGeometryPointData Point(float x, float y) => new() { X = x, Y = y };

    private static void AssertFailure(RoadGeometryData data, RoadGeometryDataError expectedError)
    {
        RoadGeometryDeserializationResult result = RoadGeometrySerializer.FromData(data);
        Assert.False(result.Success);
        Assert.Null(result.Geometry);
        Assert.Equal(expectedError, result.Error);
    }

    private static void AssertVectorApproximatelyEqual(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(actual.DistanceTo(expected), 0f, 2e-5f);
    }
}
