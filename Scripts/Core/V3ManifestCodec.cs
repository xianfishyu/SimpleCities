using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

internal static class V3ManifestCodec
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    internal const int MaximumTextUtf8Bytes = 512;
    internal const int MaximumTextScalars = 128;

    internal static void Write(Stream destination, V3Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateModel(manifest);

        using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false,
        });
        writer.WriteStartObject();
        writer.WriteString("formatFamily", V3Json.FormatFamily);
        writer.WriteNumber("schemaVersion", V3Json.SchemaVersion);
        writer.WriteString("slotId", manifest.SlotID);
        writer.WriteString("displayName", manifest.DisplayName);
        writer.WriteString("timestamp", manifest.Timestamp);
        writer.WriteString("cityName", manifest.CityName);
        if (manifest.Population.HasValue)
            writer.WriteNumber("population", manifest.Population.Value);
        else
            writer.WriteNull("population");
        if (manifest.Funds.HasValue)
            writer.WriteNumber("funds", manifest.Funds.Value);
        else
            writer.WriteNull("funds");
        if (manifest.ThumbnailFile is null)
            writer.WriteNull("thumbnailFile");
        else
            writer.WriteString("thumbnailFile", manifest.ThumbnailFile);
        writer.WriteStartArray("files");
        foreach (V3ManifestFile file in manifest.Files.OrderBy(file => file.Name, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("name", file.Name);
            writer.WriteNumber("encodedLength", file.EncodedLength);
            writer.WriteString("sha256", file.Sha256);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }

    internal static V3Manifest Read(Stream source)
    {
        bool ignored = false;
        return Read(source, ref ignored);
    }

    internal static V3Manifest Read(Stream source, ref bool declaresV3Family)
    {
        ArgumentNullException.ThrowIfNull(source);
        declaresV3Family = false;
        using var reader = new V3JsonStreamReader(source, V3StorageBudget.ManifestJson);
        reader.ReadRequired("V3 manifest");
        reader.Expect(JsonTokenType.StartObject, "V3 manifest");

        ulong seen = 0;
        string? formatFamily = null;
        int schemaVersion = -1;
        string? slotID = null;
        string? displayName = null;
        string? timestamp = null;
        string? cityName = null;
        long? population = null;
        decimal? funds = null;
        string? thumbnailFile = null;
        List<V3ManifestFile>? files = null;
        while (ReadObjectProperty(reader, "V3 manifest", out string property))
        {
            reader.ReadRequired($"Manifest property '{property}'");
            switch (property)
            {
                case "formatFamily":
                    MarkField(ref seen, 1UL << 0, property, "V3 manifest");
                    formatFamily = reader.GetString("manifest formatFamily");
                    declaresV3Family = formatFamily == V3Json.FormatFamily;
                    break;
                case "schemaVersion":
                    MarkField(ref seen, 1UL << 1, property, "V3 manifest");
                    schemaVersion = V3Json.ReadNonNegativeInt32(
                        reader.GetNumberToken("manifest schemaVersion"),
                        "manifest schemaVersion");
                    break;
                case "slotId":
                    MarkField(ref seen, 1UL << 2, property, "V3 manifest");
                    slotID = reader.GetString("manifest slotId");
                    break;
                case "displayName":
                    MarkField(ref seen, 1UL << 3, property, "V3 manifest");
                    displayName = reader.GetString("manifest displayName");
                    break;
                case "timestamp":
                    MarkField(ref seen, 1UL << 4, property, "V3 manifest");
                    timestamp = reader.GetString("manifest timestamp");
                    break;
                case "cityName":
                    MarkField(ref seen, 1UL << 5, property, "V3 manifest");
                    cityName = reader.GetString("manifest cityName");
                    break;
                case "population":
                    MarkField(ref seen, 1UL << 6, property, "V3 manifest");
                    population = reader.TokenType == JsonTokenType.Null
                        ? null
                        : V3Json.ReadNonNegativeInt64(
                            reader.GetNumberToken("manifest population"),
                            "manifest population");
                    break;
                case "funds":
                    MarkField(ref seen, 1UL << 7, property, "V3 manifest");
                    funds = ReadNullableFunds(reader);
                    break;
                case "thumbnailFile":
                    MarkField(ref seen, 1UL << 8, property, "V3 manifest");
                    thumbnailFile = reader.TokenType == JsonTokenType.Null
                        ? null
                        : reader.GetString("manifest thumbnailFile");
                    break;
                case "files":
                    MarkField(ref seen, 1UL << 9, property, "V3 manifest");
                    files = ReadFiles(reader);
                    break;
                default:
                    throw new JsonException($"V3 manifest contains unknown property '{property}'.");
            }
        }
        RequireFields(seen, (1UL << 10) - 1, "V3 manifest");
        reader.RequireEndOfDocument("V3 manifest");

        if (formatFamily != V3Json.FormatFamily)
            throw new JsonException("Manifest formatFamily is not simple-cities-v3.");
        if (schemaVersion != V3Json.SchemaVersion)
            throw new JsonException("Manifest schemaVersion is not supported.");

        var manifest = new V3Manifest(
            slotID!,
            displayName!,
            timestamp!,
            cityName!,
            population,
            funds,
            thumbnailFile,
            files!);
        ValidateModel(manifest);
        return manifest;
    }

    internal static string CreateTimestamp(DateTime utcTime) =>
        utcTime.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static List<V3ManifestFile> ReadFiles(V3JsonStreamReader reader)
    {
        reader.Expect(JsonTokenType.StartArray, "manifest files");
        var files = new List<V3ManifestFile>();
        var caseInsensitiveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? previousName = null;
        long totalEncodedBytes = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return files;
            if (files.Count >= V3StorageBudget.MaximumManifestFiles)
                throw new JsonException("Manifest file item budget is exceeded.");
            reader.Expect(JsonTokenType.StartObject, "manifest file");

            ulong seen = 0;
            string? name = null;
            long encodedLength = -1;
            string? sha256 = null;
            while (ReadObjectProperty(reader, "manifest file", out string property))
            {
                reader.ReadRequired($"Manifest file property '{property}'");
                switch (property)
                {
                    case "name":
                        MarkField(ref seen, 1UL << 0, property, "manifest file");
                        name = reader.GetString("manifest file name");
                        break;
                    case "encodedLength":
                        MarkField(ref seen, 1UL << 1, property, "manifest file");
                        encodedLength = V3Json.ReadNonNegativeInt64(
                            reader.GetNumberToken("manifest file encodedLength"),
                            "manifest file encodedLength");
                        break;
                    case "sha256":
                        MarkField(ref seen, 1UL << 2, property, "manifest file");
                        sha256 = V3Json.ReadCanonicalSha256(
                            reader.GetString("manifest file sha256"),
                            "manifest file sha256");
                        break;
                    default:
                        throw new JsonException($"Manifest file contains unknown property '{property}'.");
                }
            }
            RequireFields(seen, 0b111, "manifest file");
            SaveSlotStore.ValidateManifestFileName(name!);
            if (!caseInsensitiveNames.Add(name!))
                throw new JsonException($"Manifest contains duplicate file '{name}'.");
            if (previousName is not null && StringComparer.Ordinal.Compare(previousName, name) >= 0)
                throw new JsonException("Manifest files must be sorted by ordinal file name.");
            ValidatePayloadLength(encodedLength, name!);
            if (totalEncodedBytes > V3StorageBudget.MaximumSlotEncodedBytes - encodedLength)
                throw new JsonException("Manifest payloads exceed the slot encoded byte budget.");
            totalEncodedBytes += encodedLength;
            previousName = name;
            files.Add(new V3ManifestFile(name!, encodedLength, sha256!));
        }
        throw new JsonException("Manifest files array is incomplete.");
    }

    private static void ValidateModel(V3Manifest manifest)
    {
        SaveSlotStore.ValidateSlotID(manifest.SlotID);
        ValidateText(manifest.DisplayName, "displayName");
        ValidateText(manifest.CityName, "cityName");
        if (!DateTimeOffset.TryParseExact(
                manifest.Timestamp,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset timestamp) || timestamp.Offset != TimeSpan.Zero)
        {
            throw new JsonException("Manifest timestamp must use exact seven-digit UTC format.");
        }
        if (manifest.Population < 0)
            throw new JsonException("Manifest population cannot be negative.");
        if (manifest.Funds.HasValue && decimal.Round(manifest.Funds.Value, 2) != manifest.Funds.Value)
            throw new JsonException("Manifest funds cannot contain more than two decimal places.");
        if (manifest.ThumbnailFile is not null)
            ValidateThumbnailFileName(manifest.ThumbnailFile);
        ArgumentNullException.ThrowIfNull(manifest.Files);
        if (manifest.Files.Count is < 1 or > V3StorageBudget.MaximumManifestFiles)
            throw new JsonException("Manifest must contain between 1 and 64 business files.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalEncodedBytes = 0;
        foreach (V3ManifestFile file in manifest.Files)
        {
            ArgumentNullException.ThrowIfNull(file);
            SaveSlotStore.ValidateManifestFileName(file.Name);
            if (!names.Add(file.Name))
                throw new JsonException($"Manifest contains duplicate file '{file.Name}'.");
            ValidatePayloadLength(file.EncodedLength, file.Name);
            _ = V3Json.ReadCanonicalSha256(file.Sha256, $"manifest file '{file.Name}' sha256");
            if (totalEncodedBytes > V3StorageBudget.MaximumSlotEncodedBytes - file.EncodedLength)
                throw new JsonException("Manifest payloads exceed the slot encoded byte budget.");
            totalEncodedBytes += file.EncodedLength;
        }
    }

    private static decimal? ReadNullableFunds(V3JsonStreamReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        string token = reader.GetNumberToken("manifest funds");
        if (!IsCanonicalFundsToken(token) || !decimal.TryParse(
                token,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal value))
        {
            throw new JsonException("Manifest funds uses an invalid decimal token.");
        }
        if (value == 0m && token.StartsWith("-", StringComparison.Ordinal))
            throw new JsonException("Manifest funds cannot use negative zero.");
        return value;
    }

    private static void ValidatePayloadLength(long encodedLength, string name)
    {
        if (encodedLength <= 0 || encodedLength > V3StorageBudget.MaximumPayloadEncodedBytes)
            throw new JsonException($"Manifest file '{name}' exceeds its encoded byte budget.");
    }

    private static bool ReadObjectProperty(
        V3JsonStreamReader reader,
        string context,
        out string property)
    {
        reader.ReadRequired(context);
        if (reader.TokenType == JsonTokenType.EndObject)
        {
            property = string.Empty;
            return false;
        }
        property = reader.GetPropertyName(context);
        return true;
    }

    private static void MarkField(ref ulong seen, ulong field, string property, string context)
    {
        if ((seen & field) != 0)
            throw new JsonException($"{context} contains duplicate property '{property}'.");
        seen |= field;
    }

    private static void RequireFields(ulong seen, ulong expected, string context)
    {
        if (seen != expected)
            throw new JsonException($"{context} does not contain exactly its required properties.");
    }

    private static bool IsCanonicalFundsToken(string token)
    {
        if (token.Length is 0 or > 64 || token[0] == '+')
            return false;
        int index = token[0] == '-' ? 1 : 0;
        if (index == token.Length)
            return false;
        int integerStart = index;
        while (index < token.Length && char.IsAsciiDigit(token[index]))
            index++;
        int integerLength = index - integerStart;
        if (integerLength == 0 || (integerLength > 1 && token[integerStart] == '0'))
            return false;
        if (index == token.Length)
            return true;
        if (token[index++] != '.')
            return false;
        int decimalLength = token.Length - index;
        if (decimalLength is < 1 or > 2)
            return false;
        while (index < token.Length)
        {
            if (!char.IsAsciiDigit(token[index++]))
                return false;
        }
        return true;
    }

    internal static void ValidateText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new JsonException($"Manifest {fieldName} cannot be empty or contain surrounding whitespace.");
        int scalars = 0;
        foreach (System.Text.Rune rune in value.EnumerateRunes())
        {
            if (System.Text.Rune.IsControl(rune))
                throw new JsonException($"Manifest {fieldName} cannot contain control characters.");
            scalars++;
        }
        if (scalars > MaximumTextScalars || Encoding.UTF8.GetByteCount(value) > MaximumTextUtf8Bytes)
            throw new JsonException($"Manifest {fieldName} exceeds its length budget.");
    }

    private static void ValidateThumbnailFileName(string fileName)
    {
        if (!fileName.EndsWith(".png", StringComparison.Ordinal) ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new JsonException("Manifest thumbnailFile must be a safe lowercase .png file name.");
        }
        foreach (char character in fileName[..^4])
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-')
                throw new JsonException("Manifest thumbnailFile contains an unsafe character.");
        }
    }
}
