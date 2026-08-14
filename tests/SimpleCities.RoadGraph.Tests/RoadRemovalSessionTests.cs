using Godot;

namespace SimpleCities.Tests;

public sealed class RoadRemovalSessionTests
{
    [Fact]
    public void EmptyRectangleProducesNoSelectionOrMutation()
    {
        var graph = new RoadGraph();
        int edgeID = AddLine(graph, Vector2.Zero, new Vector2(20f, 0f));
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token(graph);
        var provider = new TestRoadSurfaceSelectionProvider(
            token,
            RoadRemovalSurfaceFixture.Ribbon(edgeID, Vector2.Zero, new Vector2(20f, 0f)));
        var session = new RoadRemovalSession(
            provider,
            token,
            RoadRemovalSelectionMode.Rectangle,
            new Vector2(100f, 100f),
            5f);

        Assert.True(session.Update(new Vector2(120f, 120f)));

        Assert.Empty(session.SelectedEdgeIDs);
        Assert.False(graph.RemoveEdges(session.SelectedEdgeIDs));
        Assert.Single(graph.GetAllEdges());
        graph.AssertInvariants();
    }

    [Fact]
    public void ContinuousClickUsesVisibleSurfaceInsteadOfCenterlineRadius()
    {
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token();
        var provider = new TestRoadSurfaceSelectionProvider(
            token,
            RoadRemovalSurfaceFixture.Ribbon(
                edgeID: 7,
                Vector2.Zero,
                new Vector2(20f, 0f),
                halfWidth: 10f));
        var session = new RoadRemovalSession(
            provider,
            token,
            RoadRemovalSelectionMode.Continuous,
            new Vector2(10f, 9f),
            interactionRadius: 1f);

        Assert.Equal([7], session.SelectedEdgeIDs);
        Assert.All(provider.QueryTokens, observed => Assert.Equal(token, observed));
    }

    [Fact]
    public void ContinuousPointSelectsOnlyTheClosestOverlappingOwner()
    {
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token();
        RoadSurfaceTriangle[] triangles = [
            .. RoadRemovalSurfaceFixture.Ribbon(7, new Vector2(0f, 0f), new Vector2(20f, 0f), 1f),
            .. RoadRemovalSurfaceFixture.Ribbon(9, new Vector2(0f, 3f), new Vector2(20f, 3f), 1f),
        ];
        var provider = new TestRoadSurfaceSelectionProvider(token, triangles);
        var session = new RoadRemovalSession(
            provider,
            token,
            RoadRemovalSelectionMode.Continuous,
            new Vector2(10f, 1.4f),
            interactionRadius: 2f);

        Assert.Equal([7], session.SelectedEdgeIDs);
    }

    [Fact]
    public void ContinuousMotionSelectsEveryCrossedSurfaceInStableOrder()
    {
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token();
        RoadSurfaceTriangle[] triangles = [
            .. RoadRemovalSurfaceFixture.Ribbon(7, new Vector2(0f, -20f), new Vector2(0f, 20f)),
            .. RoadRemovalSurfaceFixture.Ribbon(3, new Vector2(100f, -20f), new Vector2(100f, 20f)),
            .. RoadRemovalSurfaceFixture.Ribbon(5, new Vector2(200f, -20f), new Vector2(200f, 20f)),
        ];
        var provider = new TestRoadSurfaceSelectionProvider(token, triangles);
        var session = new RoadRemovalSession(
            provider,
            token,
            RoadRemovalSelectionMode.Continuous,
            new Vector2(-20f, 0f),
            6f);

        Assert.True(session.Update(new Vector2(220f, 0f)));
        Assert.True(session.Update(new Vector2(-20f, 0f)));

        Assert.Equal([3, 5, 7], session.SelectedEdgeIDs);
        Assert.Equal(3, session.SelectedEdgeIDs.Distinct().Count());
    }

    [Fact]
    public void RectangleSelectionReplacesTheSetWhenTheBoundsShrink()
    {
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token();
        RoadSurfaceTriangle[] triangles = [
            .. RoadRemovalSurfaceFixture.Ribbon(7, new Vector2(0f, 0f), new Vector2(20f, 0f)),
            .. RoadRemovalSurfaceFixture.Ribbon(3, new Vector2(0f, 40f), new Vector2(20f, 40f)),
            .. RoadRemovalSurfaceFixture.Ribbon(5, new Vector2(0f, 100f), new Vector2(20f, 100f)),
        ];
        var provider = new TestRoadSurfaceSelectionProvider(token, triangles);
        var session = new RoadRemovalSession(
            provider,
            token,
            RoadRemovalSelectionMode.Rectangle,
            new Vector2(-5f, -5f),
            5f);

        Assert.True(session.Update(new Vector2(25f, 45f)));
        Assert.Equal([3, 7], session.SelectedEdgeIDs);

        Assert.True(session.Update(new Vector2(25f, 5f)));
        Assert.Equal([7], session.SelectedEdgeIDs);
    }

