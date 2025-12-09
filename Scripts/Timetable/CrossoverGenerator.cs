using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 渡线生成器 - 自动生成车站入口/出口渡线
/// 只需要提供轨道Y坐标列表，即可自动生成45度角渡线连接
/// </summary>
public static class CrossoverGenerator
{
    /// <summary>
    /// 轨道参数配置
    /// </summary>
    public class TrackConfig
    {
        public float TrackWidth { get; set; } = 1.435f;      // 轨道宽度（标准轨距）
        public float TrackSpacing { get; set; } = 10f;       // 轨道间距
        public float PlatformWidth { get; set; } = 5f;       // 站台宽度
        public Color CrossoverColor { get; set; } = Colors.Black;  // 渡线颜色
        public int ZIndex { get; set; } = -2;                // 渡线层级
    }

    /// <summary>
    /// 全局正线配置
    /// </summary>
    public class MainLineConfig
    {
        public float MainLine1Y { get; set; } = 0f;          // 正线1的Y坐标
        public float MainLine2Y { get; set; } = 10f;         // 正线2的Y坐标
    }

    /// <summary>
    /// 生成出口渡线（从车站到发线连接到全局正线，向右分叉）
    /// </summary>
    /// <param name="parent">父节点，用于添加Line2D子节点</param>
    /// <param name="stationEndX">车站右边界X坐标</param>
    /// <param name="upperTracks">上半部分轨道Y坐标列表（连接到正线1）</param>
    /// <param name="lowerTracks">下半部分轨道Y坐标列表（连接到正线2）</param>
    /// <param name="mainLine">全局正线配置</param>
    /// <param name="config">轨道参数配置</param>
    public static void GenerateExitCrossovers(
        Node2D parent,
        float stationEndX,
        List<float> upperTracks,
        List<float> lowerTracks,
        MainLineConfig mainLine = null,
        TrackConfig config = null)
    {
        mainLine ??= new MainLineConfig();
        config ??= new TrackConfig();

        // 上半部分：各轨道 → 正线1
        foreach (float trackY in upperTracks)
        {
            float len = Mathf.Abs(mainLine.MainLine1Y - trackY);
            DrawCrossoverLine(parent,
                new Vector2(stationEndX, trackY),
                new Vector2(stationEndX + len, mainLine.MainLine1Y),
                config);
        }

        // 下半部分：各轨道 → 正线2
        foreach (float trackY in lowerTracks)
        {
            float len = Mathf.Abs(trackY - mainLine.MainLine2Y);
            DrawCrossoverLine(parent,
                new Vector2(stationEndX, trackY),
                new Vector2(stationEndX + len, mainLine.MainLine2Y),
                config);
        }
    }

    /// <summary>
    /// 生成入口渡线（从全局正线连接到车站到发线，向右分叉）
    /// </summary>
    /// <param name="parent">父节点，用于添加Line2D子节点</param>
    /// <param name="stationStartX">车站左边界X坐标</param>
    /// <param name="upperTracks">上半部分轨道Y坐标列表（从正线1连接）</param>
    /// <param name="lowerTracks">下半部分轨道Y坐标列表（从正线2连接）</param>
    /// <param name="mainLine">全局正线配置</param>
    /// <param name="config">轨道参数配置</param>
    public static void GenerateEntryCrossovers(
        Node2D parent,
        float stationStartX,
        List<float> upperTracks,
        List<float> lowerTracks,
        MainLineConfig mainLine = null,
        TrackConfig config = null)
    {
        mainLine ??= new MainLineConfig();
        config ??= new TrackConfig();

        // 上半部分：正线1 → 各轨道
        foreach (float trackY in upperTracks)
        {
            float len = Mathf.Abs(mainLine.MainLine1Y - trackY);
            DrawCrossoverLine(parent,
                new Vector2(stationStartX - len, mainLine.MainLine1Y),
                new Vector2(stationStartX, trackY),
                config);
        }

        // 下半部分：正线2 → 各轨道
        foreach (float trackY in lowerTracks)
        {
            float len = Mathf.Abs(trackY - mainLine.MainLine2Y);
            DrawCrossoverLine(parent,
                new Vector2(stationStartX - len, mainLine.MainLine2Y),
                new Vector2(stationStartX, trackY),
                config);
        }
    }

    /// <summary>
    /// 根据车站类型自动获取上半部分轨道Y坐标列表
    /// </summary>
    public static List<float> GetUpperTracks(Station station, float stationGlobalY)
    {
        var tracks = new List<float>();
        float platformWidth = 5f;
        float trackSpacing = 10f;

        switch (station.Type)
        {
            case StationType.带越行线的两台四线:
                // 到发线1: platformWidth/2 + trackSpacing/2 = 7.5
                tracks.Add(stationGlobalY + platformWidth / 2 + trackSpacing / 2);
                break;

            case StationType.不带越行线的两台四线:
                // 没有需要连接的上部轨道（外侧就是正线）
                break;

            case StationType.四台七线:
                // 到发线1: 0, 到发线2: 15, 到发线3: 25
                float y = 0;
                tracks.Add(stationGlobalY + y);  // 到发线1
                y += trackSpacing / 2 + platformWidth / 2;  // 站台1
                y += platformWidth / 2 + trackSpacing / 2;
                tracks.Add(stationGlobalY + y);  // 到发线2
                y += trackSpacing;
                tracks.Add(stationGlobalY + y);  // 到发线3
                break;
        }

        return tracks;
    }

