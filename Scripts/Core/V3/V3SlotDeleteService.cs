using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽删除服务：从文件槽存储中删除指定槽。
/// </summary>
public static class V3SlotDeleteService
{
    public static bool Delete(string slotId, string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return new V3FileSlotStore(root).Delete(slotId);
    }
}
