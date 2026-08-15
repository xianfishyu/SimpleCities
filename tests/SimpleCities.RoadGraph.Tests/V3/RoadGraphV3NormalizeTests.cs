using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3NormalizeTests
{
    [Fact]
    public void Normalize_MergesChainAndReturnsSummary()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out int a);
        facade.TryAddNode(new Vector2(1f, 0f), out _, out int b);
        facade.TryAddNode(new Vector2(2f, 0f), out _, out int c);
        facade.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out _, out _);
        facade.TryAddEdge(b, c, [new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f))], RoadType.Street, out _, out _);

        Assert.True(facade.TryNormalize(out RoadGraphV3ChangeSummary summary));

        Assert.Equal(2, facade.Revision.Nodes.Count);
        Assert.Single(facade.Revision.Edges);
        Assert.False(summary.IsFullReset);
        Assert.NotEmpty(summary.RemovedNodeIDs);
    }

    [Fact]
    public void Normalize_NoChanges_ReturnsFalse()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out _);

        Assert.False(facade.TryNormalize(out _));
    }
}
