using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

/// <summary>
/// 时刻表条目类型
/// </summary>
public enum ScheduleEventType
{
    /// <summary>到达</summary>
    Arrival,
    /// <summary>出发</summary>
    Departure
}

/// <summary>
/// 单条时刻表记录
/// </summary>
public class ScheduleEntry
{
    /// <summary>时间（格式: "HH:mm"）</summary>
    [JsonPropertyName("time")]
    public string Time { get; set; }

    /// <summary>车站名称</summary>
    [JsonPropertyName("station")]
    public string Station { get; set; }

    /// <summary>站台编号</summary>
    [JsonPropertyName("track")]
    public int Track { get; set; }

    /// <summary>事件类型（到/开）</summary>
    [JsonPropertyName("event")]
    public ScheduleEventType Event { get; set; }

    /// <summary>
    /// 获取时间的分钟数（从00:00开始）
    /// </summary>
    [JsonIgnore]
    public int TimeInMinutes
    {
        get
        {
            var parts = Time.Split(':');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int hours) &&
                int.TryParse(parts[1], out int minutes))
            {
                return hours * 60 + minutes;
            }
            return 0;
        }
    }

    /// <summary>
    /// 获取时间的秒数（从00:00开始）
    /// </summary>
    [JsonIgnore]
    public int TimeInSeconds => TimeInMinutes * 60;

    public ScheduleEntry() { }

    public ScheduleEntry(string time, string station, int track, ScheduleEventType eventType)
    {
        Time = time;
        Station = station;
        Track = track;
        Event = eventType;
    }

    /// <summary>
    /// 从简写字符串解析
    /// 格式: "18:47 北京南 站台20 开" 或 "18:57 亦庄 站台1 到"
    /// </summary>
    public static ScheduleEntry Parse(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return null;

        string time = parts[0];
        string station = parts[1];

        // 解析站台编号（格式: "站台20" 或 "20"）
        string trackStr = parts[2];
        if (trackStr.StartsWith("站台"))
            trackStr = trackStr[2..];
        int.TryParse(trackStr, out int track);

        // 解析事件类型
        string eventStr = parts[3];
        ScheduleEventType eventType = eventStr switch
        {
            "到" => ScheduleEventType.Arrival,
            "开" => ScheduleEventType.Departure,
            _ => ScheduleEventType.Departure
        };

        return new ScheduleEntry(time, station, track, eventType);
    }
}

/// <summary>
/// 列车时刻表
/// </summary>
public class TrainSchedule
{
    /// <summary>列车车次</summary>
    [JsonPropertyName("trainId")]
    public string TrainId { get; set; }

    /// <summary>运行方向（true=下行/北京到天津，false=上行）</summary>
    [JsonPropertyName("isDownbound")]
    public bool IsDownbound { get; set; } = true;

    /// <summary>时刻表条目列表</summary>
    [JsonPropertyName("entries")]
    public List<ScheduleEntry> Entries { get; set; } = new();

    public TrainSchedule() { }

    public TrainSchedule(string trainId, bool isDownbound = true)
    {
        TrainId = trainId;
        IsDownbound = isDownbound;
    }

    /// <summary>
    /// 从文本解析时刻表
    /// </summary>
    public static TrainSchedule ParseFromText(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return null;

        // 第一行是车次
        string trainId = lines[0].Trim();
        var schedule = new TrainSchedule(trainId);

        for (int i = 1; i < lines.Length; i++)
        {
            var entry = ScheduleEntry.Parse(lines[i].Trim());
            if (entry != null)
            {
                schedule.Entries.Add(entry);
            }
        }

        return schedule;
    }

    /// <summary>
    /// 获取第一个出发时间（秒）
    /// </summary>
    public int GetFirstDepartureTimeSeconds()
    {
        foreach (var entry in Entries)
        {
            if (entry.Event == ScheduleEventType.Departure)
                return entry.TimeInSeconds;
        }
        return 0;
    }

    /// <summary>
    /// 获取最后一个到达时间（秒）
    /// </summary>
    public int GetLastArrivalTimeSeconds()
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            if (Entries[i].Event == ScheduleEventType.Arrival)
                return Entries[i].TimeInSeconds;
        }
        return 0;
    }

    /// <summary>
    /// 获取总运行时间（秒）
    /// </summary>
    public int GetTotalTravelTimeSeconds()
    {
        return GetLastArrivalTimeSeconds() - GetFirstDepartureTimeSeconds();
    }

    /// <summary>
    /// 从 JSON 文件加载时刻表
    /// </summary>
    public static TrainSchedule LoadFromFile(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        string json = System.IO.File.ReadAllText(absolutePath);
        return JsonSerializer.Deserialize<TrainSchedule>(json, GetJsonOptions());
    }

    /// <summary>
    /// 保存时刻表到 JSON 文件
    /// </summary>
    public void SaveToFile(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        string json = JsonSerializer.Serialize(this, GetJsonOptions());
        System.IO.File.WriteAllText(absolutePath, json);
    }

    /// <summary>
    /// 获取 JSON 序列化选项
    /// </summary>
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}

