using Godot;

namespace SimpleCities.Tests;

public sealed class RoadEditHistoryTests
{
    [Fact]
    public void MultiStepPlacementUndoRedoRestoresContentWithoutRegressingWatermark()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(graph);
        string emptyState = Capture(graph);

        Assert.True(history.Execute(() => graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success));
        string singleSegmentState = Capture(graph);
        Assert.True(history.Execute(() => graph.SubmitPolyline(RoadType.Street, [
            new Vector2(100f, 0f),
            new Vector2(120f, 0f),
            new Vector2(120f, 20f),
        ]).Success));
        string multiSegmentState = Capture(graph);

        Assert.Equal(2, history.UndoCount);
        Assert.True(history.Undo());
        AssertSameGraphContent(singleSegmentState, graph);
        Assert.True(history.Undo());
        AssertSameGraphContent(emptyState, graph);
        Assert.False(history.Undo());

        Assert.True(history.Redo());
        AssertSameGraphContent(singleSegmentState, graph);
        Assert.True(history.Redo());
        AssertSameGraphContent(multiSegmentState, graph);
        Assert.False(history.Redo());
        Assert.Equal(8, graph.NextIDWatermark);
        graph.AssertInvariants();
    }

    [Fact]
    public void CrossingPlacementRestoresRetainedEdgeIDAndNativeGeometry()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                new Vector2(0f, -20f),
                new Vector2(0f, -10f),
                new Vector2(0f, 10f),
                new Vector2(0f, 20f)),
        ]), RoadType.Street)).Success);
        GraphEdge originalEdge = Assert.Single(graph.GetAllEdges());
        string beforeCrossing = Capture(graph);
        int fullResetEvents = 0;
        graph.GraphChanged += change =>
            fullResetEvents += change.Changes.IsFullReset ? 1 : 0;
        using var history = new RoadEditHistory(graph);

        Assert.True(history.Execute(() => graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-20f, 0f),
            new Vector2(20f, 0f),
        ]).Success));
        string afterCrossing = Capture(graph);
        GraphEdge splitEdge = Assert.IsType<GraphEdge>(graph.GetEdge(originalEdge.ID));
        Assert.NotSame(originalEdge, splitEdge);
        Assert.IsType<CubicBezierRoadGeometrySegment>(Assert.Single(splitEdge.GeometrySegments));

        Assert.True(history.Undo());
        AssertSameGraphContent(beforeCrossing, graph);
        GraphEdge restoredEdge = Assert.IsType<GraphEdge>(graph.GetEdge(originalEdge.ID));
        Assert.Same(originalEdge, restoredEdge);
        Assert.IsType<CubicBezierRoadGeometrySegment>(Assert.Single(restoredEdge.GeometrySegments));

        Assert.True(history.Redo());
        AssertSameGraphContent(afterCrossing, graph);
        Assert.NotNull(graph.GetEdge(originalEdge.ID));
        Assert.Equal(0, fullResetEvents);
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
        AssertSameGraphContent(initialState, graph);

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
        AssertSameGraphContent(initialState, graph);
        Assert.True(history.Redo());
        AssertSameGraphContent(rectangleRemovedState, graph);
        graph.AssertInvariants();
    }

    [Fact]
    public void SuccessfulDivergentEditClearsRedoStack()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(graph);
        Assert.True(history.Execute(() => graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success));
        Assert.True(history.Execute(() => graph.SubmitPolyline(RoadType.Street, [
            new Vector2(0f, 40f),
            new Vector2(20f, 40f),
        ]).Success));
        Assert.True(history.Undo());
        Assert.True(history.CanRedo);

        Assert.True(history.Execute(() => graph.SubmitPolyline(RoadType.Street, [
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
        Assert.True(history.Execute(() => graph.SubmitPolyline(RoadType.Street, path).Success));
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
        Assert.True(history.Execute(() => graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success));
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
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
        Assert.True(history.Execute(() => graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success));
        Assert.True(history.Undo());
        Assert.True(history.CanRedo);
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
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
        Assert.True(history.Execute(() => graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success));
        Assert.True(history.Undo());
        Assert.True(history.CanRedo);

        RoadGraphTestCodec.LoadJson(graph, emptyState);

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
        AssertSameGraphContent(afterFirst, graph);
    }

    [Fact]
    public void ByteCapacityDropsTheOldestCommittedEdit()
    {
        var probeGraph = new RoadGraph();
        var deltaSizes = new List<long>();
        probeGraph.GraphChanged += change => deltaSizes.Add(change.Delta.EstimatedByteSize);
        Assert.True(AddRoadAtY(probeGraph, 0f));
        Assert.True(AddRoadAtY(probeGraph, 40f));
        Assert.Equal(2, deltaSizes.Count);
        long byteCapacity = deltaSizes.Max();

        var graph = new RoadGraph();
        using var history = new RoadEditHistory(
            graph,
            capacity: RoadEditHistory.DefaultCapacity,
            byteCapacity: byteCapacity);
        Assert.True(history.Execute(() => AddRoadAtY(graph, 0f)));
        string afterFirst = Capture(graph);
        Assert.True(history.Execute(() => AddRoadAtY(graph, 40f)));

        Assert.Equal(1, history.UndoCount);
        Assert.InRange(history.RetainedByteSize, 1, byteCapacity);
        Assert.True(history.Undo());
        Assert.False(history.Undo());
        AssertSameGraphContent(afterFirst, graph);
    }

    [Fact]
    public void SingleEditOverByteCapacityIsRejectedBeforeGraphCommit()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(
            graph,
            byteCapacity: 1);
        RoadGraphRevision root = graph.CaptureRevision();
        GraphStateToken token = graph.CurrentStateToken;
        int watermark = graph.NextIDWatermark;
        int eventCount = 0;
        graph.GraphChanged += _ => eventCount++;

        bool result = history.Execute(() => graph.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(20f, 0f)]).Success);

        Assert.False(result);
        Assert.Same(root, graph.CaptureRevision());
        Assert.Equal(token, graph.CurrentStateToken);
        Assert.Equal(watermark, graph.NextIDWatermark);
        Assert.Empty(graph.GetAllNodes());
        Assert.Empty(graph.GetAllEdges());
        Assert.Equal(0, eventCount);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(0, history.RetainedByteSize);
    }

    [Fact]
    public void HistoryRetainsOnlyDeltaBudgetAndUsesOrdinaryEventsForUndoRedo()
    {
        var graph = new RoadGraph();
        using var history = new RoadEditHistory(graph);
        var events = new List<RoadGraphChangedEvent>();
        graph.GraphChanged += events.Add;

        Assert.True(history.Execute(() => AddRoadAtY(graph, 0f)));
        long retainedBytes = history.RetainedByteSize;
        Assert.InRange(retainedBytes, 1, history.ByteCapacity);
        Assert.True(history.Undo());
        Assert.True(history.Redo());

        Assert.Equal(3, events.Count);
        Assert.All(events, change => Assert.False(change.Changes.IsFullReset));
        Assert.Equal([1L, 2L, 3L], events.Select(change => change.Changes.ChangeSequence));
        Assert.Equal(retainedBytes, history.RetainedByteSize);
    }

    [Fact]
    public void SimpleLoopHistoryRoundTripsCanonicalContent()
    {
        VerifyHistoryRoundTrip(
            _ => { },
            graph => graph.SubmitPolyline(RoadType.Street, [
                Vector2.Zero,
                new Vector2(10f, 0f),
                new Vector2(10f, 10f),
                new Vector2(0f, 10f),
                Vector2.Zero,
            ]).Success);
    }

    [Fact]
    public void FigureEightHistoryRoundTripsCanonicalContent()
    {
        VerifyHistoryRoundTrip(
            _ => { },
            graph => graph.SubmitPolyline(RoadType.Street, [
                Vector2.Zero,
                new Vector2(-10f, 10f),
                new Vector2(-20f, 0f),
                new Vector2(-10f, -10f),
                Vector2.Zero,
                new Vector2(10f, 10f),
                new Vector2(20f, 0f),
                new Vector2(10f, -10f),
                Vector2.Zero,
            ]).Success);
    }

    [Fact]
    public void RoadTypeMergeHistoryRoundTripsCanonicalContent()
    {
        int arterialID = -1;
        VerifyHistoryRoundTrip(
            graph =>
            {
                Assert.True(graph.SubmitPolyline(RoadType.Street, [
                    Vector2.Zero,
                    new Vector2(10f, 0f),
                ]).Success);
                arterialID = Assert.Single(graph.SubmitPolyline(RoadType.Arterial, [
                    new Vector2(10f, 0f),
                    new Vector2(20f, 0f),
                ]).Changes.CreatedEdgeIDs);
            },
            graph => graph.ChangeRoadType([arterialID], RoadType.Street).Success);
    }

    [Fact]
    public void BulkRemovalHistoryRoundTripsCanonicalContent()
    {
        int[] edgeIDs = [];
        VerifyHistoryRoundTrip(
            graph =>
            {
                for (int index = 0; index < 64; index++)
                {
                    Assert.True(graph.SubmitPolyline(RoadType.Street, [
                        new Vector2(0f, index * 20f),
                        new Vector2(8f, index * 20f),
                    ]).Success);
                }
                edgeIDs = graph.GetAllEdges().Select(edge => edge.ID).ToArray();
                Assert.Equal(64, edgeIDs.Length);
            },
            graph => graph.RemoveEdges(edgeIDs));
    }

    private static RoadGraph CreateThreeParallelRoads()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [new Vector2(0f, -20f), new Vector2(0f, 20f)]).Success);
        Assert.True(graph.SubmitPolyline(RoadType.Street, [new Vector2(100f, -20f), new Vector2(100f, 20f)]).Success);
        Assert.True(graph.SubmitPolyline(RoadType.Street, [new Vector2(200f, -20f), new Vector2(200f, 20f)]).Success);
        return graph;
    }

    private static bool AddRoadAtY(RoadGraph graph, float y) => graph.SubmitPolyline(RoadType.Street, [
        new Vector2(0f, y),
        new Vector2(20f, y),
    ]).Success;

    private static void VerifyHistoryRoundTrip(
        Action<RoadGraph> arrange,
        Func<RoadGraph, bool> mutate)
    {
        var graph = new RoadGraph();
        arrange(graph);
        GraphContent before = CaptureContent(graph);
        using var history = new RoadEditHistory(graph);

        Assert.True(history.Execute(() => mutate(graph)));
        GraphContent after = CaptureContent(graph);
        Assert.Equal(1, history.UndoCount);
        Assert.True(history.Undo());
        AssertGraphContent(before, graph);
        Assert.True(history.Redo());
        AssertGraphContent(after, graph);
        graph.AssertInvariants();
    }

    private static GraphContent CaptureContent(RoadGraph graph) => new(
        graph.GetAllNodes().OrderBy(node => node.ID).Select(DescribeNode).ToArray(),
        graph.GetAllEdges().OrderBy(edge => edge.ID).Select(DescribeEdge).ToArray());

    private static void AssertGraphContent(GraphContent expected, RoadGraph actual)
    {
        GraphContent content = CaptureContent(actual);
        Assert.Equal(expected.Nodes, content.Nodes);
        Assert.Equal(expected.Edges, content.Edges);
    }

    private static string DescribeNode(GraphNode node) =>
        $"{node.ID}:{BitConverter.SingleToInt32Bits(node.Position.X)}:" +
        $"{BitConverter.SingleToInt32Bits(node.Position.Y)}:" +
        string.Join("|", node.Incidences.Select(incidence =>
            $"{incidence.EdgeID},{incidence.Endpoint},{incidence.NeighborNodeID}"));

    private static string Capture(RoadGraph graph) => RoadGraphTestCodec.CaptureJson(graph);

    private static void AssertSameGraphContent(string expectedState, RoadGraph actual)
    {
        var expected = new RoadGraph();
        RoadGraphTestCodec.LoadJson(expected, expectedState);
        Assert.Equal(
            expected.GetAllNodes().OrderBy(node => node.ID)
                .Select(node => (node.ID, node.Position)),
            actual.GetAllNodes().OrderBy(node => node.ID)
                .Select(node => (node.ID, node.Position)));
        Assert.Equal(
            expected.GetAllEdges().OrderBy(edge => edge.ID)
                .Select(DescribeEdge),
            actual.GetAllEdges().OrderBy(edge => edge.ID)
                .Select(DescribeEdge));
    }

    private static string DescribeEdge(GraphEdge edge) =>
        $"{edge.ID}:{edge.NodeA}:{edge.NodeB}:{edge.RoadType}:" +
        string.Join("|", edge.GeometrySegments.Select(RoadGeometrySerializer.Serialize));

    private sealed record GraphContent(string[] Nodes, string[] Edges);
}
