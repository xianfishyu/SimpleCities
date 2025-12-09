using Godot;
using System;

/// <summary>
/// 铁路线生成器 - 自动生成两条平行的正线连接车站
/// </summary>
public static class RailwayLineGenerator
{
    /// <summary>
    /// 铁路线配置
    /// </summary>
    public class RailwayConfig
    {
        public float TrackWidth { get; set; } = 1.435f;      // 轨道宽度（标准轨距）
        public float TrackSpacing { get; set; } = 10f;       // 双轨间距
        public Color LineColor { get; set; } = Colors.Black; // 铁路线颜色
        public int ZIndex { get; set; } = -1;                // 层级
    }

    /// <summary>
    /// 生成双轨铁路线（连接两个车站）
    /// </summary>
    /// <param name="parent">父节点</param>
    /// <param name="startX">起点X坐标（起始站右边界）</param>
    /// <param name="endX">终点X坐标（终点站左边界）</param>
    /// <param name="mainLine1Y">正线1的Y坐标</param>
    /// <param name="mainLine2Y">正线2的Y坐标</param>
    /// <param name="config">配置</param>
    public static void GenerateDoubleTrack(
        Node2D parent,
        float startX,
        float endX,
        float mainLine1Y = 0f,
        float mainLine2Y = 10f,
        RailwayConfig config = null)
    {
        config ??= new RailwayConfig();

        // 正线1
        DrawRailwayLine(parent,
            new Vector2(startX, mainLine1Y),
            new Vector2(endX, mainLine1Y),
            config);

        // 正线2
        DrawRailwayLine(parent,
            new Vector2(startX, mainLine2Y),
            new Vector2(endX, mainLine2Y),
            config);
    }

    /// <summary>
    /// 生成连接两个车站的双轨铁路线
    /// </summary>
    /// <param name="parent">父节点</param>
    /// <param name="startStation">起始站</param>
    /// <param name="startPosition">起始站位置</param>
    /// <param name="endStation">终点站</param>
    /// <param name="endPosition">终点站位置</param>
    /// <param name="mainLine1Y">正线1的Y坐标</param>
    /// <param name="mainLine2Y">正线2的Y坐标</param>
    /// <param name="config">配置</param>
    public static void GenerateBetweenStations(
        Node2D parent,
        Station startStation,
        Vector2 startPosition,
        Station endStation,
        Vector2 endPosition,
        float mainLine1Y = 0f,
        float mainLine2Y = 10f,
        RailwayConfig config = null)
    {
        float startX = startPosition.X + startStation.StationLength;
        float endX = endPosition.X;

        GenerateDoubleTrack(parent, startX, endX, mainLine1Y, mainLine2Y, config);
    }

    /// <summary>
    /// 绘制单条铁路线
    /// </summary>
    private static void DrawRailwayLine(Node2D parent, Vector2 from, Vector2 to, RailwayConfig config)
    {
        Line2D line = new Line2D();
        line.AddPoint(from);
        line.AddPoint(to);
        line.Width = config.TrackWidth;
        line.DefaultColor = config.LineColor;
        line.ZIndex = config.ZIndex;
        parent.AddChild(line);
    }
}
