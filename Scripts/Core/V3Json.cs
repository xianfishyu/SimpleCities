using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

internal static class V3Json
{
    internal const string FormatFamily = "simple-cities-v3";
    internal const int SchemaVersion = 1;

    internal static IReadOnlyDictionary<string, JsonElement> ReadObject(
        JsonElement element,
        string context,
        params string[] expectedProperties)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException($"{context} must be a JSON object.");

        var expected = new HashSet<string>(expectedProperties, StringComparer.Ordinal);
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!properties.TryAdd(property.Name, property.Value))
                throw new JsonException($"{context} contains duplicate property '{property.Name}'.");
            if (!expected.Contains(property.Name))
                throw new JsonException($"{context} contains unknown property '{property.Name}'.");
        }

        foreach (string propertyName in expectedProperties)
        {
            if (!properties.ContainsKey(propertyName))
                throw new JsonException($"{context} is missing property '{propertyName}'.");
        }
        return properties;
    }

    internal static JsonElement ReadArray(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new JsonException($"{context} must be a JSON array.");
        return element;
    }

    internal static string ReadString(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.String)
            throw new JsonException($"{context} must be a JSON string.");
        return element.GetString() ?? throw new JsonException($"{context} cannot be null.");
    }

    internal static string? ReadNullableString(JsonElement element, string context) =>
        element.ValueKind == JsonValueKind.Null ? null : ReadString(element, context);

    internal static int ReadNonNegativeInt32(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.Number)
            throw new JsonException($"{context} must be an integer number token.");
        return ReadNonNegativeInt32(element.GetRawText(), context);
    }

    internal static int ReadNonNegativeInt32(string token, string context)
    {
        token = ReadCanonicalUnsignedIntegerToken(token, context, 10);
        if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
            throw new JsonException($"{context} exceeds Int32 range.");
        return value;
    }

    internal static long ReadNonNegativeInt64(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.Number)
            throw new JsonException($"{context} must be an integer number token.");
        return ReadNonNegativeInt64(element.GetRawText(), context);
    }

    internal static long ReadNonNegativeInt64(string token, string context)
    {
        token = ReadCanonicalUnsignedIntegerToken(token, context, 19);
        if (!long.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out long value))
            throw new JsonException($"{context} exceeds Int64 range.");
        return value;
    }

    internal static float ReadFiniteSingle(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.Number)
            throw new JsonException($"{context} must be a JSON number.");
        return ReadFiniteSingle(element.GetRawText(), context);
    }

    internal static float ReadFiniteSingle(string token, string context)
    {
        if (token.Length > 64 || !float.TryParse(
                token,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value) || !float.IsFinite(value))
        {
            throw new JsonException($"{context} must be a finite binary32 value.");
        }
        return RoadNumericPolicy.Canonicalize(value);
    }

    internal static string ReadCanonicalSha256(JsonElement element, string context)
    {
        return ReadCanonicalSha256(ReadString(element, context), context);
    }

    internal static string ReadCanonicalSha256(string value, string context)
    {
        if (value.Length != 64)
            throw new JsonException($"{context} must contain 64 lowercase hexadecimal characters.");
        foreach (char character in value)
        {
            if (!char.IsAsciiHexDigit(character) || char.IsAsciiLetterUpper(character))
                throw new JsonException($"{context} must contain 64 lowercase hexadecimal characters.");
        }
        return value;
    }

    private static string ReadCanonicalUnsignedIntegerToken(
        string token,
        string context,
        int maximumLength)
    {
        if (token.Length == 0 || token.Length > maximumLength ||
            (token.Length > 1 && token[0] == '0'))
        {
            throw new JsonException($"{context} must use canonical unsigned decimal syntax.");
        }
        foreach (char character in token)
        {
            if (!char.IsAsciiDigit(character))
                throw new JsonException($"{context} must use canonical unsigned decimal syntax.");
        }
        return token;
    }
}