    /// <summary>
    /// 根据车站类型自动获取下半部分轨道Y坐标列表
    /// </summary>
    public static List<float> GetLowerTracks(Station station, float stationGlobalY)
    {
        var tracks = new List<float>();
        float platformWidth = 5f;
        float trackSpacing = 10f;

        switch (station.Type)
        {
            case StationType.带越行线的两台四线:
                // 到发线2: localMainLine2Y + trackSpacing
                var (_, localMainLine2Y) = GetMainLineY(station);
                tracks.Add(stationGlobalY + localMainLine2Y + trackSpacing);
                break;

            case StationType.不带越行线的两台四线:
                // 没有需要连接的下部轨道（外侧就是正线）
                break;

            case StationType.四台七线:
                // 计算下半部分轨道Y坐标
                float y = 0;
                y += trackSpacing / 2 + platformWidth / 2;  // 站台1
                y += platformWidth / 2 + trackSpacing / 2;  // 到发线2
                y += trackSpacing;                          // 到发线3
                y += trackSpacing / 2 + platformWidth / 2;  // 站台2
                y += platformWidth / 2 + trackSpacing / 2;  // 正线1
                y += trackSpacing;                          // 正线2
                y += trackSpacing / 2 + platformWidth / 2;  // 站台3
                y += platformWidth / 2 + trackSpacing / 2;
                tracks.Add(stationGlobalY + y);  // 到发线4
                y += trackSpacing;
                tracks.Add(stationGlobalY + y);  // 到发线5
                y += trackSpacing / 2 + platformWidth / 2;  // 站台4
                y += platformWidth / 2 + trackSpacing / 2;
                tracks.Add(stationGlobalY + y);  // 到发线6
                break;
        }

        return tracks;
    }

    /// <summary>
    /// 一键生成车站出口渡线
    /// </summary>
    public static void GenerateStationExitCrossovers(
        Node2D parent,
        Station station,
        Vector2 stationPosition,
        MainLineConfig mainLine = null,
        TrackConfig config = null)
    {
        float stationEndX = stationPosition.X + station.StationLength;
        var upperTracks = GetUpperTracks(station, stationPosition.Y);
        var lowerTracks = GetLowerTracks(station, stationPosition.Y);

        GenerateExitCrossovers(parent, stationEndX, upperTracks, lowerTracks, mainLine, config);
    }

    /// <summary>
    /// 一键生成车站入口渡线
    /// </summary>
    public static void GenerateStationEntryCrossovers(
        Node2D parent,
        Station station,
        Vector2 stationPosition,
        MainLineConfig mainLine = null,
        TrackConfig config = null)
    {
        float stationStartX = stationPosition.X;
        var upperTracks = GetUpperTracks(station, stationPosition.Y);
        var lowerTracks = GetLowerTracks(station, stationPosition.Y);

        GenerateEntryCrossovers(parent, stationStartX, upperTracks, lowerTracks, mainLine, config);
    }

    /// <summary>
    /// 计算车站正线的Y坐标
    /// </summary>
    private static (float mainLine1Y, float mainLine2Y) GetMainLineY(Station station)
    {
        float platformWidth = 5f;
        float trackSpacing = 10f;

        switch (station.Type)
        {
            case StationType.带越行线的两台四线:
                float line1Y = platformWidth / 2 + trackSpacing / 2 + trackSpacing;  // 17.5
                float line2Y = line1Y + trackSpacing;  // 27.5
                return (line1Y, line2Y);

            case StationType.不带越行线的两台四线:
                return (platformWidth / 2 + trackSpacing / 2, 
                        platformWidth / 2 + trackSpacing / 2 + trackSpacing);

            case StationType.四台七线:
                float y = 0;
                y += trackSpacing / 2 + platformWidth / 2;  // 站台1
                y += platformWidth / 2 + trackSpacing / 2;  // 到发线2
                y += trackSpacing;                          // 到发线3
                y += trackSpacing / 2 + platformWidth / 2;  // 站台2
                y += platformWidth / 2 + trackSpacing / 2;
                float main1 = y;  // 正线1: 40
                y += trackSpacing;
                float main2 = y;  // 正线2: 50
                return (main1, main2);

            default:
                return (0, trackSpacing);
        }
    }

    /// <summary>
    /// 绘制单条渡线
    /// </summary>
    private static void DrawCrossoverLine(Node2D parent, Vector2 from, Vector2 to, TrackConfig config)
    {
        Line2D line = new Line2D();
        line.AddPoint(from);
        line.AddPoint(to);
        line.Width = config.TrackWidth;
        line.DefaultColor = config.CrossoverColor;
        line.ZIndex = config.ZIndex;
        parent.AddChild(line);
    }
}
