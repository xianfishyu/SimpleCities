using System;
using SimpleCities.Core.V3;

/// <summary>
/// PauseMenu 使用的 V3 槽展示模型：决定槽是否出现在列表中，以及可执行哪些操作。
/// </summary>
public sealed record V3SaveSlotUiSummary(
    string SlotId,
    string DisplayName,
    V3SlotOccupant Occupant,
    string? Timestamp,
    bool IsListable,
    bool CanLoadOrOverwrite,
    bool CanDelete,
    string? Error)
{
    public static V3SaveSlotUiSummary FromSlot(V3SlotSummary slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        bool isComplete = slot.Occupant == V3SlotOccupant.CompleteV3;
        bool isCorrupt = slot.Occupant == V3SlotOccupant.CorruptV3;
        bool isForeign = slot.Occupant == V3SlotOccupant.Foreign;
        bool isUnsafe = slot.Occupant == V3SlotOccupant.Unsafe;
        bool isListable = isComplete || isCorrupt;
        bool canLoadOrOverwrite = isComplete;
        bool canDelete = isComplete || isCorrupt;
        string? error = slot.Occupant switch
        {
            V3SlotOccupant.CorruptV3 => "槽内容损坏",
            V3SlotOccupant.Foreign => "非 V3 槽",
            V3SlotOccupant.Unsafe => "不安全槽",
            _ => null,
        };

        return new V3SaveSlotUiSummary(
            slot.SlotId,
            slot.DisplayName,
            slot.Occupant,
            slot.Timestamp,
            isListable,
            canLoadOrOverwrite,
            canDelete,
            error);
    }
}
