using System;
using System.Text.Json;

namespace SimpleCities.Core.V3;

public sealed record V3PublishDescriptorCodecResult(bool Success, V3PublishDescriptor? Descriptor, string? Error)
{
    public static V3PublishDescriptorCodecResult Failure(string error) => new(false, null, error);
}

/// <summary>
/// V3 publish descriptor JSON 编解码：camelCase 字段，反序列化后执行基础校验。
/// </summary>
public static class V3PublishDescriptorCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(V3PublishDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return JsonSerializer.Serialize(descriptor, Options);
    }

    public static V3PublishDescriptorCodecResult Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return V3PublishDescriptorCodecResult.Failure("EmptyPayload");

        try
        {
            V3PublishDescriptor? descriptor = JsonSerializer.Deserialize<V3PublishDescriptor>(json, Options);
            if (descriptor is null || !V3PublishDescriptorValidator.IsValid(descriptor))
                return V3PublishDescriptorCodecResult.Failure("InvalidDescriptor");

            return new V3PublishDescriptorCodecResult(true, descriptor, null);
        }
        catch (JsonException)
        {
            return V3PublishDescriptorCodecResult.Failure("MalformedJson");
        }
    }
}
