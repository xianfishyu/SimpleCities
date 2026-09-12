using System.Text.Json;

namespace SimpleCities.RoadCore;

/// <summary>完整验证后的纯内容；不携带运行时网络身份。</summary>
public sealed class PreparedRoadState
{
    internal PreparedRoadState(MapDefinition map, long contentRevision, long nextNodeId, long nextEdgeId)
    {
        Map = map;
        ContentRevision = contentRevision;
        NextNodeId = nextNodeId;
        NextEdgeId = nextEdgeId;
    }

    public MapDefinition Map { get; }
    public long ContentRevision { get; }
    public long NextNodeId { get; }
    public long NextEdgeId { get; }
}

/// <summary>V4 空地图 schema 1；有界 stream 入口，后续实体格式扩展时重新审定预算。</summary>
public static class RoadCodec
{
    public const int MaximumPayloadBytes = 4096;

    public static void Write(Stream destination, RoadSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var writer = new Utf8JsonWriter(destination);
        writer.WriteStartObject();
        writer.WriteString("formatFamily", "simple-cities-v4");
        writer.WriteString("payloadType", "road-network");
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteNumber("contentRevision", snapshot.Token.ContentRevision);
        writer.WriteNumber("nextNodeId", snapshot.NextNodeId);
        writer.WriteNumber("nextEdgeId", snapshot.NextEdgeId);
        writer.WriteNumber("profileCatalogVersion", 1);
        writer.WriteStartObject("map");
        writer.WriteNumber("widthMetres", 8000);
        writer.WriteNumber("heightMetres", 8000);
        writer.WriteString("origin", "center");
        writer.WriteNumber("metresPerUnit", 1);
        writer.WriteString("grid", "square-eight");
        writer.WriteNumber("cellSizeMetres", snapshot.Map.CellSizeMetres);
        writer.WriteEndObject();
        writer.WriteStartArray("nodes");
        writer.WriteEndArray();
        writer.WriteStartArray("edges");
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }

    public static PreparedRoadState Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        // One extra byte detects oversized input, including non-seekable streams.
        byte[] bytes = new byte[MaximumPayloadBytes + 1];
        int count = 0;
        while (count < bytes.Length)
        {
            int read = source.Read(bytes, count, bytes.Length - count);
            if (read == 0)
                break;
            count += read;
        }
        if (count > MaximumPayloadBytes)
            throw new InvalidDataException("V4 empty-map payload exceeds 4096 bytes.");
        using JsonDocument document = JsonDocument.Parse(bytes.AsMemory(0, count),
            new JsonDocumentOptions { MaxDepth = 4 });
        JsonElement root = document.RootElement;
        Fields(root, "formatFamily", "payloadType", "schemaVersion", "contentRevision",
            "nextNodeId", "nextEdgeId", "profileCatalogVersion", "map", "nodes", "edges");
        RequireText(root, "formatFamily", "simple-cities-v4");
        RequireText(root, "payloadType", "road-network");
        RequireNumber(root, "schemaVersion", 1);
        RequireNumber(root, "profileCatalogVersion", 1);
        JsonElement map = root.GetProperty("map");
        Fields(map, "widthMetres", "heightMetres", "origin", "metresPerUnit", "grid", "cellSizeMetres");
        RequireNumber(map, "widthMetres", 8000);
        RequireNumber(map, "heightMetres", 8000);
        RequireText(map, "origin", "center");
        RequireNumber(map, "metresPerUnit", 1);
        RequireText(map, "grid", "square-eight");
        long cell = Number(map, "cellSizeMetres");
        if (cell is not (25 or 50 or 100 or 200))
            throw new InvalidDataException("V4 cellSizeMetres must be 25, 50, 100 or 200.");
        foreach (string name in new[] { "nodes", "edges" })
        {
            JsonElement array = root.GetProperty(name);
            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != 0)
                throw new InvalidDataException($"V4 empty-map schema requires an empty {name} array.");
        }
        return new PreparedRoadState(new MapDefinition((int)cell),
            Positive(root, "contentRevision"), Positive(root, "nextNodeId"), Positive(root, "nextEdgeId"));
    }

    private static void Fields(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Expected a V4 object.");
        var remaining = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
            if (!remaining.Remove(property.Name))
                throw new InvalidDataException($"Unknown or duplicate V4 field '{property.Name}'.");
        if (remaining.Count != 0)
            throw new InvalidDataException($"Missing V4 field '{remaining.First()}'.");
    }

    private static long Number(JsonElement value, string name)
    {
        JsonElement field = value.GetProperty(name);
        if (field.ValueKind != JsonValueKind.Number || !field.TryGetInt64(out long number))
            throw new InvalidDataException($"V4 field '{name}' must be an integer.");
        return number;
    }

    private static long Positive(JsonElement value, string name)
    {
        long number = Number(value, name);
        if (number <= 0 || number == long.MaxValue)
            throw new InvalidDataException($"V4 field '{name}' must be positive and leave allocation headroom.");
        return number;
    }

    private static void RequireNumber(JsonElement value, string name, long expected)
    {
        if (Number(value, name) != expected)
            throw new InvalidDataException($"Unsupported V4 {name}; expected {expected}.");
    }

    private static void RequireText(JsonElement value, string name, string expected)
    {
        JsonElement field = value.GetProperty(name);
        if (field.ValueKind != JsonValueKind.String || field.GetString() != expected)
            throw new InvalidDataException($"Unsupported V4 {name}; expected '{expected}'.");
    }
}
