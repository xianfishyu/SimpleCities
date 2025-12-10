using Godot;

/// <summary>
/// 编辑器配置 - 可导出的属性
/// </summary>
public class EditorConfig
{
    // 网格设置
    public float GridSize { get; set; } = 10f;
    public bool SnapToGrid { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public Color GridColor { get; set; } = new Color(0.3f, 0.3f, 0.3f, 0.3f);

    // 视觉设置
    public float NodeRadius { get; set; } = 4f;
    public float TrackWidth { get; set; } = 2f;
    public float StationHandleSize { get; set; } = 6f;

    // 轨道颜色
    public Color MainLineColor { get; set; } = new Color(0.1f, 0.1f, 0.5f);
    public Color ArrivalDepartureColor { get; set; } = new Color(0.2f, 0.2f, 0.2f);
    public Color MainLineWithPlatformColor { get; set; } = new Color(0.3f, 0.1f, 0.5f);
    public Color CrossoverColor { get; set; } = new Color(0.1f, 0.4f, 0.1f);

    // 节点颜色
    public Color EndpointColor { get; set; } = new Color(1f, 0.2f, 0.2f);
    public Color ConnectionColor { get; set; } = Colors.SkyBlue;
    public Color SwitchColor { get; set; } = new Color(0.8f, 0.2f, 0.8f);
    public Color SelectedColor { get; set; } = new Color(1f, 1f, 0f);
    public Color HoverColor { get; set; } = new Color(0.5f, 1f, 0.5f);

    // 站场颜色
    public Color StationBorderColor { get; set; } = new Color(0.8f, 0.6f, 0.2f, 0.8f);
    public Color StationLabelColor { get; set; } = new Color(1.637f, 1.241f, 0.436f);
    public Color StationHandleColor { get; set; } = new Color(1f, 0.8f, 0.2f, 1f);

    /// <summary>
    /// 获取节点颜色
    /// </summary>
    public Color GetNodeColor(RailwayNodeType type)
    {
        return type switch
        {
            RailwayNodeType.Endpoint => EndpointColor,
            RailwayNodeType.Switch => SwitchColor,
            _ => ConnectionColor
        };
    }

    /// <summary>
    /// 获取轨道颜色
    /// </summary>
    public Color GetTrackColor(TrackType type)
    {
        return type switch
        {
            TrackType.MainLine => MainLineColor,
            TrackType.ArrivalDeparture => ArrivalDepartureColor,
            TrackType.MainLineWithPlatform => MainLineWithPlatformColor,
            TrackType.Crossover => CrossoverColor,
            _ => ArrivalDepartureColor
        };
    }

    /// <summary>
    /// 对齐位置到网格
    /// </summary>
    public Vector2 SnapPosition(Vector2 pos)
    {
        return new Vector2(
            Mathf.Round(pos.X / GridSize) * GridSize,
            Mathf.Round(pos.Y / GridSize) * GridSize
        );
    }
}
