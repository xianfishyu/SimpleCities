using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadToolInputRouterTests
{
    [Fact]
    public void HandleLeftClick_Place_StartsSession()
    {
        var router = CreateRouter();

        Assert.True(router.HandleLeftClick(Vector2.Zero, closeRadius: 10f, hitRadius: 10f));
        Assert.True(router.IsPlacing);
        Assert.True(router.TryTakePlacementSession(out RoadPlacementSessionV3 session));
        Assert.Equal(Vector2.Zero, session.StartPosition);
    }

    [Fact]
    public void HandleLeftClick_Place_AddsPointsAndCanClose()
    {
        var router = CreateRouter();

        router.HandleLeftClick(Vector2.Zero, closeRadius: 10f, hitRadius: 10f);
        router.HandleLeftClick(new Vector2(10f, 0f), closeRadius: 10f, hitRadius: 10f);
        router.HandleLeftClick(new Vector2(10f, 10f), closeRadius: 10f, hitRadius: 10f);
        router.HandleLeftClick(new Vector2(0.1f, 0.1f), closeRadius: 1f, hitRadius: 10f);

        Assert.True(router.IsPlacementClosed);
        Assert.True(router.TryTakePlacementSession(out RoadPlacementSessionV3 session));
        Assert.True(session.IsClosed);
    }

    [Fact]
    public void SwitchTool_ClearsPlacement()
    {
        var router = CreateRouter();
        router.HandleLeftClick(Vector2.Zero, closeRadius: 10f, hitRadius: 10f);
        Assert.True(router.IsPlacing);

        router.SwitchTool(RoadToolType.Remove);

        Assert.False(router.IsPlacing);
        Assert.Equal(RoadToolType.Remove, router.CurrentTool);
    }

    [Fact]
    public void HandleLeftClick_Upgrade_SelectsHit()
    {
        var router = CreateRouter((_, _) => CreateHit());
        router.SwitchTool(RoadToolType.Upgrade);

        Assert.True(router.HandleLeftClick(new Vector2(5f, 0f), closeRadius: 10f, hitRadius: 10f));
        Assert.True(router.TryTakeUpgradeSession(out RoadUpgradeSessionV3 session));
        Assert.Equal([20], session.SelectedEdgeIDs);
    }

    [Fact]
    public void HandleLeftClick_Remove_SelectsHit()
    {
        var router = CreateRouter((_, _) => CreateHit());
        router.SwitchTool(RoadToolType.Remove);

        Assert.True(router.HandleLeftClick(new Vector2(5f, 0f), closeRadius: 10f, hitRadius: 10f));
        Assert.True(router.TryTakeRemovalSession(out RoadRemovalSessionV3 session));
        Assert.Equal([20], session.SelectedEdgeIDs);
    }

    [Fact]
    public void HandleLeftClick_Selection_NoHit_DoesNotCreateSession()
    {
        var router = CreateRouter((_, _) => null);
        router.SwitchTool(RoadToolType.Upgrade);

        Assert.False(router.HandleLeftClick(new Vector2(5f, 0f), closeRadius: 10f, hitRadius: 10f));
        Assert.False(router.IsSelecting);
    }

    [Fact]
    public void Cancel_ClearsSessions()
    {
        var router = CreateRouter((_, _) => CreateHit());
        router.HandleLeftClick(Vector2.Zero, closeRadius: 10f, hitRadius: 10f);
        router.SwitchTool(RoadToolType.Upgrade);
        router.HandleLeftClick(new Vector2(5f, 0f), closeRadius: 10f, hitRadius: 10f);
        Assert.True(router.HasActiveSession);

        router.Cancel();

        Assert.False(router.HasActiveSession);
    }

    [Fact]
    public void TrySelectRoadType_Invalid_Fails()
    {
        var router = CreateRouter();

        Assert.False(router.TrySelectRoadType((RoadType)99));
    }

    private static RoadToolInputRouter CreateRouter(Func<Vector2, float, RoadSurfaceHit?>? resolver = null) =>
        new(new RoadToolState(), resolver ?? ((_, _) => null));

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
