using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3RendererLoadParticipantTests
{
    [Fact]
    public void Prepare_WithValidPlan_CanCommit()
    {
        RoadPresentationFullReset plan = CreatePlan(includeMesh: true);

        V3RendererLoadParticipant participant = V3RendererLoadParticipant.Prepare(plan);

        Assert.True(participant.IsPrepared);
        Assert.True(participant.CanCommit);
    }

    [Fact]
    public void Unprepared_NotPrepared()
    {
        V3RendererLoadParticipant participant = V3RendererLoadParticipant.Unprepared;

        Assert.False(participant.IsPrepared);
        Assert.False(participant.CanCommit);
    }

    [Fact]
    public void Prepare_WithoutMeshData_NotCanCommit()
    {
        RoadPresentationFullReset plan = CreatePlan(includeMesh: false);

        V3RendererLoadParticipant participant = V3RendererLoadParticipant.Prepare(plan);

        Assert.True(participant.IsPrepared);
        Assert.False(participant.CanCommit);
    }

    [Fact]
    public void TryApplyTo_WhenPrepared_AppliesPlan()
    {
        V3RendererLoadParticipant participant = V3RendererLoadParticipant.Prepare(CreatePlan(includeMesh: true));
        var state = new RoadPresentationState(new RoadRenderToken(1, 2, 3, 4, 5, 6));

        Assert.True(participant.TryApplyTo(state));
        Assert.False(state.IsStalled);
        Assert.NotNull(state.PresentedSnapshot);
    }

    [Fact]
    public void TryApplyTo_Unprepared_Fails()
    {
        V3RendererLoadParticipant participant = V3RendererLoadParticipant.Unprepared;

        Assert.False(participant.TryApplyTo(new RoadPresentationState(new RoadRenderToken(1, 2, 3, 4, 5, 6))));
    }

    private static RoadPresentationFullReset CreatePlan(bool includeMesh)
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
}
