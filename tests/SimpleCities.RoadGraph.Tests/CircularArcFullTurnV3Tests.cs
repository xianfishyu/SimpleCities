using Godot;

public sealed class CircularArcFullTurnV3Tests
{
    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void ExactTauFullTurn_ReusesStartAtEndAndRoundTrips(float direction)
    {
        float sweep = direction * Mathf.Tau;
        var arc = new CircularArcRoadGeometrySegment(
            new Vector2(7f, -3f), 11f, 0.37f, sweep);

        Assert.True(arc.IsFullTurn);
        Assert.True(RoadExactPredicates.SameBits(arc.Start, arc.End));
        Assert.True(RoadExactPredicates.SameBits(arc.Start, arc.GetPosition(1f)));

        RoadGeometryDeserializationResult restored = RoadGeometrySerializer.Deserialize(
            RoadGeometrySerializer.Serialize(arc));
        CircularArcRoadGeometrySegment restoredArc =
            Assert.IsType<CircularArcRoadGeometrySegment>(restored.Geometry);
        Assert.True(restored.Success);
        Assert.Equal(
            BitConverter.SingleToInt32Bits(sweep),
            BitConverter.SingleToInt32Bits(restoredArc.SweepAngle));
        Assert.True(RoadExactPredicates.SameBits(restoredArc.Start, restoredArc.End));
    }

    [Fact]
    public void NeighboringTauValues_AreNotAcceptedAsFullTurn()
    {
        float below = MathF.BitDecrement(Mathf.Tau);
        float above = MathF.BitIncrement(Mathf.Tau);
        var partial = new CircularArcRoadGeometrySegment(Vector2.Zero, 3f, 0.2f, below);

        Assert.False(partial.IsFullTurn);
        Assert.False(RoadExactPredicates.SameBits(partial.Start, partial.End));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CircularArcRoadGeometrySegment(Vector2.Zero, 3f, 0.2f, above));
    }

    [Fact]
    public void FullTurnSplit_UsesOneSharedJoinAndPreservesTotalLength()
    {
        var arc = new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, -0.7f, -Mathf.Tau);

        RoadGeometrySplit split = arc.Split(0.375f);

        Assert.True(RoadExactPredicates.SameBits(split.Before.End, split.After.Start));
        Assert.Equal(arc.Length, split.Before.Length + split.After.Length, 4);
    }
}
