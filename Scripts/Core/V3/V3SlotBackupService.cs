using System;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽备份/恢复服务：在 root 与 backupRoot 之间复制/恢复整个槽目录。
/// </summary>
public static class V3SlotBackupService
{
    public static bool Backup(string slotId, string root, string backupRoot)
    {
        if (!V3SlotId.IsValid(slotId))
            return false;

        string source = Path.Combine(root, slotId);
        if (!Directory.Exists(source))
            return false;

        string destination = Path.Combine(backupRoot, slotId);
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);

        Directory.CreateDirectory(backupRoot);
        CopyDirectory(source, destination);
        return true;
    }

    public static bool Restore(string slotId, string root, string backupRoot)
    {
        if (!V3SlotId.IsValid(slotId))
            return false;

        string source = Path.Combine(backupRoot, slotId);
        if (!Directory.Exists(source))
            return false;

        string destination = Path.Combine(root, slotId);
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);

        Directory.CreateDirectory(root);
        CopyDirectory(source, destination);
        return true;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            string target = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, target, overwrite: true);
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            string target = Path.Combine(destination, Path.GetFileName(directory));
            CopyDirectory(directory, target);
        }
    }
}
