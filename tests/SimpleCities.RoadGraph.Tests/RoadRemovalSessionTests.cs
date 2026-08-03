using Godot;

namespace SimpleCities.Tests;

public sealed class RoadRemovalSessionTests
{
    [Fact]
    public void EmptyRectangleProducesNoSelectionOrMutation()
    {
        var graph = new RoadGraph();
        AddLine(graph, Vector2.Zero, new Vector2(20f, 0f));
        var session = new RoadRemovalSession(
            graph,
            RoadRemovalSelectionMode.Rectangle,
            new Vector2(100f, 100f),
            5f);

        session.Update(new Vector2(120f, 120f));

        Assert.Empty(session.SelectedEdgeIDs);
        Assert.False(graph.RemoveEdges(session.SelectedEdgeIDs));
        Assert.Single(graph.GetAllEdges());
        graph.AssertInvariants();
    }

    [Fact]
    public void ContinuousClickSelectsOneEdgeWithoutMutatingUntilCommit()
    {
        var graph = new RoadGraph();
        int edgeID = AddLine(graph, Vector2.Zero, new Vector2(20f, 0f));
        var session = new RoadRemovalSession(
            graph,
            RoadRemovalSelectionMode.Continuous,
            new Vector2(10f, 2f),
            5f);

        Assert.Equal([edgeID], session.SelectedEdgeIDs);
        Assert.NotNull(graph.GetEdge(edgeID));

        Assert.True(graph.RemoveEdges(session.SelectedEdgeIDs));
        Assert.Empty(graph.GetAllEdges());
        graph.AssertInvariants();
    }

    [Fact]
    public void ContinuousMotionSelectsEveryCrossedEdgeInStableOrder()
    {
        var graph = new RoadGraph();
        int first = AddLine(graph, new Vector2(0f, -20f), new Vector2(0f, 20f));
        int second = AddLine(graph, new Vector2(100f, -20f), new Vector2(100f, 20f));
        int third = AddLine(graph, new Vector2(200f, -20f), new Vector2(200f, 20f));
        var session = new RoadRemovalSession(
            graph,
            RoadRemovalSelectionMode.Continuous,
            new Vector2(-20f, 0f),
            6f);

        session.Update(new Vector2(220f, 0f));
        session.Update(new Vector2(-20f, 0f));

        Assert.Equal(new[] { first, second, third }.Order(), session.SelectedEdgeIDs);
        Assert.Equal(3, session.SelectedEdgeIDs.Distinct().Count());
    }

    [Fact]
    public void RectangleSelectionReplacesTheSetWhenTheBoundsShrink()
    {
        var graph = new RoadGraph();
        int first = AddLine(graph, new Vector2(0f, 0f), new Vector2(20f, 0f));
        int second = AddLine(graph, new Vector2(0f, 40f), new Vector2(20f, 40f));
        AddLine(graph, new Vector2(0f, 100f), new Vector2(20f, 100f));
        var session = new RoadRemovalSession(
            graph,
            RoadRemovalSelectionMode.Rectangle,
            new Vector2(-5f, -5f),
            5f);

        session.Update(new Vector2(25f, 45f));
        Assert.Equal(new[] { first, second }.Order(), session.SelectedEdgeIDs);

        session.Update(new Vector2(25f, 5f));
        Assert.Equal([first], session.SelectedEdgeIDs);
    }

    [Fact]
    public void DiscardingASelectionLeavesEveryGroupAndEdgeUntouched()
    {
        var graph = new RoadGraph();
        AddLine(graph, new Vector2(0f, -20f), new Vector2(0f, 20f));
        AddLine(graph, new Vector2(100f, -20f), new Vector2(100f, 20f));
        int[] edgeIDs = graph.GetAllEdges().Select(edge => edge.ID).Order().ToArray();
        int[] groupIDs = graph.GetAllGroups().Select(group => group.ID).Order().ToArray();
        var session = new RoadRemovalSession(
            graph,
            RoadRemovalSelectionMode.Continuous,
            new Vector2(-10f, 0f),
            5f);

        session.Update(new Vector2(110f, 0f));

        Assert.Equal(2, session.SelectedEdgeIDs.Length);
        Assert.Equal(edgeIDs, graph.GetAllEdges().Select(edge => edge.ID).Order());
        Assert.Equal(groupIDs, graph.GetAllGroups().Select(group => group.ID).Order());
        graph.AssertInvariants();
    }

    [Fact]
    public void BatchRemovalSkipsMissingAndDuplicateTargetsAcrossGroups()
    {
        var graph = new RoadGraph();
        int first = AddLine(graph, new Vector2(0f, 0f), new Vector2(20f, 0f));
        int second = AddLine(graph, new Vector2(0f, 40f), new Vector2(20f, 40f));
        int survivor = AddLine(graph, new Vector2(0f, 80f), new Vector2(20f, 80f));
        Assert.True(graph.RemoveEdge(first));
        var observed = new List<int>();
        graph.EdgeRemoved += edge =>
        {
            graph.AssertInvariants();
            Assert.Null(graph.GetEdge(first));
            Assert.Null(graph.GetEdge(second));
            Assert.NotNull(graph.GetEdge(survivor));
            observed.Add(edge.ID);
        };

        Assert.True(graph.RemoveEdges([second, first, second, int.MaxValue]));

        Assert.Equal([second], observed);
        Assert.Equal([survivor], graph.GetAllEdges().Select(edge => edge.ID));
        graph.AssertInvariants();
    }

    [Fact]
    public void RectangleQueryUsesNativeCurveGeometryInsteadOfOnlyItsBounds()
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                new Vector2(-10f, 0f),
                new Vector2(-10f, 10f),
                new Vector2(10f, 10f),
                new Vector2(10f, 0f)),
        ]));
        int edgeID = Assert.Single(result.Changes.CreatedEdgeIDs);

        Assert.Empty(graph.FindEdgeIDsIntersecting(new Rect2(-1f, 0f, 2f, 1f)));
        Assert.Equal([edgeID], graph.FindEdgeIDsIntersecting(new Rect2(-1f, 7f, 2f, 1f)));
    }

    private static int AddLine(RoadGraph graph, Vector2 start, Vector2 end)
    {
        RoadPathSubmissionResult result = graph.SubmitPolyline([start, end]);
        Assert.True(result.Success);
        return Assert.Single(result.Changes.CreatedEdgeIDs);
    }
}
