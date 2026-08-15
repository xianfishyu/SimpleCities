using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SimpleCities.Core.V3;

/// <summary>
/// 计算槽文件集合的聚合 digest：按文件名排序后对 name/length/hash 做 SHA-256。
/// </summary>
public static class V3SlotDigest
{
    public static string Compute(IReadOnlyDictionary<string, byte[]> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (KeyValuePair<string, byte[]> file in files.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(file.Key);
            byte[] lengthBytes = BitConverter.GetBytes(file.Value.LongLength);
            byte[] contentHash = Convert.FromHexString(V3PayloadDigest.ComputeSha256(file.Value));

            hash.AppendData(nameBytes);
            hash.AppendData(lengthBytes);
            hash.AppendData(contentHash);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
