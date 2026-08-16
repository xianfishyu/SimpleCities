using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadQueryFragmentBuilderTests
{
    [Fact]
    public void BuildLineFragments_StraightLineCrossingBuckets_ProducesMultipleFragments()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(3f, 0f));

        IReadOnlyList<RoadQueryFragment> fragments = RoadQueryFragmentBuilder.BuildLineFragments(7, 0, line, 1f);

        Assert.Equal(3, fragments.Count);
        Assert.Equal(0f, fragments[0].ParameterStart);
        Assert.Equal(1f, fragments[^1].ParameterEnd);
    }

    [Fact]
    public void BuildLineFragments_ShortLineWithinOneBucket_ProducesSingleFragment()
    {
        var line = new LineRoadGeometrySegment(new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.1f));

        IReadOnlyList<RoadQueryFragment> fragments = RoadQueryFragmentBuilder.BuildLineFragments(7, 0, line, 1f);

        RoadQueryFragment fragment = Assert.Single(fragments);
        Assert.Equal(0f, fragment.ParameterStart);
        Assert.Equal(1f, fragment.ParameterEnd);
    }

    [Fact]
    public void BuildLineFragments_ParametersCoverZeroToOne()
    {
        var line = new LineRoadGeometrySegment(new Vector2(0f, 0f), new Vector2(3f, 2f));

        IReadOnlyList<RoadQueryFragment> fragments = RoadQueryFragmentBuilder.BuildLineFragments(7, 0, line, 1f);

        float total = 0f;
        foreach (RoadQueryFragment fragment in fragments)
            total += fragment.ParameterEnd - fragment.ParameterStart;
        Assert.Equal(1f, total, 5);
    }

    [Fact]
    public void BuildLineFragments_ConservativeBoundsContainEndpoints()
    {
        var line = new LineRoadGeometrySegment(new Vector2(0f, 0f), new Vector2(3f, 2f));

        IReadOnlyList<RoadQueryFragment> fragments = RoadQueryFragmentBuilder.BuildLineFragments(7, 0, line, 1f);

        foreach (RoadQueryFragment fragment in fragments)
        {
            Vector2 start = line.GetPosition(fragment.ParameterStart);
            Vector2 end = line.GetPosition(fragment.ParameterEnd);
            Assert.True(ContainsInclusive(fragment.ConservativeBounds, start));
            Assert.True(ContainsInclusive(fragment.ConservativeBounds, end));
        }
    }

    [Fact]
    public void BuildLineFragments_DiagonalLine_SplitsAtBothAxes()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(3f, 2f));

        IReadOnlyList<RoadQueryFragment> fragments = RoadQueryFragmentBuilder.BuildLineFragments(7, 0, line, 1f);

        Assert.Equal(4, fragments.Count);
    }

    [Fact]
    public void BuildLineFragments_InvalidBucketSize_Throws()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, Vector2.One);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoadQueryFragmentBuilder.BuildLineFragments(7, 0, line, 0f));
    }

    [Fact]
    public void BuildSegmentFragments_Line_DelegatesToLineBuilder()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(3f, 0f));

        IReadOnlyList<RoadQueryFragment> fragments = RoadQueryFragmentBuilder.BuildSegmentFragments(7, 0, line, 1f);

        Assert.Equal(3, fragments.Count);
    }

    [Fact]
    public void BuildSegmentFragments_Curve_ProducesSingleWholeBoundsFragment()
    {
        var curve = new CubicBezierRoadGeometrySegment(
            Vector2.Zero,
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(2f, 1f));

        IReadOnlyList<RoadQueryFragment> fragments = RoadQueryFragmentBuilder.BuildSegmentFragments(7, 0, curve, 1f);

        RoadQueryFragment fragment = Assert.Single(fragments);
        Assert.Equal(0f, fragment.ParameterStart);
        Assert.Equal(1f, fragment.ParameterEnd);
        Assert.Equal(curve.Bounds, fragment.ConservativeBounds);
    }

    private static bool ContainsInclusive(Rect2 bounds, Vector2 point) =>
        point.X >= bounds.Position.X - 1e-5f &&
        point.X <= bounds.End.X + 1e-5f &&
        point.Y >= bounds.Position.Y - 1e-5f &&
        point.Y <= bounds.End.Y + 1e-5f;
}
