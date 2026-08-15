using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Core.V3;

/// <summary>
/// 将已分类的 direct child 集合转换为有序槽列表摘要。
/// </summary>
public static class V3SlotLister
{
    public static IReadOnlyList<V3SlotSummary> List(
        IReadOnlyDictionary<string, V3SlotOccupant> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        return children
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new V3SlotSummary(pair.Key, pair.Key, pair.Value, null))
            .ToList();
    }
}
