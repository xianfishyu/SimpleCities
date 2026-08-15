using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadPresentationControllerTests
{
    [Fact]
    public void TryRequest_ThenTryPublish_MatchingToken_Succeeds()
    {
        RoadPresentationController controller = CreateController();
        RoadGraphV3Revision revision = CreateRevision();
        RoadRenderToken desired = CreateToken(2);
        RoadRenderToken result = CreateToken(2);

        Assert.True(controller.TryRequest(revision, new GraphStateToken(1, 2, 3), desired));
        Assert.True(controller.IsStalled);
        Assert.True(controller.TryPublish(result));

        Assert.False(controller.IsStalled);
        Assert.NotNull(controller.PresentedSnapshot);
        Assert.Null(controller.PendingSnapshot);
    }

    [Fact]
    public void TryPublish_StaleToken_Fails()
    {
        RoadPresentationController controller = CreateController();
        RoadGraphV3Revision revision = CreateRevision();
        RoadRenderToken desired = CreateToken(2);

        Assert.True(controller.TryRequest(revision, new GraphStateToken(1, 2, 3), desired));
        Assert.False(controller.TryPublish(CreateToken(1)));
        Assert.True(controller.IsStalled);
    }

    [Fact]
    public void TryRequest_MissingStyle_Fails()
    {
        var state = new RoadPresentationState(CreateToken(1));
        var styles = new RoadStyleProvider(new System.Collections.Generic.Dictionary<RoadType, RoadTypeStyle>
        {
            [RoadType.Street] = new RoadTypeStyle { RoadType = RoadType.Street, DisplayName = "街道", Color = Colors.White, Width = 1f },
        });
        var controller = new RoadPresentationController(state, styles);

        // Revision uses Highway, which is missing from styles.
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Highway, out revision, out _);

        Assert.False(controller.TryRequest(revision, new GraphStateToken(1, 2, 3), CreateToken(2)));
    }

    private static RoadPresentationController CreateController()
    {
        var state = new RoadPresentationState(CreateToken(1));
        var styles = new RoadStyleProvider(RoadTypeStyleCatalog.CreateDefault());
        return new RoadPresentationController(state, styles);
    }

    private static RoadRenderToken CreateToken(long requestID) =>
        new(SceneGeneration: 1, GraphFacadeID: 2, GraphFacadeGeneration: 3, ChangeSequence: 4, RoadStyleRevision: 5, RenderRequestID: requestID);

    private static RoadGraphV3Revision CreateRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        return revision;
    }
}
