using System;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽重命名服务：将槽目录移动到新 ID。
/// </summary>
public static class V3SlotRenameService
{
    public static bool Rename(string oldSlotId, string newSlotId, string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (!V3SlotId.IsValid(oldSlotId) || !V3SlotId.IsValid(newSlotId))
            return false;
        if (string.Equals(oldSlotId, newSlotId, StringComparison.Ordinal))
            return true;

        string oldPath = Path.Combine(root, oldSlotId);
        string newPath = Path.Combine(root, newSlotId);
        if (!Directory.Exists(oldPath) || Directory.Exists(newPath))
            return false;

        V3ManifestCodecResult manifestResult = V3ManifestStrictFileReader.Read(
            Path.Combine(oldPath, V3SlotReader.ManifestFileName));
        if (!manifestResult.Success || manifestResult.Manifest is null)
            return false;

        Directory.Move(oldPath, newPath);
        V3Manifest updated = manifestResult.Manifest with { SlotId = newSlotId };
        File.WriteAllText(
            Path.Combine(newPath, V3SlotReader.ManifestFileName),
            V3ManifestCodec.Serialize(updated));
        return true;
    }
}
