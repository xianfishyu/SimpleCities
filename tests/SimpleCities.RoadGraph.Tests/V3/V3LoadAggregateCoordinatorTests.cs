using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3LoadAggregateCoordinatorTests
{
    [Fact]
    public void HappyPath_CommitsWhenAllParticipantsPrepared()
    {
        var coordinator = new V3LoadAggregateCoordinator(["graph", "tool", "renderer"]);

        Assert.True(coordinator.TryBegin());
        Assert.True(coordinator.TryEnterPrepare());
        Assert.True(coordinator.TryPrepare("graph"));
        Assert.True(coordinator.TryPrepare("tool"));
        Assert.True(coordinator.TryPrepare("renderer"));
        Assert.True(coordinator.TryEnterPreflight());

        bool committed = false;
        Assert.True(coordinator.TryCommit(() => committed = true));

        Assert.True(committed);
        Assert.Equal(V3LoadPhase.Completed, coordinator.Phase);
    }

    [Fact]
    public void TryEnterPreflight_MissingParticipant_FailsWithoutAdvancing()
    {
        var coordinator = new V3LoadAggregateCoordinator(["graph", "tool"]);
        coordinator.TryBegin();
        coordinator.TryEnterPrepare();
        coordinator.TryPrepare("graph");

        Assert.False(coordinator.TryEnterPreflight());
        Assert.Equal(V3LoadPhase.Prepare, coordinator.Phase);
    }

    [Fact]
    public void TryPrepare_UnknownParticipant_Fails()
    {
        var coordinator = new V3LoadAggregateCoordinator(["graph"]);

        Assert.False(coordinator.TryPrepare("renderer"));
    }

    [Fact]
    public void TryCommit_ActionThrows_FailsAndRunsNoFurtherCommit()
    {
        var coordinator = CreatePreflightCoordinator();

        Assert.False(coordinator.TryCommit(() => throw new System.InvalidOperationException("boom")));
        Assert.Equal(V3LoadPhase.Failed, coordinator.Phase);
    }

    [Fact]
    public void TryCommit_WithoutPreflight_Fails()
    {
        var coordinator = new V3LoadAggregateCoordinator(["graph"]);
        coordinator.TryBegin();
        coordinator.TryEnterPrepare();
        coordinator.TryPrepare("graph");

        Assert.False(coordinator.TryCommit(() => { }));
        Assert.Equal(V3LoadPhase.Prepare, coordinator.Phase);
    }

    [Fact]
    public void AddWarning_IsExposedInAggregate()
    {
        var coordinator = new V3LoadAggregateCoordinator(["graph"]);
        coordinator.AddWarning("observer failed");

        Assert.Equal(new[] { "observer failed" }, coordinator.Aggregate.Warnings);
    }

    [Fact]
    public void Constructor_EmptyRequired_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => new V3LoadAggregateCoordinator([]));
    }

    private static V3LoadAggregateCoordinator CreatePreflightCoordinator()
    {
        var coordinator = new V3LoadAggregateCoordinator(["graph"]);
        coordinator.TryBegin();
        coordinator.TryEnterPrepare();
        coordinator.TryPrepare("graph");
        Assert.True(coordinator.TryEnterPreflight());
        return coordinator;
    }
}
