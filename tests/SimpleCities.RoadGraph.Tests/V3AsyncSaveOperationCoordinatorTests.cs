using SimpleCities.Core.V3;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SimpleCities.Tests;

public sealed class V3AsyncSaveOperationCoordinatorTests
{
    [Fact]
    public async Task SaveAsAsync_ReturnsCompletedAndCallsBackend()
    {
        var backend = new FakeBackend();
        var coordinator = new V3AsyncSaveOperationCoordinator(backend);

        V3SaveOperationUiState state = await coordinator.SaveAsAsync(
            "City",
            "City",
            "2026-08-16T00:00:00.0000000Z",
            null,
            null,
            null);

        Assert.True(state.IsComplete);
        Assert.Equal(V3SaveOperationKind.Publish, state.Kind);
        Assert.Equal(1, backend.SaveAsCalls);
    }

    [Fact]
    public async Task SaveAsAsync_WhenCancelRequestedBeforeStart_DoesNotCallBackend()
    {
        var backend = new FakeBackend();
        var coordinator = new V3AsyncSaveOperationCoordinator(backend);
        coordinator.RequestCancel();

        V3SaveOperationUiState state = await coordinator.SaveAsAsync(
            "City",
            "City",
            "2026-08-16T00:00:00.0000000Z",
            null,
            null,
            null);

        Assert.Equal(V3SaveOperationUiPhase.Idle, state.Phase);
        Assert.Equal(0, backend.SaveAsCalls);
    }

    [Fact]
    public async Task SaveAsync_WhenBusy_DoesNotCallBackend()
    {
        var backend = new FakeBackend();
        var controller = new V3SaveOperationController();
        var coordinator = new V3AsyncSaveOperationCoordinator(backend, controller);
        controller.TryBegin(V3SaveOperationKind.Delete, 1);

        V3SaveOperationUiState state = await coordinator.SaveAsync(
            "city-001",
            "City",
            "City",
            "2026-08-16T00:00:00.0000000Z",
            null,
            null,
            null);

        Assert.Equal(V3SaveOperationUiPhase.Busy, state.Phase);
        Assert.Equal(0, backend.SaveCalls);
    }

    [Fact]
    public async Task SaveAsync_ReturnsCompletedPublishResult()
    {
        var backend = new FakeBackend();
        var coordinator = new V3AsyncSaveOperationCoordinator(backend);

        V3SaveOperationUiState state = await coordinator.SaveAsync(
            "city-001",
            "City",
            "City",
            "2026-08-16T00:00:00.0000000Z",
            null,
            null,
            null);

        Assert.True(state.IsComplete);
        Assert.Equal(V3SaveOperationKind.Publish, state.Kind);
        Assert.Equal(1, backend.SaveCalls);
        Assert.Equal("city-001", backend.LastSlotId);
    }

    [Fact]
    public async Task SaveAsync_WhenBackendFails_ReturnsFailed()
    {
        var backend = new FakeBackend { FailOperations = true };
        var coordinator = new V3AsyncSaveOperationCoordinator(backend);

        V3SaveOperationUiState state = await coordinator.SaveAsync(
            "city-001",
            "City",
            "City",
            "2026-08-16T00:00:00.0000000Z",
            null,
            null,
            null);

        Assert.Equal(V3SaveOperationUiPhase.Failed, state.Phase);
        Assert.False(state.IsComplete);
        Assert.Equal("fail", state.Error);
        Assert.Equal(1, backend.SaveCalls);
    }

    [Fact]
    public async Task LoadAsync_ReturnsCompletedLoadResult()
    {
        var backend = new FakeBackend();
        var coordinator = new V3AsyncSaveOperationCoordinator(backend);

        V3SaveOperationUiState state = await coordinator.LoadAsync("city-001", lineageID: 2);

        Assert.True(state.IsComplete);
        Assert.Equal(V3SaveOperationKind.Load, state.Kind);
        Assert.Equal(1, backend.LoadCalls);
        Assert.Equal("city-001", backend.LastSlotId);
    }

