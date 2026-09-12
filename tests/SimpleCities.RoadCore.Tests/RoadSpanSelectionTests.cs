namespace SimpleCities.RoadCore.Tests;

public sealed class RoadSpanSelectionTests
{
    [Fact]
    public void ReleasedGestureDoesNotAccumulate_AndNextGestureStartsAnotherSelection()
    {
        RoadSnapshot snapshot = MakeRoad().Snapshot;
        RoadEdge edge = Assert.Single(snapshot.Edges);
        RoadGridSpan first = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 0.15))!;
        RoadGridSpan next = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, 0.25))!;
        var session = new RoadSpanSelectionSession();
        session.Begin(snapshot);
        Assert.True(session.Accumulate(snapshot, new[] { first }));
        session.End();
        Assert.False(session.Accumulate(snapshot, new[] { next }));
        Assert.Equal(first.Key, Assert.Single(session.Selected).Key);
        session.Begin(snapshot);
        Assert.True(session.Accumulate(snapshot, new[] { next }));
        Assert.Equal(next.Key, Assert.Single(session.Selected).Key);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SnapshotChangeRejectsOldSessionAndClearsItsHoverAndSelection(bool throughHover)
    {
        RoadNetwork network = MakeRoad();
        RoadSnapshot before = network.Snapshot;
        RoadEdge edge = Assert.Single(before.Edges);
        RoadGridSpan span = RoadSpanQuery.Pick(before, new(before.Token, edge.Id, 0.15))!;
        var session = new RoadSpanSelectionSession();
        session.Begin(before);
        session.Hover(before, span);
        session.Accumulate(before, new[] { span });
        RoadBuildResult build = network.PlanBuild(new(before.Token, new(0, 100), new(100, 100), RoadProfileId.Street));
        Assert.True(network.TryCommit(build.Plan!));
        RoadSnapshot current = network.Snapshot;

        Assert.False(throughHover ? session.Hover(current, null) : session.Accumulate(current, new[] { span }));
        Assert.Empty(session.Selected);
        Assert.Null(session.Hovered);
        Assert.Null(session.Source);
        Assert.False(session.IsSelecting);
        Assert.Same(current, network.Snapshot);
    }

    [Fact]
    public void MixedSourceBatchRejectsAllAdditionsAndClearsTheGesture()
    {
        RoadSnapshot snapshot = MakeRoad().Snapshot;
        RoadSnapshot foreign = MakeRoad().Snapshot;
        RoadGridSpan currentSpan = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, snapshot.Edges[0].Id, 0.15))!;
        RoadGridSpan foreignSpan = RoadSpanQuery.Pick(foreign, new(foreign.Token, foreign.Edges[0].Id, 0.15))!;
        var session = new RoadSpanSelectionSession();
        session.Begin(snapshot);
        Assert.False(session.Accumulate(snapshot, new[] { currentSpan, foreignSpan }));
        Assert.Empty(session.Selected);
        Assert.Null(session.Source);
        Assert.False(session.IsSelecting);
        Assert.False(session.Hover(snapshot, foreignSpan));
        Assert.Null(session.Hovered);
    }

    [Fact]
    public void HoverAndDragSelectionStayDistinct_DeduplicateAndPersistUntilClear()
    {
        var network = new RoadNetwork();
        RoadBuildResult build = network.PlanBuild(new(network.Snapshot.Token, new(0, 0), new(1000, 0), RoadProfileId.Street));
        Assert.True(network.TryCommit(build.Plan!));
        RoadSnapshot before = network.Snapshot;
        RoadEdge edge = Assert.Single(before.Edges);
        RoadGridSpan first = RoadSpanQuery.Pick(before, new(before.Token, edge.Id, 0.15))!;
        RoadGridSpan next = RoadSpanQuery.Pick(before, new(before.Token, edge.Id, 0.25))!;
        var session = new RoadSpanSelectionSession();

        Assert.True(session.Hover(before, first));
        Assert.Equal(first.Key, session.Hovered!.Key);
        Assert.Empty(session.Selected);
        session.Begin(before);
        Assert.True(session.Accumulate(before, new[] { first, next, first }));
        Assert.True(session.Hover(before, null));
        session.End();

        Assert.False(session.IsSelecting);
        Assert.Null(session.Hovered);
        Assert.Equal(new[] { first.Key, next.Key }, session.Selected.Select(span => span.Key));
        session.Clear();
        Assert.Empty(session.Selected);
        Assert.Null(session.Source);
        Assert.Same(before, network.Snapshot);
    }

    private static RoadNetwork MakeRoad()
    {
        var network = new RoadNetwork();
        RoadBuildResult build = network.PlanBuild(new(network.Snapshot.Token, new(0, 0), new(1000, 0), RoadProfileId.Street));
        Assert.True(network.TryCommit(build.Plan!));
        return network;
    }
}
