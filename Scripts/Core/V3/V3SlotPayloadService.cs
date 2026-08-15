using System;
using System.IO;
using System.Linq;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽 payload 服务：从文件槽直接读取指定 payload，并在返回前校验 manifest 声明的 length/hash。
/// </summary>
public static class V3SlotPayloadService
{
    public static byte[]? GetPayload(string slotId, string root, string fileName)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(fileName);

        if (!V3SlotId.IsValid(slotId) ||
            string.IsNullOrWhiteSpace(fileName) ||
            fileName.IndexOfAny(['/', '\\']) >= 0)
            return null;

        string slotDirectory = Path.Combine(root, slotId);
        string manifestPath = Path.Combine(slotDirectory, V3SlotReader.ManifestFileName);
        if (!File.Exists(manifestPath))
            return null;

        V3ManifestCodecResult manifestResult = V3ManifestStrictFileReader.Read(manifestPath);
        if (!manifestResult.Success || manifestResult.Manifest is null)
            return null;

        V3ManifestFile? file = manifestResult.Manifest.Files
            .FirstOrDefault(candidate => string.Equals(candidate.Name, fileName, StringComparison.Ordinal));
        if (file is null)
            return null;

        string payloadPath = Path.Combine(slotDirectory, fileName);
        if (!File.Exists(payloadPath))
            return null;

        byte[] data = File.ReadAllBytes(payloadPath);
        return V3PayloadDigest.Matches(file, data) ? data : null;
    }
}
