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
}
