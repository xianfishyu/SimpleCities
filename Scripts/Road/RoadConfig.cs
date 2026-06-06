using Godot;

/// <summary>
/// 共享配置资源：网格尺寸 + 渲染参数。
/// 在 Godot 编辑器中创建一个 .tres 文件，由 RoadBuilder / RoadRenderer / RoadSystem 共同引用，
/// 避免多处 [Export] _cellSize 各自漂移导致逻辑与渲染错位。
/// </summary>
[GlobalClass]
public partial class RoadConfig : Resource
{
    /// <summary>网格单元尺寸（像素）。所有 Road 端点 / waypoint 都对齐到 cell 中心点。</summary>
    [Export] public float CellSize { get; set; } = 64f;

    /// <summary>道路颜色。</summary>
    [Export] public Color RoadColor { get; set; } = new("#37474F");

    /// <summary>道路线宽（像素）。</summary>
    [Export] public float RoadWidth { get; set; } = 12f;

    /// <summary>
    /// 真路口（ConnectionCount >= 3 或 ConnectionCount == 2 且方向非对向）的圆点半径。
    /// 用于让 T 字、十字、转弯点在视觉上明显区别于"一条直路"。
    /// </summary>
    [Export] public float JunctionRadius { get; set; } = 10f;

    /// <summary>真路口圆点颜色。默认偏黄，便于在深色路面上一眼可见。</summary>
    [Export] public Color JunctionColor { get; set; } = new("#FFC107");

    /// <summary>端点（ConnectionCount == 1）圆点半径。可设 0 关掉端点显示。</summary>
    [Export] public float EndpointRadius { get; set; } = 6f;

    /// <summary>端点圆点颜色（区别于 JunctionColor 与 RoadColor，便于辨认）。</summary>
    [Export] public Color EndpointColor { get; set; } = new("#90A4AE");

    /// <summary>拆除工具悬停高亮色（半透明亮色，叠加在路面上）。</summary>
    [Export] public Color HoverHighlightColor { get; set; } = new(1f, 0.8f, 0.2f, 0.6f);

    /// <summary>拆除工具悬停高亮线宽（比 RoadWidth 稍宽以视觉突出）。</summary>
    [Export] public float HoverHighlightWidth { get; set; } = 18f;

    // ── 道路分级样式（Phase 6 准备）──────────────────────
    /// <summary>四种道路等级的样式（索引对应 RoadType 枚举值）。</summary>
    [Export] public RoadTypeStyle[] TypeStyles { get; set; } = new RoadTypeStyle[4]
    {
        new() { Color = new Color(0.5f, 0.4f, 0.3f), Width = 2f },   // Dirt
        new() { Color = new Color(0.8f, 0.8f, 0.8f), Width = 4f },   // Street
        new() { Color = new Color(0.9f, 0.7f, 0.2f), Width = 6f },   // Arterial
        new() { Color = new Color(0.2f, 0.6f, 1.0f), Width = 8f },   // Highway
    };

    /// <summary>按 RoadType 取样式。越界时回退 Street 样式。</summary>
    public RoadTypeStyle GetStyle(RoadType type)
    {
        int idx = (int)type;
        if (TypeStyles == null || idx < 0 || idx >= TypeStyles.Length)
            return new RoadTypeStyle { Color = RoadColor, Width = RoadWidth };
        return TypeStyles[idx];
    }
}

[GlobalClass]
public partial class RoadTypeStyle : Resource
{
    [Export] public Color Color { get; set; } = new Color(0.8f, 0.8f, 0.8f);
    [Export] public float Width { get; set; } = 4f;
    [Export] public float DashLength { get; set; } = 0f; // 0 = 实线
}
