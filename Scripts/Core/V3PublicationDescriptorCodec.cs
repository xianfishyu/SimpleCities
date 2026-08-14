using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

internal sealed record V3PublicationDescriptor(
    string SlotID,
    string OperationToken,
    string? OldAggregateDigest,
    string NewAggregateDigest,
    string StagingPath,
    string BackupPath);

internal static class V3PublicationDescriptorCodec
{
    internal const int MaximumEncodedBytes = 4 * 1024;
    internal const string StagingPath = "staging";
    internal const string BackupPath = "backup";

    internal static void Write(Stream destination, V3PublicationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(descriptor);
        Validate(descriptor);

        using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false,
        });
        writer.WriteStartObject();
        writer.WriteString("formatFamily", V3Json.FormatFamily);
        writer.WriteNumber("schemaVersion", V3Json.SchemaVersion);
        writer.WriteString("descriptorType", "publish");
        writer.WriteString("slotId", descriptor.SlotID);
        writer.WriteString("operationToken", descriptor.OperationToken);
        if (descriptor.OldAggregateDigest is null)
            writer.WriteNull("oldAggregateDigest");
        else
            writer.WriteString("oldAggregateDigest", descriptor.OldAggregateDigest);
        writer.WriteString("newAggregateDigest", descriptor.NewAggregateDigest);
        writer.WriteString("stagingPath", descriptor.StagingPath);
        writer.WriteString("backupPath", descriptor.BackupPath);
        writer.WriteEndObject();
        writer.Flush();
    }

    internal static V3PublicationDescriptor Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanSeek)
            throw new InvalidDataException("Publication descriptor stream must be seekable.");
        if (source.Length is < 2 or > MaximumEncodedBytes)
            throw new InvalidDataException("Publication descriptor exceeds its byte budget.");

        using JsonDocument document = JsonDocument.Parse(source, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 4,
        });
        IReadOnlyDictionary<string, JsonElement> fields = V3Json.ReadObject(
            document.RootElement,
            "V3 publication descriptor",
            "formatFamily",
            "schemaVersion",
            "descriptorType",
            "slotId",
            "operationToken",
            "oldAggregateDigest",
            "newAggregateDigest",
            "stagingPath",
            "backupPath");
        if (V3Json.ReadString(fields["formatFamily"], "publication formatFamily") != V3Json.FormatFamily)
            throw new JsonException("Publication formatFamily is not simple-cities-v3.");
        if (V3Json.ReadNonNegativeInt32(fields["schemaVersion"], "publication schemaVersion") !=
            V3Json.SchemaVersion)
        {
            throw new JsonException("Publication schemaVersion is not supported.");
        }
        if (V3Json.ReadString(fields["descriptorType"], "publication descriptorType") != "publish")
            throw new JsonException("Publication descriptorType is not publish.");

        var descriptor = new V3PublicationDescriptor(
            V3Json.ReadString(fields["slotId"], "publication slotId"),
            V3Json.ReadString(fields["operationToken"], "publication operationToken"),
            fields["oldAggregateDigest"].ValueKind == JsonValueKind.Null
                ? null
                : V3Json.ReadCanonicalSha256(
                    fields["oldAggregateDigest"], "publication oldAggregateDigest"),
            V3Json.ReadCanonicalSha256(
                fields["newAggregateDigest"], "publication newAggregateDigest"),
            V3Json.ReadString(fields["stagingPath"], "publication stagingPath"),
            V3Json.ReadString(fields["backupPath"], "publication backupPath"));
        Validate(descriptor);
        return descriptor;
    }

    internal static void ValidateOperationToken(string operationToken)
    {
        if (operationToken.Length != 32)
            throw new JsonException("Publication operationToken must be 32 lowercase hexadecimal characters.");
        foreach (char character in operationToken)
        {
            if (!char.IsAsciiHexDigit(character) || char.IsAsciiLetterUpper(character))
                throw new JsonException("Publication operationToken must be 32 lowercase hexadecimal characters.");
        }
    }

    private static void Validate(V3PublicationDescriptor descriptor)
    {
        SaveSlotStore.ValidateSlotID(descriptor.SlotID);
        ValidateOperationToken(descriptor.OperationToken);
        if (descriptor.OldAggregateDigest is not null)
            ValidateDigest(descriptor.OldAggregateDigest, "oldAggregateDigest");
        ValidateDigest(descriptor.NewAggregateDigest, "newAggregateDigest");
        if (descriptor.StagingPath != StagingPath || descriptor.BackupPath != BackupPath)
            throw new JsonException("Publication descriptor paths are not canonical.");
    }

    private static void ValidateDigest(string digest, string fieldName)
    {
        if (digest.Length != 64)
            throw new JsonException($"Publication {fieldName} must be a canonical SHA-256 digest.");
        foreach (char character in digest)
        {
            if (!char.IsAsciiHexDigit(character) || char.IsAsciiLetterUpper(character))
                throw new JsonException($"Publication {fieldName} must be a canonical SHA-256 digest.");
        }
    }
}
