using System;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽恢复服务：当槽缺失时从 backupRoot 恢复。
/// </summary>
public static class V3SlotRecoveryService
{
    public static bool Recover(string slotId, string root, string backupRoot)
    {
        if (!V3SlotId.IsValid(slotId))
            return false;

        string slotDirectory = Path.Combine(root, slotId);
        string backupDirectory = Path.Combine(backupRoot, slotId);
        if (Directory.Exists(slotDirectory) || !Directory.Exists(backupDirectory))
            return false;

        return V3SlotBackupService.Restore(slotId, root, backupRoot);
    }
}
