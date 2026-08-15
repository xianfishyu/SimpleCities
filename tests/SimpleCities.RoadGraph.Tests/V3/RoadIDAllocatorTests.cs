using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadIDAllocatorTests
{
    [Fact]
    public void TryAllocate_ReturnsSequentialIdsAndAdvancesWatermark()
    {
        var allocator = new RoadIDAllocator(10, 100);

        Assert.True(allocator.TryAllocate(out int first));
        Assert.True(allocator.TryAllocate(out int second));

        Assert.Equal(10, first);
        Assert.Equal(11, second);
        Assert.Equal(12, allocator.NextID);
    }

    [Fact]
    public void TryAllocate_StopsAtMaxIdWithoutOverflow()
    {
        var allocator = new RoadIDAllocator(99, 100);

        Assert.True(allocator.TryAllocate(out int id));
        Assert.Equal(99, id);
        Assert.False(allocator.TryAllocate(out _));
        Assert.Equal(100, allocator.NextID);
    }

    [Fact]
    public void TryReserve_ExactRemainingCapacity_Succeeds()
    {
        var allocator = new RoadIDAllocator(8, 10);

        Assert.True(allocator.TryReserve(2, out int first));

        Assert.Equal(8, first);
        Assert.Equal(10, allocator.NextID);
    }

    [Fact]
    public void TryReserve_TooManyIds_FailsWithoutAdvancing()
    {
        var allocator = new RoadIDAllocator(8, 10);

        Assert.False(allocator.TryReserve(4, out _));

        Assert.Equal(8, allocator.NextID);
    }

    [Fact]
    public void Constructor_RejectsNextIdAboveMaxId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoadIDAllocator(11, 10));
    }
}
