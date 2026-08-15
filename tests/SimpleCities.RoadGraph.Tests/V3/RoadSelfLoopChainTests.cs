using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadSelfLoopChainTests
{
    [Fact]
    public void Canonicalize_KeepsSeamAndMergesInteriorCollinearLines()
    {
        var chain = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
            new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f)),
            new LineRoadGeometrySegment(new Vector2(2f, 0f), Vector2.Zero),
        };

        IReadOnlyList<RoadGeometrySegment> result = RoadSelfLoopChain.Canonicalize(chain);

        Assert.Equal(2, result.Count);
        Assert.Equal(Vector2.Zero, result[0].Start);
        Assert.Equal(Vector2.Zero, result[^1].End);
    }

    [Fact]
    public void Canonicalize_DoesNotMergeAcrossSeam()
    {
        var chain = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
            new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(1f, 1f)),
            new LineRoadGeometrySegment(new Vector2(1f, 1f), Vector2.Zero),
        };

        IReadOnlyList<RoadGeometrySegment> result = RoadSelfLoopChain.Canonicalize(chain);

        Assert.Equal(3, result.Count);
        Assert.Equal(Vector2.Zero, result[0].Start);
        Assert.Equal(Vector2.Zero, result[^1].End);
    }

    [Fact]
    public void Canonicalize_ForwardAndReversedInputsProduceSameDirectionKey()
    {
        var forward = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
            new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(1f, 1f)),
            new LineRoadGeometrySegment(new Vector2(1f, 1f), Vector2.Zero),
        };

        IReadOnlyList<RoadGeometrySegment> reversed = RoadDirectionKey.ReverseChain(forward);
        IReadOnlyList<RoadGeometrySegment> forwardCanonical = RoadSelfLoopChain.Canonicalize(forward);
        IReadOnlyList<RoadGeometrySegment> reversedCanonical = RoadSelfLoopChain.Canonicalize(reversed);

        Assert.Equal(
            RoadDirectionKey.Compute(forwardCanonical),
            RoadDirectionKey.Compute(reversedCanonical));
    }

    [Fact]
    public void Canonicalize_PreservesSeamPosition()
    {
        var seam = new Vector2(3f, -2f);
        var chain = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(seam, new Vector2(4f, -2f)),
            new LineRoadGeometrySegment(new Vector2(4f, -2f), seam),
        };

        IReadOnlyList<RoadGeometrySegment> result = RoadSelfLoopChain.Canonicalize(chain);

        Assert.Equal(seam, result[0].Start);
        Assert.Equal(seam, result[^1].End);
    }

    [Fact]
    public void Canonicalize_RejectsOpenChain()
    {
        var chain = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
            new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f)),
        };

        Assert.Throws<ArgumentException>(() => RoadSelfLoopChain.Canonicalize(chain));
    }

    [Fact]
    public void Canonicalize_EmptyChain_Throws()
    {
        Assert.Throws<ArgumentException>(() => RoadSelfLoopChain.Canonicalize([]));
    }
}
