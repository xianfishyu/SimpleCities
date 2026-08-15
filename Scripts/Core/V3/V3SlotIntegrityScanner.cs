using System;
using System.Collections.Generic;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 扫描 V3 根，使用槽完整性校验对 direct child 分类。
/// </summary>
public static class V3SlotIntegrityScanner
{
    public static IReadOnlyList<V3SlotSummary> Scan(string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var children = new Dictionary<string, V3SlotOccupant>(StringComparer.Ordinal);
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            string name = Path.GetFileName(directory);
            if (!V3SlotId.IsValid(name))
            {
                children[name] = V3SlotOccupant.Unsafe;
                continue;
            }

            if (!File.Exists(Path.Combine(directory, V3SlotReader.ManifestFileName)))
            {
                children[name] = V3SlotOccupant.Foreign;
                continue;
            }

            V3SlotIntegrityResult integrity = V3SlotIntegrity.Verify(directory);
            children[name] = integrity.Success ? V3SlotOccupant.CompleteV3 : V3SlotOccupant.CorruptV3;
        }

        return V3SlotLister.List(children);
    }
}
