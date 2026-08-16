using SimpleCities.Core.V3;

namespace SimpleCities.Tests;

public sealed class V3SaveOperationControllerTests
{
    [Fact]
    public void TryBegin_SetsBusyAdmissionAndToken()
    {
        var controller = new V3SaveOperationController();

        bool began = controller.TryBegin(V3SaveOperationKind.Load, 42);

        Assert.True(began);
        Assert.True(controller.IsBusy);
        Assert.False(controller.IsCancelling);
        Assert.NotNull(controller.ActiveToken);
        Assert.Equal(V3SaveOperationKind.Load, controller.ActiveToken!.Kind);
        Assert.Equal(42, controller.ActiveToken.SceneGeneration);
        Assert.Equal(V3SaveOperationUiPhase.Busy, controller.State.Phase);
        Assert.Equal(V3SaveOperationPhase.Admission, controller.State.OperationPhase);
        Assert.True(controller.State.IsCancellable);
    }

    [Fact]
    public void TryBegin_WhenBusy_ReturnsFalse()
    {
        var controller = new V3SaveOperationController();
        controller.TryBegin(V3SaveOperationKind.Publish, 1);

        bool secondBegin = controller.TryBegin(V3SaveOperationKind.Delete, 1);

        Assert.False(secondBegin);
        Assert.Equal(V3SaveOperationKind.Publish, controller.ActiveToken!.Kind);
    }

    [Fact]
    public void Complete_MatchingResult_TransitionsToCompletedAndClearsToken()
    {
        var controller = new V3SaveOperationController();
        controller.TryBegin(V3SaveOperationKind.Publish, 5);
        V3SaveOperationToken token = controller.ActiveToken!;
        V3SaveOperationResult result = V3SaveOperationResult.Succeeded(token);

        V3SaveOperationUiState state = controller.Complete(result);

        Assert.Equal(V3SaveOperationUiPhase.Completed, state.Phase);
        Assert.True(state.IsComplete);
        Assert.False(controller.IsBusy);
        Assert.Null(controller.ActiveToken);
    }

    [Fact]
    public void Complete_StaleResult_IsIgnored()
    {
        var controller = new V3SaveOperationController();
        controller.TryBegin(V3SaveOperationKind.Load, 5);
        V3SaveOperationToken staleToken = V3SaveOperationToken.Create(V3SaveOperationKind.Delete, 99);
        V3SaveOperationResult staleResult = V3SaveOperationResult.Succeeded(staleToken);

        V3SaveOperationUiState state = controller.Complete(staleResult);

        Assert.Equal(V3SaveOperationUiPhase.Busy, state.Phase);
        Assert.True(controller.IsBusy);
        Assert.NotNull(controller.ActiveToken);
    }

    [Fact]
    public void Complete_BeforeBegin_IsIgnored()
    {
        var controller = new V3SaveOperationController();
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Delete, 1);
        V3SaveOperationResult result = V3SaveOperationResult.Succeeded(token);

        V3SaveOperationUiState state = controller.Complete(result);

        Assert.Equal(V3SaveOperationUiPhase.Idle, state.Phase);
        Assert.False(controller.IsBusy);
        Assert.Null(controller.ActiveToken);
    }

    [Fact]
    public void RequestCancel_WhenCancellable_SetsCancelling()
    {
        var controller = new V3SaveOperationController();
        controller.TryBegin(V3SaveOperationKind.Load, 7);

        V3SaveOperationUiState state = controller.RequestCancel();

        Assert.Equal(V3SaveOperationUiPhase.Cancelling, state.Phase);
        Assert.True(controller.IsCancelling);
        Assert.True(controller.IsBusy);
        Assert.False(state.IsCancellable);
        Assert.NotNull(controller.ActiveToken);
    }

    [Fact]
    public void RequestCancel_WhenNotCancellable_KeepsState()
    {
        var controller = new V3SaveOperationController();
        controller.TryBegin(V3SaveOperationKind.Publish, 1);
        V3SaveOperationToken token = controller.ActiveToken!;
        controller.Complete(V3SaveOperationResult.Succeeded(token));

        V3SaveOperationUiState state = controller.RequestCancel();

        Assert.Equal(V3SaveOperationUiPhase.Completed, state.Phase);
        Assert.False(controller.IsCancelling);
    }

    [Fact]
    public void Reset_ReturnsIdle()
    {
        var controller = new V3SaveOperationController();
        controller.TryBegin(V3SaveOperationKind.Delete, 1);

        controller.Reset();

        Assert.Equal(V3SaveOperationUiPhase.Idle, controller.State.Phase);
        Assert.False(controller.IsBusy);
        Assert.Null(controller.ActiveToken);
    }
}
