using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Core.V3;

/// <summary>
/// 从槽元数据与 payload 字节构造 manifest v1；自动计算 encodedLength 与 SHA-256。
/// </summary>
public static class V3ManifestBuilder
{
    public static V3Manifest Create(
        string slotId,
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile,
        IReadOnlyList<KeyValuePair<string, byte[]>> payloads)
    {
        ArgumentNullException.ThrowIfNull(payloads);

        var files = payloads
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new V3ManifestFile(
                pair.Key,
                pair.Value.LongLength,
                V3PayloadDigest.ComputeSha256(pair.Value)))
            .ToList();

        return new V3Manifest(
            V3SaveRoot.FormatFamily,
            V3SaveRoot.SchemaVersion,
            slotId,
            displayName,
            timestamp,
            cityName,
            population,
            funds,
            thumbnailFile,
            files);
    }
}
