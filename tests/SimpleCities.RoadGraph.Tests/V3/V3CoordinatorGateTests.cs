using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3CoordinatorGateTests
{
    [Fact]
    public void TryAcquire_SucceedsWhenIdle()
    {
        var gate = new V3CoordinatorGate();

        Assert.True(gate.TryAcquire(out Guid operationId));
        Assert.NotEqual(Guid.Empty, operationId);
        Assert.True(gate.IsBusy);
    }

    [Fact]
    public void TryAcquire_FailsWhenBusy()
    {
        var gate = new V3CoordinatorGate();
        gate.TryAcquire(out _);

        Assert.False(gate.TryAcquire(out Guid operationId));
        Assert.Equal(Guid.Empty, operationId);
    }

    [Fact]
    public void Release_AllowsNextAcquire()
    {
        var gate = new V3CoordinatorGate();
        gate.TryAcquire(out Guid operationId);

        gate.Release(operationId);

        Assert.False(gate.IsBusy);
        Assert.True(gate.TryAcquire(out _));
    }

    [Fact]
    public void SetPendingAutosave_CanBeSetAndCleared()
    {
        var gate = new V3CoordinatorGate();

        gate.SetPendingAutosave(true);
        Assert.True(gate.HasPendingAutosave);

        gate.SetPendingAutosave(false);
        Assert.False(gate.HasPendingAutosave);
    }
}
