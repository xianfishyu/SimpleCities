using Godot;

namespace SimpleCities.Tests;

public sealed class RoadRendererLoadPrepareTests
{
    private static readonly RoadRendererLoadSettings Settings = new(
        CurveDisplayTolerance: 0.25f,
        RoadWidth: 12f,
        EndpointRadius: 6f,
        JunctionRadius: 10f,
        EndpointColor: new Color("#90A4AE"),
        JunctionColor: new Color("#FFC107"));

    [Fact]
    public async Task PurePreparer_CanRunOnWorkerWithoutRendererAndIsDeterministic()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(10f, 0f)]).Success);
        Assert.True(graph.SubmitPolyline(
            RoadType.Highway,
            [new Vector2(5f, 0f), new Vector2(5f, 8f)]).Success);
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(new Vector2(20f, 20f), new Vector2(28f, 20f)),
            new LineRoadGeometrySegment(new Vector2(28f, 20f), new Vector2(28f, 28f)),
            new LineRoadGeometrySegment(new Vector2(28f, 28f), new Vector2(20f, 28f)),
            new LineRoadGeometrySegment(new Vector2(20f, 28f), new Vector2(20f, 20f)),
        ]), RoadType.Dirt)).Success);
        RoadGraphRevision revision = graph.CaptureRevision();
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);
        int callerThread = System.Environment.CurrentManagedThreadId;
        int workerThread = 0;

        RoadRendererPreparedLoad first = await Task.Run(() =>
        {
            workerThread = System.Environment.CurrentManagedThreadId;
            return preparer.Prepare(revision);
        });
        RoadRendererPreparedLoad second = preparer.Prepare(revision);

        Assert.NotEqual(callerThread, workerThread);
        Assert.Equal(first.RoadVertices, second.RoadVertices);
        Assert.Equal(first.RoadUvs, second.RoadUvs);
        Assert.Equal(first.RoadIndices, second.RoadIndices);
        Assert.Equal(first.NodeMarkers, second.NodeMarkers);
        Assert.Equal(
            first.EdgePoints.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value)),
            second.EdgePoints.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value)));
        Assert.NotEmpty(first.RoadVertices);
        Assert.Contains(first.NodeMarkers, marker => marker.Diameter == Settings.JunctionRadius * 2f);
    }

    [Fact]
    public void PurePreparer_PreservesParallelEdgesAndClosedLoopData()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(8f, 0f)),
        ]), RoadType.Street)).Success);
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                Vector2.Zero,
                new Vector2(2f, 3f),
                new Vector2(6f, 3f),
                new Vector2(8f, 0f)),
        ]), RoadType.Highway)).Success);
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(new Vector2(20f, 0f), new Vector2(26f, 0f)),
            new LineRoadGeometrySegment(new Vector2(26f, 0f), new Vector2(26f, 6f)),
            new LineRoadGeometrySegment(new Vector2(26f, 6f), new Vector2(20f, 6f)),
            new LineRoadGeometrySegment(new Vector2(20f, 6f), new Vector2(20f, 0f)),
        ]), RoadType.Dirt)).Success);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());

        Assert.Equal(graph.GetAllEdges().Count(), prepared.EdgePoints.Count);
        Assert.All(graph.GetAllEdges(), edge => Assert.True(prepared.EdgePoints.ContainsKey(edge.ID)));
        Assert.True(prepared.RoadVertices.Length >= prepared.EdgePoints.Count * 4);
        Assert.True(prepared.RoadIndices.Length >= prepared.EdgePoints.Count * 6);
    }

    [Fact]
    public void PurePreparer_ClosesSquareSelfLoopWithoutDuplicateSeamOrMarker()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
            new LineRoadGeometrySegment(new Vector2(10f, 0f), new Vector2(10f, 10f)),
            new LineRoadGeometrySegment(new Vector2(10f, 10f), new Vector2(0f, 10f)),
            new LineRoadGeometrySegment(new Vector2(0f, 10f), Vector2.Zero),
        ]), RoadType.Street)).Success);
        GraphEdge loop = Assert.Single(graph.GetAllEdges());
        Assert.Equal(loop.NodeA, loop.NodeB);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());

        Vector2[] points = Assert.Single(prepared.EdgePoints).Value;
        Assert.Equal(points[0], points[^1]);
        int uniquePointCount = points.Length - 1;
        Assert.Equal(uniquePointCount * 2, prepared.RoadVertices.Length);
        Assert.Equal(prepared.RoadVertices.Length, prepared.RoadUvs.Length);
        Assert.Equal(uniquePointCount * 6, prepared.RoadIndices.Length);
        Assert.Equal(
            [
                (uniquePointCount - 1) * 2,
                (uniquePointCount - 1) * 2 + 1,
                0,
                0,
                (uniquePointCount - 1) * 2 + 1,
                1,
            ],
            prepared.RoadIndices[^6..]);
        Assert.All(
            prepared.RoadIndices,
            index => Assert.InRange(index, 0, prepared.RoadVertices.Length - 1));
        Assert.True(
            prepared.RoadVertices[0].DistanceTo(points[0]) > Settings.RoadWidth * 0.5f,
            "The loop seam must use both wrapped neighbors rather than an open endpoint normal.");
        Assert.Empty(prepared.NodeMarkers);
    }

    [Fact]
    public void PurePreparer_ClosesFullTurnArcWithoutDuplicateSeamOrMarker()
    {
        Vector2 seam = new(5f, 0f);
        var graph = RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
            2,
            [new PreparedRoadNode(0, seam)],
            [
                new PreparedRoadEdge(
                    RoadType.Highway,
                    1,
                    0,
                    0,
                    [new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, Mathf.Tau)]),
            ]));
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());

        Vector2[] points = Assert.Single(prepared.EdgePoints).Value;
        Assert.Equal(points[0], points[^1]);
        int uniquePointCount = points.Length - 1;
        Assert.Equal(uniquePointCount * 2, prepared.RoadVertices.Length);
        Assert.Equal(uniquePointCount * 6, prepared.RoadIndices.Length);
        Assert.Empty(prepared.NodeMarkers);
    }

    [Fact]
    public void PurePreparer_SelfLoopWithBranchKeepsJunctionAndEndpointMarkers()
    {
        Vector2 seam = new(5f, 0f);
        var graph = RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
            5,
            [
                new PreparedRoadNode(0, seam),
                new PreparedRoadNode(3, new Vector2(12f, 0f)),
            ],
            [
                new PreparedRoadEdge(
                    RoadType.Street,
                    1,
                    0,
                    0,
                    [new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, Mathf.Tau)]),
                new PreparedRoadEdge(
                    RoadType.Street,
                    4,
                    0,
                    3,
                    [new LineRoadGeometrySegment(seam, new Vector2(12f, 0f))]),
            ]));
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());

        Assert.Collection(
            prepared.NodeMarkers.OrderBy(marker => marker.Diameter),
            marker => Assert.Equal(Settings.EndpointRadius * 2f, marker.Diameter),
            marker =>
            {
                Assert.Equal(seam, marker.Position);
                Assert.Equal(Settings.JunctionRadius * 2f, marker.Diameter);
            });
    }

    [Fact]
    public void PurePreparer_PreservesOpenRibbonVertexAndIndexLayout()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
            new LineRoadGeometrySegment(new Vector2(10f, 0f), new Vector2(10f, 10f)),
        ]), RoadType.Street)).Success);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());

        Assert.Equal(6, prepared.RoadVertices.Length);
        Assert.Equal(6, prepared.RoadUvs.Length);
        Assert.Equal([0, 1, 2, 2, 1, 3, 2, 3, 4, 4, 3, 5], prepared.RoadIndices);
        Assert.Equal(2, prepared.NodeMarkers.Length);
    }

    [Fact]
    public void PurePreparer_FigureEightClosesBothLoopsAndKeepsSharedJunction()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
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
        GraphNode junction = Assert.Single(graph.GetAllNodes());
        Assert.Equal(4, junction.IncidenceCount);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());

        Assert.Equal(2, prepared.EdgePoints.Count);
        Assert.All(prepared.EdgePoints.Values, points => Assert.Equal(points[0], points[^1]));
        int uniquePointCount = prepared.EdgePoints.Values.Sum(points => points.Length - 1);
        Assert.Equal(uniquePointCount * 2, prepared.RoadVertices.Length);
        Assert.Equal(uniquePointCount * 6, prepared.RoadIndices.Length);
        RoadRendererNodeMarker marker = Assert.Single(prepared.NodeMarkers);
        Assert.Equal(junction.Position, marker.Position);
        Assert.Equal(Settings.JunctionRadius * 2f, marker.Diameter);
    }

    [Fact]
    public void PurePreparer_RemovingOneBranchRelocatesSeamAndClosesRemainingLoop()
    {
        Vector2 originalSeamPosition = Vector2.Zero;
        Vector2 remainingJunctionPosition = new(100f, 100f);
        Vector2 removedBranchEnd = new(-100f, 0f);
        Vector2 remainingBranchEnd = new(200f, 100f);
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            originalSeamPosition,
            new Vector2(100f, 0f),
            remainingJunctionPosition,
            new Vector2(0f, 100f),
            originalSeamPosition,
        ]).Success);
        Assert.True(graph.SubmitPolyline(
            RoadType.Street,
            [originalSeamPosition, removedBranchEnd]).Success);
        Assert.True(graph.SubmitPolyline(
            RoadType.Street,
            [remainingJunctionPosition, remainingBranchEnd]).Success);
        GraphNode originalSeam = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position == originalSeamPosition);
        GraphNode remainingJunction = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position == remainingJunctionPosition);
        GraphNode removedEndpoint = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position == removedBranchEnd);
        Assert.Equal(3, originalSeam.IncidenceCount);
        Assert.Equal(3, remainingJunction.IncidenceCount);
        GraphEdge removedBranch = Assert.Single(
            graph.GetAllEdges(),
            edge => edge.NodeA == removedEndpoint.ID || edge.NodeB == removedEndpoint.ID);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad beforeRemoval = preparer.Prepare(graph.CaptureRevision());

        Assert.Equal(4, beforeRemoval.NodeMarkers.Length);
        Assert.Equal(
            2,
            beforeRemoval.NodeMarkers.Count(marker =>
                marker.Diameter == Settings.JunctionRadius * 2f));

        Assert.True(graph.RemoveEdge(removedBranch.ID));

        Assert.Null(graph.GetNode(originalSeam.ID));
        Assert.Null(graph.GetNode(removedEndpoint.ID));
        GraphNode relocatedSeam = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position == remainingJunctionPosition);
        GraphEdge loop = Assert.Single(
            graph.GetAllEdges(),
            edge => edge.NodeA == edge.NodeB);
        Assert.Equal(relocatedSeam.ID, loop.NodeA);
        Assert.Equal(3, relocatedSeam.IncidenceCount);

        RoadRendererPreparedLoad afterRemoval = preparer.Prepare(graph.CaptureRevision());

        Vector2[] loopPoints = afterRemoval.EdgePoints[loop.ID];
        Assert.Equal(loopPoints[0], loopPoints[^1]);
        int expectedVertexCount = graph.GetAllEdges().Sum(edge =>
        {
            Vector2[] points = afterRemoval.EdgePoints[edge.ID];
            return (edge.NodeA == edge.NodeB ? points.Length - 1 : points.Length) * 2;
        });
        int expectedIndexCount = graph.GetAllEdges().Sum(edge =>
        {
            Vector2[] points = afterRemoval.EdgePoints[edge.ID];
            return (edge.NodeA == edge.NodeB ? points.Length - 1 : points.Length - 1) * 6;
        });
        Assert.Equal(expectedVertexCount, afterRemoval.RoadVertices.Length);
        Assert.Equal(expectedIndexCount, afterRemoval.RoadIndices.Length);
        Assert.Collection(
            afterRemoval.NodeMarkers.OrderBy(marker => marker.Diameter),
            marker => Assert.Equal(Settings.EndpointRadius * 2f, marker.Diameter),
            marker =>
            {
                Assert.Equal(relocatedSeam.Position, marker.Position);
                Assert.Equal(Settings.JunctionRadius * 2f, marker.Diameter);
            });
    }
}
