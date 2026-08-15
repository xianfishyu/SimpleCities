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
}
