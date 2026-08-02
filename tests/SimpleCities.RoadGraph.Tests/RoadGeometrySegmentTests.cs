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
}
