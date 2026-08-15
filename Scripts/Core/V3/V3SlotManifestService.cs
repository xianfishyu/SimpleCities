using System;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽 manifest 服务：从文件槽直接读取并严格解析 manifest，不读取业务 payload。
/// </summary>
public static class V3SlotManifestService
{
    public static V3Manifest? GetManifest(string slotId, string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (!V3SlotId.IsValid(slotId))
            return null;

        string manifestPath = Path.Combine(root, slotId, V3SlotReader.ManifestFileName);
        if (!File.Exists(manifestPath))
            return null;

        V3ManifestCodecResult result = V3ManifestStrictFileReader.Read(manifestPath);
        return result.Success ? result.Manifest : null;
    }
}
