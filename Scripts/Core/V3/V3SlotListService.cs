using System;
using System.Collections.Generic;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽列表服务：列出文件槽存储中的槽摘要。
/// </summary>
public static class V3SlotListService
{
    public static IReadOnlyList<V3SlotSummary> List(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return new V3FileSlotStore(root).List();
    }
}
