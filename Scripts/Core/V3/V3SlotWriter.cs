using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleCities.Core.V3;

/// <summary>
/// 将 manifest 与 payload 字节组装为槽内文件字典（manifest.json + 各 payload）。
/// </summary>
public static class V3SlotWriter
{
    public const string ManifestFileName = "manifest.json";

    public static IReadOnlyDictionary<string, byte[]> BuildFiles(
        V3Manifest manifest,
        IReadOnlyDictionary<string, byte[]> payloads)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(payloads);

        var declared = manifest.Files.Select(file => file.Name).ToHashSet(StringComparer.Ordinal);
        if (payloads.Keys.Any(name => !declared.Contains(name)))
            throw new ArgumentException("Payload set contains undeclared files.", nameof(payloads));

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [ManifestFileName] = Encoding.UTF8.GetBytes(V3ManifestCodec.Serialize(manifest)),
        };

        foreach (V3ManifestFile file in manifest.Files)
        {
            if (!payloads.TryGetValue(file.Name, out byte[]? data))
                throw new ArgumentException($"Missing payload: {file.Name}", nameof(payloads));
            files[file.Name] = data;
        }

        return files;
    }
}
