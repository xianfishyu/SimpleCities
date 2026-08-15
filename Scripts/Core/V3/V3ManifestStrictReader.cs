using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// manifest 严格读取：先拒绝重复键，再执行 manifest codec/validator。
/// </summary>
public static class V3ManifestStrictReader
{
    public static V3ManifestCodecResult Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (V3JsonDuplicateDetector.TryDetectDuplicateKey(json, out string? duplicateKey))
            return V3ManifestCodecResult.Failure($"DuplicateKey:{duplicateKey}");

        return V3ManifestCodec.Deserialize(json);
    }
}
