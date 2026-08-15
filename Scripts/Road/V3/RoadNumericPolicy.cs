using Godot;
using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 mutation 与 format v1 load 共用的数值边界。
/// 所有写入权威状态的坐标、长度和角度都必须经过这里的范围与规范化检查。
/// </summary>
public static class RoadNumericPolicy
{
    public const float MaxCoordinateMagnitude = 1_000_000f;
    public const float MaxSegmentLength = 1_000_000f;
    public const float MaxTotalLength = 1_000_000_000f;
    public const float MaxClusterDiameter = 1e-3f;

    public static float NormalizeZero(float value) => value == 0f ? 0f : value;

    public static Vector2 NormalizeVector(Vector2 value) =>
        new(NormalizeZero(value.X), NormalizeZero(value.Y));

    public static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);

    public static bool IsWithinCoordinateRange(float x, float y) =>
        float.IsFinite(x) &&
        float.IsFinite(y) &&
        MathF.Abs(x) <= MaxCoordinateMagnitude &&
        MathF.Abs(y) <= MaxCoordinateMagnitude;

    public static bool IsWithinCoordinateRange(Vector2 value) =>
        IsWithinCoordinateRange(value.X, value.Y);

    public static bool IsWithinSegmentLengthRange(float length) =>
        float.IsFinite(length) && length > 0f && length <= MaxSegmentLength;

    public static bool IsWithinTotalLengthRange(double totalLength) =>
        double.IsFinite(totalLength) && totalLength > 0d && totalLength <= MaxTotalLength;

    /// <summary>
    /// 使用受检 double 中间值计算距离平方，避免 binary32 平方溢出。
    /// 返回 double；调用方负责把最终长度落回许可范围。
    /// </summary>
    public static double CheckedDistanceSquared(Vector2 a, Vector2 b)
    {
        if (!IsWithinCoordinateRange(a) || !IsWithinCoordinateRange(b))
            throw new ArgumentOutOfRangeException(nameof(a), "Coordinates must be finite and within the V3 numeric range.");

        double dx = (double)b.X - a.X;
        double dy = (double)b.Y - a.Y;
        return dx * dx + dy * dy;
    }

    public static bool TryGetFiniteSegmentLength(Vector2 a, Vector2 b, out float length)
    {
        if (!IsWithinCoordinateRange(a) || !IsWithinCoordinateRange(b))
        {
            length = 0f;
            return false;
        }

        double distanceSquared = CheckedDistanceSquared(a, b);
        double distance = Math.Sqrt(distanceSquared);
        if (!double.IsFinite(distance) || distance <= 0d || distance > MaxSegmentLength)
        {
            length = 0f;
            return false;
        }

        length = (float)distance;
        return true;
    }

    /// <summary>
    /// 把角度规范到 [0, Tau)，并把 -0 规范为 +0。
    /// </summary>
    public static float NormalizeStartAngle(float angle)
    {
        if (!float.IsFinite(angle))
            throw new ArgumentOutOfRangeException(nameof(angle), angle, "Start angle must be finite.");

        float normalized = Mathf.PosMod(angle, Mathf.Tau);
        if (normalized == Mathf.Tau)
            normalized = 0f;
        return NormalizeZero(normalized);
    }

    /// <summary>
    /// 只有 sweep 绝对值与 canonical binary32 Tau 逐 bit 相等时才算 full-turn。
    /// BitDecrement(Tau) 仍是开弧，BitIncrement(Tau) 越界拒绝。
    /// </summary>
    public static bool IsExactFullTurn(float sweepAngle)
    {
        if (!float.IsFinite(sweepAngle))
            return false;

        int magnitudeBits = BitConverter.SingleToInt32Bits(MathF.Abs(sweepAngle));
        return magnitudeBits == BitConverter.SingleToInt32Bits(Mathf.Tau);
    }

    public static float BitDecrement(float value) => MathF.BitDecrement(value);

    public static float BitIncrement(float value) => MathF.BitIncrement(value);
}
