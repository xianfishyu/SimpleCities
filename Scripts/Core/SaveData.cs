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
    public int? SchemaVersion { get; set; }

    [JsonPropertyName("slotId")]
    public string SlotID { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("cityName")]
    public string CityName { get; set; } = "My City";

    /// <summary>存档包含的文件列表（不含路径，仅文件名）</summary>
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();
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
