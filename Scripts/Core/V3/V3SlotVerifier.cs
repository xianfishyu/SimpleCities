using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Core.V3;

public sealed record V3SlotVerificationResult(bool Success, string? Error)
{
    public static V3SlotVerificationResult Failure(string error) => new(false, error);
}

/// <summary>
/// 聚合验证：manifest 校验 + 文件集合匹配 + 每个 payload 的 length/hash 校验。
/// </summary>
public static class V3SlotVerifier
{
    public static V3SlotVerificationResult Verify(
        V3Manifest manifest,
        IReadOnlyDictionary<string, byte[]> files)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(files);

        V3ManifestValidationResult manifestResult = V3ManifestValidator.Validate(manifest);
        if (!manifestResult.Success)
            return V3SlotVerificationResult.Failure(manifestResult.Error ?? "InvalidManifest");

        V3FileSetValidationResult fileSetResult = V3FileSetValidator.Validate(
            manifest.Files,
            files.Keys.ToHashSet(StringComparer.Ordinal));
        if (!fileSetResult.Success)
            return V3SlotVerificationResult.Failure(fileSetResult.Error ?? "InvalidFileSet");

        foreach (V3ManifestFile file in manifest.Files)
        {
            if (!files.TryGetValue(file.Name, out byte[]? data))
                return V3SlotVerificationResult.Failure($"MissingPayload:{file.Name}");
            if (!V3PayloadDigest.Matches(file, data))
                return V3SlotVerificationResult.Failure($"DigestMismatch:{file.Name}");
        }

        return new V3SlotVerificationResult(true, null);
    }
}
