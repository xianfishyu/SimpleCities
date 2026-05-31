using Godot;

/// <summary>
/// 共享网格系统：集中管理 CellSize / SnapToGrid / IsSnapGrid。
/// 替代 RoadNetwork 中的静态方法，消除到处传 cellSize 参数。
///
/// 初始化：RoadSystem._Ready() 中注入 RoadConfig。
/// </summary>
public static class GridSystem
{
    /// <summary>共享配置资源（在 RoadSystem._Ready 中注入）</summary>
    public static RoadConfig Config { get; set; } = null!;

    /// <summary>当前网格单元尺寸。未初始化时返回默认 64。</summary>
    public static float CellSize => Config?.CellSize ?? 64f;

    /// <summary>将世界坐标对齐到最近的网格原点（cellSize 整数倍）。</summary>
    public static Vector2 SnapToGrid(Vector2 pos) =>
        new(
            Mathf.Round(pos.X / CellSize) * CellSize,
            Mathf.Round(pos.Y / CellSize) * CellSize
        );

    /// <summary>位置是否落在标准 snap 格点上（cellSize 的整数倍）。</summary>
    public static bool IsSnapGrid(Vector2 pos)
    {
        float rx = pos.X / CellSize;
        float ry = pos.Y / CellSize;
        return Mathf.Abs(rx - Mathf.Round(rx)) < 1e-3f
            && Mathf.Abs(ry - Mathf.Round(ry)) < 1e-3f;
    }
}
