using Godot;

public sealed class RoadGeometryCanonicalizerV3Tests
{
    [Fact]
    public void ExactPredicates_HandleAxisDiagonalAndCoordinateLimitWithoutFloatCross()
    {
        float limit = RoadNumericPolicy.MaximumCoordinateMagnitude;

        Assert.Equal(0, RoadExactPredicates.Orient2DSign(
            new Vector2(-limit, -limit),
            Vector2.Zero,
            new Vector2(limit, limit)));
        Assert.Equal(0, RoadExactPredicates.Orient2DSign(
            new Vector2(2f, -limit),
            new Vector2(2f, 0f),
            new Vector2(2f, limit)));
        Assert.True(RoadExactPredicates.DotSign(new Vector2(1f, 1f), new Vector2(2f, 2f)) > 0);
        Assert.True(RoadExactPredicates.DotSign(new Vector2(1f, 1f), new Vector2(-2f, -2f)) < 0);
    }

    [Fact]
    public void Canonicalize_MergesOnlyExactForwardCollinearLines()
    {
        var result = RoadGeometryCanonicalizer.Canonicalize(
        [
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 1f)),
            new LineRoadGeometrySegment(new Vector2(1f, 1f), new Vector2(2f, 2f)),
            new LineRoadGeometrySegment(new Vector2(2f, 2f), new Vector2(4f, 4f)),
        ]);

        LineRoadGeometrySegment line = Assert.IsType<LineRoadGeometrySegment>(
            Assert.Single(result.GeometrySegments));
        Assert.True(result.Changed);
        Assert.Equal(Vector2.Zero, line.Start);
        Assert.Equal(new Vector2(4f, 4f), line.End);
    }

    [Fact]
    public void Canonicalize_PreservesOneUlpBendAndReverseOverlap()
    {
        float bentY = MathF.BitIncrement(2f);
        var bend = RoadGeometryCanonicalizer.Canonicalize(
        [
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 1f)),
            new LineRoadGeometrySegment(new Vector2(1f, 1f), new Vector2(2f, bentY)),
        ]);
        var reverse = RoadGeometryCanonicalizer.Canonicalize(
        [
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(2f, 0f)),
            new LineRoadGeometrySegment(new Vector2(2f, 0f), new Vector2(1f, 0f)),
        ]);

        Assert.Equal(2, bend.GeometrySegments.Count);
        Assert.Equal(2, reverse.GeometrySegments.Count);
        Assert.False(RoadExactPredicates.CanMergeForwardLines(
            (LineRoadGeometrySegment)bend.GeometrySegments[0],
            (LineRoadGeometrySegment)bend.GeometrySegments[1]));
    }

    [Fact]
    public void ExactPredicate_RejectsOneUlpBendAtCoordinateLimit()
    {
        float limit = RoadNumericPolicy.MaximumCoordinateMagnitude;
        var first = new LineRoadGeometrySegment(
            new Vector2(limit - 4f, limit - 4f),
            new Vector2(limit - 2f, limit - 2f));
        var second = new LineRoadGeometrySegment(
            first.End,
            new Vector2(limit, MathF.BitDecrement(limit)));

        Assert.NotEqual(
            0,
            RoadExactPredicates.Orient2DSign(first.Start, first.End, second.End));
        Assert.False(RoadExactPredicates.CanMergeForwardLines(first, second));
    }

    [Fact]
    public void Canonicalize_RewritesNegativeZeroAndIsIdempotent()
    {
        float negativeZero = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
        var first = RoadGeometryCanonicalizer.Canonicalize(
        [
            new LineRoadGeometrySegment(
                new Vector2(negativeZero, negativeZero),
                new Vector2(1f, negativeZero)),
        ]);
        var second = RoadGeometryCanonicalizer.Canonicalize(first.GeometrySegments);
        LineRoadGeometrySegment line = Assert.IsType<LineRoadGeometrySegment>(
            Assert.Single(second.GeometrySegments));

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal(0, BitConverter.SingleToInt32Bits(line.Start.X));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(line.Start.Y));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(line.End.Y));
    }

    [Fact]
    public void Canonicalize_DifferentExactLineChunkingProducesSamePrimitive()
    {
        RoadGeometryCanonicalizationResult chunked = RoadGeometryCanonicalizer.Canonicalize(
        [
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
            new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(3f, 0f)),
            new LineRoadGeometrySegment(new Vector2(3f, 0f), new Vector2(8f, 0f)),
        ]);
        RoadGeometryCanonicalizationResult direct = RoadGeometryCanonicalizer.Canonicalize(
        [
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(8f, 0f)),
        ]);

        Assert.Equal(
            RoadGeometrySerializer.Serialize(Assert.Single(direct.GeometrySegments)),
            RoadGeometrySerializer.Serialize(Assert.Single(chunked.GeometrySegments)));
    }
}
