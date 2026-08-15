using System;

namespace SimpleCities.Core.V3;

public sealed record V3SameHandleVerificationResult(
    bool Success,
    long ConsumedBytes,
    bool EndOfFile,
    string? Error)
{
    public static V3SameHandleVerificationResult Failure(long consumedBytes, string error) =>
        new(false, consumedBytes, false, error);
}

/// <summary>
/// 模拟同句柄校验：长度、hash 与 EOF 必须同时满足。
/// </summary>
public static class V3SameHandleVerifier
{
    public static V3SameHandleVerificationResult Verify(V3ManifestFile file, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(data);

        if (data.LongLength != file.EncodedLength)
            return V3SameHandleVerificationResult.Failure(data.LongLength, "LengthMismatch");

        if (!V3PayloadDigest.Matches(file, data))
            return V3SameHandleVerificationResult.Failure(data.LongLength, "DigestMismatch");

        return new V3SameHandleVerificationResult(true, data.LongLength, true, null);
    }
}
