using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadSurfaceHitProviderTests
{
    [Fact]
    public void TryResolve_WhenPresentedAndOwnerExists_ReturnsTrue()
    {
        RoadSurfaceSnapshot snapshot = CreateSnapshot();
        var state = new RoadPresentationState(CreateToken(1));
        Assert.True(state.TryPublish(CreateToken(1), snapshot));
        var provider = new RoadSurfaceHitProvider(state);

        bool resolved = provider.TryResolve(CreateHit(), out RoadSurfaceHit result);

        Assert.True(resolved);
        Assert.Equal(CreateHit(), result);
    }

    [Fact]
    public void TryResolve_WhenStalled_ReturnsFalse()
    {
        var state = new RoadPresentationState(CreateToken(1));
        state.SetDesired(CreateToken(2));
        var provider = new RoadSurfaceHitProvider(state);

        Assert.False(provider.TryResolve(CreateHit(), out _));
    }

    [Fact]
    public void TryResolve_WhenOwnerMissing_ReturnsFalse()
    {
        RoadSurfaceSnapshot snapshot = CreateSnapshot();
        var state = new RoadPresentationState(CreateToken(1));
        Assert.True(state.TryPublish(CreateToken(1), snapshot));
        var provider = new RoadSurfaceHitProvider(state);

        RoadSurfaceHit hit = CreateHit() with { EdgeID = 999 };

        Assert.False(provider.TryResolve(hit, out _));
    }

    [Fact]
    public void TryResolve_WhenTokenMismatch_ReturnsFalse()
    {
        RoadSurfaceSnapshot snapshot = CreateSnapshot();
        var state = new RoadPresentationState(CreateToken(1));
        Assert.True(state.TryPublish(CreateToken(1), snapshot));
        var provider = new RoadSurfaceHitProvider(state);

        RoadSurfaceHit hit = CreateHit() with
        {
            Token = new GraphStateToken(99, 99, 99),
        };

        Assert.False(provider.TryResolve(hit, out _));
    }

    private static RoadRenderToken CreateToken(long requestID) =>
        new(SceneGeneration: 1, GraphFacadeID: 2, GraphFacadeGeneration: 3, ChangeSequence: 4, RoadStyleRevision: 5, RenderRequestID: requestID);

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

    private static RoadSurfaceHit CreateHit() =>
        new(
            new GraphStateToken(1, 3, 4),
            RoadSurfaceOwnerKind.Ribbon,
            NodeID: 10,
            EdgeID: 20,
            Endpoint: EdgeEndpoint.A,
            new RoadLocation(20, 0, 0.5f),
            1f);
}
