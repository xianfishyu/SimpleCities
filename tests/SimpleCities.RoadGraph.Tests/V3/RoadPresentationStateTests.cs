using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadPresentationStateTests
{
    [Fact]
    public void InitialState_IsNotStalled()
    {
        RoadRenderToken token = CreateToken(1);
        var state = new RoadPresentationState(token);

        Assert.False(state.IsStalled);
        Assert.Equal(token, state.PresentedToken);
    }

    [Fact]
    public void SetDesired_ThenIsStalled_ReturnsTrue()
    {
        var state = new RoadPresentationState(CreateToken(1));

        state.SetDesired(CreateToken(2));

        Assert.True(state.IsStalled);
    }

    [Fact]
    public void TryPublish_MatchingToken_SucceedsAndClearsStall()
    {
        var state = new RoadPresentationState(CreateToken(1));
        state.SetDesired(CreateToken(2));
        RoadSurfaceSnapshot snapshot = CreateSnapshot(CreateToken(2));

        bool published = state.TryPublish(CreateToken(2), snapshot);

        Assert.True(published);
        Assert.False(state.IsStalled);
        Assert.Same(snapshot, state.PresentedSnapshot);
    }

    [Fact]
    public void TryPublish_StaleToken_Fails()
    {
        var state = new RoadPresentationState(CreateToken(1));
        state.SetDesired(CreateToken(2));

        bool published = state.TryPublish(CreateToken(1), CreateSnapshot(CreateToken(1)));

        Assert.False(published);
        Assert.True(state.IsStalled);
        Assert.Null(state.PresentedSnapshot);
    }

    [Fact]
    public void CaptureRestore_RestoresPreviousState()
    {
        var state = new RoadPresentationState(CreateToken(1));
        state.SetDesired(CreateToken(2));
        RoadSurfaceSnapshot snapshot = CreateSnapshot(CreateToken(2));
        Assert.True(state.TryPublish(CreateToken(2), snapshot));
        RoadPresentationStateSnapshot captured = state.Capture();

        state.SetDesired(CreateToken(3));
        Assert.True(state.TryPublish(CreateToken(3), CreateSnapshot(CreateToken(3))));
        state.Restore(captured);

        Assert.Equal(CreateToken(2), state.DesiredToken);
        Assert.Equal(CreateToken(2), state.PresentedToken);
        Assert.Same(snapshot, state.PresentedSnapshot);
        Assert.False(state.IsStalled);
    }

    private static RoadRenderToken CreateToken(long requestID) =>
        new(SceneGeneration: 1, GraphFacadeID: 2, GraphFacadeGeneration: 3, ChangeSequence: 4, RoadStyleRevision: 5, RenderRequestID: requestID);

    private static RoadSurfaceSnapshot CreateSnapshot(RoadRenderToken token) =>
        new(
            new GraphStateToken(token.SceneGeneration, token.GraphFacadeGeneration, token.ChangeSequence),
            []);
}
