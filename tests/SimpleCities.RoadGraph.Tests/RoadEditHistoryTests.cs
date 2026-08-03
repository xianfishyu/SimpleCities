using Godot;

namespace SimpleCities.Tests;

public sealed class RoadEditHistoryTests
{
    [Fact]
    public void MultiStepPlacementUndoRedoRestoresExactGraphStates()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(graph);
        string emptyState = Capture(graph);

        Assert.True(history.Execute(() => graph.SubmitPolyline([
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success));
        string singleSegmentState = Capture(graph);
        Assert.True(history.Execute(() => graph.SubmitPolyline([
            new Vector2(100f, 0f),
            new Vector2(120f, 0f),
            new Vector2(120f, 20f),
        ]).Success));
        string multiSegmentState = Capture(graph);

        Assert.Equal(2, history.UndoCount);
        Assert.True(history.Undo());
        Assert.Equal(singleSegmentState, Capture(graph));
        Assert.True(history.Undo());
        Assert.Equal(emptyState, Capture(graph));
        Assert.False(history.Undo());

        Assert.True(history.Redo());
        Assert.Equal(singleSegmentState, Capture(graph));
        Assert.True(history.Redo());
        Assert.Equal(multiSegmentState, Capture(graph));
        Assert.False(history.Redo());
        graph.AssertInvariants();
    }

    [Fact]
    public void CrossingPlacementRestoresDeletedEdgeIDsAndNativeGeometry()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                new Vector2(0f, -20f),
                new Vector2(0f, -10f),
                new Vector2(0f, 10f),
                new Vector2(0f, 20f)),
        ])).Success);
        GraphEdge originalEdge = Assert.Single(graph.GetAllEdges());
        string beforeCrossing = Capture(graph);
        int graphClearedEvents = 0;
        graph.GraphCleared += () => graphClearedEvents++;
        using var history = new RoadEditHistory(graph);

        Assert.True(history.Execute(() => graph.SubmitPolyline([
            new Vector2(-20f, 0f),
            new Vector2(20f, 0f),
        ]).Success));
        string afterCrossing = Capture(graph);
        Assert.Null(graph.GetEdge(originalEdge.ID));

        Assert.True(history.Undo());
        Assert.Equal(beforeCrossing, Capture(graph));
        GraphEdge restoredEdge = Assert.IsType<GraphEdge>(graph.GetEdge(originalEdge.ID));
        Assert.NotSame(originalEdge, restoredEdge);
        Assert.IsType<CubicBezierRoadGeometrySegment>(Assert.Single(restoredEdge.GeometrySegments));

        Assert.True(history.Redo());
        Assert.Equal(afterCrossing, Capture(graph));
        Assert.Equal(2, graphClearedEvents);
        graph.AssertInvariants();
    }

    [Fact]
    public void ContinuousAndRectangleRemovalShareReversibleTransactionBoundaries()
    {
        var graph = CreateThreeParallelRoads();
        string initialState = Capture(graph);
        using var history = new RoadEditHistory(graph);
        var continuous = new RoadRemovalSession(
            graph,
            RoadRemovalSelectionMode.Continuous,
            new Vector2(-10f, 0f),
            6f);
        continuous.Update(new Vector2(210f, 0f));

        Assert.True(history.Execute(() => graph.RemoveEdges(continuous.SelectedEdgeIDs)));
        Assert.Empty(graph.GetAllEdges());
        Assert.True(history.Undo());
        Assert.Equal(initialState, Capture(graph));

        var rectangle = new RoadRemovalSession(
            graph,
            RoadRemovalSelectionMode.Rectangle,
            new Vector2(-5f, -25f),
            6f);
        rectangle.Update(new Vector2(105f, 25f));
        Assert.Equal(2, rectangle.SelectedEdgeIDs.Length);
        Assert.True(history.Execute(() => graph.RemoveEdges(rectangle.SelectedEdgeIDs)));

        string rectangleRemovedState = Capture(graph);
        Assert.Single(graph.GetAllEdges());
        Assert.False(history.CanRedo);
        Assert.True(history.Undo());
        Assert.Equal(initialState, Capture(graph));
        Assert.True(history.Redo());
        Assert.Equal(rectangleRemovedState, Capture(graph));
        graph.AssertInvariants();
    }

    [Fact]
    public void SuccessfulDivergentEditClearsRedoStack()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(graph);
        Assert.True(history.Execute(() => graph.SubmitPolyline([
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success));
        Assert.True(history.Execute(() => graph.SubmitPolyline([
            new Vector2(0f, 40f),
            new Vector2(20f, 40f),
        ]).Success));
        Assert.True(history.Undo());
        Assert.True(history.CanRedo);

        Assert.True(history.Execute(() => graph.SubmitPolyline([
            new Vector2(0f, 80f),
            new Vector2(20f, 80f),
        ]).Success));

        Assert.False(history.CanRedo);
        Assert.False(history.Redo());
        Assert.Equal(2, history.UndoCount);
    }

    [Fact]
    public void FailedEditDoesNotEnterHistoryOrClearRedo()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(graph);
        Vector2[] path = [Vector2.Zero, new Vector2(20f, 0f)];
        Assert.True(history.Execute(() => graph.SubmitPolyline(path).Success));
        Assert.True(history.Undo());
        Assert.True(history.CanRedo);
        string beforeFailure = Capture(graph);

        Assert.False(history.Execute(() => graph.RemoveEdges([int.MaxValue])));

        Assert.Equal(beforeFailure, Capture(graph));
        Assert.True(history.CanRedo);
        Assert.Equal(0, history.UndoCount);
    }

    [Fact]
    public void ExternalMutationInvalidatesHistoryBeforeUndo()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(graph);
        Assert.True(history.Execute(() => graph.SubmitPolyline([
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success));
        Assert.True(graph.SubmitPolyline([
            new Vector2(0f, 40f),
            new Vector2(20f, 40f),
        ]).Success);
        string externalState = Capture(graph);

        Assert.False(history.Undo());

        Assert.Equal(externalState, Capture(graph));
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void ExternalMutationInvalidatesRedoBeforeFailedEdit()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(graph);
        Assert.True(history.Execute(() => graph.SubmitPolyline([
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success));
        Assert.True(history.Undo());
        Assert.True(history.CanRedo);
        Assert.True(graph.SubmitPolyline([
            new Vector2(0f, 40f),
            new Vector2(20f, 40f),
        ]).Success);

        Assert.False(history.Execute(() => graph.RemoveEdges([int.MaxValue])));

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void ExternalRestoreClearsBothHistoryStacks()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(graph);
        string emptyState = Capture(graph);
        Assert.True(history.Execute(() => graph.SubmitPolyline([
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success));
        Assert.True(history.Undo());
        Assert.True(history.CanRedo);

        graph.RestoreState(emptyState);

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void CapacityDropsTheOldestCommittedEdit()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(graph, capacity: 2);
        Assert.True(history.Execute(() => AddRoadAtY(graph, 0f)));
        string afterFirst = Capture(graph);
        Assert.True(history.Execute(() => AddRoadAtY(graph, 40f)));
        Assert.True(history.Execute(() => AddRoadAtY(graph, 80f)));

        Assert.Equal(2, history.UndoCount);
        Assert.True(history.Undo());
        Assert.True(history.Undo());
        Assert.False(history.Undo());
        Assert.Equal(afterFirst, Capture(graph));
    }

    private static RoadGraph CreateThreeParallelRoads()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline([new Vector2(0f, -20f), new Vector2(0f, 20f)]).Success);
        Assert.True(graph.SubmitPolyline([new Vector2(100f, -20f), new Vector2(100f, 20f)]).Success);
        Assert.True(graph.SubmitPolyline([new Vector2(200f, -20f), new Vector2(200f, 20f)]).Success);
        return graph;
    }

    private static bool AddRoadAtY(RoadGraph graph, float y) => graph.SubmitPolyline([
        new Vector2(0f, y),
        new Vector2(20f, y),
    ]).Success;

    private static string Capture(RoadGraph graph) => SaveJson.Serialize(graph.CaptureState());
}
