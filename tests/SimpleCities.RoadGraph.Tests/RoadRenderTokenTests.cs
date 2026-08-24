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
    [InlineData(nameof(RoadRenderToken.SceneGeneration))]
    [InlineData(nameof(RoadRenderToken.GraphFacadeID))]
    [InlineData(nameof(RoadRenderToken.GraphFacadeGeneration))]
    [InlineData(nameof(RoadRenderToken.ChangeSequence))]
    [InlineData(nameof(RoadRenderToken.RoadStyleRevision))]
    [InlineData(nameof(RoadRenderToken.RenderRequestID))]
    public void AnyTokenDimensionPerturbationRejectsLateBuildFailure(string component)
    {
        var tracker = new RoadPresentationTokenTracker();
        RoadRenderToken initial = tracker.BindGraph(graphFacadeID: 61, changeSequence: 0);
        tracker.CommitDesired(initial);
        RoadRenderToken target = tracker.RequestGraphChange(changeSequence: 1, isFullReset: false);
        int attempt = tracker.BeginBuildAttempt(target);

        RoadRenderToken stale = Perturb(target, component);

        Assert.NotEqual(target, stale);
        Assert.Null(tracker.ReportBuildFailure(
            stale,
            attempt,
            new InvalidOperationException($"late {component}")));
        Assert.False(tracker.IsPresentationStalled);
        Assert.Null(tracker.CurrentFailure);
        Assert.Equal(initial, tracker.PresentedToken);
        Assert.Equal(target, tracker.DesiredToken);
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
    public void CommittedAttemptRejectsLateFailure()
    {
        var tracker = new RoadPresentationTokenTracker();
        RoadRenderToken initial = tracker.BindGraph(graphFacadeID: 12, changeSequence: 0);
        tracker.CommitDesired(initial);
        RoadRenderToken target = tracker.RequestGraphChange(
            changeSequence: 1,
            isFullReset: false);
        int attempt = tracker.BeginBuildAttempt(target);
        tracker.CommitDesired(target);

        RoadPresentationFailure? failure = tracker.ReportBuildFailure(
            target,
            attempt,
            new InvalidOperationException("late failure after commit"));

        Assert.Null(failure);
        Assert.True(tracker.IsPresentationCurrent);
        Assert.False(tracker.IsPresentationStalled);
        Assert.Null(tracker.CurrentFailure);
        Assert.Equal(target, tracker.PresentedToken);
    }

    [Fact]
    public void NewAttemptRejectsPreviousAttemptLateFailure()
    {
        var tracker = new RoadPresentationTokenTracker();
        RoadRenderToken initial = tracker.BindGraph(graphFacadeID: 12, changeSequence: 0);
        tracker.CommitDesired(initial);
        RoadRenderToken target = tracker.RequestGraphChange(
            changeSequence: 1,
            isFullReset: false);
        int firstAttempt = tracker.BeginBuildAttempt(target);
        Assert.NotNull(tracker.ReportBuildFailure(
            target,
            firstAttempt,
            new InvalidOperationException("first failure")));
        int secondAttempt = tracker.BeginBuildAttempt(target);

        RoadPresentationFailure? lateFailure = tracker.ReportBuildFailure(
            target,
            firstAttempt,
            new InvalidOperationException("late first failure"));
        RoadPresentationFailure currentFailure = Assert.IsType<RoadPresentationFailure>(
            tracker.ReportBuildFailure(
                target,
                secondAttempt,
                new InvalidOperationException("second failure")));

        Assert.Null(lateFailure);
        Assert.Equal(secondAttempt, currentFailure.AttemptNumber);
        Assert.Equal(currentFailure, tracker.CurrentFailure);
        Assert.True(tracker.IsPresentationStalled);
        Assert.Equal(initial, tracker.PresentedToken);
        Assert.Equal(target, tracker.DesiredToken);
    }

    [Fact]
    public void FailedBuildStallsCurrentDesiredAndRetriesWithoutChangingItsIdentity()
    {
        var tracker = new RoadPresentationTokenTracker();
        RoadRenderToken initial = tracker.BindGraph(graphFacadeID: 12, changeSequence: 0);
        tracker.CommitDesired(initial);
        RoadRenderToken target = tracker.RequestGraphChange(
            changeSequence: 1,
            isFullReset: false);

        int firstAttempt = tracker.BeginBuildAttempt(target);
        RoadPresentationFailure firstFailure = Assert.IsType<RoadPresentationFailure>(
            tracker.ReportBuildFailure(
                target,
                firstAttempt,
                new InvalidOperationException("first failure")));

        Assert.True(tracker.IsPresentationStalled);
        Assert.Equal(1, firstFailure.AttemptNumber);
        Assert.Equal(target, firstFailure.RenderToken);
        Assert.Equal(typeof(InvalidOperationException).FullName, firstFailure.ExceptionType);
        Assert.Equal("first failure", firstFailure.Message);
        Assert.Equal(initial, tracker.PresentedToken);

        int retryAttempt = tracker.BeginBuildAttempt(target);

        Assert.Equal(2, retryAttempt);
        Assert.False(tracker.IsPresentationStalled);
        Assert.Null(tracker.CurrentFailure);
        Assert.Equal(target, tracker.DesiredToken);

        tracker.CommitDesired(target);

        Assert.True(tracker.IsPresentationCurrent);
        Assert.Equal(target, tracker.PresentedToken);
        Assert.Equal(2, tracker.AttemptCount);
        Assert.Null(tracker.CurrentFailure);
    }

    [Fact]
    public void NewerRequestSupersedesStalledFailureAndRejectsItsLateResult()
    {
        var tracker = new RoadPresentationTokenTracker();
        RoadRenderToken initial = tracker.BindGraph(graphFacadeID: 13, changeSequence: 0);
        tracker.CommitDesired(initial);
        RoadRenderToken superseded = tracker.RequestGraphChange(
            changeSequence: 1,
            isFullReset: false);
        int attempt = tracker.BeginBuildAttempt(superseded);
        Assert.NotNull(tracker.ReportBuildFailure(
            superseded,
            attempt,
            new InvalidOperationException("superseded")));

        RoadRenderToken current = tracker.RequestStyleRefresh(changeSequence: 1);

        Assert.False(tracker.IsPresentationStalled);
        Assert.Null(tracker.CurrentFailure);
        Assert.Equal(0, tracker.AttemptCount);
        Assert.Null(tracker.ReportBuildFailure(
            superseded,
            attempt,
            new InvalidOperationException("late")));
        Assert.Equal(current, tracker.DesiredToken);
        Assert.Equal(initial, tracker.PresentedToken);
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

    private static RoadRenderToken Perturb(RoadRenderToken token, string component) => component switch
    {
        nameof(RoadRenderToken.SceneGeneration) => new(
            token.SceneGeneration + 1,
            token.GraphFacadeID,
            token.GraphFacadeGeneration,
            token.ChangeSequence,
            token.RoadStyleRevision,
            token.RenderRequestID),
        nameof(RoadRenderToken.GraphFacadeID) => new(
            token.SceneGeneration,
            token.GraphFacadeID + 1,
            token.GraphFacadeGeneration,
            token.ChangeSequence,
            token.RoadStyleRevision,
            token.RenderRequestID),
        nameof(RoadRenderToken.GraphFacadeGeneration) => new(
            token.SceneGeneration,
            token.GraphFacadeID,
            token.GraphFacadeGeneration + 1,
            token.ChangeSequence,
            token.RoadStyleRevision,
            token.RenderRequestID),
        nameof(RoadRenderToken.ChangeSequence) => new(
            token.SceneGeneration,
            token.GraphFacadeID,
            token.GraphFacadeGeneration,
            token.ChangeSequence + 1,
            token.RoadStyleRevision,
            token.RenderRequestID),
        nameof(RoadRenderToken.RoadStyleRevision) => new(
            token.SceneGeneration,
            token.GraphFacadeID,
            token.GraphFacadeGeneration,
            token.ChangeSequence,
            token.RoadStyleRevision + 1,
            token.RenderRequestID),
        nameof(RoadRenderToken.RenderRequestID) => new(
            token.SceneGeneration,
            token.GraphFacadeID,
            token.GraphFacadeGeneration,
            token.ChangeSequence,
            token.RoadStyleRevision,
            token.RenderRequestID + 1),
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown road render token component."),
    };
}
