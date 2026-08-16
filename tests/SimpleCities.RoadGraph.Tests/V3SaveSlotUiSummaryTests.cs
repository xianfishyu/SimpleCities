using SimpleCities.Core.V3;

namespace SimpleCities.Tests;

public sealed class V3SaveSlotUiSummaryTests
{
    [Fact]
    public void FromSlot_CompleteV3_CanLoadOverwriteAndDelete()
    {
        var slot = new V3SlotSummary("city-001", "City", V3SlotOccupant.CompleteV3, "2026-08-16T00:00:00Z");

        V3SaveSlotUiSummary summary = V3SaveSlotUiSummary.FromSlot(slot);

        Assert.True(summary.IsListable);
        Assert.True(summary.CanLoadOrOverwrite);
        Assert.True(summary.CanDelete);
        Assert.True(summary.IsComplete);
        Assert.False(summary.IsCorrupt);
        Assert.Equal("手动", summary.SlotKind);
        Assert.Null(summary.Error);
    }

    [Fact]
    public void FromSlot_CorruptV3_CanDeleteOnly()
    {
        var slot = new V3SlotSummary("broken-001", "Broken", V3SlotOccupant.CorruptV3, null);

        V3SaveSlotUiSummary summary = V3SaveSlotUiSummary.FromSlot(slot);

        Assert.True(summary.IsListable);
        Assert.False(summary.CanLoadOrOverwrite);
        Assert.True(summary.CanDelete);
        Assert.False(summary.IsComplete);
        Assert.True(summary.IsCorrupt);
        Assert.Equal("手动", summary.SlotKind);
        Assert.Equal("槽内容损坏", summary.Error);
    }

    [Fact]
    public void FromSlot_Foreign_NotListable()
    {
        var slot = new V3SlotSummary("legacy-001", "Legacy", V3SlotOccupant.Foreign, null);

        V3SaveSlotUiSummary summary = V3SaveSlotUiSummary.FromSlot(slot);

        Assert.False(summary.IsListable);
        Assert.False(summary.CanLoadOrOverwrite);
        Assert.False(summary.CanDelete);
        Assert.Equal("非 V3 槽", summary.Error);
    }

    [Fact]
    public void FromSlot_AutosaveSlotId_UsesAutomaticKind()
    {
        var slot = new V3SlotSummary("autosave", "自动存档", V3SlotOccupant.CompleteV3, null);

        V3SaveSlotUiSummary summary = V3SaveSlotUiSummary.FromSlot(slot);

        Assert.Equal("自动", summary.SlotKind);
        Assert.True(summary.IsComplete);
    }

    [Fact]
    public void FromSlot_Unsafe_NotListable()
    {
        var slot = new V3SlotSummary("unsafe-001", "Unsafe", V3SlotOccupant.Unsafe, null);

        V3SaveSlotUiSummary summary = V3SaveSlotUiSummary.FromSlot(slot);

        Assert.False(summary.IsListable);
        Assert.False(summary.CanLoadOrOverwrite);
        Assert.False(summary.CanDelete);
        Assert.Equal("不安全槽", summary.Error);
    }
}
