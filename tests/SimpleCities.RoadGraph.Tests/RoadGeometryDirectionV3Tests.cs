using Godot;

public sealed class RoadGeometryDirectionV3Tests
{
    public static TheoryData<RoadGeometrySegment> NativeGeometryCases => new()
    {
        new LineRoadGeometrySegment(new Vector2(-3f, 1f), new Vector2(8f, 4f)),
        new CubicBezierRoadGeometrySegment(
            new Vector2(10f, 0f), new Vector2(12f, 7f),
            new Vector2(19f, -3f), new Vector2(23f, 2f)),
        new CubicHermiteRoadGeometrySegment(
            new Vector2(30f, 1f), new Vector2(7f, 5f),
            new Vector2(41f, 3f), new Vector2(4f, -6f)),
        new CircularArcRoadGeometrySegment(
            new Vector2(52f, -2f), 6f, 0.37f, -2.1f),
        new ClothoidRoadGeometrySegment(
            new Vector2(64f, 2f), -0.45f, -0.03f, 0.11f, 14f),
        new RationalQuadraticRoadGeometrySegment(
            new Vector2(82f, 0f), 1.2f,
            new Vector2(88f, 9f), 0.65f,
            new Vector2(95f, 2f), 1.4f),
    };

    [Theory]
    [MemberData(nameof(NativeGeometryCases))]
    public void Reverse_PreservesNativeTrajectoryAndIsAParameterInvolution(
        RoadGeometrySegment source)
    {
        RoadGeometrySegment reversed = source.Reverse();
        RoadGeometrySegment restored = reversed.Reverse();

        Assert.IsType(source.GetType(), reversed);
        AssertVectorApproximatelyEqual(source.End, reversed.Start);
        AssertVectorApproximatelyEqual(source.Start, reversed.End);
        Assert.Equal(source.Length, reversed.Length, 3);
        foreach (float parameter in new[] { 0f, 0.2f, 0.5f, 0.8f, 1f })
        {
            AssertVectorApproximatelyEqual(
                source.GetPosition(1f - parameter),
                reversed.GetPosition(parameter));
            AssertVectorApproximatelyEqual(
                -source.GetUnitTangent(1f - parameter),
                reversed.GetUnitTangent(parameter));
        }

        AssertEquivalentParameters(source, restored);
        Assert.Equal(
            RoadGeometrySerializer.Serialize(source),
            RoadGeometrySerializer.Serialize(restored));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void Reverse_FullTurnPreservesSeamAndOnlyFlipsSweep(float direction)
    {
        var source = new CircularArcRoadGeometrySegment(
            new Vector2(4f, -7f), 9f, 0.73f, direction * Mathf.Tau);

        var reversed = Assert.IsType<CircularArcRoadGeometrySegment>(source.Reverse());

        Assert.True(reversed.IsFullTurn);
        Assert.True(RoadExactPredicates.SameBits(source.Start, reversed.Start));
        Assert.Equal(
            BitConverter.SingleToInt32Bits(source.StartAngle),
            BitConverter.SingleToInt32Bits(reversed.StartAngle));
        Assert.Equal(
            BitConverter.SingleToInt32Bits(-source.SweepAngle),
            BitConverter.SingleToInt32Bits(reversed.SweepAngle));
        Assert.Equal(
            RoadGeometrySerializer.Serialize(source),
            RoadGeometrySerializer.Serialize(reversed.Reverse()));
    }

    [Fact]
    public void TypedDirectionKeyCanonicalizesPeriodicAnglesAndNegativeZero()
    {
        float negativeZero = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
        RoadGeometrySegment first = new CircularArcRoadGeometrySegment(
            new Vector2(negativeZero, negativeZero), 5f, negativeZero, Mathf.Tau);
        RoadGeometrySegment second = new CircularArcRoadGeometrySegment(
            Vector2.Zero, 5f, Mathf.Tau, Mathf.Tau);

        Assert.Equal(
            0,
            RoadGeometryDirection.CompareCanonicalKeys([first], [second]));
    }

    [Fact]
    public void SelfLoopDirectionKeyIsIndependentOfFullTurnInputDirection()
    {
        var clockwise = new GraphEdge(RoadType.Street,
            1,
            0,
            0,
            [new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, -Mathf.Tau)]);
        var counterClockwise = new GraphEdge(RoadType.Street,
            1,
            0,
            0,
            [new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, Mathf.Tau)]);

