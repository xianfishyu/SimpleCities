using System;
using System.Collections.Generic;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽快照服务：读取槽目录全部文件（含 manifest）为字典。
/// </summary>
public static class V3SlotSnapshotService
{
    public static IReadOnlyDictionary<string, byte[]>? Capture(string slotId, string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (!V3SlotId.IsValid(slotId))
            return null;

        string slotDirectory = Path.Combine(root, slotId);
        if (!Directory.Exists(slotDirectory))
            return null;

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string filePath in Directory.EnumerateFiles(slotDirectory))
            files[Path.GetFileName(filePath)] = File.ReadAllBytes(filePath);
        return files;
    }
}
