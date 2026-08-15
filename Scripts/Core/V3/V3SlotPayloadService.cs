using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽 payload 服务：从文件槽读取指定 payload 字节。
/// </summary>
public static class V3SlotPayloadService
{
    public static byte[]? GetPayload(string slotId, string root, string fileName)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(fileName);

        V3SlotReadResult result = new V3FileSlotStore(root).Load(slotId);
        if (!result.Success || result.Payloads is null)
            return null;

        return result.Payloads.TryGetValue(fileName, out byte[]? data) ? data : null;
    }
}