        var first = Assert.IsType<CircularArcRoadGeometrySegment>(
            Assert.Single(clockwise.GeometrySegments));
        var second = Assert.IsType<CircularArcRoadGeometrySegment>(
            Assert.Single(counterClockwise.GeometrySegments));
        Assert.Equal(RoadGeometrySerializer.Serialize(first), RoadGeometrySerializer.Serialize(second));
        Assert.Equal(-Mathf.Tau, first.SweepAngle);
    }

    [Fact]
    public void SelfLoopDirectionIsIndependentOfEntityIDsAndJsonPropertyOrder()
    {
        const string reorderedJson =
            "{\"sweepAngle\":6.2831855,\"endAngle\":0,\"startAngle\":0," +
            "\"radius\":5,\"center\":{\"y\":0,\"x\":0}," +
            "\"end\":{\"y\":0,\"x\":5},\"start\":{\"y\":0,\"x\":5}," +
            "\"kind\":\"circularArc\",\"version\":1}";
        RoadGeometrySegment restored = Assert.IsType<CircularArcRoadGeometrySegment>(
            RoadGeometrySerializer.Deserialize(reorderedJson).Geometry);
        var first = new GraphEdge(RoadType.Street, 1, 0, 0, [restored]);
        var second = new GraphEdge(RoadType.Street,
            97,
            41,
            41,
            [new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, Mathf.Tau)]);

        Assert.Equal(
            0,
            RoadGeometryDirection.CompareCanonicalKeys(
                first.GeometrySegments,
                second.GeometrySegments));
        Assert.Equal(
            RoadGeometrySerializer.Serialize(first.GeometrySegments[0]),
            RoadGeometrySerializer.Serialize(second.GeometrySegments[0]));
    }

    private static void AssertVectorApproximatelyEqual(Vector2 expected, Vector2 actual) =>
        Assert.InRange(actual.DistanceTo(expected), 0f, 8e-4f);

    private static void AssertEquivalentParameters(
        RoadGeometrySegment expected,
        RoadGeometrySegment actual)
    {
        Assert.Equal(expected.Kind, actual.Kind);
        switch (expected, actual)
        {
            case (LineRoadGeometrySegment first, LineRoadGeometrySegment second):
                AssertVectorApproximatelyEqual(first.Start, second.Start);
                AssertVectorApproximatelyEqual(first.End, second.End);
                break;
            case (CubicBezierRoadGeometrySegment first, CubicBezierRoadGeometrySegment second):
                AssertVectorApproximatelyEqual(first.Start, second.Start);
                AssertVectorApproximatelyEqual(first.Control1, second.Control1);
                AssertVectorApproximatelyEqual(first.Control2, second.Control2);
                AssertVectorApproximatelyEqual(first.End, second.End);
                break;
            case (CubicHermiteRoadGeometrySegment first, CubicHermiteRoadGeometrySegment second):
                AssertVectorApproximatelyEqual(first.Start, second.Start);
                AssertVectorApproximatelyEqual(first.StartTangent, second.StartTangent);
                AssertVectorApproximatelyEqual(first.End, second.End);
                AssertVectorApproximatelyEqual(first.EndTangent, second.EndTangent);
                break;
            case (CircularArcRoadGeometrySegment first, CircularArcRoadGeometrySegment second):
                AssertVectorApproximatelyEqual(first.Center, second.Center);
                Assert.Equal(first.Radius, second.Radius, 5);
                AssertPeriodicAngleApproximatelyEqual(first.StartAngle, second.StartAngle);
                Assert.Equal(first.SweepAngle, second.SweepAngle, 5);
                break;
            case (ClothoidRoadGeometrySegment first, ClothoidRoadGeometrySegment second):
                AssertVectorApproximatelyEqual(first.Start, second.Start);
                AssertPeriodicAngleApproximatelyEqual(first.StartHeading, second.StartHeading);
                Assert.Equal(first.StartCurvature, second.StartCurvature, 5);
                Assert.Equal(first.EndCurvature, second.EndCurvature, 5);
                Assert.Equal(first.ArcLength, second.ArcLength, 5);
                break;
            case (RationalQuadraticRoadGeometrySegment first, RationalQuadraticRoadGeometrySegment second):
                AssertVectorApproximatelyEqual(first.Start, second.Start);
                Assert.Equal(first.StartWeight, second.StartWeight, 5);
                AssertVectorApproximatelyEqual(first.Control, second.Control);
                Assert.Equal(first.ControlWeight, second.ControlWeight, 5);
                AssertVectorApproximatelyEqual(first.End, second.End);
                Assert.Equal(first.EndWeight, second.EndWeight, 5);
                break;
            default:
                throw new Xunit.Sdk.XunitException(
                    $"Geometry types do not match: {expected.GetType().Name}, {actual.GetType().Name}.");
        }
    }

    private static void AssertPeriodicAngleApproximatelyEqual(float expected, float actual)
    {
        float difference = Mathf.PosMod(actual - expected + Mathf.Pi, Mathf.Tau) - Mathf.Pi;
        Assert.InRange(Mathf.Abs(difference), 0f, 1e-5f);
    }
}