    [Fact]
    public async Task LoadAsync_WhenBackendFails_ReturnsFailed()
    {
        var backend = new FakeBackend { FailOperations = true };
        var coordinator = new V3AsyncSaveOperationCoordinator(backend);

        V3SaveOperationUiState state = await coordinator.LoadAsync("city-001", lineageID: 2);

        Assert.Equal(V3SaveOperationUiPhase.Failed, state.Phase);
        Assert.False(state.IsComplete);
        Assert.Equal("fail", state.Error);
        Assert.Equal(1, backend.LoadCalls);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsCompletedDeleteResult()
    {
        var backend = new FakeBackend();
        var coordinator = new V3AsyncSaveOperationCoordinator(backend);

        V3SaveOperationUiState state = await coordinator.DeleteAsync("city-001");

        Assert.True(state.IsComplete);
        Assert.Equal(V3SaveOperationKind.Delete, state.Kind);
        Assert.Equal(1, backend.DeleteCalls);
        Assert.Equal("city-001", backend.LastSlotId);
    }

    [Fact]
    public async Task DeleteAsync_WhenBackendFails_ReturnsFailed()
    {
        var backend = new FakeBackend { FailOperations = true };
        var coordinator = new V3AsyncSaveOperationCoordinator(backend);

        V3SaveOperationUiState state = await coordinator.DeleteAsync("city-001");

        Assert.Equal(V3SaveOperationUiPhase.Failed, state.Phase);
        Assert.False(state.IsComplete);
        Assert.Equal("fail", state.Error);
        Assert.Equal(1, backend.DeleteCalls);
    }

    [Fact]
    public async Task SaveAsAsync_WhenBackendFails_ReturnsFailed()
    {
        var backend = new FakeBackend { FailOperations = true };
        var coordinator = new V3AsyncSaveOperationCoordinator(backend);

        V3SaveOperationUiState state = await coordinator.SaveAsAsync(
            "City",
            "City",
            "2026-08-16T00:00:00.0000000Z",
            null,
            null,
            null);

        Assert.Equal(V3SaveOperationUiPhase.Failed, state.Phase);
        Assert.False(state.IsComplete);
        Assert.Equal("fail", state.Error);
    }

    [Fact]
    public void RequestCancel_DelegatesToController()
    {
        var backend = new FakeBackend();
        var controller = new V3SaveOperationController();
        var coordinator = new V3AsyncSaveOperationCoordinator(backend, controller);
        controller.TryBegin(V3SaveOperationKind.Load, 1);

        V3SaveOperationUiState state = coordinator.RequestCancel();

        Assert.Equal(V3SaveOperationUiPhase.Cancelling, state.Phase);
        Assert.True(coordinator.IsBusy);
        Assert.True(coordinator.IsCancellationRequested);
    }

    [Fact]
    public void Reset_ClearsCancellationAndReturnsIdle()
    {
        var backend = new FakeBackend();
        var controller = new V3SaveOperationController();
        var coordinator = new V3AsyncSaveOperationCoordinator(backend, controller);
        controller.TryBegin(V3SaveOperationKind.Delete, 1);
        coordinator.RequestCancel();

        coordinator.Reset();

        Assert.Equal(V3SaveOperationUiPhase.Idle, coordinator.State.Phase);
        Assert.False(coordinator.IsBusy);
        Assert.False(coordinator.IsCancellationRequested);
    }

    private sealed class FakeBackend : IV3SaveOperationBackend
    {
        public string? CurrentSlotID { get; set; } = "city-001";
        public bool FailOperations;
        public int SaveAsCalls;
        public int SaveCalls;
        public int LoadCalls;
        public int DeleteCalls;
        public string LastSlotId = "";

        public IReadOnlyList<V3SlotSummary> ListSlots() => [];

        public V3SaveOperationResult SaveAs(
            string displayName,
            string cityName,
            string timestamp,
            long? population,
            decimal? funds,
            string? thumbnailFile)
        {
            SaveAsCalls++;
            return Result(V3SaveOperationKind.Publish);
        }

        public V3SaveOperationResult Save(
            string slotId,
            string displayName,
            string cityName,
            string timestamp,
            long? population,
            decimal? funds,
            string? thumbnailFile)
        {
            SaveCalls++;
            LastSlotId = slotId;
            return Result(V3SaveOperationKind.Publish);
        }

        public V3SaveOperationResult Load(string slotId, long lineageID)
        {
            LoadCalls++;
            LastSlotId = slotId;
            return Result(V3SaveOperationKind.Load);
        }

        public V3SaveOperationResult Delete(string slotId)
        {
            DeleteCalls++;
            LastSlotId = slotId;
            return Result(V3SaveOperationKind.Delete);
        }

        private V3SaveOperationResult Result(V3SaveOperationKind kind)
        {
            V3SaveOperationToken token = V3SaveOperationToken.Create(kind, 1);
            return FailOperations
                ? V3SaveOperationResult.FailedBeforeCommit(token, V3SaveOperationPhase.Prepare, "fail")
                : V3SaveOperationResult.Succeeded(token);
        }
    }
}
