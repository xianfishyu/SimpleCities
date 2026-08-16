using SimpleCities.Core.V3;

namespace SimpleCities.Tests;

public sealed class V3SaveOperationUiStateTests
{
    [Fact]
    public void FromResult_Null_ReturnsIdle()
    {
        V3SaveOperationUiState state = V3SaveOperationUiState.FromResult(null);

        Assert.Equal(V3SaveOperationUiPhase.Idle, state.Phase);
        Assert.False(state.IsBusy);
        Assert.False(state.IsCancellable);
        Assert.False(state.IsComplete);
        Assert.False(state.IsFailed);
        Assert.False(state.HasWarnings);
        Assert.Null(state.Error);
        Assert.Null(state.WarningSummary);
        Assert.False(state.IsTerminal);
    }

    [Fact]
    public void FromResult_Succeeded_ReturnsCompleted()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Publish, 7);
        V3SaveOperationResult result = V3SaveOperationResult.Succeeded(token);

        V3SaveOperationUiState state = V3SaveOperationUiState.FromResult(result);

        Assert.Equal(V3SaveOperationUiPhase.Completed, state.Phase);
        Assert.True(state.IsComplete);
        Assert.False(state.IsBusy);
        Assert.False(state.IsFailed);
        Assert.False(state.IsCancellable);
        Assert.Equal(V3SaveOperationKind.Publish, state.Kind);
        Assert.Equal(V3SaveOperationPhase.Completed, state.OperationPhase);
        Assert.False(state.HasWarnings);
        Assert.Null(state.WarningSummary);
        Assert.True(state.IsTerminal);
    }

    [Fact]
    public void FromResult_FailedBeforeCommit_ReturnsFailedAndCancellable()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Load, 3);
        V3SaveOperationResult result = V3SaveOperationResult.FailedBeforeCommit(
            token,
            V3SaveOperationPhase.Prepare,
            "boom");

        V3SaveOperationUiState state = V3SaveOperationUiState.FromResult(result);

        Assert.Equal(V3SaveOperationUiPhase.Failed, state.Phase);
        Assert.True(state.IsFailed);
        Assert.False(state.IsComplete);
        Assert.False(state.IsBusy);
        Assert.True(state.IsCancellable);
        Assert.Equal("boom", state.Error);
        Assert.Equal(V3SaveOperationKind.Load, state.Kind);
        Assert.Equal(V3SaveOperationPhase.Prepare, state.OperationPhase);
        Assert.True(state.IsTerminal);
    }

    [Fact]
    public void FromResult_FailedBeforeAdmission_ReturnsFailedAndCancellable()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Publish, 2);
        V3SaveOperationResult result = V3SaveOperationResult.FailedBeforeCommit(
            token,
            V3SaveOperationPhase.Admission,
            "busy");

        V3SaveOperationUiState state = V3SaveOperationUiState.FromResult(result);

        Assert.Equal(V3SaveOperationUiPhase.Failed, state.Phase);
        Assert.True(state.IsFailed);
        Assert.True(state.IsCancellable);
        Assert.Equal("busy", state.Error);
    }

    [Fact]
    public void FromResult_FailedBeforePreflight_ReturnsFailedAndCancellable()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Load, 3);
        V3SaveOperationResult result = V3SaveOperationResult.FailedBeforeCommit(
            token,
            V3SaveOperationPhase.Preflight,
            "preflight failed");

        V3SaveOperationUiState state = V3SaveOperationUiState.FromResult(result);

        Assert.Equal(V3SaveOperationUiPhase.Failed, state.Phase);
        Assert.True(state.IsFailed);
        Assert.True(state.IsCancellable);
        Assert.Equal("preflight failed", state.Error);
    }

    [Fact]
    public void FromResult_InProgress_ReturnsBusyAndCancellable()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Load, 1);
        var result = new V3SaveOperationResult(
            token,
            V3SaveOperationPhase.Admission,
            false,
            false,
            [],
            null);

        V3SaveOperationUiState state = V3SaveOperationUiState.FromResult(result);

        Assert.Equal(V3SaveOperationUiPhase.Busy, state.Phase);
        Assert.True(state.IsBusy);
        Assert.True(state.IsCancellable);
        Assert.False(state.IsComplete);
        Assert.False(state.IsFailed);
        Assert.False(state.IsTerminal);
        Assert.Null(state.Error);
    }

    [Fact]
    public void FromResult_ObserverWarnings_ExposesWarningSummary()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Delete, 1);
        V3SaveOperationResult result = V3SaveOperationResult.SucceededWithObserverWarnings(
            token,
            new[] { "cleanup pending" });

        V3SaveOperationUiState state = V3SaveOperationUiState.FromResult(result);

        Assert.True(state.IsComplete);
        Assert.True(state.HasWarnings);
        Assert.Equal("cleanup pending", state.WarningSummary);
        Assert.True(state.IsTerminal);
        Assert.Null(state.Error);
    }

    [Fact]
    public void FromResult_CommitPhaseInProgress_IsBusyButNotCancellable()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Load, 1);
        var result = new V3SaveOperationResult(
            token,
            V3SaveOperationPhase.Commit,
            false,
            false,
            [],
            null);

        V3SaveOperationUiState state = V3SaveOperationUiState.FromResult(result);

        Assert.Equal(V3SaveOperationUiPhase.Busy, state.Phase);
        Assert.True(state.IsBusy);
        Assert.False(state.IsCancellable);
        Assert.False(state.IsTerminal);
    }

    [Fact]
    public void FromResult_EmptyObserverWarnings_HasNoWarnings()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Delete, 1);
        V3SaveOperationResult result = V3SaveOperationResult.SucceededWithObserverWarnings(token, []);

        V3SaveOperationUiState state = V3SaveOperationUiState.FromResult(result);

        Assert.True(state.IsComplete);
        Assert.False(state.HasWarnings);
        Assert.Null(state.WarningSummary);
    }

    [Fact]
    public void FromResult_MultipleObserverWarnings_JoinsWithSemicolon()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Delete, 1);
        V3SaveOperationResult result = V3SaveOperationResult.SucceededWithObserverWarnings(
            token,
            new[] { "cleanup pending", "thumbnail missing" });

        V3SaveOperationUiState state = V3SaveOperationUiState.FromResult(result);

        Assert.True(state.HasWarnings);
        Assert.Equal("cleanup pending；thumbnail missing", state.WarningSummary);
        Assert.True(state.IsComplete);
    }

    [Fact]
    public void Cancelling_ReturnsCancellingPhaseForKind()
    {
        V3SaveOperationUiState state = V3SaveOperationUiState.Cancelling(V3SaveOperationKind.Delete);

        Assert.Equal(V3SaveOperationUiPhase.Cancelling, state.Phase);
        Assert.Equal(V3SaveOperationKind.Delete, state.Kind);
        Assert.True(state.IsBusy);
        Assert.False(state.IsCancellable);
        Assert.False(state.IsTerminal);
        Assert.False(state.IsComplete);
        Assert.False(state.IsFailed);
    }
}
