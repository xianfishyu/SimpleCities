using Godot;

public sealed class RoadQueryFragmentV3Tests
{
    [Fact]
    public void LongDiagonalUsesLinearQueryFragmentsInsteadOfAreaFilledBounds()
    {
        var topology = new PreparedRoadGraphTopology(
            4,
            [
                new PreparedRoadNode(0, Vector2.Zero),
                new PreparedRoadNode(1, new Vector2(1024f, 1024f)),
            ],
            [
                new PreparedRoadEdge(RoadType.Street,
                    2,
                    0,
                    1,
                    [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1024f, 1024f))]),
            ]);

        RoadGraph graph = RoadGraph.FromPreparedTopology(topology);
        RoadGraphResourceCounts counts = graph.CaptureResourceCounts();

        Assert.InRange(counts.QueryFragments, 16, 80);
        Assert.InRange(counts.Buckets, 16, 100);
        Assert.InRange(counts.SpatialReferences, 16, 200);
        graph.AssertInvariants();
    }

    [Fact]
    public void LocalClosestQueryDoesNotTestRemoteGeometryOnTheSameEdge()
    {
        const int geometryCount = 256;
        var geometry = new RoadGeometrySegment[geometryCount];
        Vector2 start = Vector2.Zero;
        for (int index = 0; index < geometry.Length; index++)
        {
            Vector2 end = new(
                (index + 1) * 8f,
                index % 2 == 0 ? 8f : 0f);
            geometry[index] = new LineRoadGeometrySegment(start, end);
            start = end;
        }

        var topology = new PreparedRoadGraphTopology(
            4,
            [
                new PreparedRoadNode(0, Vector2.Zero),
                new PreparedRoadNode(1, start),
            ],
            [new PreparedRoadEdge(RoadType.Street, 2, 0, 1, geometry)]);
        RoadGraph graph = RoadGraph.FromPreparedTopology(topology);

        GraphEdge? result = graph.FindClosestEdge(new Vector2(4f, 4f), 0.01f);
        RoadGraphOperationMetrics metrics = graph.LastOperationMetrics;

        Assert.Equal(2, Assert.IsType<GraphEdge>(result).ID);
        Assert.Equal(1, metrics.SpatialCandidateEdgeCount);
        Assert.InRange(metrics.QueryFragmentCandidateCount, 1, 8);
        Assert.InRange(metrics.ExactGeometryTestCount, 1, 8);
    }

    [Fact]
    public void HeadMiddleAndTailQueriesStayLocalAsTheSameLineGrowsRemotely()
    {
        RoadGraph shortGraph = CreateStraightLineGraph(4_096f);
        RoadGraph longGraph = CreateStraightLineGraph(65_536f);

        AssertLocalQueries(shortGraph, CreateStraightLineQueryPoints(4_096f));
        AssertLocalQueries(longGraph, CreateStraightLineQueryPoints(65_536f));

        RoadGraphResourceCounts shortCounts = shortGraph.CaptureResourceCounts();
        RoadGraphResourceCounts longCounts = longGraph.CaptureResourceCounts();
        Assert.True(longCounts.QueryFragments >= shortCounts.QueryFragments * 16);
        Assert.True(longCounts.Buckets >= shortCounts.Buckets * 15);
    }

    [Fact]
    public void HeadMiddleAndTailQueriesStayLocalAsRemoteGeometryCountGrows()
    {
        RoadGraph shortGraph = CreateZigZagGraph(64);
        RoadGraph longGraph = CreateZigZagGraph(1_024);

        AssertLocalQueries(shortGraph, CreateZigZagQueryPoints(64));
        AssertLocalQueries(longGraph, CreateZigZagQueryPoints(1_024));

        RoadGraphResourceCounts shortCounts = shortGraph.CaptureResourceCounts();
        RoadGraphResourceCounts longCounts = longGraph.CaptureResourceCounts();
        Assert.Equal(64, shortCounts.GeometrySegments);
        Assert.Equal(1_024, longCounts.GeometrySegments);
        Assert.True(longCounts.QueryFragments >= shortCounts.QueryFragments * 16);
    }

    [Fact]
    public void LongDiagonalSubmissionUsesFragmentReferencesForAdmission()
    {
        var capacity = new RoadGraphCapacity
        {
            MaximumQueryFragments = 64,
            MaximumBuckets = 100,
            MaximumSpatialReferences = 200,
        };
        var graph = new RoadGraph(capacity);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1024f, 1024f)),
        ]), RoadType.Street));

        Assert.True(result.Success);
        RoadGraphResourceCounts counts = graph.CaptureResourceCounts();
        Assert.InRange(counts.QueryFragments, 16, 64);
        Assert.InRange(counts.Buckets, 16, 100);
        Assert.InRange(counts.SpatialReferences, 16, 200);
    }

    [Fact]
    public void PreparedTopologyRejectsActualFragmentCountBeforeIndexPublication()
    {
        var topology = new PreparedRoadGraphTopology(
            4,
            [
                new PreparedRoadNode(0, Vector2.Zero),
                new PreparedRoadNode(1, new Vector2(1024f, 1024f)),
            ],
            [
                new PreparedRoadEdge(RoadType.Street,
                    2,
                    0,
                    1,
                    [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1024f, 1024f))]),
            ]);
        var capacity = new RoadGraphCapacity
        {
            MaximumQueryFragments = 1,
        };

        Assert.Throws<ArgumentException>(() => RoadGraph.FromPreparedTopology(topology, capacity));
    }

    [Fact]
    public void FragmentCutHasOneCanonicalRoadLocationOwner()
    {
        RoadGraph graph = CreateLongLineGraph(selfLoop: false, geometryCount: 1);
        EdgeGeometryRef[] fragments = graph.CaptureQueryFragments(2).ToArray();
        Assert.True(fragments.Length > 1);

        EdgeGeometryRef before = fragments[0];
        EdgeGeometryRef after = fragments[1];

        Assert.False(before.TryGetRoadLocation(1f, out _));
        Assert.True(after.TryGetRoadLocation(0f, out RoadLocation location));
        Assert.Equal(new RoadLocation(2, 0, after.ParameterStart), location);
    }

    [Fact]
    public void PrimitiveJoinAndNonLoopEndHaveOneCanonicalOwner()
    {
        RoadGraph graph = CreateLongLineGraph(selfLoop: false, geometryCount: 2);
        EdgeGeometryRef[] fragments = graph.CaptureQueryFragments(2).ToArray();
        EdgeGeometryRef firstGeometryEnd = fragments.Last(fragment => fragment.GeometryIndex == 0);
        EdgeGeometryRef secondGeometryStart = fragments.First(fragment => fragment.GeometryIndex == 1);
        EdgeGeometryRef edgeEnd = fragments[^1];

        Assert.False(firstGeometryEnd.TryGetRoadLocation(1f, out _));
        Assert.True(secondGeometryStart.TryGetRoadLocation(0f, out RoadLocation join));
        Assert.Equal(new RoadLocation(2, 1, 0f), join);
        Assert.True(edgeEnd.TryGetRoadLocation(1f, out RoadLocation end));
        Assert.Equal(new RoadLocation(2, 1, 1f), end);
    }

    [Fact]
    public void SelfLoopSeamIsOwnedOnlyByFirstFragment()
    {
        var arc = new CircularArcRoadGeometrySegment(Vector2.Zero, 128f, 0f, Mathf.Tau);
        var topology = new PreparedRoadGraphTopology(
            4,
            [new PreparedRoadNode(0, arc.Start)],
            [
                new PreparedRoadEdge(RoadType.Street,
                    2,
                    0,
                    0,
                    [arc]),
            ]);
        RoadGraph graph = RoadGraph.FromPreparedTopology(topology);
        EdgeGeometryRef[] fragments = graph.CaptureQueryFragments(2).ToArray();

        Assert.True(fragments[0].TryGetRoadLocation(0f, out RoadLocation seam));
        Assert.Equal(new RoadLocation(2, 0, 0f), seam);
        Assert.False(fragments[^1].TryGetRoadLocation(1f, out _));
    }

    [Fact]
    public void OverlapAcrossFragmentCutsDoesNotCreateCutNodes()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1024f, 0f)),
        ]), RoadType.Street)).Success);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(new Vector2(-128f, 0f), new Vector2(1152f, 0f)),
        ]), RoadType.Street));

        Assert.True(result.Success);
        Assert.Equal(2, graph.GetAllNodes().Count());
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        var line = Assert.IsType<LineRoadGeometrySegment>(Assert.Single(edge.GeometrySegments));
        Assert.Equal(new Vector2(-128f, 0f), line.Start);
        Assert.Equal(new Vector2(1152f, 0f), line.End);
        Assert.DoesNotContain(graph.GetAllNodes(), node =>
            node.Position.X > 0f && node.Position.X < 1024f);
        graph.AssertInvariants();
    }

    [Fact]
    public void TangentCircleAndRectangleReturnOneEdgeAtFragmentCut()
    {
        RoadGraph graph = CreateLongLineGraph(selfLoop: false, geometryCount: 1);
        EdgeGeometryRef[] fragments = graph.CaptureQueryFragments(2).ToArray();
        Vector2 cut = fragments[0].Geometry.End;

        Assert.Equal([2], graph.FindEdgeIDsNear(cut + Vector2.Up, 1f));
        Assert.Equal([2], graph.FindEdgeIDsIntersecting(new Rect2(cut, Vector2.Zero)));
    }

    private static RoadGraph CreateLongLineGraph(bool selfLoop, int geometryCount)
    {
        RoadGeometrySegment[] geometry = geometryCount == 1
            ? [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1024f, 0f))]
            :
            [
                new LineRoadGeometrySegment(Vector2.Zero, new Vector2(512f, 0f)),
                new CubicBezierRoadGeometrySegment(
                    new Vector2(512f, 0f),
                    new Vector2(640f, 64f),
                    new Vector2(896f, -64f),
                    new Vector2(1024f, 0f)),
            ];
        Vector2 end = selfLoop ? Vector2.Zero : geometry[^1].End;
        var topology = new PreparedRoadGraphTopology(
            4,
            selfLoop
                ? [new PreparedRoadNode(0, Vector2.Zero)]
                : [new PreparedRoadNode(0, Vector2.Zero), new PreparedRoadNode(1, end)],
            [new PreparedRoadEdge(RoadType.Street, 2, 0, selfLoop ? 0 : 1, geometry)]);
        return RoadGraph.FromPreparedTopology(topology);
    }

    private static RoadGraph CreateStraightLineGraph(float length)
    {
        var end = new Vector2(length, 0f);
        var topology = new PreparedRoadGraphTopology(
            4,
            [new PreparedRoadNode(0, Vector2.Zero), new PreparedRoadNode(1, end)],
            [
                new PreparedRoadEdge(RoadType.Street,
                    2,
                    0,
                    1,
                    [new LineRoadGeometrySegment(Vector2.Zero, end)]),
            ]);
        return RoadGraph.FromPreparedTopology(topology);
    }

    private static RoadGraph CreateZigZagGraph(int geometryCount)
    {
        var geometry = new RoadGeometrySegment[geometryCount];
        Vector2 start = Vector2.Zero;
        for (int index = 0; index < geometry.Length; index++)
        {
            Vector2 end = new(
                (index + 1) * 16f,
                index % 2 == 0 ? 16f : 0f);
            geometry[index] = new LineRoadGeometrySegment(start, end);
            start = end;
        }

        var topology = new PreparedRoadGraphTopology(
            4,
            [new PreparedRoadNode(0, Vector2.Zero), new PreparedRoadNode(1, start)],
            [new PreparedRoadEdge(RoadType.Street, 2, 0, 1, geometry)]);
        return RoadGraph.FromPreparedTopology(topology);
    }

    private static Vector2[] CreateStraightLineQueryPoints(float length) =>
    [
        new Vector2(17f, 0f),
        new Vector2(length * 0.5f + 17f, 0f),
        new Vector2(length - 17f, 0f),
    ];

    private static Vector2[] CreateZigZagQueryPoints(int geometryCount)
    {
        int[] indices = [0, geometryCount / 2, geometryCount - 1];
        return indices.Select(index =>
        {
            Vector2 start = new(index * 16f, index % 2 == 0 ? 0f : 16f);
            Vector2 end = new((index + 1) * 16f, index % 2 == 0 ? 16f : 0f);
            return (start + end) * 0.5f;
        }).ToArray();
    }

    private static void AssertLocalQueries(RoadGraph graph, IReadOnlyList<Vector2> queryPoints)
    {
        Assert.Equal(3, queryPoints.Count);
        foreach (Vector2 point in queryPoints)
        {
            Assert.Equal(2, Assert.IsType<GraphEdge>(graph.FindClosestEdge(point, 0.01f)).ID);
            AssertLocalMetrics(graph.LastOperationMetrics);

            Assert.Equal([2], graph.FindEdgeIDsNear(point, 0.01f));
            AssertLocalMetrics(graph.LastOperationMetrics);

            var bounds = new Rect2(point - new Vector2(0.01f, 0.01f), new Vector2(0.02f, 0.02f));
            Assert.Equal([2], graph.FindEdgeIDsIntersecting(bounds));
            AssertLocalMetrics(graph.LastOperationMetrics);
        }
    }

    private static void AssertLocalMetrics(RoadGraphOperationMetrics metrics)
    {
        Assert.Equal(1, metrics.SpatialCandidateEdgeCount);
        Assert.InRange(metrics.QueryFragmentCandidateCount, 1, 8);
        Assert.InRange(metrics.ExactGeometryTestCount, 1, 8);
        Assert.Equal(0, metrics.FullEdgeScanPassCount);
        Assert.Equal(0, metrics.FullEdgeVisitCount);
    }
}
