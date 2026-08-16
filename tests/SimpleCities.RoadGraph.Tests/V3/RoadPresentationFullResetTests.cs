using Godot;
using SimpleCities.Road.V3;
using System.Linq;

namespace SimpleCities.Tests.V3;

public sealed class RoadPresentationFullResetTests
{
    [Fact]
    public void Create_WithSnapshotAndToken_IsValid()
    {
        RoadPresentationFullReset plan = RoadPresentationFullReset.Create(CreateToken(), CreateSnapshot());

        Assert.True(plan.IsValid);
        Assert.Equal(CreateToken(), plan.DesiredToken);
        AssertSnapshotEqual(CreateSnapshot(), plan.Snapshot);
    }

    [Fact]
    public void Create_WithMeshData_SetsProperties()
    {
        var ribbon = new RoadRibbonMeshData(
            [new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(10f, 1f), new Vector2(10f, -1f)],
            [0, 1, 2, 1, 3, 2],
            [Colors.White, Colors.White, Colors.White, Colors.White]);
        var patch = new RoadJunctionPatchData(
            1,
            [new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0f)],
            Colors.White);

        RoadPresentationFullReset plan = RoadPresentationFullReset.Create(
            CreateToken(),
            CreateSnapshot(),
            [ribbon],
            [patch]);

        Assert.Single(plan.RibbonMeshes);
        Assert.Single(plan.JunctionPatches);
        Assert.True(plan.HasMeshData);
    }

    [Fact]
    public void Prepare_FromPublishedState_CapturesPlan()
    {
        var state = new RoadPresentationState(CreateToken());
        Assert.True(state.TryPublish(CreateToken(), CreateSnapshot()));

        RoadPresentationFullReset plan = RoadPresentationFullReset.Prepare(state);

        Assert.Equal(CreateToken(), plan.DesiredToken);
        AssertSnapshotEqual(CreateSnapshot(), plan.Snapshot);
    }

    [Fact]
    public void Prepare_WithoutPresentedSnapshot_Throws()
    {
        var state = new RoadPresentationState(CreateToken());

        Assert.Throws<System.InvalidOperationException>(() => RoadPresentationFullReset.Prepare(state));
    }

    [Fact]
    public void IsValid_FalseForInvalidSnapshot()
    {
        var invalidSnapshot = CreateSnapshot() with { Token = new GraphStateToken(-1, 0, 0) };
        var plan = new RoadPresentationFullReset(CreateToken(), invalidSnapshot);

        Assert.False(plan.IsValid);
    }

    [Fact]
    public void TryApplyTo_AppliesDesiredAndPublishes()
    {
        var state = new RoadPresentationState(CreateToken());
        RoadPresentationFullReset plan = RoadPresentationFullReset.Create(CreateToken(), CreateSnapshot());

        Assert.True(plan.TryApplyTo(state));
        Assert.False(state.IsStalled);
        Assert.NotNull(state.PresentedSnapshot);
        AssertSnapshotEqual(CreateSnapshot(), state.PresentedSnapshot!);
    }

    [Fact]
    public void TryApplyTo_InvalidPlan_Fails()
    {
        var state = new RoadPresentationState(CreateToken());
        var invalidSnapshot = CreateSnapshot() with { Token = new GraphStateToken(-1, 0, 0) };
        var plan = new RoadPresentationFullReset(CreateToken(), invalidSnapshot);

        Assert.False(plan.TryApplyTo(state));
        Assert.Null(state.PresentedSnapshot);
    }

    [Fact]
    public void TryApplyTo_NullTarget_Throws()
    {
        RoadPresentationFullReset plan = RoadPresentationFullReset.Create(CreateToken(), CreateSnapshot());

        Assert.Throws<System.ArgumentNullException>(() => plan.TryApplyTo(null!));
    }

    private static void AssertSnapshotEqual(RoadSurfaceSnapshot expected, RoadSurfaceSnapshot actual)
    {
        Assert.Equal(expected.Token, actual.Token);
        Assert.Equal(expected.Owners.ToArray(), actual.Owners.ToArray());
    }

    private static RoadRenderToken CreateToken() =>
        new(SceneGeneration: 1, GraphFacadeID: 2, GraphFacadeGeneration: 3, ChangeSequence: 4, RoadStyleRevision: 5, RenderRequestID: 6);

    private static RoadSurfaceSnapshot CreateSnapshot() =>
        new(
            new GraphStateToken(1, 3, 4),
            [
                new RoadSurfaceOwner(
                    RoadSurfaceOwnerKind.Ribbon,
                    NodeID: 10,
                    EdgeID: 20,
                    Endpoint: EdgeEndpoint.A,
                    new RoadLocation(20, 0, 0.5f)),
            ]);
}
