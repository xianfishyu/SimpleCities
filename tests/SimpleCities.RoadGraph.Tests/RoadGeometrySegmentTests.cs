using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGeometrySegmentTests
{
    [Fact]
    public void Line_ExposesExactGeometryContract()
    {
        var segment = new LineRoadGeometrySegment(new Vector2(-2, 1), new Vector2(4, 9));

        Assert.Equal(RoadGeometryKind.Line, segment.Kind);
        Assert.Equal(new Vector2(-2, 1), segment.Start);
        Assert.Equal(new Vector2(4, 9), segment.End);
        Assert.Equal(10f, segment.Length);
        Assert.Equal(new Rect2(-2, 1, 6, 8), segment.Bounds);
        Assert.Equal(segment.Start, segment.GetPosition(RoadGeometrySegment.ParameterStart));
        Assert.Equal(new Vector2(1, 5), segment.GetPosition(0.5f));
        Assert.Equal(segment.End, segment.GetPosition(RoadGeometrySegment.ParameterEnd));
        Assert.Equal(new Vector2(0.6f, 0.8f), segment.GetUnitTangent(0.25f));
    }

    [Fact]
    public void Line_ReversedEndpoints_KeepPositiveBoundsAndOppositeTangent()
    {
        var segment = new LineRoadGeometrySegment(new Vector2(4, 9), new Vector2(-2, 1));

        Assert.Equal(new Rect2(-2, 1, 6, 8), segment.Bounds);
        Assert.Equal(new Vector2(-0.6f, -0.8f), segment.GetUnitTangent(0.75f));
    }

    [Fact]
    public void Line_SplitPreservesJoinAndTotalLength()
    {
        var segment = new LineRoadGeometrySegment(new Vector2(-5, 2), new Vector2(15, 12));

        RoadGeometrySplit split = segment.Split(0.25f);

        var before = Assert.IsType<LineRoadGeometrySegment>(split.Before);
        var after = Assert.IsType<LineRoadGeometrySegment>(split.After);
        Assert.Equal(segment.Start, before.Start);
        Assert.Equal(segment.GetPosition(0.25f), before.End);
        Assert.Equal(before.End, after.Start);
        Assert.Equal(segment.End, after.End);
        Assert.Equal(segment.Length, before.Length + after.Length, 5);
        Assert.Equal(segment.GetUnitTangent(0f), before.GetUnitTangent(1f));
        Assert.Equal(segment.GetUnitTangent(1f), after.GetUnitTangent(0f));
    }

    [Theory]
    [MemberData(nameof(InvalidEndpoints))]
    public void Line_InvalidEndpoints_AreRejected(Vector2 start, Vector2 end)
    {
        Assert.Throws<ArgumentException>(() => new LineRoadGeometrySegment(start, end));
    }

    public static TheoryData<Vector2, Vector2> InvalidEndpoints => new()
    {
        { new Vector2(float.NaN, 0), Vector2.One },
        { Vector2.Zero, new Vector2(float.PositiveInfinity, 0) },
        { new Vector2(3, -7), new Vector2(3, -7) },
    };

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Line_QueryOutsideParameterDomain_IsRejected(float parameter)
    {
        var segment = new LineRoadGeometrySegment(Vector2.Zero, Vector2.One);

        Assert.Throws<ArgumentOutOfRangeException>(() => segment.GetPosition(parameter));
        Assert.Throws<ArgumentOutOfRangeException>(() => segment.GetUnitTangent(parameter));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    public void Line_SplitOutsideOpenParameterDomain_IsRejected(float parameter)
    {
        var segment = new LineRoadGeometrySegment(Vector2.Zero, Vector2.One);

        Assert.Throws<ArgumentOutOfRangeException>(() => segment.Split(parameter));
    }

    [Fact]
    public void CubicBezier_PreservesControlsAndComputesExactQueries()
    {
        var segment = new CubicBezierRoadGeometrySegment(
            Vector2.Zero,
            new Vector2(0, 2),
            new Vector2(2, 2),
            new Vector2(2, 0));

        Assert.Equal(RoadGeometryKind.CubicBezier, segment.Kind);
        Assert.Equal(Vector2.Zero, segment.Start);
        Assert.Equal(new Vector2(0, 2), segment.Control1);
        Assert.Equal(new Vector2(2, 2), segment.Control2);
        Assert.Equal(new Vector2(2, 0), segment.End);
        Assert.Equal(new Vector2(1, 1.5f), segment.GetPosition(0.5f));
        Assert.Equal(Vector2.Down, segment.GetUnitTangent(0f));
        Assert.Equal(Vector2.Right, segment.GetUnitTangent(0.5f));
        Assert.Equal(Vector2.Up, segment.GetUnitTangent(1f));
        Assert.Equal(new Rect2(0, 0, 2, 1.5f), segment.Bounds);
        Assert.InRange(segment.Length, segment.Start.DistanceTo(segment.End), 6f);
    }

    [Fact]
    public void CubicBezier_CollinearControlsHaveExactLineLengthAndBounds()
    {
        var segment = new CubicBezierRoadGeometrySegment(
            Vector2.Zero,
            Vector2.Right,
            2f * Vector2.Right,
            3f * Vector2.Right);

        Assert.Equal(3f, segment.Length);
        Assert.Equal(new Rect2(0, 0, 3, 0), segment.Bounds);
        Assert.Equal(new Vector2(2.25f, 0), segment.GetPosition(0.75f));
        Assert.Equal(Vector2.Right, segment.GetUnitTangent(0.75f));
    }

    [Fact]
    public void CubicBezier_QuadraticDerivativeRootsDefineInteriorBounds()
    {
        var segment = new CubicBezierRoadGeometrySegment(
            Vector2.Zero,
            new Vector2(2, 0),
            new Vector2(-2, 0),
            Vector2.Zero);
        float firstExtremum = (3f - Mathf.Sqrt(3f)) / 6f;
        float maximumX = segment.GetPosition(firstExtremum).X;

        Assert.Equal(-maximumX, segment.Bounds.Position.X, 5);
        Assert.Equal(2f * maximumX, segment.Bounds.Size.X, 5);
        Assert.Equal(0f, segment.Bounds.Position.Y);
        Assert.Equal(0f, segment.Bounds.Size.Y);
    }

    [Fact]
    public void CubicBezier_SplitPreservesControlSemanticsAndCurveEquivalence()
    {
        var segment = new CubicBezierRoadGeometrySegment(
            new Vector2(-3, 1),
            new Vector2(2, 9),
            new Vector2(7, -4),
            new Vector2(11, 3));
        const float splitParameter = 0.3f;

        RoadGeometrySplit split = segment.Split(splitParameter);

        var before = Assert.IsType<CubicBezierRoadGeometrySegment>(split.Before);
        var after = Assert.IsType<CubicBezierRoadGeometrySegment>(split.After);
        Assert.Equal(segment.Start, before.Start);
        Assert.Equal(segment.GetPosition(splitParameter), before.End);
        Assert.Equal(before.End, after.Start);
        Assert.Equal(segment.End, after.End);
        Assert.Equal(segment.Length, before.Length + after.Length, 3);

        foreach (float localParameter in new[] { 0f, 0.2f, 0.5f, 0.8f, 1f })
        {
            AssertVectorApproximatelyEqual(
                segment.GetPosition(splitParameter * localParameter),
                before.GetPosition(localParameter));
            AssertVectorApproximatelyEqual(
                segment.GetPosition(splitParameter + (1f - splitParameter) * localParameter),
                after.GetPosition(localParameter));
        }
    }

    [Fact]
    public void CubicBezier_RepeatedEndpointControlsUseOneSidedTangents()
    {
        var segment = new CubicBezierRoadGeometrySegment(
            Vector2.Zero,
            Vector2.Zero,
            new Vector2(2, 0),
            new Vector2(2, 2));
        var reversedEnd = new CubicBezierRoadGeometrySegment(
            Vector2.Zero,
            new Vector2(0, 2),
            new Vector2(2, 2),
            new Vector2(2, 2));

        Assert.Equal(Vector2.Right, segment.GetUnitTangent(0f));
        Assert.Equal(Vector2.Right, reversedEnd.GetUnitTangent(1f));
    }

    [Theory]
    [MemberData(nameof(InvalidCubicBezierControls))]
    public void CubicBezier_InvalidControlsAreRejected(
        Vector2 start,
        Vector2 control1,
        Vector2 control2,
        Vector2 end)
    {
        Assert.Throws<ArgumentException>(() =>
            new CubicBezierRoadGeometrySegment(start, control1, control2, end));
    }

    public static TheoryData<Vector2, Vector2, Vector2, Vector2> InvalidCubicBezierControls => new()
    {
        { new Vector2(float.NaN, 0), Vector2.Zero, Vector2.One, Vector2.Right },
        { Vector2.Zero, new Vector2(float.PositiveInfinity, 0), Vector2.One, Vector2.Right },
        { Vector2.Zero, Vector2.One, new Vector2(0, float.NegativeInfinity), Vector2.Right },
        { Vector2.Zero, Vector2.One, Vector2.Right, new Vector2(float.NaN, 0) },
        { Vector2.One, Vector2.One, Vector2.One, Vector2.One },
    };

    private static void AssertVectorApproximatelyEqual(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(actual.DistanceTo(expected), 0f, 1e-5f);
    }
}
