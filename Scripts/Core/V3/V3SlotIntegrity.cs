using System;
using System.IO;

namespace SimpleCities.Core.V3;

public sealed record V3SlotIntegrityResult(bool Success, string? Error)
{
    public static V3SlotIntegrityResult Failure(string error) => new(false, error);
}

/// <summary>
/// 验证槽目录：读取 manifest，并对每个声明 payload 执行文件同句柄校验。
/// </summary>
public static class V3SlotIntegrity
{
    public static V3SlotIntegrityResult Verify(string slotDirectory)
    {
        if (string.IsNullOrWhiteSpace(slotDirectory))
            return V3SlotIntegrityResult.Failure("InvalidDirectory");

        string manifestPath = Path.Combine(slotDirectory, V3SlotReader.ManifestFileName);
        if (!File.Exists(manifestPath))
            return V3SlotIntegrityResult.Failure("MissingManifest");

        string json = File.ReadAllText(manifestPath);
        V3ManifestCodecResult manifestResult = V3ManifestCodec.Deserialize(json);
        if (!manifestResult.Success || manifestResult.Manifest is null)
            return V3SlotIntegrityResult.Failure(manifestResult.Error ?? "InvalidManifest");

        foreach (V3ManifestFile file in manifestResult.Manifest.Files)
        {
            V3SameHandleVerificationResult payloadResult = V3FilePayloadVerifier.Verify(
                Path.Combine(slotDirectory, file.Name),
                file);
            if (!payloadResult.Success)
                return V3SlotIntegrityResult.Failure($"InvalidPayload:{file.Name}");
        }

        return new V3SlotIntegrityResult(true, null);
    }
}
