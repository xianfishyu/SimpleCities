using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadTypeChangeTests
{
    [Fact]
    public void PrepareSelection_SortsAndDeduplicates()
    {
        IReadOnlyList<int> result = RoadTypeChangeValidator.PrepareSelection([3, 1, 3, 2]);

        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void IsValidRoadType_AcceptsAllDefinedValues()
    {
        Assert.True(RoadTypeChangeValidator.IsValidRoadType(RoadType.Dirt));
        Assert.True(RoadTypeChangeValidator.IsValidRoadType(RoadType.Street));
        Assert.True(RoadTypeChangeValidator.IsValidRoadType(RoadType.Arterial));
        Assert.True(RoadTypeChangeValidator.IsValidRoadType(RoadType.Highway));
        Assert.False(RoadTypeChangeValidator.IsValidRoadType((RoadType)999));
        Assert.False(RoadTypeChangeValidator.IsValidRoadType((RoadType)(-1)));
    }

    [Fact]
    public void NoChange_HasExpectedFlags()
    {
        RoadTypeChangeResult result = RoadTypeChangeResult.NoChange;

        Assert.True(result.Success);
        Assert.True(result.NoChanges);
        Assert.Empty(result.ChangedEdgeIDs);
        Assert.Empty(result.RemovedEdgeIDs);
        Assert.Empty(result.CreatedEdgeIDs);
    }
}
