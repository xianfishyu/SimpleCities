using Godot;
using System;
using System.Collections.Generic;

public partial class TimetableManager : Node2D
{
    // 车站定义
    private static Station start = new("亦庄", StationType.带越行线的两台四线, 40f);
    private static Station beijing = new("北京南", StationType.四台七线, 40f);
    private static Station end = new("武清", StationType.带越行线的两台四线, 40f);
    
    // 铁路定义
    private static Railway railway1 = new("Line1", 220f, start, beijing);
    private static Railway railway2 = new("MainLine", 620f, start, end);

    // 全局正线Y坐标
    private const float MainLine1Y = 0f;
    private const float MainLine2Y = 10f;

    // 车站位置
    private Vector2 beijingPosition;
    private Vector2 startPosition;
    private Vector2 endPosition;

    public override void _Ready()
    {
        // 计算车站位置（正线对齐）
        beijingPosition = CalculateStationPosition(beijing, 0);
        startPosition = CalculateStationPosition(start, 220);
        endPosition = CalculateStationPosition(end, railway2.Length + 220 + 100);

        // 渲染车站
        RenderStation(beijing, beijingPosition, "BeijingStation");
        RenderStation(start, startPosition, "StartStation");
        RenderStation(end, endPosition, "EndStation");

        // 生成铁路线
        var mainLineConfig = new CrossoverGenerator.MainLineConfig 
        { 
            MainLine1Y = MainLine1Y, 
            MainLine2Y = MainLine2Y 
        };

        RailwayLineGenerator.GenerateBetweenStations(this, beijing, beijingPosition, start, startPosition, MainLine1Y, MainLine2Y);
        RailwayLineGenerator.GenerateBetweenStations(this, start, startPosition, end, endPosition, MainLine1Y, MainLine2Y);

        // 生成渡线
        // 北京南 → 亦庄
        CrossoverGenerator.GenerateStationExitCrossovers(this, beijing, beijingPosition, mainLineConfig);
        CrossoverGenerator.GenerateStationEntryCrossovers(this, start, startPosition, mainLineConfig);

        // 亦庄 → 武清
        CrossoverGenerator.GenerateStationExitCrossovers(this, start, startPosition, mainLineConfig);
        CrossoverGenerator.GenerateStationEntryCrossovers(this, end, endPosition, mainLineConfig);
    }

    /// <summary>
    /// 计算车站位置（使正线对齐到全局正线）
    /// </summary>
    private Vector2 CalculateStationPosition(Station station, float xPosition)
    {
        var (mainLine1Y, _) = GetMainLineY(station);
        return new Vector2(xPosition, MainLine1Y - mainLine1Y);
    }

    /// <summary>
    /// 渲染车站
    /// </summary>
    private void RenderStation(Station station, Vector2 position, string name)
    {
        var renderer = new StationRenderer();
        renderer.Name = name;
        AddChild(renderer);
        renderer.DrawStation(station, position);
    }

    /// <summary>
    /// 计算车站正线的Y坐标
    /// </summary>
    private (float mainLine1Y, float mainLine2Y) GetMainLineY(Station station)
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

            case StationType.四台七线_天津:
                float yT = 0;
                yT += platformWidth / 2 + trackSpacing / 2;  // 到发线1
                yT += trackSpacing;                          // 到发线2
                yT += trackSpacing / 2 + platformWidth / 2;  // 站台2
                yT += platformWidth / 2 + trackSpacing / 2;  // 到发线3
                yT += trackSpacing;
                float mainT1 = yT;  // 正线1
                yT += trackSpacing / 2 + platformWidth / 2;  // 站台3
                yT += platformWidth / 2 + trackSpacing / 2;
                float mainT2 = yT;  // 正线2
                return (mainT1, mainT2);

            default:
                return (0, trackSpacing);
        }
    }
}
