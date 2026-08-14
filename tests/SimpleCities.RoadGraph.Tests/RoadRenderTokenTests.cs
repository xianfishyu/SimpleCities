namespace SimpleCities.Tests;

public sealed class RoadRenderTokenTests
{
    [Fact]
    public void EveryIdentityComponentParticipatesInEquality()
    {
        var token = new RoadRenderToken(
            SceneGeneration: 1,
            GraphFacadeID: 2,
            GraphFacadeGeneration: 3,
            ChangeSequence: 4,
            RoadStyleRevision: 5,
            RenderRequestID: 6);

        RoadRenderToken[] changed =
        [
            new(7, 2, 3, 4, 5, 6),
            new(1, 7, 3, 4, 5, 6),
            new(1, 2, 7, 4, 5, 6),
            new(1, 2, 3, 7, 5, 6),
            new(1, 2, 3, 4, 7, 6),
            new(1, 2, 3, 4, 5, 7),
        ];

        Assert.All(changed, candidate => Assert.NotEqual(token, candidate));
    }

    [Theory]
    [InlineData(0, 2, 3, 4, 5, 6)]
    [InlineData(1, 0, 3, 4, 5, 6)]
    [InlineData(1, 2, 0, 4, 5, 6)]
    [InlineData(1, 2, 3, -1, 5, 6)]
    [InlineData(1, 2, 3, 4, 0, 6)]
    [InlineData(1, 2, 3, 4, 5, 0)]
    public void InvalidIdentityComponentsAreRejected(
        long sceneGeneration,
        long graphFacadeID,
        long graphFacadeGeneration,
        long changeSequence,
        long roadStyleRevision,
        long renderRequestID)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
        {
            _ = new RoadRenderToken(
                sceneGeneration,
                graphFacadeID,
                graphFacadeGeneration,
                changeSequence,
                roadStyleRevision,
                renderRequestID);
        });
    }

    [Fact]
    public void OrdinaryRequestAdvancesDesiredBeforePresented()
    {
        var tracker = new RoadPresentationTokenTracker();
        RoadRenderToken initial = tracker.BindGraph(graphFacadeID: 11, changeSequence: 0);
        tracker.CommitDesired(initial);

        RoadRenderToken requested = tracker.RequestGraphChange(changeSequence: 1, isFullReset: false);

        Assert.Equal(requested, tracker.DesiredToken);
        Assert.Equal(initial, tracker.PresentedToken);
        Assert.Equal(initial.GraphFacadeGeneration, requested.GraphFacadeGeneration);
        Assert.Equal(initial.RenderRequestID + 1, requested.RenderRequestID);

        tracker.CommitDesired(requested);
        Assert.Equal(requested, tracker.PresentedToken);
    }

    [Fact]
    public void FullResetAndFacadeRebindAdvanceFacadeGeneration()
    {
        var tracker = new RoadPresentationTokenTracker();
        RoadRenderToken initial = tracker.BindGraph(graphFacadeID: 21, changeSequence: 0);
        tracker.CommitDesired(initial);

        RoadRenderToken reset = tracker.RequestGraphChange(changeSequence: 1, isFullReset: true);
        tracker.CommitDesired(reset);
        RoadRenderToken rebound = tracker.BindGraph(graphFacadeID: 22, changeSequence: 0);

        Assert.Equal(initial.GraphFacadeGeneration + 1, reset.GraphFacadeGeneration);
        Assert.Equal(reset.GraphFacadeGeneration + 1, rebound.GraphFacadeGeneration);
        Assert.Equal(22, rebound.GraphFacadeID);
    }

    [Fact]
    public void SceneAndStyleChangesAdvanceOnlyTheirOwnedIdentity()
    {
        var tracker = new RoadPresentationTokenTracker();
        RoadRenderToken initial = tracker.BindGraph(graphFacadeID: 31, changeSequence: 0);
        tracker.CommitDesired(initial);

        Assert.True(tracker.SetSceneGeneration(9, changeSequence: 0, out RoadRenderToken scene));
        tracker.CommitDesired(scene);
        RoadRenderToken style = tracker.RequestStyleRefresh(changeSequence: 0);

        Assert.Equal(9, scene.SceneGeneration);
        Assert.Equal(initial.GraphFacadeID, scene.GraphFacadeID);
        Assert.Equal(initial.GraphFacadeGeneration, scene.GraphFacadeGeneration);
        Assert.Equal(initial.RoadStyleRevision, scene.RoadStyleRevision);
        Assert.Equal(scene.RoadStyleRevision + 1, style.RoadStyleRevision);
        Assert.Equal(scene.ChangeSequence, style.ChangeSequence);
    }

    [Fact]
    public void ReservedLoadDoesNotPublishUntilAtomicCommit()
    {
        var tracker = new RoadPresentationTokenTracker();
        RoadRenderToken initial = tracker.BindGraph(graphFacadeID: 41, changeSequence: 3);
        tracker.CommitDesired(initial);

        RoadRenderLoadReservation reservation = tracker.ReserveLoad();
        RoadRenderToken target = tracker.CreateReservedLoadToken(reservation, changeSequence: 4);

        Assert.True(tracker.IsReservationCurrent(reservation));
        Assert.Equal(initial, tracker.DesiredToken);
        Assert.Equal(initial, tracker.PresentedToken);
        Assert.Equal(initial.GraphFacadeGeneration + 1, target.GraphFacadeGeneration);

        tracker.CommitReservedLoad(reservation, target);

        Assert.Equal(target, tracker.DesiredToken);
        Assert.Equal(target, tracker.PresentedToken);
    }

    [Fact]
    public void LaterRequestInvalidatesReservedLoad()
    {
        var tracker = new RoadPresentationTokenTracker();
        RoadRenderToken initial = tracker.BindGraph(graphFacadeID: 51, changeSequence: 0);
        tracker.CommitDesired(initial);
        RoadRenderLoadReservation reservation = tracker.ReserveLoad();

        tracker.RequestStyleRefresh(changeSequence: 0);

        Assert.False(tracker.IsReservationCurrent(reservation));
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = tracker.CreateReservedLoadToken(reservation, changeSequence: 1);
        });
    }

    [Fact]
    public void RoadGraphFacadeIdentityIsStableAndUnique()
    {
        var first = new RoadGraph();
        var second = new RoadGraph();

        Assert.True(first.FacadeID > 0);
        Assert.Equal(first.FacadeID, first.FacadeID);
        Assert.NotEqual(first.FacadeID, second.FacadeID);
    }
}
