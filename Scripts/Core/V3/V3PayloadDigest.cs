using System;
using System.Security.Cryptography;
using System.Text;

namespace SimpleCities.Core.V3;

/// <summary>
/// V3 payload digest 工具：计算 SHA-256 小写 hex，并校验 manifest 文件项与字节内容匹配。
/// </summary>
public static class V3PayloadDigest
{
    public static string ComputeSha256(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        byte[] hash = SHA256.HashData(data);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash)
            builder.Append(b.ToString("x2"));
        return builder.ToString();
    }

    public static bool Matches(V3ManifestFile file, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(data);
        if (file.EncodedLength != data.LongLength)
            return false;
        return string.Equals(file.Sha256, ComputeSha256(data), StringComparison.Ordinal);
    }
}