    [Fact]
    public void RectangleSelectsAVisibleSurfaceWhoseCenterlineIsOutsideTheBounds()
    {
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token();
        var provider = new TestRoadSurfaceSelectionProvider(
            token,
            RoadRemovalSurfaceFixture.Ribbon(
                edgeID: 7,
                new Vector2(0f, 10f),
                new Vector2(20f, 10f),
                halfWidth: 5f));
        var session = new RoadRemovalSession(
            provider,
            token,
            RoadRemovalSelectionMode.Rectangle,
            new Vector2(5f, 4f),
            1f);

        Assert.True(session.Update(new Vector2(15f, 6f)));

        Assert.Equal([7], session.SelectedEdgeIDs);
    }

    [Fact]
    public void InvalidatedRenderTokenClearsAndPermanentlyRejectsTheSelection()
    {
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token();
        var provider = new TestRoadSurfaceSelectionProvider(
            token,
            RoadRemovalSurfaceFixture.Ribbon(7, Vector2.Zero, new Vector2(20f, 0f)));
        var session = new RoadRemovalSession(
            provider,
            token,
            RoadRemovalSelectionMode.Rectangle,
            new Vector2(-1f, -1f),
            2f);
        Assert.True(session.Update(new Vector2(21f, 1f)));
        Assert.Equal([7], session.SelectedEdgeIDs);

        provider.Invalidate();

        Assert.False(session.IsCurrent);
        Assert.Empty(session.SelectedEdgeIDs);
        Assert.Null(session.SelectionBounds);
        Assert.False(session.Update(new Vector2(21f, 2f)));
        Assert.Empty(session.SelectedEdgeIDs);
    }

    [Fact]
    public void DiscardingASelectionLeavesEveryEdgeUntouched()
    {
        var graph = new RoadGraph();
        int first = AddLine(graph, new Vector2(0f, -20f), new Vector2(0f, 20f));
        int second = AddLine(graph, new Vector2(100f, -20f), new Vector2(100f, 20f));
        int[] edgeIDs = graph.GetAllEdges().Select(edge => edge.ID).Order().ToArray();
        RoadRenderToken token = RoadRemovalSurfaceFixture.Token(graph);
        RoadSurfaceTriangle[] triangles = [
            .. RoadRemovalSurfaceFixture.Ribbon(first, new Vector2(0f, -20f), new Vector2(0f, 20f)),
            .. RoadRemovalSurfaceFixture.Ribbon(second, new Vector2(100f, -20f), new Vector2(100f, 20f)),
        ];
        var provider = new TestRoadSurfaceSelectionProvider(token, triangles);
        var session = new RoadRemovalSession(
            provider,
            token,
            RoadRemovalSelectionMode.Continuous,
            new Vector2(-10f, 0f),
            5f);

        Assert.True(session.Update(new Vector2(110f, 0f)));

        Assert.Equal(2, session.SelectedEdgeIDs.Length);
        Assert.Equal(edgeIDs, graph.GetAllEdges().Select(edge => edge.ID).Order());
        graph.AssertInvariants();
    }

    [Fact]
    public void BatchRemovalSkipsMissingAndDuplicateTargets()
    {
        var graph = new RoadGraph();
        int first = AddLine(graph, new Vector2(0f, 0f), new Vector2(20f, 0f));
        int second = AddLine(graph, new Vector2(0f, 40f), new Vector2(20f, 40f));
        int survivor = AddLine(graph, new Vector2(0f, 80f), new Vector2(20f, 80f));
        Assert.True(graph.RemoveEdge(first));
        var observed = new List<RoadGraphChangedEvent>();
        graph.GraphChanged += change =>
        {
            graph.AssertInvariants();
            Assert.Null(graph.GetEdge(first));
            Assert.Null(graph.GetEdge(second));
            Assert.NotNull(graph.GetEdge(survivor));
            observed.Add(change);
        };

        Assert.True(graph.RemoveEdges([second, first, second, int.MaxValue]));

        Assert.Equal([second], Assert.Single(observed).Changes.RemovedEdgeIDs);
        Assert.Equal([survivor], graph.GetAllEdges().Select(edge => edge.ID));
        graph.AssertInvariants();
    }

