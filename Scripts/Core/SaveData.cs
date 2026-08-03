using Godot;
using System;
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
    public string CityName { get; set; } = "Unknown City";

    [JsonPropertyName("population")]
    public long? Population { get; set; }

    [JsonPropertyName("funds")]
    public decimal? Funds { get; set; }

    [JsonPropertyName("thumbnailFile")]
    public string? ThumbnailFile { get; set; }

    /// <summary>存档包含的文件列表（不含路径，仅文件名）</summary>
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();
}

public sealed class SaveSlotSummary
{
    public string SlotID { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public DateTimeOffset? SavedAtUtc { get; init; }
    public string CityName { get; init; } = "Unknown City";
    public long? Population { get; init; }
    public decimal? Funds { get; init; }
    public string? ThumbnailPath { get; init; }
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
    public bool IsValid { get; init; }
    public string? Error { get; init; }
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
