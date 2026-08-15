using System;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 从文件路径读取 payload 字节并执行同句柄校验（长度/hash/EOF）。
/// </summary>
public static class V3FilePayloadVerifier
{
    public static V3SameHandleVerificationResult Verify(string filePath, V3ManifestFile file)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(file);

        if (!File.Exists(filePath))
            return V3SameHandleVerificationResult.Failure(0, "FileMissing");

        byte[] data = File.ReadAllBytes(filePath);
        return V3SameHandleVerifier.Verify(file, data);
    }
}
