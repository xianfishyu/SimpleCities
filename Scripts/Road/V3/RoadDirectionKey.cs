using Godot;
using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// 版本化 typed direction key：把原生几何链编码为可比较的 binary32 token 序列。
/// 用于固定 self-loop 的规范存储方向；排除 ID、RoadType、JSON、Length、Bounds、显示采样与 query fragment。
/// </summary>
public static class RoadDirectionKey
{
    private const byte Version = 1;

    private enum KindTag : byte
    {
        Line = 0,
        CubicBezier = 1,
        CubicHermite = 2,
        CircularArc = 3,
        Clothoid = 4,
        RationalQuadratic = 5,
    }

    public static byte[] Compute(IReadOnlyList<RoadGeometrySegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count == 0)
            throw new ArgumentException("A direction key requires at least one geometry segment.", nameof(segments));

        var buffer = new List<byte>(1 + segments.Count * 40)
        {
            Version,
        };

        foreach (RoadGeometrySegment segment in segments)
        {
            ArgumentNullException.ThrowIfNull(segment);
            WriteSegment(buffer, segment);
        }

        return buffer.ToArray();
    }

    public static int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        int common = Math.Min(left.Length, right.Length);
        for (int index = 0; index < common; index++)
        {
            int comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
                return comparison;
        }

        return left.Length.CompareTo(right.Length);
    }

    public static IReadOnlyList<RoadGeometrySegment> ReverseChain(
        IReadOnlyList<RoadGeometrySegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var reversed = new RoadGeometrySegment[segments.Count];
        for (int index = 0; index < segments.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(segments[index]);
            reversed[segments.Count - 1 - index] = RoadGeometryReverser.Reverse(segments[index]);
        }

        return reversed;
    }

    public static IReadOnlyList<RoadGeometrySegment> SelectCanonicalDirection(
        IReadOnlyList<RoadGeometrySegment> segments)
    {
        byte[] forward = Compute(segments);
        IReadOnlyList<RoadGeometrySegment> reversed = ReverseChain(segments);
        byte[] backward = Compute(reversed);

        return Compare(backward, forward) < 0 ? reversed : segments;
    }

    private static void WriteSegment(List<byte> buffer, RoadGeometrySegment segment)
    {
        switch (segment)
        {
            case LineRoadGeometrySegment line:
                buffer.Add((byte)KindTag.Line);
                WriteVector(buffer, line.Start);
                WriteVector(buffer, line.End);
                break;

            case CubicBezierRoadGeometrySegment bezier:
                buffer.Add((byte)KindTag.CubicBezier);
                WriteVector(buffer, bezier.Start);
                WriteVector(buffer, bezier.Control1);
                WriteVector(buffer, bezier.Control2);
                WriteVector(buffer, bezier.End);
                break;

            case CubicHermiteRoadGeometrySegment hermite:
                buffer.Add((byte)KindTag.CubicHermite);
                WriteVector(buffer, hermite.Start);
                WriteVector(buffer, hermite.StartTangent);
                WriteVector(buffer, hermite.End);
                WriteVector(buffer, hermite.EndTangent);
                break;

            case CircularArcRoadGeometrySegment arc:
                buffer.Add((byte)KindTag.CircularArc);
                WriteVector(buffer, arc.Center);
                WriteFloat(buffer, arc.Radius);
                WriteFloat(buffer, RoadNumericPolicy.NormalizeStartAngle(arc.StartAngle));
                WriteFloat(buffer, RoadNumericPolicy.NormalizeZero(arc.SweepAngle));
                break;

            case ClothoidRoadGeometrySegment clothoid:
                buffer.Add((byte)KindTag.Clothoid);
                WriteVector(buffer, clothoid.Start);
                WriteFloat(buffer, RoadNumericPolicy.NormalizeStartAngle(clothoid.StartHeading));
                WriteFloat(buffer, clothoid.StartCurvature);
                WriteFloat(buffer, clothoid.EndCurvature);
                WriteFloat(buffer, clothoid.ArcLength);
                break;

            case RationalQuadraticRoadGeometrySegment rational:
                buffer.Add((byte)KindTag.RationalQuadratic);
                WriteVector(buffer, rational.Start);
                WriteFloat(buffer, rational.StartWeight);
                WriteVector(buffer, rational.Control);
                WriteFloat(buffer, rational.ControlWeight);
                WriteVector(buffer, rational.End);
                WriteFloat(buffer, rational.EndWeight);
                break;

            default:
                throw new NotSupportedException($"Unsupported geometry kind: {segment.Kind}.");
        }
    }

    private static void WriteVector(List<byte> buffer, Vector2 vector)
    {
        WriteFloat(buffer, vector.X);
        WriteFloat(buffer, vector.Y);
    }

    private static void WriteFloat(List<byte> buffer, float value)
    {
        uint totalOrderBits = ToTotalOrderBits(RoadNumericPolicy.NormalizeZero(value));
        buffer.Add((byte)(totalOrderBits >> 24));
        buffer.Add((byte)(totalOrderBits >> 16));
        buffer.Add((byte)(totalOrderBits >> 8));
        buffer.Add((byte)totalOrderBits);
    }

    private static uint ToTotalOrderBits(float value)
    {
        uint bits = (uint)BitConverter.SingleToInt32Bits(value);
        return (bits & 0x80000000u) != 0u ? ~bits : bits | 0x80000000u;
    }
}
