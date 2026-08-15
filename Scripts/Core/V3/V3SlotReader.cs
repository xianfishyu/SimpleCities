using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleCities.Core.V3;

public sealed record V3SlotReadResult(
    bool Success,
    V3Manifest? Manifest,
    IReadOnlyDictionary<string, byte[]>? Payloads,
    string? Error)
{
    public static V3SlotReadResult Failure(string error) => new(false, null, null, error);
}

/// <summary>
/// 从槽文件字典读取 manifest 与 payload（排除 manifest.json）。
/// </summary>
public static class V3SlotReader
{
    public const string ManifestFileName = "manifest.json";

    public static V3SlotReadResult Read(IReadOnlyDictionary<string, byte[]> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (!files.TryGetValue(ManifestFileName, out byte[]? manifestBytes))
            return V3SlotReadResult.Failure("MissingManifest");

        string manifestJson = Encoding.UTF8.GetString(manifestBytes);
        V3ManifestCodecResult manifestResult = V3ManifestStrictReader.Read(manifestJson);
        if (!manifestResult.Success || manifestResult.Manifest is null)
            return V3SlotReadResult.Failure(manifestResult.Error ?? "InvalidManifest");

        var payloads = files
            .Where(pair => !string.Equals(pair.Key, ManifestFileName, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return new V3SlotReadResult(true, manifestResult.Manifest, payloads, null);
    }
}
