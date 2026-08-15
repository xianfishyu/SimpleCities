using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGeometryCanonicalizerTests
{
    [Fact]
    public void Canonicalize_ThreeCollinearUnitLines_MergesToOneLine()
    {
        var segments = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
            new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f)),
            new LineRoadGeometrySegment(new Vector2(2f, 0f), new Vector2(3f, 0f)),
        };

        IReadOnlyList<RoadGeometrySegment> result = RoadGeometryCanonicalizer.Canonicalize(segments);

        var line = Assert.IsType<LineRoadGeometrySegment>(Assert.Single(result));
        Assert.Equal(Vector2.Zero, line.Start);
        Assert.Equal(new Vector2(3f, 0f), line.End);
    }

    [Fact]
    public void Canonicalize_NonCollinearBend_KeepsTwoLines()
    {
        var segments = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
            new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(1f, 1f)),
        };

        IReadOnlyList<RoadGeometrySegment> result = RoadGeometryCanonicalizer.Canonicalize(segments);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Canonicalize_ReversedDirection_KeepsSegments()
    {
        var segments = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
            new LineRoadGeometrySegment(new Vector2(1f, 0f), Vector2.Zero),
        };

        IReadOnlyList<RoadGeometrySegment> result = RoadGeometryCanonicalizer.Canonicalize(segments);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Canonicalize_OneUlnKink_KeepsSegments()
    {
        var segments = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
            new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, MathF.BitIncrement(0f))),
        };

        IReadOnlyList<RoadGeometrySegment> result = RoadGeometryCanonicalizer.Canonicalize(segments);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Canonicalize_MixedLineAndCurve_KeepsAllNonMergeableSegments()
    {
        var segments = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
            new CubicBezierRoadGeometrySegment(
                new Vector2(1f, 0f),
                new Vector2(2f, 1f),
                new Vector2(3f, 1f),
                new Vector2(4f, 0f)),
            new LineRoadGeometrySegment(new Vector2(4f, 0f), new Vector2(5f, 0f)),
        };

        IReadOnlyList<RoadGeometrySegment> result = RoadGeometryCanonicalizer.Canonicalize(segments);

        Assert.Equal(3, result.Count);
        Assert.IsType<LineRoadGeometrySegment>(result[0]);
        Assert.IsType<CubicBezierRoadGeometrySegment>(result[1]);
        Assert.IsType<LineRoadGeometrySegment>(result[2]);
    }

    [Fact]
    public void Canonicalize_IsIdempotent()
    {
        var segments = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
            new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f)),
            new CubicBezierRoadGeometrySegment(
                new Vector2(2f, 0f),
                new Vector2(3f, 1f),
                new Vector2(4f, 1f),
                new Vector2(5f, 0f)),
            new LineRoadGeometrySegment(new Vector2(5f, 0f), new Vector2(6f, 0f)),
        };

        IReadOnlyList<RoadGeometrySegment> once = RoadGeometryCanonicalizer.Canonicalize(segments);
        IReadOnlyList<RoadGeometrySegment> twice = RoadGeometryCanonicalizer.Canonicalize(once);

        Assert.Equal(once.Count, twice.Count);
        Assert.Equal(once[0].Start, twice[0].Start);
        Assert.Equal(once[0].End, twice[0].End);
        Assert.Equal(once[^1].Start, twice[^1].Start);
        Assert.Equal(once[^1].End, twice[^1].End);
    }

    [Fact]
    public void Canonicalize_NormalizesNegativeZero()
    {
        var segments = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(new Vector2(-0f, 0f), new Vector2(1f, 0f)),
        };

        IReadOnlyList<RoadGeometrySegment> result = RoadGeometryCanonicalizer.Canonicalize(segments);

        var line = Assert.IsType<LineRoadGeometrySegment>(Assert.Single(result));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(line.Start.X));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(line.Start.Y));
    }

    [Fact]
    public void Canonicalize_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(RoadGeometryCanonicalizer.Canonicalize([]));
    }
}
