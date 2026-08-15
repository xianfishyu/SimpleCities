using Godot;

namespace SimpleCities.Tests;

public sealed class RoadUpgradeSessionTests
{
    [Fact]
    public void ConstructorFreezesDefinedTargetTypeAndRejectsInvalidType()
    {
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token();
        var provider = new TestRoadSurfaceSelectionProvider(token, []);

        var session = new RoadUpgradeSession(
            provider,
            token,
            RoadType.Highway,
            RoadUpgradeSelectionMode.Continuous,
            Vector2.Zero,
            2f);

        Assert.Equal(RoadType.Highway, session.TargetRoadType);
        Assert.Equal(token, session.RenderToken);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoadUpgradeSession(
            provider,
            token,
            (RoadType)int.MaxValue,
            RoadUpgradeSelectionMode.Continuous,
            Vector2.Zero,
            2f));
    }

    [Fact]
    public void ContinuousSelectionUsesVisibleSurfaceAndReturnsStableEdgeIDs()
    {
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token();
        RoadSurfaceTriangle[] triangles = [
            .. RoadRemovalSurfaceFixture.Ribbon(7, new Vector2(0f, -20f), new Vector2(0f, 20f), 10f),
            .. RoadRemovalSurfaceFixture.Ribbon(3, new Vector2(100f, -20f), new Vector2(100f, 20f), 10f),
        ];
        var provider = new TestRoadSurfaceSelectionProvider(token, triangles);
        var session = new RoadUpgradeSession(
            provider,
            token,
            RoadType.Arterial,
            RoadUpgradeSelectionMode.Continuous,
            new Vector2(-9f, 0f),
            1f);

        Assert.True(session.Update(new Vector2(109f, 0f)));
        Assert.True(session.Update(new Vector2(-9f, 0f)));

        Assert.Equal([3, 7], session.SelectedEdgeIDs);
        Assert.All(provider.QueryTokens, observed => Assert.Equal(token, observed));
    }

    [Fact]
    public void RectangleSelectionReplacesTargetsAndIncludesVisibleSurfaceContact()
    {
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token();
        RoadSurfaceTriangle[] triangles = [
            .. RoadRemovalSurfaceFixture.Ribbon(7, new Vector2(0f, 10f), new Vector2(20f, 10f), 5f),
            .. RoadRemovalSurfaceFixture.Ribbon(3, new Vector2(0f, 40f), new Vector2(20f, 40f), 5f),
        ];
        var provider = new TestRoadSurfaceSelectionProvider(token, triangles);
        var session = new RoadUpgradeSession(
            provider,
            token,
            RoadType.Dirt,
            RoadUpgradeSelectionMode.Rectangle,
            new Vector2(5f, 4f),
            1f);

        Assert.True(session.Update(new Vector2(15f, 45f)));
        Assert.Equal([3, 7], session.SelectedEdgeIDs);
        Assert.True(session.Update(new Vector2(15f, 6f)));

        Assert.Equal([7], session.SelectedEdgeIDs);
        Assert.NotNull(session.SelectionBounds);
    }

    [Fact]
    public void JunctionOwnerMapsToItsExecutableEdgeID()
    {
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token();
        var triangle = new RoadSurfaceTriangle(
            RoadSurfaceOwner.JunctionPatch(11, 5, EdgeEndpoint.A, sectorOrder: 2),
            new Vector2(0f, 0f),
            new Vector2(10f, 0f),
            new Vector2(0f, 10f),
            new Vector2(0f, 0f),
            new Vector2(10f, 10f),
            locationStart: null,
            locationEnd: null);
        var provider = new TestRoadSurfaceSelectionProvider(token, [triangle]);

        var session = new RoadUpgradeSession(
            provider,
            token,
            RoadType.Highway,
            RoadUpgradeSelectionMode.Continuous,
            new Vector2(1f, 1f),
            0.5f);

        Assert.Equal([11], session.SelectedEdgeIDs);
    }

    [Fact]
    public void InvalidatedTokenClearsAndPermanentlyRejectsSelection()
    {
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token();
        var provider = new TestRoadSurfaceSelectionProvider(
            token,
            RoadRemovalSurfaceFixture.Ribbon(7, Vector2.Zero, new Vector2(20f, 0f)));
        var session = new RoadUpgradeSession(
            provider,
            token,
            RoadType.Highway,
            RoadUpgradeSelectionMode.Rectangle,
            new Vector2(-1f, -1f),
            2f);
        Assert.True(session.Update(new Vector2(21f, 1f)));
        Assert.Equal([7], session.SelectedEdgeIDs);

        provider.Invalidate();

        Assert.False(session.IsCurrent);
        Assert.Empty(session.SelectedEdgeIDs);
        Assert.Null(session.SelectionBounds);
        Assert.False(session.Update(new Vector2(21f, 2f)));
    }

    [Fact]
    public void SelectedSemanticBoundaryCommitsOnceAndHistoryRestoresBothTypes()
    {
        var graph = new RoadGraph();
        int streetID = Assert.Single(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(0f, 0f),
            new Vector2(10f, 0f),
        ]).Changes.CreatedEdgeIDs);
        int arterialID = Assert.Single(graph.SubmitPolyline(RoadType.Arterial, [
            new Vector2(10f, 0f),
            new Vector2(20f, 0f),
        ]).Changes.CreatedEdgeIDs);
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token(graph);
        RoadSurfaceTriangle[] triangles = [
            .. RoadRemovalSurfaceFixture.Ribbon(streetID, new Vector2(0f, 0f), new Vector2(10f, 0f)),
            .. RoadRemovalSurfaceFixture.Ribbon(arterialID, new Vector2(10f, 0f), new Vector2(20f, 0f)),
        ];
        var session = new RoadUpgradeSession(
            new TestRoadSurfaceSelectionProvider(token, triangles),
            token,
            RoadType.Street,
            RoadUpgradeSelectionMode.Continuous,
            new Vector2(15f, 0f),
            1f);
        using var history = new RoadEditHistory(graph);
        int eventCount = 0;
        graph.GraphChanged += _ => eventCount++;

        Assert.Equal([arterialID], session.SelectedEdgeIDs);
        Assert.True(history.Execute(() =>
            graph.ChangeRoadType(session.SelectedEdgeIDs, session.TargetRoadType).Success));

        GraphEdge merged = Assert.Single(graph.GetAllEdges());
        Assert.Equal(Math.Min(streetID, arterialID), merged.ID);
        Assert.Equal(RoadType.Street, merged.RoadType);
        Assert.Equal(1, eventCount);
        Assert.Equal(1, history.UndoCount);

        Assert.True(history.Undo());
        Assert.Equal(2, graph.GetAllEdges().Count());
        Assert.Equal(RoadType.Street, graph.GetEdge(streetID)!.RoadType);
        Assert.Equal(RoadType.Arterial, graph.GetEdge(arterialID)!.RoadType);
        Assert.True(history.Redo());
        Assert.Single(graph.GetAllEdges());
        graph.AssertInvariants();
    }

    [Fact]
    public void NoChangesTargetProducesNoEventOrHistory()
    {
        var graph = new RoadGraph();
        int edgeID = Assert.Single(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Changes.CreatedEdgeIDs);
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token(graph);
        var session = new RoadUpgradeSession(
            new TestRoadSurfaceSelectionProvider(
                token,
                RoadRemovalSurfaceFixture.Ribbon(edgeID, Vector2.Zero, new Vector2(20f, 0f))),
            token,
            RoadType.Street,
            RoadUpgradeSelectionMode.Continuous,
            new Vector2(10f, 0f),
            1f);
        using var history = new RoadEditHistory(graph);
        RoadGraphRevision before = graph.CaptureRevision();
        GraphStateToken beforeToken = graph.CurrentStateToken;
        int eventCount = 0;
        graph.GraphChanged += _ => eventCount++;

        Assert.False(history.Execute(() =>
            graph.ChangeRoadType(session.SelectedEdgeIDs, session.TargetRoadType).Success));

        Assert.Same(before, graph.CaptureRevision());
        Assert.Equal(beforeToken, graph.CurrentStateToken);
        Assert.Equal(0, eventCount);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
    }
}
