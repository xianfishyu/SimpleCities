namespace SimpleCities.Core.V3;

/// <summary>
/// V3 槽列表项摘要：正常/损坏/外部分类与展示名。
/// </summary>
public sealed record V3SlotSummary(
    string SlotId,
    string DisplayName,
    V3SlotOccupant Occupant,
    string? Timestamp)
{
    public bool IsUsable => Occupant == V3SlotOccupant.CompleteV3;
}
