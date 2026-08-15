using System;
using System.Text.Json;

namespace SimpleCities.Core.V3;

public sealed record V3ManifestCodecResult(bool Success, V3Manifest? Manifest, string? Error)
{
    public static V3ManifestCodecResult Failure(string error) => new(false, null, error);
}

/// <summary>
/// V3 manifest v1 JSON 编解码：使用 camelCase 字段名，并在反序列化后执行基础校验。
/// </summary>
public static class V3ManifestCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(V3Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, Options);
    }

    public static V3ManifestCodecResult Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return V3ManifestCodecResult.Failure("EmptyPayload");

        try
        {
            V3Manifest? manifest = JsonSerializer.Deserialize<V3Manifest>(json, Options);
            if (manifest is null)
                return V3ManifestCodecResult.Failure("MalformedJson");

            V3ManifestValidationResult validation = V3ManifestValidator.Validate(manifest);
            if (!validation.Success)
                return V3ManifestCodecResult.Failure(validation.Error ?? "InvalidManifest");

            return new V3ManifestCodecResult(true, manifest, null);
        }
        catch (JsonException)
        {
            return V3ManifestCodecResult.Failure("MalformedJson");
        }
    }
}
