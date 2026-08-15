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

        Directory.Move(oldPath, newPath);
        return true;
    }
}
