using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

internal sealed record V3DeletionDescriptor(
    string SlotID,
    string OperationToken,
    long UIGeneration,
    SaveSlotOccupantKind OccupantKind,
    string OccupantDigest,
    string TombstonePath,
    string ConfirmationSummary);

internal static class V3DeletionDescriptorCodec
{
    internal const int MaximumEncodedBytes = 4 * 1024;
    internal const string TombstonePath = "tombstone";

    internal static void Write(Stream destination, V3DeletionDescriptor descriptor)
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
        writer.WriteString("descriptorType", "delete");
        writer.WriteString("slotId", descriptor.SlotID);
        writer.WriteString("operationToken", descriptor.OperationToken);
        writer.WriteNumber("uiGeneration", descriptor.UIGeneration);
        writer.WriteString("occupantKind", ToToken(descriptor.OccupantKind));
        writer.WriteString("occupantDigest", descriptor.OccupantDigest);
        writer.WriteString("tombstonePath", descriptor.TombstonePath);
        writer.WriteString("confirmationSummary", descriptor.ConfirmationSummary);
        writer.WriteEndObject();
        writer.Flush();
    }

    internal static V3DeletionDescriptor Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanSeek)
            throw new InvalidDataException("Deletion descriptor stream must be seekable.");
        if (source.Length is < 2 or > MaximumEncodedBytes)
            throw new InvalidDataException("Deletion descriptor exceeds its byte budget.");

        using JsonDocument document = JsonDocument.Parse(source, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 4,
        });
        IReadOnlyDictionary<string, JsonElement> fields = V3Json.ReadObject(
            document.RootElement,
            "V3 deletion descriptor",
            "formatFamily",
            "schemaVersion",
            "descriptorType",
            "slotId",
            "operationToken",
            "uiGeneration",
            "occupantKind",
            "occupantDigest",
            "tombstonePath",
            "confirmationSummary");
        if (V3Json.ReadString(fields["formatFamily"], "deletion formatFamily") != V3Json.FormatFamily)
            throw new JsonException("Deletion formatFamily is not simple-cities-v3.");
        if (V3Json.ReadNonNegativeInt32(fields["schemaVersion"], "deletion schemaVersion") !=
            V3Json.SchemaVersion)
        {
            throw new JsonException("Deletion schemaVersion is not supported.");
        }
        if (V3Json.ReadString(fields["descriptorType"], "deletion descriptorType") != "delete")
            throw new JsonException("Deletion descriptorType is not delete.");

        long generation = V3Json.ReadNonNegativeInt64(fields["uiGeneration"], "deletion uiGeneration");
        if (generation == 0)
            throw new JsonException("Deletion uiGeneration must be positive.");
        var descriptor = new V3DeletionDescriptor(
            V3Json.ReadString(fields["slotId"], "deletion slotId"),
            V3Json.ReadString(fields["operationToken"], "deletion operationToken"),
            generation,
            ParseOccupantKind(V3Json.ReadString(fields["occupantKind"], "deletion occupantKind")),
            V3Json.ReadCanonicalSha256(fields["occupantDigest"], "deletion occupantDigest"),
            V3Json.ReadString(fields["tombstonePath"], "deletion tombstonePath"),
            V3Json.ReadString(fields["confirmationSummary"], "deletion confirmationSummary"));
        Validate(descriptor);
        return descriptor;
    }

    private static void Validate(V3DeletionDescriptor descriptor)
    {
        SaveSlotStore.ValidateSlotID(descriptor.SlotID);
        V3PublicationDescriptorCodec.ValidateOperationToken(descriptor.OperationToken);
        if (descriptor.UIGeneration <= 0)
            throw new JsonException("Deletion uiGeneration must be positive.");
        _ = ToToken(descriptor.OccupantKind);
        ValidateDigest(descriptor.OccupantDigest);
        if (descriptor.TombstonePath != TombstonePath)
            throw new JsonException("Deletion tombstonePath is not canonical.");
        V3ManifestCodec.ValidateText(descriptor.ConfirmationSummary, "deletion confirmationSummary");
    }

    private static string ToToken(SaveSlotOccupantKind kind) => kind switch
    {
        SaveSlotOccupantKind.CompleteV3 => "completeV3",
        SaveSlotOccupantKind.CorruptV3 => "corruptV3",
        _ => throw new JsonException("Deletion occupantKind must be completeV3 or corruptV3."),
    };

    private static SaveSlotOccupantKind ParseOccupantKind(string token) => token switch
    {
        "completeV3" => SaveSlotOccupantKind.CompleteV3,
        "corruptV3" => SaveSlotOccupantKind.CorruptV3,
        _ => throw new JsonException("Deletion occupantKind is not supported."),
    };

    private static void ValidateDigest(string digest)
    {
        if (digest.Length != 64)
            throw new JsonException("Deletion occupantDigest must be a canonical SHA-256 digest.");
        foreach (char character in digest)
        {
            if (!char.IsAsciiHexDigit(character) || char.IsAsciiLetterUpper(character))
                throw new JsonException("Deletion occupantDigest must be a canonical SHA-256 digest.");
        }
    }
}