    [Fact]
    public void RectangleQueryUsesNativeCurveGeometryInsteadOfOnlyItsBounds()
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                new Vector2(-10f, 0f),
                new Vector2(-10f, 10f),
                new Vector2(10f, 10f),
                new Vector2(10f, 0f)),
        ]), RoadType.Street));
        int edgeID = Assert.Single(result.Changes.CreatedEdgeIDs);

        Assert.Empty(graph.FindEdgeIDsIntersecting(new Rect2(-1f, 0f, 2f, 1f)));
        Assert.Equal([edgeID], graph.FindEdgeIDsIntersecting(new Rect2(-1f, 7f, 2f, 1f)));
    }

    private static int AddLine(RoadGraph graph, Vector2 start, Vector2 end)
    {
        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [start, end]);
        Assert.True(result.Success);
        return Assert.Single(result.Changes.CreatedEdgeIDs);
    }
}

internal sealed class TestRoadSurfaceSelectionProvider : IRoadSurfaceSelectionProvider
{
    private readonly RoadRenderToken _renderToken;
    private readonly RoadSurfaceSnapshot _surface;
    private bool _available = true;

    internal TestRoadSurfaceSelectionProvider(
        RoadRenderToken renderToken,
        IReadOnlyCollection<RoadSurfaceTriangle> triangles)
    {
        _renderToken = renderToken;
        _surface = new RoadSurfaceSnapshot(renderToken, triangles);
    }

    internal List<RoadRenderToken> QueryTokens { get; } = [];

    internal void Invalidate() => _available = false;

    bool IRoadSurfaceSelectionProvider.TryCaptureCurrentToken(
        out RoadRenderToken renderToken)
    {
        renderToken = _renderToken;
        return _available;
    }

    bool IRoadSurfaceSelectionProvider.IsCurrent(RoadRenderToken expectedToken) =>
        _available && expectedToken == _renderToken;

    bool IRoadSurfaceSelectionProvider.TryFindClosest(
        RoadRenderToken expectedToken,
        Vector2 position,
        float maxSurfaceDistance,
        out RoadSurfaceHit? hit)
    {
        QueryTokens.Add(expectedToken);
        if (!_available || expectedToken != _renderToken)
        {
            hit = null;
            return false;
        }

        hit = _surface.FindClosest(position, maxSurfaceDistance);
        return true;
    }

    bool IRoadSurfaceSelectionProvider.TryFindEdgeIDsIntersecting(
        RoadRenderToken expectedToken,
        Rect2 bounds,
        out int[] edgeIDs)
    {
        QueryTokens.Add(expectedToken);
        if (!_available || expectedToken != _renderToken)
        {
            edgeIDs = [];
            return false;
        }

        edgeIDs = _surface.FindEdgeIDsIntersecting(bounds);
        return true;
    }
}

internal static class RoadRemovalSurfaceFixture
{
    internal static RoadRenderToken Token(
        RoadGraph? graph = null,
        long renderRequestID = 1) => new(
        SceneGeneration: 1,
        GraphFacadeID: graph?.FacadeID ?? 1,
        GraphFacadeGeneration: 1,
        ChangeSequence: graph?.CurrentStateToken.ChangeSequence ?? 0,
        RoadStyleRevision: 1,
        RenderRequestID: renderRequestID);

    internal static RoadSurfaceTriangle[] Ribbon(
        int edgeID,
        Vector2 start,
        Vector2 end,
        float halfWidth = 2f)
    {
        Vector2 direction = start.DirectionTo(end);
        Vector2 offset = new Vector2(-direction.Y, direction.X) * halfWidth;
        RoadSurfaceOwner owner = RoadSurfaceOwner.EdgeRibbon(edgeID);
        return [
            new RoadSurfaceTriangle(
                owner,
                start - offset,
                end - offset,
                end + offset,
                start,
                end,
                locationStart: null,
                locationEnd: null),
            new RoadSurfaceTriangle(
                owner,
                start - offset,
                end + offset,
                start + offset,
                start,
                end,
                locationStart: null,
                locationEnd: null),
        ];
    }
}
