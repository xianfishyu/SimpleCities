using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphDiagnosticsSnapshotTests
{
    [Fact]
    public void SnapshotTracksCanonicalMultiSegmentAndSelfLoopCounts()
    {
        var straight = new RoadGraph();
        RoadGraphDiagnosticsSnapshot empty = straight.CaptureDiagnosticsSnapshot();
        RoadGraphDiagnosticsSnapshot? observedBySubscriber = null;
        straight.GraphChanged += _ => observedBySubscriber = straight.CaptureDiagnosticsSnapshot();

        Assert.True(straight.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(10f, 0f),
            new Vector2(10f, 10f),
            new Vector2(20f, 10f),
        ]).Success);

        RoadGraphDiagnosticsSnapshot line = straight.CaptureDiagnosticsSnapshot();
        Assert.NotSame(empty, line);
        Assert.Same(line, observedBySubscriber);
        Assert.Equal(2, line.NodeCount);
        Assert.Equal(1, line.CanonicalEdgeCount);
        Assert.Equal(3, line.GeometrySegmentCount);
        Assert.Equal(straight.CaptureResourceCounts().QueryFragments, line.QueryFragmentCount);
        Assert.Equal(0, line.SelfLoopCount);
        Assert.Equal(straight.CurrentStateToken, line.StateToken);
        Assert.Equal(0, empty.NodeCount);
        Assert.Equal(0, empty.CanonicalEdgeCount);

        var loopGraph = new RoadGraph();
        Assert.True(loopGraph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(10f, 0f),
            new Vector2(10f, 10f),
            new Vector2(0f, 10f),
            Vector2.Zero,
        ]).Success);

        RoadGraphDiagnosticsSnapshot loop = loopGraph.CaptureDiagnosticsSnapshot();
        Assert.Equal(1, loop.NodeCount);
        Assert.Equal(1, loop.CanonicalEdgeCount);
        Assert.Equal(4, loop.GeometrySegmentCount);
        Assert.Equal(loopGraph.CaptureResourceCounts().QueryFragments, loop.QueryFragmentCount);
        Assert.Equal(1, loop.SelfLoopCount);
    }

    [Fact]
    public void SnapshotReportsStraightTwoJunctionLoopAndCrossingTopology()
    {
        var straight = new RoadGraph();
        Assert.True(straight.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success);
        AssertCounts(straight, nodes: 2, edges: 1, geometry: 1, selfLoops: 0);

        RoadGraph twoJunctionLoop = RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
            4,
            [
                new PreparedRoadNode(0, new Vector2(-10f, 0f)),
                new PreparedRoadNode(1, new Vector2(10f, 0f)),
            ],
            [
                new PreparedRoadEdge(RoadType.Street, 2, 0, 1, [
                    new LineRoadGeometrySegment(new Vector2(-10f, 0f), new Vector2(0f, 5f)),
                    new LineRoadGeometrySegment(new Vector2(0f, 5f), new Vector2(10f, 0f)),
                ]),
                new PreparedRoadEdge(RoadType.Highway, 3, 0, 1, [
                    new LineRoadGeometrySegment(new Vector2(-10f, 0f), new Vector2(0f, -5f)),
                    new LineRoadGeometrySegment(new Vector2(0f, -5f), new Vector2(10f, 0f)),
                ]),
            ]));
        AssertCounts(twoJunctionLoop, nodes: 2, edges: 2, geometry: 4, selfLoops: 0);

        var crossing = new RoadGraph();
        Assert.True(crossing.SubmitPolyline(RoadType.Street, [
            new Vector2(-20f, 0f),
            new Vector2(20f, 0f),
        ]).Success);
        Assert.True(crossing.SubmitPolyline(RoadType.Street, [
            new Vector2(0f, -20f),
            new Vector2(0f, 20f),
        ]).Success);
        AssertCounts(crossing, nodes: 5, edges: 4, geometry: 4, selfLoops: 0);
    }

    [Fact]
    public void SnapshotFollowsDeltaReplayDeletionNormalizationAndFullReset()
    {
        var graph = new RoadGraph();
        RoadGraphChangedEvent? branchChange = null;
        graph.GraphChanged += change => branchChange = change;

        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success);
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(10f, 0f),
            new Vector2(10f, 10f),
        ]).Success);
        RoadGraphChangedEvent branch = Assert.IsType<RoadGraphChangedEvent>(branchChange);
        AssertSnapshotMatchesGraph(graph);

        RoadGraphDeltaApplyResult undo = graph.ApplyDelta(
            branch.Delta,
            RoadGraphDeltaDirection.Reverse,
            graph.CurrentStateToken);
        Assert.True(undo.Success);
        RoadGraphDiagnosticsSnapshot normalized = graph.CaptureDiagnosticsSnapshot();
        Assert.Equal(2, normalized.NodeCount);
        Assert.Equal(1, normalized.CanonicalEdgeCount);
        Assert.Equal(1, normalized.GeometrySegmentCount);
        AssertSnapshotMatchesGraph(graph);

        var source = new RoadGraph();
        Assert.True(source.SubmitPolyline(RoadType.Highway, [
            Vector2.Zero,
            new Vector2(8f, 0f),
            new Vector2(8f, 8f),
            Vector2.Zero,
        ]).Success);
        RoadGraphDiagnosticsSnapshot beforeReset = graph.CaptureDiagnosticsSnapshot();

        RoadGraphTestCodec.LoadJson(graph, RoadGraphTestCodec.CaptureJson(source));

        RoadGraphDiagnosticsSnapshot reset = graph.CaptureDiagnosticsSnapshot();
        Assert.NotSame(beforeReset, reset);
        Assert.NotEqual(beforeReset.LineageID, reset.LineageID);
        Assert.Equal(beforeReset.ChangeSequence + 1, reset.ChangeSequence);
        Assert.Equal(1, reset.NodeCount);
        Assert.Equal(1, reset.CanonicalEdgeCount);
        Assert.Equal(1, reset.SelfLoopCount);
        AssertSnapshotMatchesGraph(graph);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4_096)]
    public void RepeatedCaptureReturnsThePublishedSnapshotWithoutAllocating(int geometryCount)
    {
        RoadGraph graph = CreateGeometryDenseEdge(geometryCount);
        RoadGraphDiagnosticsSnapshot published = graph.CaptureDiagnosticsSnapshot();
        _ = graph.CaptureDiagnosticsSnapshot();

        long before = GC.GetAllocatedBytesForCurrentThread();
        RoadGraphDiagnosticsSnapshot observed = published;
        for (int index = 0; index < 100_000; index++)
            observed = graph.CaptureDiagnosticsSnapshot();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Same(published, observed);
        Assert.Same(published, graph.DiagnosticsSnapshot);
        Assert.Equal(1, published.CanonicalEdgeCount);
        Assert.Equal(geometryCount, published.GeometrySegmentCount);
        Assert.Equal(0, allocated);
    }

    private static RoadGraph CreateGeometryDenseEdge(int geometryCount)
    {
        var geometry = new RoadGeometrySegment[geometryCount];
        Vector2 start = Vector2.Zero;
        for (int index = 0; index < geometry.Length; index++)
        {
            Vector2 end = new((index + 1) * 8f, index % 2 == 0 ? 8f : 0f);
            geometry[index] = new LineRoadGeometrySegment(start, end);
            start = end;
        }

        return RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
            3,
            [
                new PreparedRoadNode(0, Vector2.Zero),
                new PreparedRoadNode(1, start),
            ],
            [new PreparedRoadEdge(RoadType.Street, 2, 0, 1, geometry)]));
    }

    private static void AssertSnapshotMatchesGraph(RoadGraph graph)
    {
        RoadGraphDiagnosticsSnapshot snapshot = graph.CaptureDiagnosticsSnapshot();
        RoadGraphResourceCounts resources = graph.CaptureResourceCounts();
        Assert.Equal(graph.CurrentStateToken, snapshot.StateToken);
        Assert.Equal(graph.GetAllNodes().Count(), snapshot.NodeCount);
        Assert.Equal(graph.GetAllEdges().Count(), snapshot.CanonicalEdgeCount);
        Assert.Equal(resources.GeometrySegments, snapshot.GeometrySegmentCount);
        Assert.Equal(resources.QueryFragments, snapshot.QueryFragmentCount);
        Assert.Equal(
            graph.GetAllEdges().Count(edge => edge.NodeA == edge.NodeB),
            snapshot.SelfLoopCount);
    }

    private static void AssertCounts(
        RoadGraph graph,
        int nodes,
        int edges,
        long geometry,
        int selfLoops)
    {
        RoadGraphDiagnosticsSnapshot snapshot = graph.CaptureDiagnosticsSnapshot();
        Assert.Equal(nodes, snapshot.NodeCount);
        Assert.Equal(edges, snapshot.CanonicalEdgeCount);
        Assert.Equal(geometry, snapshot.GeometrySegmentCount);
        Assert.Equal(selfLoops, snapshot.SelfLoopCount);
        AssertSnapshotMatchesGraph(graph);
    }
}
