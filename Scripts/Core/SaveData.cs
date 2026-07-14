using Godot;
using System.Collections.Generic;
using System.Text.Json.Serialization;

// ============================================================================
// 存档 DTO - 纯数据类，与运行时类型解耦，避免循环引用
// ============================================================================

#region Manifest

public class ManifestData
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("slotName")]
    public string SlotName { get; set; } = "autosave";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("cityName")]
    public string CityName { get; set; } = "My City";

    /// <summary>存档包含的文件列表（不含路径，仅文件名）</summary>
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();
}

#endregion

#region RoadNetwork

public class RoadNetworkData
{
    [JsonPropertyName("nextID")]
    public int NextID { get; set; }

    [JsonPropertyName("cellSize")]
    public float CellSize { get; set; }

    [JsonPropertyName("junctions")]
    public List<JunctionData> Junctions { get; set; } = new();

    [JsonPropertyName("segments")]
    public List<SegmentData> Segments { get; set; } = new();

    [JsonPropertyName("roads")]
    public List<RoadData> Roads { get; set; } = new();
}

public class JunctionData
{
    [JsonPropertyName("id")]
    public int ID { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}

public class SegmentData
{
    [JsonPropertyName("id")]
    public int ID { get; set; }

    [JsonPropertyName("fromJunctionID")]
    public int FromJunctionID { get; set; }

    [JsonPropertyName("toJunctionID")]
    public int ToJunctionID { get; set; }

    [JsonPropertyName("roadID")]
    public int RoadID { get; set; }

    [JsonPropertyName("waypoints")]
    public List<Vector2Data> Waypoints { get; set; } = new();

    [JsonPropertyName("totalLength")]
    public float TotalLength { get; set; }

    /// <summary>
    /// Road tier (see <see cref="RoadType"/>). Nullable so legacy v1 saves
    /// without this field fall back to <see cref="RoadType.Street"/>.
    /// </summary>
    [JsonPropertyName("type")]
    public int? Type { get; set; }
}

public class RoadData
{
    [JsonPropertyName("id")]
    public int ID { get; set; }

    [JsonPropertyName("segmentIDs")]
    public List<int> SegmentIDs { get; set; } = new();

    /// <summary>
    /// Group road tier. Nullable — legacy v1 saves default to <see cref="RoadType.Street"/>.
    /// </summary>
    [JsonPropertyName("type")]
    public int? Type { get; set; }
}

/// <summary>Vector2 的 JSON 表示，避免序列化私有字段</summary>
public class Vector2Data
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    public Vector2Data() { }

    public Vector2Data(Vector2 v)
    {
        X = v.X;
        Y = v.Y;
    }

    public Vector2 ToVector2() => new(X, Y);
}

#endregion

#region Camera

public class CameraData
{
    [JsonPropertyName("positionX")]
    public float PositionX { get; set; }

    [JsonPropertyName("positionY")]
    public float PositionY { get; set; }

    [JsonPropertyName("zoom")]
    public float Zoom { get; set; }
}

#endregion
