using System;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽复制服务：将槽从 sourceRoot 复制到 destinationRoot。
/// </summary>
public static class V3SlotCopyService
{
    public static bool Copy(string slotId, string sourceRoot, string destinationRoot)
    {
        ArgumentNullException.ThrowIfNull(sourceRoot);
        ArgumentNullException.ThrowIfNull(destinationRoot);

        string sourceDirectory = Path.Combine(sourceRoot, slotId);
        if (!V3SlotIntegrity.Verify(sourceDirectory).Success)
            return false;
        return V3SlotBackupService.Backup(slotId, sourceRoot, destinationRoot);
    }
}
