using System;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 从文件读取 manifest 并执行严格读取（重复键 + codec/validator）。
/// </summary>
public static class V3ManifestStrictFileReader
{
    public static V3ManifestCodecResult Read(string manifestPath)
    {
        ArgumentNullException.ThrowIfNull(manifestPath);

        V3StrictTokenResult token = V3StrictTokenReader.ReadFile(manifestPath);
        if (!token.Success)
            return V3ManifestCodecResult.Failure(token.Error ?? "TokenReadFailed");
        if (token.Json is null)
            return V3ManifestCodecResult.Failure("EmptyPayload");

        return V3ManifestStrictReader.Read(token.Json);
    }
}
