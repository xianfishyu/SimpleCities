using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽 manifest 服务：从文件槽读取 manifest。
/// </summary>
public static class V3SlotManifestService
{
    public static V3Manifest? GetManifest(string slotId, string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        V3SlotReadResult result = new V3FileSlotStore(root).Load(slotId);
        return result.Success ? result.Manifest : null;
    }
}
