using Godot;

/// <summary>
/// 车站绘制器 - 根据 Station 数据绘制车站轨道布局
/// </summary>
public partial class StationRenderer : Node2D
{
    [Export] public float TrackSpacing = 10f;      // 轨道间距（米）
    [Export] public float TrackWidth = 1.435f;     // 轨道宽度（标准轨距）
    [Export] public Color MainLineColor = Colors.Black;   // 正线颜色
    [Export] public Color PlatformLineColor = Colors.Black;   // 到发线颜色
    [Export] public Color PlatformColor = Colors.Yellow;     // 站台颜色
    [Export] public float PlatformWidth = 5f;      // 站台宽度

    private Station station;
    private float stationLength;

    public override void _Ready()
    {
    }

    /// <summary>
    /// 设置并绘制车站
    /// </summary>
    public void DrawStation(Station stationData, Vector2 startPosition)
    {
        station = stationData;
        stationLength = station.StationLength;
        GlobalPosition = startPosition;

        // 清除之前的绘制
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        // 根据站型绘制
        switch (station.Type)
        {
            case StationType.带越行线的两台四线:
                Draw带越行线的两台四线();
                break;
            case StationType.不带越行线的两台四线:
                Draw不带越行线的两台四线();
                break;
            case StationType.四台七线:
                Draw四台七线();
                break;
            case StationType.四台七线_天津:
                Draw四台七线_天津();
                break;
        }

        // 绘制站名标签
        DrawStationLabel();
    }

    /// <summary>
    /// 带越行线的两台四线布局
    /// 站台1
    /// 到发线1
    /// 正线1 (越行线)
    /// 正线2 (越行线)
    /// 到发线2
    /// 站台2
    /// </summary>
    private void Draw带越行线的两台四线()
    {
        float y = 0;

        // 站台1
        AddPlatform(new Vector2(0, y), stationLength, "1站台");
        y += PlatformWidth / 2 + TrackSpacing / 2;

        // 到发线1
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing;

        // 正线1（越行线）
        AddTrack(new Vector2(0, y), stationLength, MainLineColor, true);
        y += TrackSpacing;

        // 正线2（越行线）
        AddTrack(new Vector2(0, y), stationLength, MainLineColor, true);
        y += TrackSpacing;

        // 到发线2
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing / 2 + PlatformWidth / 2;

        // 站台2
        AddPlatform(new Vector2(0, y), stationLength, "2站台");
    }

    /// <summary>
    /// 不带越行线的两台四线布局
    /// 到发线1
    /// 站台
    /// 到发线2
    /// 站台
    /// 到发线3
    /// 到发线4
    /// </summary>
    private void Draw不带越行线的两台四线()
    {
        float y = 0;

        // 到发线1（兼正线）
        AddTrack(new Vector2(0, y), stationLength, MainLineColor, true);
        
        // 站台1
        y += TrackSpacing / 2;
        AddPlatform(new Vector2(0, y), stationLength, "1站台");
        y += TrackSpacing / 2;

        // 到发线2
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing;

        // 到发线3
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        
        // 站台2
        y += TrackSpacing / 2;
        AddPlatform(new Vector2(0, y), stationLength, "2站台");
        y += TrackSpacing / 2;

        // 到发线4（兼正线）
        AddTrack(new Vector2(0, y), stationLength, MainLineColor, true);
    }

