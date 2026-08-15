using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SimpleCities.Core.V3;

/// <summary>
/// 扫描 V3 根，使用槽完整性校验对 direct child 分类并附带 manifest 摘要。
/// </summary>
public static class V3SlotIntegrityScanner
{
    public static IReadOnlyList<V3SlotSummary> Scan(string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var summaries = new List<V3SlotSummary>();
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            string name = Path.GetFileName(directory);
            if (!V3SlotId.IsValid(name))
            {
                summaries.Add(new V3SlotSummary(name, name, V3SlotOccupant.Unsafe, null));
                continue;
            }

            string manifestPath = Path.Combine(directory, V3SlotReader.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                summaries.Add(new V3SlotSummary(name, name, V3SlotOccupant.Foreign, null));
                continue;
            }

            if (!V3SlotIntegrity.Verify(directory).Success)
            {
                summaries.Add(new V3SlotSummary(name, name, V3SlotOccupant.CorruptV3, null));
                continue;
            }

            V3ManifestCodecResult manifestResult = V3ManifestStrictFileReader.Read(manifestPath);
            if (!manifestResult.Success || manifestResult.Manifest is null)
            {
                summaries.Add(new V3SlotSummary(name, name, V3SlotOccupant.CorruptV3, null));
                continue;
            }

            summaries.Add(new V3SlotSummary(
                name,
                manifestResult.Manifest.DisplayName,
                V3SlotOccupant.CompleteV3,
                manifestResult.Manifest.Timestamp));
        }

        return summaries
            .OrderBy(summary => summary.SlotId, StringComparer.Ordinal)
            .ToList();
    }
}
