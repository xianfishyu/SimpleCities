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

    [Fact]
    public void TryResolveEdge_WhenRibbonOwnerExists_ReturnsEdgeID()
    {
        RoadSurfaceSnapshot snapshot = CreateSnapshot();
        var state = new RoadPresentationState(CreateToken(1));
        Assert.True(state.TryPublish(CreateToken(1), snapshot));
        var provider = new RoadSurfaceHitProvider(state);

        Assert.True(provider.TryResolveEdge(CreateHit(), out int edgeID));
        Assert.Equal(20, edgeID);
    }

    [Fact]
    public void TryResolveEdge_WhenOwnerHasNoEdge_Fails()
    {
        RoadSurfaceSnapshot snapshot = new(
            new GraphStateToken(1, 3, 4),
            [
                new RoadSurfaceOwner(
                    RoadSurfaceOwnerKind.Cap,
                    NodeID: 10,
                    EdgeID: null,
                    Endpoint: EdgeEndpoint.A,
                    new RoadLocation(20, 0, 0.5f)),
            ]);
        var state = new RoadPresentationState(CreateToken(1));
        Assert.True(state.TryPublish(CreateToken(1), snapshot));
        var provider = new RoadSurfaceHitProvider(state);
        RoadSurfaceHit hit = CreateHit() with { OwnerKind = RoadSurfaceOwnerKind.Cap, EdgeID = null };

        Assert.False(provider.TryResolveEdge(hit, out _));
    }

    [Fact]
    public void TryResolveEdge_CapOwner_ReturnsEdgeID()
    {
        RoadSurfaceSnapshot snapshot = new(
            new GraphStateToken(1, 3, 4),
            [
                new RoadSurfaceOwner(
                    RoadSurfaceOwnerKind.Cap,
                    NodeID: 10,
                    EdgeID: 20,
                    Endpoint: EdgeEndpoint.A,
                    new RoadLocation(20, 0, 0f)),
            ]);
        var state = new RoadPresentationState(CreateToken(1));
        Assert.True(state.TryPublish(CreateToken(1), snapshot));
        var provider = new RoadSurfaceHitProvider(state);
        RoadSurfaceHit hit = CreateHit() with { OwnerKind = RoadSurfaceOwnerKind.Cap, Location = new RoadLocation(20, 0, 0f) };

        Assert.True(provider.TryResolveEdge(hit, out int edgeID));
        Assert.Equal(20, edgeID);
    }

    [Fact]
    public void TryResolveEdge_SemanticJoinOwner_ReturnsEdgeID()
    {
        RoadSurfaceSnapshot snapshot = new(
            new GraphStateToken(1, 3, 4),
            [
                new RoadSurfaceOwner(
                    RoadSurfaceOwnerKind.SemanticJoin,
                    NodeID: 10,
                    EdgeID: 20,
                    Endpoint: EdgeEndpoint.A,
                    new RoadLocation(20, 0, 0f)),
            ]);
        var state = new RoadPresentationState(CreateToken(1));
        Assert.True(state.TryPublish(CreateToken(1), snapshot));
        var provider = new RoadSurfaceHitProvider(state);
        RoadSurfaceHit hit = CreateHit() with { OwnerKind = RoadSurfaceOwnerKind.SemanticJoin, Location = new RoadLocation(20, 0, 0f) };

        Assert.True(provider.TryResolveEdge(hit, out int edgeID));
        Assert.Equal(20, edgeID);
    }

    [Fact]
    public void TryResolveEdge_WhenOwnerMissing_Fails()
    {
        RoadSurfaceSnapshot snapshot = CreateSnapshot();
        var state = new RoadPresentationState(CreateToken(1));
        Assert.True(state.TryPublish(CreateToken(1), snapshot));
        var provider = new RoadSurfaceHitProvider(state);
        RoadSurfaceHit hit = CreateHit() with { EdgeID = 999 };

        Assert.False(provider.TryResolveEdge(hit, out _));
    }

    [Fact]
    public void TryResolveEdge_WhenStalled_Fails()
    {
        var state = new RoadPresentationState(CreateToken(1));
        state.SetDesired(CreateToken(2));
        var provider = new RoadSurfaceHitProvider(state);

        Assert.False(provider.TryResolveEdge(CreateHit(), out _));
    }

    [Fact]
    public void TryResolve_JunctionOwner_ReturnsTrue()
    {
        RoadSurfaceSnapshot snapshot = CreateJunctionSnapshot();
        var state = new RoadPresentationState(CreateToken(1));
        Assert.True(state.TryPublish(CreateToken(1), snapshot));
        var provider = new RoadSurfaceHitProvider(state);
        RoadSurfaceHit hit = CreateJunctionHit();

        Assert.True(provider.TryResolve(hit, out RoadSurfaceHit result));
        Assert.Equal(RoadSurfaceOwnerKind.JunctionPatch, result.OwnerKind);
        Assert.Equal(30, result.NodeID);
        Assert.Null(result.EdgeID);
    }

    [Fact]
    public void TryResolveEdge_JunctionOwner_Fails()
    {
        RoadSurfaceSnapshot snapshot = CreateJunctionSnapshot();
        var state = new RoadPresentationState(CreateToken(1));
        Assert.True(state.TryPublish(CreateToken(1), snapshot));
        var provider = new RoadSurfaceHitProvider(state);

        Assert.False(provider.TryResolveEdge(CreateJunctionHit(), out _));
    }

    [Fact]
    public void TryResolveEdge_JunctionSectorOwner_ReturnsEdgeID()
    {
        RoadSurfaceSnapshot snapshot = new(
            new GraphStateToken(1, 3, 4),
            [
                new RoadSurfaceOwner(
                    RoadSurfaceOwnerKind.JunctionPatch,
                    NodeID: 30,
                    EdgeID: 40,
                    Endpoint: EdgeEndpoint.A,
                    new RoadLocation(40, 0, 0f)),
            ]);
        var state = new RoadPresentationState(CreateToken(1));
        Assert.True(state.TryPublish(CreateToken(1), snapshot));
        var provider = new RoadSurfaceHitProvider(state);
        RoadSurfaceHit hit = CreateJunctionHit() with
        {
            EdgeID = 40,
            Endpoint = EdgeEndpoint.A,
            Location = new RoadLocation(40, 0, 0f),
        };

        Assert.True(provider.TryResolveEdge(hit, out int edgeID));
        Assert.Equal(40, edgeID);
    }

    private static RoadSurfaceSnapshot CreateJunctionSnapshot() =>
        new(
            new GraphStateToken(1, 3, 4),
            [
                new RoadSurfaceOwner(
                    RoadSurfaceOwnerKind.JunctionPatch,
                    NodeID: 30,
                    EdgeID: null,
                    Endpoint: null,
                    new RoadLocation(0, 0, 0f)),
            ]);

    private static RoadSurfaceHit CreateJunctionHit() =>
        new(
            new GraphStateToken(1, 3, 4),
            RoadSurfaceOwnerKind.JunctionPatch,
            NodeID: 30,
            EdgeID: null,
            Endpoint: null,
            new RoadLocation(0, 0, 0f),
            1f);

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
