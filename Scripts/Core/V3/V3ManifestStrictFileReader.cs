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

        if (!File.Exists(manifestPath))
            return V3ManifestCodecResult.Failure("FileMissing");

        string json = File.ReadAllText(manifestPath);
        return V3ManifestStrictReader.Read(json);
    }
}
