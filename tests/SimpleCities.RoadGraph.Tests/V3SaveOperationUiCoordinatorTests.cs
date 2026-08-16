using SimpleCities.Core.V3;
using System.Collections.Generic;

namespace SimpleCities.Tests;

public sealed class V3SaveOperationUiCoordinatorTests
{
    [Fact]
    public void SaveAs_WhenNotBusy_ReturnsCompletedAndCallsBackend()
    {
        var backend = new FakeBackend();
        var coordinator = new V3SaveOperationUiCoordinator(backend);

        V3SaveOperationUiState state = coordinator.SaveAs(
            "City",
            "City",
            "2026-08-16T00:00:00.0000000Z",
            null,
            null,
            null);

        Assert.True(state.IsComplete);
        Assert.Equal(V3SaveOperationKind.Publish, state.Kind);
        Assert.Equal(1, backend.SaveAsCalls);
        Assert.Equal("City", backend.LastDisplayName);
    }

    [Fact]
    public void SaveAs_WhenBackendFails_ReturnsFailed()
    {
        var backend = new FakeBackend { FailOperations = true };
        var coordinator = new V3SaveOperationUiCoordinator(backend);

        V3SaveOperationUiState state = coordinator.SaveAs(
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
    public void Save_WhenBusy_DoesNotCallBackend()
    {
        var backend = new FakeBackend();
        var controller = new V3SaveOperationController();
        var coordinator = new V3SaveOperationUiCoordinator(backend, controller);
        controller.TryBegin(V3SaveOperationKind.Delete, 1);

        V3SaveOperationUiState state = coordinator.Save(
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
    public void Save_ReturnsCompletedPublishResult()
    {
        var backend = new FakeBackend();
        var coordinator = new V3SaveOperationUiCoordinator(backend);

        V3SaveOperationUiState state = coordinator.Save(
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
    public void Save_WhenBackendFails_ReturnsFailed()
    {
        var backend = new FakeBackend { FailOperations = true };
        var coordinator = new V3SaveOperationUiCoordinator(backend);

        V3SaveOperationUiState state = coordinator.Save(
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
    public void Load_ReturnsCompletedLoadResult()
    {
        var backend = new FakeBackend();
        var coordinator = new V3SaveOperationUiCoordinator(backend);

        V3SaveOperationUiState state = coordinator.Load("city-001", lineageID: 2);

        Assert.True(state.IsComplete);
        Assert.Equal(V3SaveOperationKind.Load, state.Kind);
        Assert.Equal(1, backend.LoadCalls);
        Assert.Equal("city-001", backend.LastSlotId);
    }

    [Fact]
    public void Load_WhenBackendFails_ReturnsFailed()
    {
        var backend = new FakeBackend { FailOperations = true };
        var coordinator = new V3SaveOperationUiCoordinator(backend);

        V3SaveOperationUiState state = coordinator.Load("city-001", lineageID: 2);

        Assert.Equal(V3SaveOperationUiPhase.Failed, state.Phase);
        Assert.False(state.IsComplete);
        Assert.Equal("fail", state.Error);
        Assert.Equal(1, backend.LoadCalls);
    }

    [Fact]
    public void Delete_ReturnsCompletedDeleteResult()
    {
        var backend = new FakeBackend();
        var coordinator = new V3SaveOperationUiCoordinator(backend);

        V3SaveOperationUiState state = coordinator.Delete("city-001");

        Assert.True(state.IsComplete);
        Assert.Equal(V3SaveOperationKind.Delete, state.Kind);
        Assert.Equal(1, backend.DeleteCalls);
        Assert.Equal("city-001", backend.LastSlotId);
    }

    [Fact]
    public void Delete_WhenBackendFails_ReturnsFailed()
    {
        var backend = new FakeBackend { FailOperations = true };
        var coordinator = new V3SaveOperationUiCoordinator(backend);

        V3SaveOperationUiState state = coordinator.Delete("city-001");

        Assert.Equal(V3SaveOperationUiPhase.Failed, state.Phase);
        Assert.False(state.IsComplete);
        Assert.Equal("fail", state.Error);
        Assert.Equal(1, backend.DeleteCalls);
    }

    [Fact]
    public void RequestCancel_WhenNotCancellable_KeepsCompletedState()
    {
        var backend = new FakeBackend();
        var coordinator = new V3SaveOperationUiCoordinator(backend);
        coordinator.SaveAs("City", "City", "2026-08-16T00:00:00.0000000Z", null, null, null);

        V3SaveOperationUiState state = coordinator.RequestCancel();

        Assert.Equal(V3SaveOperationUiPhase.Completed, state.Phase);
        Assert.True(state.IsComplete);
    }

    [Fact]
    public void RequestCancel_DelegatesToController()
    {
        var backend = new FakeBackend();
        var controller = new V3SaveOperationController();
        var coordinator = new V3SaveOperationUiCoordinator(backend, controller);
        controller.TryBegin(V3SaveOperationKind.Load, 1);

        V3SaveOperationUiState state = coordinator.RequestCancel();

        Assert.Equal(V3SaveOperationUiPhase.Cancelling, state.Phase);
        Assert.True(coordinator.IsBusy);
    }

    [Fact]
    public void Reset_ReturnsIdle()
    {
        var backend = new FakeBackend();
        var controller = new V3SaveOperationController();
        var coordinator = new V3SaveOperationUiCoordinator(backend, controller);
        controller.TryBegin(V3SaveOperationKind.Delete, 1);

        coordinator.Reset();

        Assert.Equal(V3SaveOperationUiPhase.Idle, coordinator.State.Phase);
        Assert.False(coordinator.IsBusy);
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
        public string LastDisplayName = "";

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
            LastDisplayName = displayName;
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
            LastDisplayName = displayName;
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
