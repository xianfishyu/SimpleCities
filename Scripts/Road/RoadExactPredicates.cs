using Godot;
using System;
using BigInteger = System.Numerics.BigInteger;

internal static class RoadExactPredicates
{
    internal static int Orient2DSign(Vector2 a, Vector2 b, Vector2 c)
    {
        EnsureFinite(a, nameof(a));
        EnsureFinite(b, nameof(b));
        EnsureFinite(c, nameof(c));

        Dyadic abX = Dyadic.FromFloat(b.X) - Dyadic.FromFloat(a.X);
        Dyadic abY = Dyadic.FromFloat(b.Y) - Dyadic.FromFloat(a.Y);
        Dyadic acX = Dyadic.FromFloat(c.X) - Dyadic.FromFloat(a.X);
        Dyadic acY = Dyadic.FromFloat(c.Y) - Dyadic.FromFloat(a.Y);
        return ((abX * acY) - (abY * acX)).Sign;
    }

    internal static int DotSign(Vector2 first, Vector2 second)
    {
        EnsureFinite(first, nameof(first));
        EnsureFinite(second, nameof(second));

        Dyadic value = Dyadic.FromFloat(first.X) * Dyadic.FromFloat(second.X) +
                       Dyadic.FromFloat(first.Y) * Dyadic.FromFloat(second.Y);
        return value.Sign;
    }

    internal static bool CanMergeForwardLines(
        LineRoadGeometrySegment first,
        LineRoadGeometrySegment second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (!SameBits(first.End, second.Start))
            return false;
        if (Orient2DSign(first.Start, first.End, second.End) != 0)
            return false;
        return DotSign(first.End - first.Start, second.End - second.Start) > 0;
    }

    internal static bool SameBits(Vector2 first, Vector2 second) =>
        BitConverter.SingleToInt32Bits(first.X) == BitConverter.SingleToInt32Bits(second.X) &&
        BitConverter.SingleToInt32Bits(first.Y) == BitConverter.SingleToInt32Bits(second.Y);

    private static void EnsureFinite(Vector2 value, string parameterName)
    {
        if (!value.IsFinite())
            throw new ArgumentException("Exact predicates require finite coordinates.", parameterName);
    }

    private readonly record struct Dyadic(BigInteger Significand, int Exponent)
    {
        internal int Sign => Significand.Sign;

        internal static Dyadic FromFloat(float value)
        {
            int bits = BitConverter.SingleToInt32Bits(value);
            int sign = (bits & int.MinValue) == 0 ? 1 : -1;
            int exponentBits = (bits >> 23) & 0xff;
            int fraction = bits & 0x7fffff;

            if (exponentBits == 0xff)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be finite.");
            if (exponentBits == 0)
                return fraction == 0
                    ? new Dyadic(BigInteger.Zero, 0)
                    : new Dyadic(sign * new BigInteger(fraction), -149);

            int significand = fraction | 0x800000;
            return new Dyadic(sign * new BigInteger(significand), exponentBits - 150);
        }

        public static Dyadic operator +(Dyadic left, Dyadic right)
        {
            int exponent = Math.Min(left.Exponent, right.Exponent);
            BigInteger leftValue = left.Significand << (left.Exponent - exponent);
            BigInteger rightValue = right.Significand << (right.Exponent - exponent);
            return new Dyadic(leftValue + rightValue, exponent);
        }

        public static Dyadic operator -(Dyadic left, Dyadic right) =>
            left + new Dyadic(-right.Significand, right.Exponent);

        public static Dyadic operator *(Dyadic left, Dyadic right) =>
            new(left.Significand * right.Significand, left.Exponent + right.Exponent);
    }
}
