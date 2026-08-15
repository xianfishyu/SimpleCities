using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphCapacityTests
{
    [Fact]
    public void Default_IsValidAndCoversEntityCapacities()
    {
        RoadGraphCapacity capacity = RoadGraphCapacity.Default;

        capacity.Validate();

        Assert.True(capacity.MaxNodes > 0);
        Assert.True(capacity.MaxEdges > 0);
        Assert.True(capacity.MaxID > capacity.MaxNodes);
        Assert.True(capacity.MaxID > capacity.MaxEdges);
        Assert.True(capacity.MaxID > capacity.MaxTotalGeometrySegments);
    }

    [Fact]
    public void Validate_RejectsNonPositiveLimits()
    {
        RoadGraphCapacity capacity = RoadGraphCapacity.Default with { MaxNodes = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => capacity.Validate());
    }

    [Fact]
    public void Validate_RejectsMaxIDBelowEntityCount()
    {
        RoadGraphCapacity capacity = RoadGraphCapacity.Default with { MaxID = 1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => capacity.Validate());
    }
}
