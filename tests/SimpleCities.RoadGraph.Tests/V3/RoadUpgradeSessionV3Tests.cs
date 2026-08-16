using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadUpgradeSessionV3Tests
{
    [Fact]
    public void TrySelectEdge_AddsAndSortsSelection()
    {
        var session = new RoadUpgradeSessionV3(RoadType.Highway);

        Assert.True(session.TrySelectEdge(5));
        Assert.True(session.TrySelectEdge(2));
        Assert.False(session.TrySelectEdge(2));

        Assert.Equal([2, 5], session.SelectedEdgeIDs);
    }

    [Fact]
    public void TrySelectEdge_Negative_Fails()
    {
        var session = new RoadUpgradeSessionV3(RoadType.Highway);

        Assert.False(session.TrySelectEdge(-1));
    }

    [Fact]
    public void DeselectEdge_RemovesSelection()
    {
        var session = new RoadUpgradeSessionV3(RoadType.Highway);
        session.TrySelectEdge(2);
        session.TrySelectEdge(5);

        Assert.True(session.DeselectEdge(2));
        Assert.Equal([5], session.SelectedEdgeIDs);
    }

    [Fact]
    public void ClearSelection_EmptiesSelection()
    {
        var session = new RoadUpgradeSessionV3(RoadType.Highway);
        session.TrySelectEdge(2);

        session.ClearSelection();

        Assert.Empty(session.SelectedEdgeIDs);
    }

    [Fact]
    public void TryCommit_WithSelection_ReturnsTrue()
    {
        var session = new RoadUpgradeSessionV3(RoadType.Arterial);
        session.TrySelectEdge(3);

        Assert.True(session.TryCommit(out IReadOnlyList<int> selection));
        Assert.Equal([3], selection);
    }

    [Fact]
    public void TryCommit_EmptySelection_ReturnsFalse()
    {
        var session = new RoadUpgradeSessionV3(RoadType.Arterial);

        Assert.False(session.TryCommit(out _));
    }

    [Fact]
    public void TrySelectHit_ValidRibbon_AddsEdge()
    {
        var session = new RoadUpgradeSessionV3(RoadType.Highway);

        Assert.True(session.TrySelectHit(CreateHit()));
        Assert.Equal([20], session.SelectedEdgeIDs);
    }

    [Fact]
    public void TrySelectHit_InvalidHit_Fails()
    {
        var session = new RoadUpgradeSessionV3(RoadType.Highway);
        RoadSurfaceHit hit = CreateHit() with { DistanceSquared = -1f };

        Assert.False(session.TrySelectHit(hit));
        Assert.Empty(session.SelectedEdgeIDs);
    }

    [Fact]
    public void TrySelectHit_OwnerWithoutEdge_Fails()
    {
        var session = new RoadUpgradeSessionV3(RoadType.Highway);
        RoadSurfaceHit hit = CreateHit() with
        {
            OwnerKind = RoadSurfaceOwnerKind.Cap,
            EdgeID = null,
        };

        Assert.False(session.TrySelectHit(hit));
        Assert.Empty(session.SelectedEdgeIDs);
    }

    [Fact]
    public void TrySelectHits_ValidHits_AddsAll()
    {
        var session = new RoadUpgradeSessionV3(RoadType.Highway);
        RoadSurfaceHit hit1 = CreateHit();
        RoadSurfaceHit hit2 = CreateHit() with { EdgeID = 21 };

        Assert.Equal(2, session.TrySelectHits([hit1, hit2]));
        Assert.Equal([20, 21], session.SelectedEdgeIDs);
    }

    [Fact]
    public void TrySelectHits_InvalidHit_Skips()
    {
        var session = new RoadUpgradeSessionV3(RoadType.Highway);
        RoadSurfaceHit invalid = CreateHit() with { DistanceSquared = -1f };

        Assert.Equal(1, session.TrySelectHits([invalid, CreateHit()]));
        Assert.Equal([20], session.SelectedEdgeIDs);
    }

    private static RoadSurfaceHit CreateHit() =>
        new(
            new GraphStateToken(1, 3, 4),
            RoadSurfaceOwnerKind.Ribbon,
            NodeID: 10,
            EdgeID: 20,
            Endpoint: EdgeEndpoint.A,
            new RoadLocation(20, 0, 0.5f),
            1f);
}
