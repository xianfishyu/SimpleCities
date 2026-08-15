using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotPathTests
{
    [Fact]
    public void TryGetSlotPath_ValidSlot_ReturnsCombinedPath()
    {
        Assert.True(V3SlotPath.TryGetSlotPath("user://saves-v3", "city-001", out string path));
        Assert.Equal("user://saves-v3/city-001", path);
    }

    [Fact]
    public void TryGetSlotPath_InvalidSlot_ReturnsFalse()
    {
        Assert.False(V3SlotPath.TryGetSlotPath("user://saves-v3", "bad/slot", out _));
        Assert.False(V3SlotPath.TryGetSlotPath("user://saves-v3", "", out _));
    }

    [Fact]
    public void TryGetSlotPath_RejectsNullOrEmptyRoot()
    {
        Assert.False(V3SlotPath.TryGetSlotPath("", "city-001", out _));
        Assert.False(V3SlotPath.TryGetSlotPath(null!, "city-001", out _));
    }

    [Fact]
    public void TryGetSlotPath_TrimsTrailingSlash()
    {
        Assert.True(V3SlotPath.TryGetSlotPath("user://saves-v3/", "city-001", out string path));
        Assert.Equal("user://saves-v3/city-001", path);
    }
}
