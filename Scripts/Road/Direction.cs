using Godot;

public enum Direction
{
    N, NE, E, SE, S, SW, W, NW
}

public static class DirectionUtil
{
    public static Vector2I GetDisplacement(Direction d) => d switch
    {
        Direction.N  => new(0, -1),
        Direction.NE => new(1, -1),
        Direction.E  => new(1, 0),
        Direction.SE => new(1, 1),
        Direction.S  => new(0, 1),
        Direction.SW => new(-1, 1),
        Direction.W  => new(-1, 0),
        Direction.NW => new(-1, -1),
        _ => Vector2I.Zero
    };

    public static Direction? FromDisplacement(Vector2 from, Vector2 to, float cellSize)
    {
        float dx = (to.X - from.X) / cellSize;
        float dy = (to.Y - from.Y) / cellSize;
        int dc = Mathf.RoundToInt(dx);
        int dr = Mathf.RoundToInt(dy);

        foreach (var d in All)
        {
            var disp = GetDisplacement(d);
            if (disp.X == dc && disp.Y == dr) return d;
        }
        return null;
    }

    /// <summary>
    /// 判断 from→to 的位移方向是否为 8 方向之一（不要求位移长度等于一个单位 cellSize）。
    /// 用于：Segment 端点为非格点的"半格 Junction"时，从端点到第一个内部 waypoint 的位移
    /// 是 8 方向但距离 < cellSize；正方向的判定靠归一化向量与 8 单位方向的余弦匹配。
    /// </summary>
    public static Direction? FromDisplacementAnyLength(Vector2 from, Vector2 to)
    {
        Vector2 v = to - from;
        float len = v.Length();
        if (len < 1e-4f) return null;
        Vector2 n = v / len;

        Direction? best = null;
        float bestDot = 0.999f; // 严格匹配阈值：cos 误差 < 1e-3 → 约 2.5°
        foreach (var d in All)
        {
            var disp = GetDisplacement(d);
            float ux = disp.X, uy = disp.Y;
            float ulen = Mathf.Sqrt(ux * ux + uy * uy);
            ux /= ulen; uy /= ulen;
            float dot = n.X * ux + n.Y * uy;
            if (dot > bestDot)
            {
                bestDot = dot;
                best = d;
            }
        }
        return best;
    }

    public static bool IsOrthogonal(Direction d) =>
        d is Direction.N or Direction.E or Direction.S or Direction.W;

    public static bool IsDiagonal(Direction d) =>
        d is Direction.NE or Direction.SE or Direction.SW or Direction.NW;

    public static float Length(Direction d, float cellSize) =>
        IsDiagonal(d) ? cellSize * Mathf.Sqrt(2) : cellSize;

    public static Direction[] All { get; } =
    [
        Direction.N, Direction.NE, Direction.E, Direction.SE,
        Direction.S, Direction.SW, Direction.W, Direction.NW
    ];
}
