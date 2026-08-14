using Godot;

public sealed class RoadNumericPolicyV3Tests
{
    [Fact]
    public void Canonicalize_RewritesNegativeZeroWithoutChangingOtherValues()
    {
        float negativeZero = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));

        Vector2 result = RoadNumericPolicy.Canonicalize(new Vector2(negativeZero, 3.5f));

        Assert.Equal(0, BitConverter.SingleToInt32Bits(result.X));
        Assert.Equal(3.5f, result.Y);
    }

    [Fact]
    public void DistanceSquared_UsesFiniteDoubleAtCoordinateExtremes()
    {
        float limit = RoadNumericPolicy.MaximumCoordinateMagnitude;

        double result = RoadNumericPolicy.DistanceSquared(
            new Vector2(-limit, -limit),
            new Vector2(limit, limit));

        Assert.True(double.IsFinite(result));
        Assert.Equal(8_000_000_000_000d, result);
    }

    [Fact]
    public void ValidateGeometry_AcceptsCoordinateLimitAndRejectsNextFloat()
    {
        float limit = RoadNumericPolicy.MaximumCoordinateMagnitude;
        var accepted = new LineRoadGeometrySegment(
            new Vector2(limit - 1f, 0f),
            new Vector2(limit, 0f));
        var rejected = new LineRoadGeometrySegment(
            new Vector2(limit, 0f),
            new Vector2(MathF.BitIncrement(limit), 0f));

        Assert.Equal(RoadNumericError.None, RoadNumericPolicy.ValidateGeometry(accepted));
        Assert.Equal(
            RoadNumericError.CoordinateOutOfRange,
            RoadNumericPolicy.ValidateGeometry(rejected));
    }

    [Fact]
    public void ValidateGeometry_AcceptsRadiusLimitAndRejectsNextFloat()
    {
        float limit = RoadNumericPolicy.MaximumRadius;
        var accepted = new CircularArcRoadGeometrySegment(
            Vector2.Zero, limit, 0f, Mathf.Tau);
        var rejected = new CircularArcRoadGeometrySegment(
            Vector2.Zero, MathF.BitIncrement(limit), 0f, Mathf.Pi * 0.5f);

        Assert.Equal(RoadNumericError.None, RoadNumericPolicy.ValidateGeometry(accepted));
        Assert.Equal(
            RoadNumericError.ControlParameterOutOfRange,
            RoadNumericPolicy.ValidateGeometry(rejected));
    }

    [Fact]
    public void ValidateGeometryChain_UsesCheckedDoubleTotals()
    {
        var arc = new CircularArcRoadGeometrySegment(
            Vector2.Zero,
            RoadNumericPolicy.MaximumRadius,
            0f,
            Mathf.Tau);

        RoadNumericError edgeError = RoadNumericPolicy.ValidateGeometryChain(
            Enumerable.Repeat<RoadGeometrySegment>(arc, 11).ToArray(),
            0d,
            out double edgeLength,
            out _);
        RoadNumericError graphError = RoadNumericPolicy.ValidateGeometryChain(
            [arc],
            RoadNumericPolicy.MaximumGraphLength,
            out _,
            out double graphLength);

        Assert.Equal(RoadNumericError.EdgeLengthExceeded, edgeError);
        Assert.True(double.IsFinite(edgeLength));
        Assert.Equal(RoadNumericError.GraphLengthExceeded, graphError);
        Assert.True(double.IsFinite(graphLength));
    }

    [Fact]
    public void ValidateGeometry_RejectsHermiteTangentAboveLimit()
    {
        var geometry = new CubicHermiteRoadGeometrySegment(
            Vector2.Zero,
            new Vector2(MathF.BitIncrement(RoadNumericPolicy.MaximumVectorComponentMagnitude), 0f),
            new Vector2(10f, 0f),
            new Vector2(1f, 0f));

        Assert.Equal(
            RoadNumericError.ControlParameterOutOfRange,
            RoadNumericPolicy.ValidateGeometry(geometry));
    }

    [Fact]
    public void ValidateGeometry_RejectsClothoidCurvatureAboveLimit()
    {
        var geometry = new ClothoidRoadGeometrySegment(
            Vector2.Zero,
            0f,
            MathF.BitIncrement(RoadNumericPolicy.MaximumCurvatureMagnitude),
            0f,
            1f);

        Assert.Equal(
            RoadNumericError.ControlParameterOutOfRange,
            RoadNumericPolicy.ValidateGeometry(geometry));
    }

    [Fact]
    public void ValidateGeometry_RejectsRationalWeightAboveLimit()
    {
        var geometry = new RationalQuadraticRoadGeometrySegment(
            Vector2.Zero,
            1f,
            new Vector2(5f, 3f),
            MathF.BitIncrement(RoadNumericPolicy.MaximumRationalWeight),
            new Vector2(10f, 0f),
            1f);

        Assert.Equal(
            RoadNumericError.ControlParameterOutOfRange,
            RoadNumericPolicy.ValidateGeometry(geometry));
    }

    [Fact]
    public void ValidateGeometry_RejectsStartAngleAboveLimit()
    {
        var geometry = new CircularArcRoadGeometrySegment(
            Vector2.Zero,
            10f,
            MathF.BitIncrement(RoadNumericPolicy.MaximumAngleMagnitude),
            Mathf.Pi * 0.5f);

        Assert.Equal(
            RoadNumericError.ControlParameterOutOfRange,
            RoadNumericPolicy.ValidateGeometry(geometry));
    }
}
