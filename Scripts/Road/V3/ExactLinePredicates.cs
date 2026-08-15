using Godot;
using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// line primitive 的无损合并谓词。禁止使用普通 float cross、IsEqualApprox 或角度近似。
/// </summary>
public static class ExactLinePredicates
{
    public static bool BitwiseEquals(Vector2 left, Vector2 right) =>
        BitConverter.SingleToInt32Bits(left.X) == BitConverter.SingleToInt32Bits(right.X) &&
        BitConverter.SingleToInt32Bits(left.Y) == BitConverter.SingleToInt32Bits(right.Y);

    /// <summary>
    /// 返回 Orient2D(A, M, B) 的符号：-1 / 0 / 1。
    /// 使用 double 中间值，binary32 坐标的乘法与减法在 double 中精确。
    /// </summary>
    public static int Orient2D(Vector2 a, Vector2 m, Vector2 b)
    {
        double ax = a.X;
        double ay = a.Y;
        double mx = m.X;
        double my = m.Y;
        double bx = b.X;
        double by = b.Y;

        double cross = (mx - ax) * (by - ay) - (my - ay) * (bx - ax);
        if (cross == 0d)
            return 0;
        return cross > 0d ? 1 : -1;
    }

    public static bool IsSameDirection(Vector2 a, Vector2 m, Vector2 b)
    {
        double ax = a.X;
        double ay = a.Y;
        double mx = m.X;
        double my = m.Y;
        double bx = b.X;
        double by = b.Y;

        double dot = (mx - ax) * (bx - mx) + (my - ay) * (by - my);
        return dot > 0d;
    }

    /// <summary>
    /// 两个 line 段只有在公共端点逐 bit 相同、共线且同向时才可无损合并为 Line(A, B)。
    /// 偏移 1 ULP 的折点、回头和反向重叠都必须保留。
    /// </summary>
    public static bool CanMergeLineSegments(LineRoadGeometrySegment first, LineRoadGeometrySegment second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (!BitwiseEquals(first.End, second.Start))
            return false;
        if (Orient2D(first.Start, first.End, second.End) != 0)
            return false;
        return IsSameDirection(first.Start, first.End, second.End);
    }
}
