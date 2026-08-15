using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadDirectionKeyTests
{
    [Fact]
    public void Compute_StartsWithVersionByte()
    {
        var chain = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, Vector2.One),
        };

        byte[] key = RoadDirectionKey.Compute(chain);

        Assert.Equal(1, key[0]);
    }

    [Fact]
    public void SelectCanonicalDirection_ReturnsReversedWhenItsKeyIsSmaller()
    {
        var chain = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(new Vector2(10f, 0f), new Vector2(0f, 0f)),
        };

        IReadOnlyList<RoadGeometrySegment> canonical = RoadDirectionKey.SelectCanonicalDirection(chain);

        var line = Assert.IsType<LineRoadGeometrySegment>(Assert.Single(canonical));
        Assert.Equal(Vector2.Zero, line.Start);
        Assert.Equal(new Vector2(10f, 0f), line.End);
    }

    [Fact]
    public void SelectCanonicalDirection_KeepsOriginalWhenItsKeyIsSmallerOrEqual()
    {
        var chain = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
        };

        IReadOnlyList<RoadGeometrySegment> canonical = RoadDirectionKey.SelectCanonicalDirection(chain);

        Assert.Same(chain, canonical);
    }

    [Fact]
    public void Compute_NormalizesNegativeZero()
    {
        var negativeZero = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(new Vector2(-0f, 0f), new Vector2(1f, 0f)),
        };
        var positiveZero = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
        };

        Assert.Equal(
            RoadDirectionKey.Compute(positiveZero),
            RoadDirectionKey.Compute(negativeZero));
    }

    [Fact]
    public void Compute_NormalizesPeriodicStartAngle()
    {
        var zero = new RoadGeometrySegment[]
        {
            new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, 1f),
        };
        var tau = new RoadGeometrySegment[]
        {
            new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, Mathf.Tau, 1f),
        };

        Assert.Equal(
            RoadDirectionKey.Compute(zero),
            RoadDirectionKey.Compute(tau));
    }

    [Fact]
    public void ReverseTwice_ProducesSameDirectionKey()
    {
        var chain = new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(2f, 0f)),
            new CubicBezierRoadGeometrySegment(
                new Vector2(2f, 0f),
                new Vector2(3f, 1f),
                new Vector2(4f, 1f),
                new Vector2(5f, 0f)),
            new CircularArcRoadGeometrySegment(new Vector2(5f, 0f), 2f, 0f, 1.5f),
        };

        IReadOnlyList<RoadGeometrySegment> twice = RoadDirectionKey.ReverseChain(
            RoadDirectionKey.ReverseChain(chain));

        Assert.Equal(
            RoadDirectionKey.Compute(chain),
            RoadDirectionKey.Compute(twice));
    }

    [Fact]
    public void Compare_UsesLexicographicUnsignedByteOrder()
    {
        var small = RoadDirectionKey.Compute(new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
        });
        var large = RoadDirectionKey.Compute(new RoadGeometrySegment[]
        {
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(2f, 0f)),
        });

        Assert.True(RoadDirectionKey.Compare(small, large) < 0);
        Assert.True(RoadDirectionKey.Compare(large, small) > 0);
        Assert.Equal(0, RoadDirectionKey.Compare(small, small));
    }
}
