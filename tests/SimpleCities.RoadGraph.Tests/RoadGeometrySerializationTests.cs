using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGeometrySerializationTests
{
    [Fact]
    public void Line_JsonRoundTripPreservesGeometrySemantics()
    {
        var source = new LineRoadGeometrySegment(new Vector2(-3.5f, 2.25f), new Vector2(8f, -4f));

        string json = RoadGeometrySerializer.Serialize(source);
        RoadGeometryDeserializationResult result = RoadGeometrySerializer.Deserialize(json);

        Assert.True(result.Success);
        var restored = Assert.IsType<LineRoadGeometrySegment>(result.Geometry);
        AssertGeometryEquivalent(source, restored);
        Assert.Contains("\"version\": 1", json);
        Assert.Contains("\"kind\": \"line\"", json);
    }

    [Fact]
    public void CubicBezier_JsonRoundTripPreservesNativeControlsAndGeometrySemantics()
    {
        var source = new CubicBezierRoadGeometrySegment(
            new Vector2(-2f, 1f),
            new Vector2(3f, 9f),
            new Vector2(7f, -5f),
            new Vector2(12f, 4f));

        string json = RoadGeometrySerializer.Serialize(source);
        RoadGeometryDeserializationResult result = RoadGeometrySerializer.Deserialize(json);

        Assert.True(result.Success);
        var restored = Assert.IsType<CubicBezierRoadGeometrySegment>(result.Geometry);
        Assert.Equal(source.Control1, restored.Control1);
        Assert.Equal(source.Control2, restored.Control2);
        AssertGeometryEquivalent(source, restored);
        Assert.Contains("\"kind\": \"cubicBezier\"", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPayload_IsRejectedWithoutGeometry(string json)
    {
        AssertFailure(RoadGeometrySerializer.Deserialize(json), RoadGeometryDataError.EmptyPayload);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"version\":1,\"kind\":7}")]
    public void MalformedPayload_IsRejectedWithoutGeometry(string json)
    {
        AssertFailure(RoadGeometrySerializer.Deserialize(json), RoadGeometryDataError.MalformedJson);
    }

    [Fact]
    public void MissingOrUnsupportedVersion_IsRejectedWithoutGeometry()
    {
        var missing = ValidLineData();
        missing.Version = null;
        var unsupported = ValidLineData();
        unsupported.Version = RoadGeometryData.CurrentVersion + 1;

        AssertFailure(RoadGeometrySerializer.FromData(missing), RoadGeometryDataError.UnsupportedVersion);
        AssertFailure(RoadGeometrySerializer.FromData(unsupported), RoadGeometryDataError.UnsupportedVersion);
    }

    [Fact]
    public void MissingOrUnknownKind_IsRejectedWithoutGeometry()
    {
        var missing = ValidLineData();
        missing.Kind = null;
        var unknown = ValidLineData();
        unknown.Kind = "quadraticBezier";

        AssertFailure(RoadGeometrySerializer.FromData(missing), RoadGeometryDataError.MissingGeometryKind);
        AssertFailure(RoadGeometrySerializer.FromData(unknown), RoadGeometryDataError.UnknownGeometryKind);
    }

    [Fact]
    public void MissingCoordinate_IsRejectedWithoutGeometry()
    {
        var data = ValidLineData();
        data.End!.Y = null;

        AssertFailure(RoadGeometrySerializer.FromData(data), RoadGeometryDataError.MissingRequiredParameter);
    }

    [Fact]
    public void NonFiniteCoordinate_IsRejectedWithoutGeometry()
    {
        var data = ValidLineData();
        data.Start!.X = float.PositiveInfinity;

        AssertFailure(RoadGeometrySerializer.FromData(data), RoadGeometryDataError.NonFiniteCoordinate);
    }

    [Fact]
    public void LineWithBezierControls_IsRejectedAsInconsistent()
    {
        var data = ValidLineData();
        data.Control1 = Point(1f, 2f);

        AssertFailure(RoadGeometrySerializer.FromData(data), RoadGeometryDataError.UnexpectedParameter);
    }

    [Fact]
    public void CubicBezierWithoutBothControls_IsRejectedWithoutGeometry()
    {
        var data = ValidCubicBezierData();
        data.Control2 = null;

        AssertFailure(RoadGeometrySerializer.FromData(data), RoadGeometryDataError.MissingRequiredParameter);
    }

    [Theory]
    [InlineData("{\"version\":1,\"kind\":\"line\",\"start\":{\"x\":0,\"y\":0},\"end\":{\"x\":1,\"y\":0},\"radius\":4}")]
    [InlineData("{\"version\":1,\"kind\":\"line\",\"start\":{\"x\":0,\"y\":0,\"z\":2},\"end\":{\"x\":1,\"y\":0}}")]
    public void UnknownFields_AreRejectedAsInconsistent(string json)
    {
        AssertFailure(RoadGeometrySerializer.Deserialize(json), RoadGeometryDataError.UnexpectedParameter);
    }

    [Fact]
    public void DegenerateGeometry_IsRejectedWithoutGeometry()
    {
        var line = ValidLineData();
        line.End = Point(0f, 0f);
        var cubic = ValidCubicBezierData();
        cubic.Control1 = Point(0f, 0f);
        cubic.Control2 = Point(0f, 0f);
        cubic.End = Point(0f, 0f);

        AssertFailure(RoadGeometrySerializer.FromData(line), RoadGeometryDataError.InvalidGeometry);
        AssertFailure(RoadGeometrySerializer.FromData(cubic), RoadGeometryDataError.InvalidGeometry);
    }

    private static RoadGeometryData ValidLineData() => new()
    {
        Version = RoadGeometryData.CurrentVersion,
        Kind = RoadGeometryData.LineKind,
        Start = Point(0f, 0f),
        End = Point(4f, 3f),
    };

    private static RoadGeometryData ValidCubicBezierData() => new()
    {
        Version = RoadGeometryData.CurrentVersion,
        Kind = RoadGeometryData.CubicBezierKind,
        Start = Point(0f, 0f),
        Control1 = Point(1f, 2f),
        Control2 = Point(3f, 2f),
        End = Point(4f, 0f),
    };

    private static RoadGeometryPointData Point(float x, float y) => new()
    {
        X = x,
        Y = y,
    };

    private static void AssertGeometryEquivalent(RoadGeometrySegment expected, RoadGeometrySegment actual)
    {
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Start, actual.Start);
        Assert.Equal(expected.End, actual.End);
        Assert.Equal(expected.Bounds, actual.Bounds);
        Assert.Equal(expected.Length, actual.Length);

        foreach (float parameter in new[] { 0f, 0.2f, 0.5f, 0.8f, 1f })
        {
            Assert.Equal(expected.GetPosition(parameter), actual.GetPosition(parameter));
            Assert.Equal(expected.GetUnitTangent(parameter), actual.GetUnitTangent(parameter));
        }
    }

    private static void AssertFailure(
        RoadGeometryDeserializationResult result,
        RoadGeometryDataError expectedError)
    {
        Assert.False(result.Success);
        Assert.Null(result.Geometry);
        Assert.Equal(expectedError, result.Error);
    }
}
