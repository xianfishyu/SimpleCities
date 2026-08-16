using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;

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
    public void TryPrepare_ToolParticipant_WhenPrepared_MarksReady()
    {
        var coordinator = new V3LoadAggregateCoordinator(["tool", "graph"]);
        coordinator.TryBegin();
        coordinator.TryEnterPrepare();
        var participant = V3ToolLoadParticipant.Prepare(new RoadToolFullReset(RoadToolType.Place, RoadType.Street));

        Assert.True(coordinator.TryPrepare(participant));
        Assert.Contains("tool", coordinator.Aggregate.PreparedParticipants);
    }

    [Fact]
    public void TryPrepare_RendererParticipant_WhenNotCanCommit_Fails()
    {
        var coordinator = new V3LoadAggregateCoordinator(["renderer", "graph"]);
        coordinator.TryBegin();
        coordinator.TryEnterPrepare();
        var participant = V3RendererLoadParticipant.Prepare(CreatePresentationPlan(includeMesh: false));

        Assert.False(coordinator.TryPrepare(participant));
        Assert.DoesNotContain("renderer", coordinator.Aggregate.PreparedParticipants);
    }

    [Fact]
    public void TryPrepare_RendererParticipant_WhenCanCommit_MarksReady()
    {
        var coordinator = new V3LoadAggregateCoordinator(["renderer", "graph"]);
        coordinator.TryBegin();
        coordinator.TryEnterPrepare();
        var participant = V3RendererLoadParticipant.Prepare(CreatePresentationPlan(includeMesh: true));

        Assert.True(coordinator.TryPrepare(participant));
        Assert.Contains("renderer", coordinator.Aggregate.PreparedParticipants);
    }

    [Fact]
    public void Constructor_EmptyRequired_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => new V3LoadAggregateCoordinator([]));
    }

    private static RoadPresentationFullReset CreatePresentationPlan(bool includeMesh)
    {
        var snapshot = new RoadSurfaceSnapshot(
            new GraphStateToken(1, 3, 4),
            [
                new RoadSurfaceOwner(
                    RoadSurfaceOwnerKind.Ribbon,
                    NodeID: 10,
                    EdgeID: 20,
                    Endpoint: EdgeEndpoint.A,
                    new RoadLocation(20, 0, 0.5f)),
            ]);
        var token = new RoadRenderToken(1, 2, 3, 4, 5, 6);
        if (!includeMesh)
            return RoadPresentationFullReset.Create(token, snapshot);

        var ribbon = new RoadRibbonMeshData(
            [new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(10f, 1f), new Vector2(10f, -1f)],
            [0, 1, 2, 1, 3, 2],
            [Colors.White, Colors.White, Colors.White, Colors.White]);
        return RoadPresentationFullReset.Create(token, snapshot, [ribbon], []);
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
