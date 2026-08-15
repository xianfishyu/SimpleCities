using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotSummaryTests
{
    [Fact]
    public void IsUsable_TrueForCompleteV3()
    {
        var summary = new V3SlotSummary("city-001", "河湾城", V3SlotOccupant.CompleteV3, "2026-08-12T08:00:00.0000000Z");

        Assert.True(summary.IsUsable);
        Assert.Equal("city-001", summary.SlotId);
    }

    [Fact]
    public void IsUsable_FalseForCorruptV3()
    {
        var summary = new V3SlotSummary("city-001", "河湾城", V3SlotOccupant.CorruptV3, null);

        Assert.False(summary.IsUsable);
    }

    [Fact]
    public void IsUsable_FalseForForeignAndUnsafe()
    {
        Assert.False(new V3SlotSummary("x", "x", V3SlotOccupant.Foreign, null).IsUsable);
        Assert.False(new V3SlotSummary("x", "x", V3SlotOccupant.Unsafe, null).IsUsable);
    }
}
