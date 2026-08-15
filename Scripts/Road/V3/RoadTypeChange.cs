using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// 批量改造结果：整批全有或全无；NoChanges 不发事件、不入历史。
/// </summary>
public sealed record RoadTypeChangeResult(
    bool Success,
    bool NoChanges,
    IReadOnlyList<int> ChangedEdgeIDs,
    IReadOnlyList<int> RemovedEdgeIDs,
    IReadOnlyList<int> CreatedEdgeIDs)
{
    public static RoadTypeChangeResult NoChange { get; } =
        new(true, true, [], [], []);
}

public static class RoadTypeChangeValidator
{
    public static bool IsValidRoadType(RoadType roadType) =>
        roadType is >= RoadType.Dirt and <= RoadType.Highway;

    public static IReadOnlyList<int> PrepareSelection(IEnumerable<int> edgeIDs)
    {
        ArgumentNullException.ThrowIfNull(edgeIDs);
        return edgeIDs.Distinct().Order().ToArray();
    }
}
