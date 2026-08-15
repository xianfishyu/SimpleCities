using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadNumericPolicyTests
{
    [Fact]
    public void NormalizeZero_NegativeZero_ReturnsPositiveZero()
    {
        float normalized = RoadNumericPolicy.NormalizeZero(-0f);

        Assert.Equal(0f, normalized);
        Assert.Equal(0, BitConverter.SingleToInt32Bits(normalized));
    }

    [Fact]
    public void IsExactFullTurn_RecognizesOnlyCanonicalBinary32Tau()
    {
        Assert.True(RoadNumericPolicy.IsExactFullTurn(Mathf.Tau));
        Assert.True(RoadNumericPolicy.IsExactFullTurn(-Mathf.Tau));
        Assert.False(RoadNumericPolicy.IsExactFullTurn(RoadNumericPolicy.BitDecrement(Mathf.Tau)));
        Assert.False(RoadNumericPolicy.IsExactFullTurn(RoadNumericPolicy.BitIncrement(Mathf.Tau)));
        Assert.False(RoadNumericPolicy.IsExactFullTurn(0f));
        Assert.False(RoadNumericPolicy.IsExactFullTurn(float.NaN));
        Assert.False(RoadNumericPolicy.IsExactFullTurn(float.PositiveInfinity));
    }

    [Fact]
    public void TryGetFiniteSegmentLength_RejectsOutOfRangeOrTooLongSegments()
    {
        Assert.False(RoadNumericPolicy.TryGetFiniteSegmentLength(
            new Vector2(-RoadNumericPolicy.MaxCoordinateMagnitude, 0f),
            new Vector2(RoadNumericPolicy.MaxCoordinateMagnitude, 0f),
            out _));

        Assert.False(RoadNumericPolicy.TryGetFiniteSegmentLength(
            new Vector2(float.PositiveInfinity, 0f),
            Vector2.One,
            out _));
    }

    [Fact]
    public void CheckedDistanceSquared_ReturnsDoubleForLargeFiniteCoordinates()
    {
        double distanceSquared = RoadNumericPolicy.CheckedDistanceSquared(
            new Vector2(-RoadNumericPolicy.MaxCoordinateMagnitude, 0f),
            new Vector2(RoadNumericPolicy.MaxCoordinateMagnitude, 0f));

        double expected = Math.Pow(2d * RoadNumericPolicy.MaxCoordinateMagnitude, 2d);
        Assert.Equal(expected, distanceSquared, 5);
    }

    [Fact]
    public void NormalizeStartAngle_CanonicalizesNegativeZeroAndFullTurn()
    {
        Assert.Equal(0, BitConverter.SingleToInt32Bits(RoadNumericPolicy.NormalizeStartAngle(-0f)));
        Assert.Equal(0f, RoadNumericPolicy.NormalizeStartAngle(Mathf.Tau));
        Assert.Equal(Mathf.Pi, RoadNumericPolicy.NormalizeStartAngle(-Mathf.Pi), 5);
    }
}
