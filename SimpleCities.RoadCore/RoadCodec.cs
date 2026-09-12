using System.Text.Json;

namespace SimpleCities.RoadCore;

/// <summary>完整验证后的纯内容；不携带运行时网络身份。</summary>
public sealed class PreparedRoadState
{
    internal PreparedRoadState(MapDefinition map, long contentRevision, long nextNodeId, long nextEdgeId,
        IEnumerable<RoadNode> nodes, IEnumerable<RoadEdge> edges)
    {
        Map = map;
        ContentRevision = contentRevision;
        NextNodeId = nextNodeId;
        NextEdgeId = nextEdgeId;
        Nodes = Array.AsReadOnly(nodes.ToArray());
        Edges = Array.AsReadOnly(edges.ToArray());
    }

    public MapDefinition Map { get; }
    public long ContentRevision { get; }
    public long NextNodeId { get; }
    public long NextEdgeId { get; }
    public IReadOnlyList<RoadNode> Nodes { get; }
    public IReadOnlyList<RoadEdge> Edges { get; }
}

/// <summary>V4 独立道路 schema 2；有界 stream 入口，后续多道路格式扩展时重新审定预算。</summary>
public static class RoadCodec
{
    public const int MaximumPayloadBytes = 4096;
    public const int SchemaVersion = 2;

    public static void Write(Stream destination, RoadSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var writer = new Utf8JsonWriter(destination);
        writer.WriteStartObject();
        writer.WriteString("formatFamily", "simple-cities-v4");
        writer.WriteString("payloadType", "road-network");
        writer.WriteNumber("schemaVersion", SchemaVersion);
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
        foreach (RoadNode node in snapshot.Nodes)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", node.Id.Value);
            writer.WriteNumber("x", node.Position.X);
            writer.WriteNumber("y", node.Position.Y);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteStartArray("edges");
        foreach (RoadEdge edge in snapshot.Edges)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", edge.Id.Value);
            writer.WriteNumber("startNodeId", edge.Start.Value);
            writer.WriteNumber("endNodeId", edge.End.Value);
            writer.WriteString("profile", edge.Profile.Value);
            writer.WriteEndObject();
        }
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
            throw new InvalidDataException("V4 independent-road payload exceeds 4096 bytes.");
        using JsonDocument document = JsonDocument.Parse(bytes.AsMemory(0, count),
            new JsonDocumentOptions { MaxDepth = 4 });
        JsonElement root = document.RootElement;
        Fields(root, "formatFamily", "payloadType", "schemaVersion", "contentRevision",
            "nextNodeId", "nextEdgeId", "profileCatalogVersion", "map", "nodes", "edges");
        RequireText(root, "formatFamily", "simple-cities-v4");
        RequireText(root, "payloadType", "road-network");
        RequireNumber(root, "schemaVersion", SchemaVersion);
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
        var definition = new MapDefinition((int)cell);
        long nextNode = Positive(root, "nextNodeId");
        long nextEdge = Positive(root, "nextEdgeId");
        JsonElement nodesArray = root.GetProperty("nodes");
        JsonElement edgesArray = root.GetProperty("edges");
        if (nodesArray.ValueKind != JsonValueKind.Array || edgesArray.ValueKind != JsonValueKind.Array ||
            !((nodesArray.GetArrayLength() == 0 && edgesArray.GetArrayLength() == 0) ||
              (nodesArray.GetArrayLength() == 2 && edgesArray.GetArrayLength() == 1)))
            throw new InvalidDataException("This V4 slice supports an empty map or one independent road.");
        var nodes = new List<RoadNode>();
        foreach (JsonElement node in nodesArray.EnumerateArray())
        {
            Fields(node, "id", "x", "y");
            long id = Positive(node, "id");
            var point = new RoadPoint(Coordinate(node, "x"), Coordinate(node, "y"));
            if (id >= nextNode || (nodes.Count != 0 && id <= nodes[^1].Id.Value) || !definition.IsPrimaryPoint(point))
                throw new InvalidDataException("V4 nodes must have sorted unique IDs below the watermark and legal grid coordinates.");
            nodes.Add(new RoadNode(new NodeId(id), point));
        }
        var edges = new List<RoadEdge>();
        foreach (JsonElement edge in edgesArray.EnumerateArray())
        {
            Fields(edge, "id", "startNodeId", "endNodeId", "profile");
            long id = Positive(edge, "id");
            long startId = Positive(edge, "startNodeId");
            long endId = Positive(edge, "endNodeId");
            RoadNode? start = nodes.Find(node => node.Id.Value == startId);
            RoadNode? end = nodes.Find(node => node.Id.Value == endId);
            JsonElement profile = edge.GetProperty("profile");
            string? name = profile.ValueKind == JsonValueKind.String ? profile.GetString() : null;
            if (id >= nextEdge || start is null || end is null || start.Id == end.Id ||
                start.Position == end.Position || !definition.IsEightDirection(start.Position, end.Position) ||
                name is not ("dirt" or "street" or "arterial" or "highway"))
                throw new InvalidDataException("Invalid V4 independent road endpoints, profile or watermark.");
            edges.Add(new RoadEdge(new EdgeId(id), start.Id, end.Id, new RoadProfileId(name)));
        }
        return new PreparedRoadState(definition, Positive(root, "contentRevision"), nextNode, nextEdge, nodes, edges);
    }

    private static double Coordinate(JsonElement value, string name)
    {
        JsonElement field = value.GetProperty(name);
        if (field.ValueKind != JsonValueKind.Number || !field.TryGetDouble(out double number) || !double.IsFinite(number))
            throw new InvalidDataException($"V4 coordinate '{name}' must be finite.");
        return number;
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