    /// <summary>
    /// 四台七线布局
    /// 到发线 站台 到发线 到发线 站台 "到发线 到发线"(正线) 站台 到发线 到发线 站台 到发线
    /// </summary>
    private void Draw四台七线()
    {
        float y = 0;
        
        // 到发线1
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing / 2 + PlatformWidth / 2;
        
        // 站台1
        AddPlatform(new Vector2(0, y), stationLength, "1台");
        y += PlatformWidth / 2 + TrackSpacing / 2;
        
        // 到发线2
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing;
        
        // 到发线3
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing / 2 + PlatformWidth / 2;
        
        // 站台2
        AddPlatform(new Vector2(0, y), stationLength, "2台");
        y += PlatformWidth / 2 + TrackSpacing / 2;
        
        // 正线1（居中）
        AddTrack(new Vector2(0, y), stationLength, MainLineColor, true);
        y += TrackSpacing;
        
        // 正线2（居中）
        AddTrack(new Vector2(0, y), stationLength, MainLineColor, true);
        y += TrackSpacing / 2 + PlatformWidth / 2;
        
        // 站台3
        AddPlatform(new Vector2(0, y), stationLength, "3台");
        y += PlatformWidth / 2 + TrackSpacing / 2;
        
        // 到发线4
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing;
        
        // 到发线5
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing / 2 + PlatformWidth / 2;
        
        // 站台4
        AddPlatform(new Vector2(0, y), stationLength, "4台");
    }

    /// <summary>
    /// 四台七线_天津布局（天津站特殊布局：正线中间夹着站台）
    /// 站台 到发线 到发线 站台 到发线 "到发线"(正线1) 站台 "到发线"(正线2) 到发线 站台 到发线
    /// </summary>
    private void Draw四台七线_天津()
    {
        float y = 0;
        
        // 站台1
        AddPlatform(new Vector2(0, y), stationLength, "1台");
        y += PlatformWidth / 2 + TrackSpacing / 2;
        
        // 到发线1
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing;
        
        // 到发线2
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing / 2 + PlatformWidth / 2;
        
        // 站台2
        AddPlatform(new Vector2(0, y), stationLength, "2台");
        y += PlatformWidth / 2 + TrackSpacing / 2;
        
        // 到发线3
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing;
        
        // 正线1（到发线4）
        AddTrack(new Vector2(0, y), stationLength, MainLineColor, true);
        y += TrackSpacing / 2 + PlatformWidth / 2;
        
        // 站台3（夹在两条正线之间）
        AddPlatform(new Vector2(0, y), stationLength, "3台");
        y += PlatformWidth / 2 + TrackSpacing / 2;
        
        // 正线2（到发线5）
        AddTrack(new Vector2(0, y), stationLength, MainLineColor, true);
        y += TrackSpacing;
        
        // 到发线6
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
        y += TrackSpacing / 2 + PlatformWidth / 2;
        
        // 站台4
        AddPlatform(new Vector2(0, y), stationLength, "4台");
        y += PlatformWidth / 2 + TrackSpacing / 2;
        
        // 到发线7
        AddTrack(new Vector2(0, y), stationLength, PlatformLineColor, false);
    }

    /// <summary>
    /// 添加轨道线
    /// </summary>
    private void AddTrack(Vector2 start, float length, Color color, bool isMainLine)
    {
        Line2D track = new Line2D();
        track.AddPoint(start);
        track.AddPoint(start + new Vector2(length, 0));
        track.Width = TrackWidth;
        track.DefaultColor = color;
        track.ZIndex = -1;
        AddChild(track);
    }

    /// <summary>
    /// 添加站台
    /// </summary>
    private void AddPlatform(Vector2 position, float length, string platformName)
    {
        // 站台矩形
        ColorRect platform = new ColorRect();
        platform.Size = new Vector2(length, PlatformWidth);
        platform.Position = position - new Vector2(0, PlatformWidth / 2);
        platform.Color = PlatformColor;
        platform.ZIndex = -2;
        AddChild(platform);
    }

    /// <summary>
    /// 绘制站名标签
    /// </summary>
    private void DrawStationLabel()
    {
        Label label = new Label();
        label.Text = station.Name;
        label.Position = new Vector2(stationLength / 2 - 60, -80);
        label.AddThemeColorOverride("font_color", Colors.Black);
        label.AddThemeFontSizeOverride("font_size", 32);
        AddChild(label);
    }
}
