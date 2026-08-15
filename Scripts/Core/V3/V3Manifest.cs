using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SimpleCities.Core.V3;

public sealed record V3ManifestFile(string Name, long EncodedLength, string Sha256);

public sealed record V3Manifest(
    string FormatFamily,
    int SchemaVersion,
    string SlotId,
    string DisplayName,
    string Timestamp,
    string CityName,
    long? Population,
    decimal? Funds,
    string? ThumbnailFile,
    IReadOnlyList<V3ManifestFile> Files);

public sealed record V3ManifestValidationResult(bool Success, string? Error)
{
    public static V3ManifestValidationResult Failure(string error) => new(false, error);
}

/// <summary>
/// V3 manifest v1 基础校验：family/version、槽 ID、文本字段、UTC timestamp、
/// population/funds、缩略图与业务文件 metadata。
/// </summary>
public static class V3ManifestValidator
{
    private const int MaxTextLength = 128;
    private const int MaxUtf8Bytes = 512;

    public static V3ManifestValidationResult Validate(V3Manifest? manifest)
    {
        if (manifest is null)
            return V3ManifestValidationResult.Failure("NullManifest");
        if (!string.Equals(manifest.FormatFamily, V3SaveRoot.FormatFamily, StringComparison.Ordinal))
            return V3ManifestValidationResult.Failure("InvalidFormatFamily");
        if (manifest.SchemaVersion != V3SaveRoot.SchemaVersion)
            return V3ManifestValidationResult.Failure("InvalidSchemaVersion");
        if (!V3SlotId.IsValid(manifest.SlotId))
            return V3ManifestValidationResult.Failure("InvalidSlotId");
        if (!IsValidText(manifest.DisplayName))
            return V3ManifestValidationResult.Failure("InvalidDisplayName");
        if (!IsValidText(manifest.CityName))
            return V3ManifestValidationResult.Failure("InvalidCityName");
        if (!IsValidTimestamp(manifest.Timestamp))
            return V3ManifestValidationResult.Failure("InvalidTimestamp");
        if (manifest.Population is < 0)
            return V3ManifestValidationResult.Failure("InvalidPopulation");
        if (manifest.Funds is decimal funds && !HasAtMostTwoDecimalPlaces(funds))
            return V3ManifestValidationResult.Failure("InvalidFunds");
        if (manifest.ThumbnailFile is not null && !IsValidThumbnailName(manifest.ThumbnailFile))
            return V3ManifestValidationResult.Failure("InvalidThumbnailFile");
        if (manifest.Files is null)
            return V3ManifestValidationResult.Failure("MissingFiles");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (V3ManifestFile file in manifest.Files)
        {
            if (!IsValidFileName(file.Name) || !names.Add(file.Name))
                return V3ManifestValidationResult.Failure("InvalidFileName");
            if (file.EncodedLength < 0)
                return V3ManifestValidationResult.Failure("InvalidEncodedLength");
            if (!IsValidSha256(file.Sha256))
                return V3ManifestValidationResult.Failure("InvalidSha256");
        }

        return new V3ManifestValidationResult(true, null);
    }

    private static bool IsValidText(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxTextLength)
            return false;
        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
            return false;
        if (char.IsControl(value[0]) || char.IsControl(value[^1]))
            return false;
        return Encoding.UTF8.GetByteCount(value) <= MaxUtf8Bytes;
    }

    private static bool IsValidTimestamp(string value) =>
        DateTimeOffset.TryParseExact(
            value,
            "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out _);

    private static bool HasAtMostTwoDecimalPlaces(decimal value) =>
        decimal.Round(value, 2) == value;

    private static bool IsValidThumbnailName(string value) =>
        IsValidFileName(value) && value.EndsWith(".png", StringComparison.Ordinal);

    private static bool IsValidFileName(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 255)
            return false;
        if (value.IndexOfAny(['/', '\\']) >= 0)
            return false;
        return !char.IsWhiteSpace(value[0]) && !char.IsWhiteSpace(value[^1]);
    }

    private static bool IsValidSha256(string value)
    {
        if (value.Length != 64)
            return false;
        foreach (char c in value)
        {
            bool isHex =
                (c >= '0' && c <= '9') ||
                (c >= 'a' && c <= 'f') ||
                (c >= 'A' && c <= 'F');
            if (!isHex)
                return false;
        }

        return true;
    }
}
