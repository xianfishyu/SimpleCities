public sealed class RoadGraphCapacityV3Tests
{
    [Fact]
    public void Validate_AcceptsEveryResourceExactlyAtItsLimit()
    {
        RoadGraphCapacity capacity = RoadGraphCapacity.Default;
        var counts = new RoadGraphResourceCounts(
            capacity.MaximumNodes,
            capacity.MaximumEdges,
            capacity.MaximumGeometrySegments,
            capacity.MaximumQueryFragments,
            capacity.MaximumBuckets,
            capacity.MaximumSpatialReferences);

        Assert.Equal(RoadGraphCapacityError.None, capacity.Validate(counts));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Validate_RejectsEachResourceOneAboveItsLimit(int resource)
    {
        RoadGraphCapacity capacity = RoadGraphCapacity.Default;
        int[] values =
        [
            capacity.MaximumNodes,
            capacity.MaximumEdges,
            capacity.MaximumGeometrySegments,
            capacity.MaximumQueryFragments,
            capacity.MaximumBuckets,
            capacity.MaximumSpatialReferences,
        ];
        values[resource]++;

        RoadGraphCapacityError result = capacity.Validate(new RoadGraphResourceCounts(
            values[0], values[1], values[2], values[3], values[4], values[5]));

        Assert.NotEqual(RoadGraphCapacityError.None, result);
    }

    [Fact]
    public void IDReservation_AcceptsLastRepresentableWatermarkAndRejectsOverflow()
    {
        RoadGraphCapacityError accepted = RoadGraphIDReservation.TryCreate(
            int.MaxValue - 2,
            2,
            out RoadGraphIDReservation reservation);
        RoadGraphCapacityError rejected = RoadGraphIDReservation.TryCreate(
            int.MaxValue - 2,
            3,
            out _);

        Assert.Equal(RoadGraphCapacityError.None, accepted);
        Assert.Equal(int.MaxValue - 2, reservation.GetID(0));
        Assert.Equal(int.MaxValue - 1, reservation.GetID(1));
        Assert.Equal(int.MaxValue, reservation.EndExclusive);
        Assert.Equal(RoadGraphCapacityError.IDSpaceExhausted, rejected);
    }

    [Fact]
    public void ValidateMutationWork_SeparatesCandidateSplitAndWitnessBudgets()
    {
        RoadGraphCapacity capacity = RoadGraphCapacity.Default;

        Assert.Equal(
            RoadGraphCapacityError.None,
            capacity.ValidateMutationWork(
                capacity.MaximumMutationCandidates,
                capacity.MaximumMutationSplits,
                capacity.MaximumIntersectionWitnesses));
        Assert.Equal(
            RoadGraphCapacityError.MutationCandidateLimitExceeded,
            capacity.ValidateMutationWork(capacity.MaximumMutationCandidates + 1, 0, 0));
        Assert.Equal(
            RoadGraphCapacityError.MutationSplitLimitExceeded,
            capacity.ValidateMutationWork(0, capacity.MaximumMutationSplits + 1, 0));
        Assert.Equal(
            RoadGraphCapacityError.IntersectionWitnessLimitExceeded,
            capacity.ValidateMutationWork(0, 0, capacity.MaximumIntersectionWitnesses + 1));
    }
}
