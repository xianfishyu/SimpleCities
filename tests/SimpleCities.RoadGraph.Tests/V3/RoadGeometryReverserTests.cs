using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGeometryReverserTests
{
    [Fact]
    public void Line_Reverse_SwapsEndpointsAndOpposesTangent()
    {
        var original = new LineRoadGeometrySegment(new Vector2(1f, 2f), new Vector2(5f, 6f));

        var reversed = RoadGeometryReverser.Reverse(original);

        var line = Assert.IsType<LineRoadGeometrySegment>(reversed);
        Assert.Equal(original.End, line.Start);
        Assert.Equal(original.Start, line.End);
        Assert.Equal(original.GetUnitTangent(1f), -line.GetUnitTangent(0f));
        Assert.Equal(original.Length, line.Length, 5);
    }

    [Fact]
    public void CubicBezier_Reverse_SwapsControlsAndEndpoints()
    {
        var original = new CubicBezierRoadGeometrySegment(
            new Vector2(0f, 0f),
            new Vector2(1f, 2f),
            new Vector2(3f, 2f),
            new Vector2(4f, 0f));

        var reversed = RoadGeometryReverser.Reverse(original);

        var bezier = Assert.IsType<CubicBezierRoadGeometrySegment>(reversed);
        Assert.Equal(original.End, bezier.Start);
        Assert.Equal(original.Control2, bezier.Control1);
        Assert.Equal(original.Control1, bezier.Control2);
        Assert.Equal(original.Start, bezier.End);
    }

    [Fact]
    public void CubicHermite_Reverse_NegatesBothTangents()
    {
        var original = new CubicHermiteRoadGeometrySegment(
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(4f, 0f),
            new Vector2(0f, 1f));

        var reversed = RoadGeometryReverser.Reverse(original);

        var hermite = Assert.IsType<CubicHermiteRoadGeometrySegment>(reversed);
        Assert.Equal(original.End, hermite.Start);
        Assert.Equal(-original.EndTangent, hermite.StartTangent);
        Assert.Equal(original.Start, hermite.End);
        Assert.Equal(-original.StartTangent, hermite.EndTangent);
    }

    [Fact]
    public void CircularArc_PartialReverse_SwapsEndpointsAndFlipsSweep()
    {
        var original = new CircularArcRoadGeometrySegment(
            new Vector2(0f, 0f),
            10f,
            0.3f,
            2.0f);

        var reversed = RoadGeometryReverser.Reverse(original);

        var arc = Assert.IsType<CircularArcRoadGeometrySegment>(reversed);
        Assert.Equal(original.Center, arc.Center);
        Assert.Equal(original.Radius, arc.Radius);
        Assert.Equal(-original.SweepAngle, arc.SweepAngle);
        Assert.Equal(original.End.X, arc.Start.X, 5);
        Assert.Equal(original.End.Y, arc.Start.Y, 5);
        Assert.Equal(original.Start.X, arc.End.X, 5);
        Assert.Equal(original.Start.Y, arc.End.Y, 5);
    }

    [Fact]
    public void CircularArc_FullTurnReverse_PreservesSeamAndFlipsSweep()
    {
        var original = new CircularArcRoadGeometrySegment(
            new Vector2(2f, 3f),
            5f,
            1.0f,
            Mathf.Tau);

        var reversed = RoadGeometryReverser.Reverse(original);

        var arc = Assert.IsType<CircularArcRoadGeometrySegment>(reversed);
        Assert.Equal(original.Center, arc.Center);
        Assert.Equal(original.Radius, arc.Radius);
        Assert.Equal(original.StartAngle, arc.StartAngle);
        Assert.Equal(-Mathf.Tau, arc.SweepAngle);
    }

    [Fact]
    public void Clothoid_Reverse_SwapsEndpointsAndFlipsCurvatureSigns()
    {
        var original = new ClothoidRoadGeometrySegment(
            new Vector2(0f, 0f),
            0f,
            0.1f,
            -0.2f,
            12f);

        var reversed = RoadGeometryReverser.Reverse(original);

        var clothoid = Assert.IsType<ClothoidRoadGeometrySegment>(reversed);
        Assert.Equal(original.End, clothoid.Start);
        Assert.Equal(original.Start.X, clothoid.End.X, 4);
        Assert.Equal(original.Start.Y, clothoid.End.Y, 4);
        Assert.Equal(-original.EndCurvature, clothoid.StartCurvature);
        Assert.Equal(-original.StartCurvature, clothoid.EndCurvature);
        Assert.Equal(original.ArcLength, clothoid.ArcLength);
        Assert.Equal(original.GetUnitTangent(1f).X, -clothoid.GetUnitTangent(0f).X, 4);
        Assert.Equal(original.GetUnitTangent(1f).Y, -clothoid.GetUnitTangent(0f).Y, 4);
    }

    [Fact]
    public void RationalQuadratic_Reverse_SwapsEndpointsAndWeights()
    {
        var original = new RationalQuadraticRoadGeometrySegment(
            new Vector2(0f, 0f),
            1f,
            new Vector2(1f, 2f),
            2f,
            new Vector2(3f, 0f),
            4f);

        var reversed = RoadGeometryReverser.Reverse(original);

        var rational = Assert.IsType<RationalQuadraticRoadGeometrySegment>(reversed);
        Assert.Equal(original.End, rational.Start);
        Assert.Equal(original.EndWeight, rational.StartWeight);
        Assert.Equal(original.Control, rational.Control);
        Assert.Equal(original.ControlWeight, rational.ControlWeight);
        Assert.Equal(original.Start, rational.End);
        Assert.Equal(original.StartWeight, rational.EndWeight);
    }

    [Theory]
    [MemberData(nameof(GeometrySamples))]
    public void ReverseTwice_RestoresStartAndEnd(RoadGeometrySegment original)
    {
        RoadGeometrySegment once = RoadGeometryReverser.Reverse(original);
        RoadGeometrySegment twice = RoadGeometryReverser.Reverse(once);

        Assert.Equal(original.Start.X, twice.Start.X, 3);
        Assert.Equal(original.Start.Y, twice.Start.Y, 3);
        Assert.Equal(original.End.X, twice.End.X, 3);
        Assert.Equal(original.End.Y, twice.End.Y, 3);
        Assert.Equal(original.Length, twice.Length, 3);
    }

    public static TheoryData<RoadGeometrySegment> GeometrySamples => new()
    {
        new LineRoadGeometrySegment(new Vector2(1f, 2f), new Vector2(5f, 6f)),
        new CubicBezierRoadGeometrySegment(
            new Vector2(0f, 0f),
            new Vector2(1f, 2f),
            new Vector2(3f, 2f),
            new Vector2(4f, 0f)),
        new CubicHermiteRoadGeometrySegment(
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(4f, 0f),
            new Vector2(0f, 1f)),
        new CircularArcRoadGeometrySegment(
            new Vector2(0f, 0f),
            10f,
            0.3f,
            2.0f),
        new ClothoidRoadGeometrySegment(
            new Vector2(0f, 0f),
            0f,
            0.1f,
            -0.2f,
            12f),
        new RationalQuadraticRoadGeometrySegment(
            new Vector2(0f, 0f),
            1f,
            new Vector2(1f, 2f),
            2f,
            new Vector2(3f, 0f),
            4f),
    };
}
