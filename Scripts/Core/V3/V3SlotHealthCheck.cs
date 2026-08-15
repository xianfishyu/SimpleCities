using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Core.V3;

public sealed record V3SlotHealthCheckResult(
    int Total,
    int Complete,
    int Corrupt,
    int Foreign,
    int Unsafe);

/// <summary>
/// 槽健康检查：统计 V3 根中各 occupant 分类数量。
/// </summary>
public static class V3SlotHealthCheck
{
    public static V3SlotHealthCheckResult Check(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        IReadOnlyList<V3SlotSummary> slots = V3SlotIntegrityScanner.Scan(root);

        return new V3SlotHealthCheckResult(
            slots.Count,
            slots.Count(slot => slot.Occupant == V3SlotOccupant.CompleteV3),
            slots.Count(slot => slot.Occupant == V3SlotOccupant.CorruptV3),
            slots.Count(slot => slot.Occupant == V3SlotOccupant.Foreign),
            slots.Count(slot => slot.Occupant == V3SlotOccupant.Unsafe));
    }
}
