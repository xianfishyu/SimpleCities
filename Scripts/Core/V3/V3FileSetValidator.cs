using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Core.V3;

public sealed record V3FileSetValidationResult(bool Success, string? Error)
{
    public static V3FileSetValidationResult Failure(string error) => new(false, error);
}

/// <summary>
/// 比较 manifest 声明的业务 payload 与槽内实际文件集合；缺失或未声明文件都失败。
/// </summary>
public static class V3FileSetValidator
{
    public static V3FileSetValidationResult Validate(
        IReadOnlyList<V3ManifestFile> declaredFiles,
        IReadOnlySet<string> actualFiles)
    {
        ArgumentNullException.ThrowIfNull(declaredFiles);
        ArgumentNullException.ThrowIfNull(actualFiles);

        foreach (V3ManifestFile file in declaredFiles)
        {
            if (!actualFiles.Contains(file.Name))
                return V3FileSetValidationResult.Failure($"MissingDeclaredFile:{file.Name}");
        }

        var declaredNames = declaredFiles.Select(file => file.Name).ToHashSet(StringComparer.Ordinal);
        foreach (string actual in actualFiles)
        {
            if (!declaredNames.Contains(actual))
                return V3FileSetValidationResult.Failure($"UndeclaredFile:{actual}");
        }

        return new V3FileSetValidationResult(true, null);
    }
}
