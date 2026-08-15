using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class ExactLinePredicatesTests
{
    [Fact]
    public void CanMergeLineSegments_CollinearSameDirection_ReturnsTrue()
    {
        var first = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f));
        var second = new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f));

        Assert.True(ExactLinePredicates.CanMergeLineSegments(first, second));
    }

    [Fact]
    public void CanMergeLineSegments_NonCollinear_ReturnsFalse()
    {
        var first = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f));
        var second = new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 1f));

        Assert.False(ExactLinePredicates.CanMergeLineSegments(first, second));
    }

    [Fact]
    public void CanMergeLineSegments_ReversedDirection_ReturnsFalse()
    {
        var first = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f));
        var second = new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(0f, 0f));

        Assert.False(ExactLinePredicates.CanMergeLineSegments(first, second));
    }

    [Fact]
    public void CanMergeLineSegments_OneUlnKink_ReturnsFalse()
    {
        var first = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f));
        var second = new LineRoadGeometrySegment(
            new Vector2(1f, 0f),
            new Vector2(2f, MathF.BitIncrement(0f)));

        Assert.False(ExactLinePredicates.CanMergeLineSegments(first, second));
    }

    [Fact]
    public void CanMergeLineSegments_SharedEndpointMustBeBitwiseIdentical()
    {
        var first = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f));
        var second = new LineRoadGeometrySegment(new Vector2(1f, -0f), new Vector2(2f, 0f));

        Assert.False(ExactLinePredicates.CanMergeLineSegments(first, second));
    }

    [Fact]
    public void Orient2D_ReturnsStableSign()
    {
        Assert.Equal(0, ExactLinePredicates.Orient2D(Vector2.Zero, new Vector2(1f, 0f), new Vector2(2f, 0f)));
        Assert.Equal(1, ExactLinePredicates.Orient2D(Vector2.Zero, new Vector2(1f, 0f), new Vector2(2f, 1f)));
        Assert.Equal(-1, ExactLinePredicates.Orient2D(Vector2.Zero, new Vector2(1f, 0f), new Vector2(2f, -1f)));
    }
}
