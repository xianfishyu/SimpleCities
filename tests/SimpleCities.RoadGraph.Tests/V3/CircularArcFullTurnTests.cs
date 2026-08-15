using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class CircularArcFullTurnTests
{
    [Fact]
    public void FullTurn_EndAndPositionAtOneReturnExactStart()
    {
        var arc = new CircularArcRoadGeometrySegment(
            new Vector2(2f, 3f),
            5f,
            1.25f,
            Mathf.Tau);

        Assert.True(arc.IsFullTurn);
        Assert.Equal(arc.Start, arc.End);
        Assert.Equal(arc.Start, arc.GetPosition(RoadGeometrySegment.ParameterEnd));
    }

    [Fact]
    public void FullTurn_TangentAtEndMatchesTangentAtStart()
    {
        var arc = new CircularArcRoadGeometrySegment(
            new Vector2(2f, 3f),
            5f,
            1.25f,
            Mathf.Tau);

        Assert.Equal(arc.GetUnitTangent(0f), arc.GetUnitTangent(1f));
    }

    [Fact]
    public void FullTurn_SplitProducesPartialArcsThatRejoin()
    {
        var source = new CircularArcRoadGeometrySegment(
            Vector2.Zero,
            4f,
            0.5f,
            Mathf.Tau);
        const float splitParameter = 0.25f;

        RoadGeometrySplit split = source.Split(splitParameter);

        var before = Assert.IsType<CircularArcRoadGeometrySegment>(split.Before);
        var after = Assert.IsType<CircularArcRoadGeometrySegment>(split.After);
        Assert.False(before.IsFullTurn);
        Assert.False(after.IsFullTurn);
        Assert.InRange(before.End.DistanceTo(after.Start), 0f, 2e-5f);
        Assert.InRange(source.GetPosition(splitParameter).DistanceTo(before.End), 0f, 2e-5f);
    }

    [Fact]
    public void BitDecrementTau_IsNotFullTurn()
    {
        var arc = new CircularArcRoadGeometrySegment(
            Vector2.Zero,
            4f,
            0f,
            RoadNumericPolicy.BitDecrement(Mathf.Tau));

        Assert.False(arc.IsFullTurn);
    }

    [Fact]
    public void BitIncrementTau_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CircularArcRoadGeometrySegment(
                Vector2.Zero,
                4f,
                0f,
                RoadNumericPolicy.BitIncrement(Mathf.Tau)));
    }
}
