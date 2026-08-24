using Godot;

namespace SimpleCities.Tests;

public sealed class RoadRendererLoadPrepareTests
{
    private static readonly RoadTypeStyleSnapshot RoadTypeStyles =
        RoadTypeStyleSnapshot.Create([
            Style(RoadType.Dirt, "Dirt", "#8A6652", 4f),
            Style(RoadType.Street, "Street", "#60727C", 6f),
            Style(RoadType.Arterial, "Arterial", "#D7A928", 8f),
            Style(RoadType.Highway, "Highway", "#C84B3A", 10f),
        ]);

    private static readonly RoadRendererLoadSettings Settings = new(
        CurveDisplayTolerance: 0.25f,
        RoadTypeStyles);

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
        GraphNode junction = Assert.Single(
            graph.GetAllNodes(),
            node => node.IncidenceCount >= 3);
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
        Assert.Equal(first.RoadColors, second.RoadColors);
        Assert.Equal(first.RoadIndices, second.RoadIndices);
        Assert.Equal(first.RoadSurface.PrimitiveCount, second.RoadSurface.PrimitiveCount);
        for (int index = 0; index < first.RoadSurface.PrimitiveCount; index++)
        {
            Assert.Equal(
                first.RoadSurface.GetPrimitive(index),
                second.RoadSurface.GetPrimitive(index));
        }
        Assert.Equal(first.NodeMarkers, second.NodeMarkers);
        Assert.Equal(
            first.EdgePoints.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value)),
            second.EdgePoints.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value)));
        Assert.Equal(
            first.EdgeDisplaySpans.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value)),
            second.EdgeDisplaySpans.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value)));
        Assert.NotEmpty(first.RoadVertices);
        Assert.Equal(first.RoadIndices.Length / 3, first.RoadSurface.TriangleCount);
        Assert.True(first.RoadSurface.DiscCount > 0);
        Assert.DoesNotContain(first.NodeMarkers, marker => marker.Position == junction.Position);
        Assert.Contains(
            SurfaceTriangles(first, RoadSurfaceOwnerKind.JunctionPatch),
            triangle => triangle.Owner.NodeID == junction.ID);
    }

    [Fact]
    public void PurePreparer_ReusesOnlyNonInvalidatedDisplayPaths()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(20f, 0f),
        ]).Success);
        Assert.True(graph.SubmitPolyline(RoadType.Arterial, [
            new Vector2(0f, 40f),
            new Vector2(20f, 40f),
        ]).Success);
        RoadGraphRevision revision = graph.CaptureRevision();
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);
        RoadRendererPreparedLoad initial = preparer.Prepare(revision);
        int invalidatedEdgeID = graph.GetAllEdges().Min(edge => edge.ID);
        int reusableEdgeID = graph.GetAllEdges().Max(edge => edge.ID);

        RoadRendererPreparedLoad rebuilt = preparer.Prepare(
            revision,
            initial.EdgePoints,
            initial.EdgeDisplaySpans,
            new HashSet<int> { invalidatedEdgeID });

        Assert.NotSame(
            initial.EdgePoints[invalidatedEdgeID],
            rebuilt.EdgePoints[invalidatedEdgeID]);
        Assert.NotSame(
            initial.EdgeDisplaySpans[invalidatedEdgeID],
            rebuilt.EdgeDisplaySpans[invalidatedEdgeID]);
        Assert.Same(initial.EdgePoints[reusableEdgeID], rebuilt.EdgePoints[reusableEdgeID]);
        Assert.Same(
            initial.EdgeDisplaySpans[reusableEdgeID],
            rebuilt.EdgeDisplaySpans[reusableEdgeID]);
        Assert.Equal(initial.RoadVertices, rebuilt.RoadVertices);
        Assert.Equal(initial.RoadIndices, rebuilt.RoadIndices);
    }

    [Fact]
    public void PurePreparer_JunctionPatchMeshSurfaceAndOwnershipStayInLockstep()
    {
        RoadGraph graph = CreateAcuteJunction(reverseEdges: false);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());
        var snapshot = new RoadSurfaceSnapshot(Token(), prepared.RoadSurface);
        (int Index, RoadSurfaceTriangle Triangle)[] patch = Enumerable
            .Range(0, prepared.RoadSurface.TriangleCount)
            .Select(index => (
                Index: index,
                Triangle: prepared.RoadSurface.GetPrimitive(index).Triangle))
            .Where(item => item.Triangle.Owner.Kind == RoadSurfaceOwnerKind.JunctionPatch)
            .ToArray();

        Assert.Equal(4, patch.Length);
        Assert.Equal(13, prepared.RoadSurface.PrimitiveCount);
        Assert.Equal(
            new[] { 10, 11, 12 },
            patch.Select(item => item.Triangle.Owner.EdgeID).Distinct().Order());
        Assert.Equal(3, prepared.NodeMarkers.Length);
        Assert.DoesNotContain(prepared.NodeMarkers, marker => marker.Position == Vector2.Zero);
        Assert.All(patch, item =>
        {
            RoadSurfaceTriangle triangle = item.Triangle;
            GraphEdge edge = Assert.IsType<GraphEdge>(graph.GetEdge(triangle.Owner.EdgeID));
            Assert.Equal(0, triangle.Owner.NodeID);
            Assert.Equal(EdgeEndpoint.A, triangle.Owner.Endpoint);
            Assert.Equal(
                new RoadLocation(edge.ID, 0, RoadGeometrySegment.ParameterStart),
                triangle.FixedLocation);
            Assert.Equal(Vector2.Zero, triangle.CenterlineStart);
            Assert.NotEqual(triangle.CenterlineStart, triangle.CenterlineEnd);

            int meshIndex = item.Index * 3;
            int[] meshTriangleIndices = prepared.RoadIndices[meshIndex..(meshIndex + 3)];
            Assert.Equal(
                [triangle.A, triangle.B, triangle.C],
                meshTriangleIndices.Select(index => prepared.RoadVertices[index]));
            Color expectedColor = RoadTypeStyles.Resolve(edge.RoadType).Color;
            Assert.All(
                meshTriangleIndices,
                index => Assert.Equal(expectedColor, prepared.RoadColors[index]));
        });
        Assert.Contains(patch, item =>
        {
            Vector2 centroid =
                (item.Triangle.A + item.Triangle.B + item.Triangle.C) / 3f;
            RoadSurfaceHit? hit = snapshot.FindClosest(centroid, maxSurfaceDistance: 0f);
            return hit is RoadSurfaceHit
            {
                OwnerKind: RoadSurfaceOwnerKind.JunctionPatch,
                NodeID: 0,
                Endpoint: EdgeEndpoint.A,
                Location: not null,
            };
        });
    }

    [Fact]
    public void PurePreparer_JunctionPatchVisualDoesNotDependOnStoredEdgeDirection()
    {
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad forward = preparer.Prepare(
            CreateAcuteJunction(reverseEdges: false).CaptureRevision());
        RoadRendererPreparedLoad reversed = preparer.Prepare(
            CreateAcuteJunction(reverseEdges: true).CaptureRevision());
        RoadSurfaceTriangle[] forwardPatch = SurfaceTriangles(
            forward,
            RoadSurfaceOwnerKind.JunctionPatch);
        RoadSurfaceTriangle[] reversedPatch = SurfaceTriangles(
            reversed,
            RoadSurfaceOwnerKind.JunctionPatch);

        Assert.Equal(
            ExtractJunctionPatchVisual(forward),
            ExtractJunctionPatchVisual(reversed));
        Assert.Equal(
            forwardPatch.Select(triangle => (
                triangle.Owner.EdgeID,
                triangle.Owner.SectorOrder)),
            reversedPatch.Select(triangle => (
                triangle.Owner.EdgeID,
                triangle.Owner.SectorOrder)));
        Assert.All(
            forwardPatch,
            triangle =>
            {
                Assert.Equal(EdgeEndpoint.A, triangle.Owner.Endpoint);
                RoadLocation location = Assert.IsType<RoadLocation>(triangle.FixedLocation);
                Assert.Equal(triangle.Owner.EdgeID, location.EdgeID);
                Assert.Equal(RoadGeometrySegment.ParameterStart, location.Parameter);
            });
        Assert.All(
            reversedPatch,
            triangle =>
            {
                Assert.Equal(EdgeEndpoint.B, triangle.Owner.Endpoint);
                RoadLocation location = Assert.IsType<RoadLocation>(triangle.FixedLocation);
                Assert.Equal(triangle.Owner.EdgeID, location.EdgeID);
                Assert.Equal(RoadGeometrySegment.ParameterEnd, location.Parameter);
            });
    }

    [Fact]
    public async Task PurePreparer_PreparesQueryableSurfaceIndexBeforeTokenBinding()
    {
        var graph = new RoadGraph();
        for (int index = 0; index < 128; index++)
        {
            float y = index * 100f;
            Assert.True(graph.SubmitPolyline(
                RoadType.Street,
                [new Vector2(0f, y), new Vector2(10f, y)]).Success);
        }
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = await Task.Run(() =>
            preparer.Prepare(graph.CaptureRevision()));
        var snapshot = new RoadSurfaceSnapshot(Token(), prepared.RoadSurface);

        RoadSurfaceHit hit = Assert.IsType<RoadSurfaceHit>(snapshot.FindClosest(
            new Vector2(5f, 0f),
            maxSurfaceDistance: 0f,
            out RoadSurfaceQueryMetrics metrics));
        Assert.Equal(graph.GetAllEdges().Min(edge => edge.ID), hit.EdgeID);
        Assert.InRange(metrics.PrimitiveCandidateCount, 2, 8);
        Assert.Equal(2, metrics.ExactPrimitiveTestCount);
        Assert.True(metrics.PrimitiveCandidateCount < snapshot.PrimitiveCount);
    }

    [Fact]
    public void PurePreparer_UsesPerEdgeWidthAndVertexColorInOneBatch()
    {
        var graph = new RoadGraph();
        RoadType[] roadTypes =
        [
            RoadType.Dirt,
            RoadType.Street,
            RoadType.Arterial,
            RoadType.Highway,
        ];
        for (int index = 0; index < roadTypes.Length; index++)
        {
            float y = index * 20f;
            Assert.True(graph.SubmitPolyline(
                roadTypes[index],
                [new Vector2(0f, y), new Vector2(40f, y)]).Success);
        }
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());

        Assert.Equal(prepared.RoadVertices.Length, prepared.RoadUvs.Length);
        Assert.Equal(prepared.RoadVertices.Length, prepared.RoadColors.Length);
        int vertexOffset = 0;
        foreach (GraphEdge edge in graph.GetAllEdges().OrderBy(edge => edge.ID))
        {
            Vector2[] points = prepared.EdgePoints[edge.ID];
            int vertexCount = points.Length * 2;
            RoadTypeStyleDefinition style = RoadTypeStyles.Resolve(edge.RoadType);
            float renderedWidth = prepared.RoadVertices[vertexOffset]
                .DistanceTo(prepared.RoadVertices[vertexOffset + 1]);
            Assert.InRange(Mathf.Abs(renderedWidth - style.Width), 0f, 0.0001f);
            Assert.All(
                prepared.RoadColors[vertexOffset..(vertexOffset + vertexCount)],
                color => Assert.Equal(style.Color, color));
            vertexOffset += vertexCount;
        }
        Assert.Equal(prepared.RoadVertices.Length, vertexOffset);
    }

    [Fact]
    public void PurePreparer_SurfaceTrianglesExactlyMatchMeshIndicesAndStableOwners()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(10f, 0f), new Vector2(10f, 10f)]).Success);
        Assert.True(graph.SubmitPolyline(
            RoadType.Highway,
            [new Vector2(20f, 0f), new Vector2(30f, 0f)]).Success);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());

        Assert.Equal(prepared.RoadIndices.Length / 3, prepared.RoadSurface.TriangleCount);
        for (int index = 0; index < prepared.RoadSurface.TriangleCount; index++)
        {
            RoadSurfacePrimitive primitive = prepared.RoadSurface.GetPrimitive(index);
            Assert.Equal(RoadSurfacePrimitiveKind.Triangle, primitive.Kind);
            RoadSurfaceTriangle triangle = primitive.Triangle;
            int meshIndex = index * 3;
            Assert.Equal(prepared.RoadVertices[prepared.RoadIndices[meshIndex]], triangle.A);
            Assert.Equal(prepared.RoadVertices[prepared.RoadIndices[meshIndex + 1]], triangle.B);
            Assert.Equal(prepared.RoadVertices[prepared.RoadIndices[meshIndex + 2]], triangle.C);
            Assert.Equal(RoadSurfaceOwnerKind.EdgeRibbon, triangle.Owner.Kind);
            Assert.Contains(graph.GetAllEdges(), edge => edge.ID == triangle.Owner.EdgeID);
            RoadLocation start = Assert.IsType<RoadLocation>(triangle.LocationStart);
            RoadLocation end = Assert.IsType<RoadLocation>(triangle.LocationEnd);
            GraphEdge edge = Assert.Single(
                graph.GetAllEdges(),
                candidate => candidate.ID == triangle.Owner.EdgeID);
            Assert.Equal(edge.ID, start.EdgeID);
            Assert.Equal(edge.ID, end.EdgeID);
            Assert.InRange(start.GeometryIndex, 0, edge.GeometrySegments.Count - 1);
            Assert.Equal(start.GeometryIndex, end.GeometryIndex);
            Assert.True(start.Parameter < end.Parameter);
        }
        int terminalNodeCount = graph.GetAllNodes().Count(node => node.IncidenceCount == 1);
        Assert.Equal(terminalNodeCount, prepared.RoadSurface.DiscCount);
        for (int index = prepared.RoadSurface.TriangleCount;
             index < prepared.RoadSurface.PrimitiveCount;
             index++)
        {
            RoadSurfacePrimitive primitive = prepared.RoadSurface.GetPrimitive(index);
            Assert.Equal(RoadSurfacePrimitiveKind.Disc, primitive.Kind);
            Assert.Equal(RoadSurfaceOwnerKind.TerminalCap, primitive.Disc.Owner.Kind);
        }
    }

    [Fact]
    public void PurePreparer_TerminalCapsMatchRoadStyleAndOwnCanonicalEndpoints()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(
            RoadType.Highway,
            [Vector2.Zero, new Vector2(10f, 0f)]).Success);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        GraphNode nodeA = Assert.IsType<GraphNode>(graph.GetNode(edge.NodeA));
        GraphNode nodeB = Assert.IsType<GraphNode>(graph.GetNode(edge.NodeB));
        RoadTypeStyleDefinition style = RoadTypeStyles.Resolve(edge.RoadType);
        float radius = style.Width * 0.5f;
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());
        var snapshot = new RoadSurfaceSnapshot(Token(), prepared.RoadSurface);

        Assert.Equal(2, prepared.RoadSurface.TriangleCount);
        Assert.Equal(2, prepared.RoadSurface.DiscCount);
        Assert.All(prepared.NodeMarkers, marker =>
        {
            Assert.Equal(style.Width, marker.Diameter);
            Assert.Equal(style.Color, marker.Color);
        });

        RoadSurfaceHit start = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(-radius * 0.5f, 0f), maxSurfaceDistance: 0f));
        Assert.Equal(RoadSurfaceOwnerKind.TerminalCap, start.OwnerKind);
        Assert.Equal(edge.ID, start.EdgeID);
        Assert.Equal(nodeA.ID, start.NodeID);
        Assert.Equal(EdgeEndpoint.A, start.Endpoint);
        Assert.Equal(new RoadLocation(edge.ID, 0, 0f), start.Location);

        RoadSurfaceHit end = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(10f + radius * 0.5f, 0f), maxSurfaceDistance: 0f));
        Assert.Equal(RoadSurfaceOwnerKind.TerminalCap, end.OwnerKind);
        Assert.Equal(edge.ID, end.EdgeID);
        Assert.Equal(nodeB.ID, end.NodeID);
        Assert.Equal(EdgeEndpoint.B, end.Endpoint);
        Assert.Equal(new RoadLocation(edge.ID, 0, 1f), end.Location);

        Assert.Equal(
            [edge.ID],
            snapshot.FindEdgeIDsIntersecting(new Rect2(-radius, -1f, radius * 0.5f, 2f)));
    }

    [Fact]
    public void PurePreparer_TerminalCapInputPermutationAndEdgeIDRenamingPreserveMappedOwnership()
    {
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);
        RoadRendererPreparedLoad first = preparer.Prepare(
            CreateTerminalCapFixture(10, 11).CaptureRevision());
        RoadRendererPreparedLoad permuted = preparer.Prepare(
            CreateTerminalCapFixture(
                10,
                11,
                reverseEdgeEnumeration: true).CaptureRevision());
        RoadRendererPreparedLoad renamed = preparer.Prepare(
            CreateTerminalCapFixture(11, 10).CaptureRevision());
        TerminalCapOwnership[] firstOwnership = ExtractTerminalCapOwnership(first);
        TerminalCapOwnership[] renamedOwnership = ExtractTerminalCapOwnership(renamed);
        IReadOnlyDictionary<int, int> edgeIDMap = new Dictionary<int, int>
        {
            [10] = 11,
            [11] = 10,
        };

        Assert.Equal(SurfaceDiscs(first), SurfaceDiscs(permuted));
        Assert.Equal(first.NodeMarkers, permuted.NodeMarkers);
        Assert.Equal(
            ExtractTerminalCapVisual(first),
            ExtractTerminalCapVisual(renamed));
        Assert.Equal(
            firstOwnership.Select(item => new TerminalCapOwnership(
                item.Center,
                edgeIDMap[item.EdgeID],
                item.NodeID,
                item.Endpoint,
                item.SectorOrder,
                new RoadLocation(
                    edgeIDMap[item.Location.EdgeID],
                    item.Location.GeometryIndex,
                    item.Location.Parameter))),
            renamedOwnership);
    }

    [Fact]
    public void PurePreparer_TerminalCapStoredDirectionPreservesVisualAndMappedOwnership()
    {
        RoadGraph forwardGraph = CreateTerminalCapFixture(10, 11);
        RoadGraph reversedGraph = CreateTerminalCapFixture(
            10,
            11,
            reverseStoredDirections: true);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad forward = preparer.Prepare(forwardGraph.CaptureRevision());
        RoadRendererPreparedLoad reversed = preparer.Prepare(reversedGraph.CaptureRevision());
        TerminalCapOwnership[] forwardOwnership = ExtractTerminalCapOwnership(forward);
        TerminalCapOwnership[] reversedOwnership = ExtractTerminalCapOwnership(reversed);
        IReadOnlyDictionary<int, int> nodeIDMap = new Dictionary<int, int>
        {
            [0] = 5,
            [1] = 4,
            [2] = 7,
            [3] = 6,
        };

        Assert.Equal(
            ExtractTerminalCapVisual(forward),
            ExtractTerminalCapVisual(reversed));
        Assert.Equal(
            forwardOwnership.Select(item =>
            {
                EdgeEndpoint mappedEndpoint = item.Endpoint switch
                {
                    EdgeEndpoint.A => EdgeEndpoint.B,
                    EdgeEndpoint.B => EdgeEndpoint.A,
                    _ => throw new InvalidOperationException("A terminal cap endpoint is invalid."),
                };
                GraphEdge reversedEdge = Assert.IsType<GraphEdge>(
                    reversedGraph.GetEdge(item.EdgeID));
                RoadLocation mappedLocation = mappedEndpoint switch
                {
                    EdgeEndpoint.A => new RoadLocation(
                        item.EdgeID,
                        0,
                        RoadGeometrySegment.ParameterStart),
                    EdgeEndpoint.B => new RoadLocation(
                        item.EdgeID,
                        reversedEdge.GeometrySegments.Count - 1,
                        RoadGeometrySegment.ParameterEnd),
                    _ => throw new InvalidOperationException("A terminal cap endpoint is invalid."),
                };
                return new TerminalCapOwnership(
                    item.Center,
                    item.EdgeID,
                    nodeIDMap[item.NodeID],
                    mappedEndpoint,
                    item.SectorOrder,
                    mappedLocation);
            }),
            reversedOwnership);
    }

    [Fact]
    public void PurePreparer_SurfaceLocationsOwnGeometryJoinsAndOpenEdgeEndCanonically()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
            new LineRoadGeometrySegment(new Vector2(10f, 0f), new Vector2(10f, 10f)),
        ]), RoadType.Street)).Success);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Equal(2, edge.GeometrySegments.Count);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());
        var snapshot = new RoadSurfaceSnapshot(Token(), prepared.RoadSurface);

        RoadSurfaceHit join = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(10f, 0f), maxSurfaceDistance: 0f));
        RoadSurfaceHit end = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(10f, 10f), maxSurfaceDistance: 0f));
        Assert.Equal(new RoadLocation(edge.ID, 1, 0f), join.Location);
        Assert.Equal(new RoadLocation(edge.ID, 1, 1f), end.Location);
    }

    [Fact]
    public void PurePreparer_SelfLoopSeamBelongsToFirstGeometryStart()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
            new LineRoadGeometrySegment(new Vector2(10f, 0f), new Vector2(10f, 10f)),
            new LineRoadGeometrySegment(new Vector2(10f, 10f), new Vector2(0f, 10f)),
            new LineRoadGeometrySegment(new Vector2(0f, 10f), Vector2.Zero),
        ]), RoadType.Street)).Success);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Equal(edge.NodeA, edge.NodeB);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());
        var snapshot = new RoadSurfaceSnapshot(Token(), prepared.RoadSurface);

        RoadSurfaceHit seam = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(edge.GeometrySegments[0].Start, maxSurfaceDistance: 0f));
        Assert.Equal(new RoadLocation(edge.ID, 0, 0f), seam.Location);
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
        Assert.Equal(prepared.RoadVertices.Length, prepared.RoadColors.Length);
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
            prepared.RoadVertices[0].DistanceTo(points[0]) >
            Settings.RoadTypeStyles.Resolve(RoadType.Street).Width * 0.5f,
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
    public void PurePreparer_SelfLoopWithBranchUsesPatchAndKeepsEndpointMarker()
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

        RoadRendererNodeMarker endpoint = Assert.Single(prepared.NodeMarkers);
        Assert.Equal(new Vector2(12f, 0f), endpoint.Position);
        Assert.Equal(
            Settings.RoadTypeStyles.Resolve(RoadType.Street).Width,
            endpoint.Diameter);
        RoadSurfaceTriangle[] patch = SurfaceTriangles(
            prepared,
            RoadSurfaceOwnerKind.JunctionPatch);
        Assert.NotEmpty(patch);
        Assert.All(patch, triangle => Assert.Equal(0, triangle.Owner.NodeID));
        Assert.Contains(
            patch,
            triangle => triangle.Owner is
                { EdgeID: 1, Endpoint: EdgeEndpoint.A });
        Assert.Contains(
            patch,
            triangle => triangle.Owner is
                { EdgeID: 1, Endpoint: EdgeEndpoint.B });
        Assert.Contains(patch, triangle => triangle.Owner.EdgeID == 4);
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
        Assert.Equal(6, prepared.RoadColors.Length);
        Assert.Equal([0, 1, 2, 2, 1, 3, 2, 3, 4, 4, 3, 5], prepared.RoadIndices);
        Assert.Equal(2, prepared.NodeMarkers.Length);
    }

    [Fact]
    public void PurePreparer_FigureEightClosesBothLoopsAndPatchesSharedJunction()
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
        RoadSurfaceTriangle[] patch = SurfaceTriangles(
            prepared,
            RoadSurfaceOwnerKind.JunctionPatch);
        Assert.NotEmpty(patch);
        Assert.All(patch, triangle => Assert.Equal(junction.ID, triangle.Owner.NodeID));
        Assert.Equal(uniquePointCount * 2 + patch.Length * 3, prepared.RoadVertices.Length);
        Assert.Equal(uniquePointCount * 6 + patch.Length * 3, prepared.RoadIndices.Length);
        Assert.Empty(prepared.NodeMarkers);
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

        Assert.Equal(2, beforeRemoval.NodeMarkers.Length);
        Assert.All(
            beforeRemoval.NodeMarkers,
            marker => Assert.Equal(
                Settings.RoadTypeStyles.Resolve(RoadType.Street).Width,
                marker.Diameter));
        RoadSurfaceTriangle[] beforePatches = SurfaceTriangles(
            beforeRemoval,
            RoadSurfaceOwnerKind.JunctionPatch);
        Assert.Equal(6, beforePatches.Length);
        Assert.Equal(20, beforeRemoval.RoadSurface.PrimitiveCount);
        Assert.Equal(
            new[] { originalSeam.ID, remainingJunction.ID }.Order(),
            beforePatches
                .Select(triangle => triangle.Owner.NodeID!.Value)
                .Distinct()
                .Order());

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
        RoadSurfaceTriangle[] afterPatches = SurfaceTriangles(
            afterRemoval,
            RoadSurfaceOwnerKind.JunctionPatch);
        Assert.Equal(3, afterPatches.Length);
        Assert.Equal(14, afterRemoval.RoadSurface.PrimitiveCount);
        Assert.All(
            afterPatches,
            triangle => Assert.Equal(relocatedSeam.ID, triangle.Owner.NodeID));
        Assert.Equal(
            expectedVertexCount + afterPatches.Length * 3,
            afterRemoval.RoadVertices.Length);
        Assert.Equal(
            expectedIndexCount + afterPatches.Length * 3,
            afterRemoval.RoadIndices.Length);
        RoadRendererNodeMarker remainingEndpoint = Assert.Single(afterRemoval.NodeMarkers);
        Assert.Equal(remainingBranchEnd, remainingEndpoint.Position);
        Assert.Equal(
            Settings.RoadTypeStyles.Resolve(RoadType.Street).Width,
            remainingEndpoint.Diameter);
    }

    [Fact]
    public void PurePreparer_BuildsQueryableSemanticJoinWithoutDegreeTwoMarker()
    {
        RoadGraph graph = CreateRightAngleSemanticBoundary(
            streetEdgeID: 3,
            highwayEdgeID: 4);
        GraphNode boundary = Assert.IsType<GraphNode>(graph.GetNode(0));
        GraphEdge street = Assert.Single(
            graph.GetAllEdges(),
            edge => edge.RoadType == RoadType.Street);
        GraphEdge highway = Assert.Single(
            graph.GetAllEdges(),
            edge => edge.RoadType == RoadType.Highway);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());
        var snapshot = new RoadSurfaceSnapshot(Token(), prepared.RoadSurface);
        (int Index, RoadSurfaceTriangle Triangle)[] joins = Enumerable
            .Range(0, prepared.RoadSurface.TriangleCount)
            .Select(index => (
                Index: index,
                Triangle: prepared.RoadSurface.GetPrimitive(index).Triangle))
            .Where(item => item.Triangle.Owner.Kind == RoadSurfaceOwnerKind.SemanticJoin)
            .ToArray();

        Assert.Equal(2, joins.Length);
        Assert.DoesNotContain(
            prepared.NodeMarkers,
            marker => marker.Position == boundary.Position);
        Assert.All(joins, item =>
        {
            RoadSurfaceTriangle triangle = item.Triangle;
            Assert.Equal(boundary.ID, triangle.Owner.NodeID);
            Assert.Equal(EdgeEndpoint.A, triangle.Owner.Endpoint);
            Assert.Equal(
                new RoadLocation(triangle.Owner.EdgeID, 0, 0f),
                triangle.FixedLocation);
            int meshIndex = item.Index * 3;
            Assert.Equal(
                [triangle.A, triangle.B, triangle.C],
                prepared.RoadIndices[meshIndex..(meshIndex + 3)]
                    .Select(index => prepared.RoadVertices[index]));
            Color expectedColor = RoadTypeStyles.Resolve(
                Assert.Single(
                    graph.GetAllEdges(),
                    edge => edge.ID == triangle.Owner.EdgeID).RoadType).Color;
            Assert.All(
                prepared.RoadIndices[meshIndex..(meshIndex + 3)],
                index => Assert.Equal(expectedColor, prepared.RoadColors[index]));
        });

        RoadSurfaceHit streetHit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(-1f, -1f), maxSurfaceDistance: 0f));
        RoadSurfaceHit highwayHit = Assert.IsType<RoadSurfaceHit>(
            snapshot.FindClosest(new Vector2(-3f, -0.5f), maxSurfaceDistance: 0f));
        Assert.Equal(RoadSurfaceOwnerKind.SemanticJoin, streetHit.OwnerKind);
        Assert.Equal(street.ID, streetHit.EdgeID);
        Assert.Equal(new RoadLocation(street.ID, 0, 0f), streetHit.Location);
        Assert.Equal(RoadSurfaceOwnerKind.SemanticJoin, highwayHit.OwnerKind);
        Assert.Equal(highway.ID, highwayHit.EdgeID);
        Assert.Equal(new RoadLocation(highway.ID, 0, 0f), highwayHit.Location);
        Assert.Equal(
            new[] { street.ID, highway.ID }.Order(),
            snapshot.FindEdgeIDsIntersecting(new Rect2(-4f, -2f, 4f, 2f)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PurePreparer_SemanticJoinInputPermutationAndEdgeIDRenamingPreserveMappedOwnership(
        bool sameDirection)
    {
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);
        RoadRendererPreparedLoad first = preparer.Prepare(
            CreateSemanticBoundary(sameDirection, 3, 4).CaptureRevision());
        RoadRendererPreparedLoad permuted = preparer.Prepare(
            CreateSemanticBoundary(
                sameDirection,
                3,
                4,
                reverseEdgeEnumeration: true).CaptureRevision());
        RoadRendererPreparedLoad renamed = preparer.Prepare(
            CreateSemanticBoundary(sameDirection, 4, 3).CaptureRevision());
        RoadSurfaceTriangle[] firstJoins = SurfaceTriangles(
            first,
            RoadSurfaceOwnerKind.SemanticJoin);
        RoadSurfaceTriangle[] permutedJoins = SurfaceTriangles(
            permuted,
            RoadSurfaceOwnerKind.SemanticJoin);
        SemanticJoinOwnership[] firstOwnership = ExtractSemanticJoinOwnership(first);
        SemanticJoinOwnership[] renamedOwnership = ExtractSemanticJoinOwnership(renamed);
        IReadOnlyDictionary<int, int> edgeIDMap = new Dictionary<int, int>
        {
            [3] = 4,
            [4] = 3,
        };

        Assert.Equal(firstJoins, permutedJoins);
        Assert.Equal(
            ExtractSemanticJoinVisual(first),
            ExtractSemanticJoinVisual(renamed));
        Assert.Equal(
            firstOwnership.Select(item => new SemanticJoinOwnership(
                edgeIDMap[item.EdgeID],
                item.NodeID,
                item.Endpoint,
                item.SectorOrder,
                new RoadLocation(
                    edgeIDMap[item.Location.EdgeID],
                    item.Location.GeometryIndex,
                    item.Location.Parameter))),
            renamedOwnership);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PurePreparer_SemanticJoinStoredDirectionPreservesVisualAndMappedOwnership(
        bool sameDirection)
    {
        const int streetEdgeID = 3;
        const int highwayEdgeID = 4;
        RoadGraph forwardGraph = CreateSemanticBoundary(
            sameDirection,
            streetEdgeID,
            highwayEdgeID);
        RoadGraph reversedGraph = CreateSemanticBoundary(
            sameDirection,
            streetEdgeID,
            highwayEdgeID,
            reverseStoredDirections: true);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad forward = preparer.Prepare(forwardGraph.CaptureRevision());
        RoadRendererPreparedLoad reversed = preparer.Prepare(reversedGraph.CaptureRevision());
        SemanticJoinOwnership[] forwardOwnership = ExtractSemanticJoinOwnership(forward);
        SemanticJoinOwnership[] reversedOwnership = ExtractSemanticJoinOwnership(reversed);
        GraphNode forwardBoundary = Assert.Single(
            forwardGraph.GetAllNodes(),
            node => node.Position == Vector2.Zero);
        GraphNode reversedBoundary = Assert.Single(
            reversedGraph.GetAllNodes(),
            node => node.Position == Vector2.Zero);

        Assert.Equal(
            ExtractSemanticJoinVisual(forward),
            ExtractSemanticJoinVisual(reversed));
        Assert.Equal(
            forwardOwnership.Select(item => (
                item.EdgeID,
                NodeID: item.NodeID == forwardBoundary.ID
                    ? (int?)reversedBoundary.ID
                    : item.NodeID,
                item.SectorOrder)),
            reversedOwnership.Select(item => (
                item.EdgeID,
                item.NodeID,
                item.SectorOrder)));
        Assert.All(forwardOwnership, item =>
        {
            Assert.Equal(forwardBoundary.ID, item.NodeID);
            Assert.Equal(EdgeEndpoint.A, item.Endpoint);
            Assert.Equal(item.EdgeID, item.Location.EdgeID);
            Assert.Equal(0, item.Location.GeometryIndex);
            Assert.Equal(RoadGeometrySegment.ParameterStart, item.Location.Parameter);
        });
        Assert.All(reversedOwnership, item =>
        {
            Assert.Equal(reversedBoundary.ID, item.NodeID);
            Assert.Equal(EdgeEndpoint.B, item.Endpoint);
            Assert.Equal(item.EdgeID, item.Location.EdgeID);
            GraphEdge edge = Assert.IsType<GraphEdge>(reversedGraph.GetEdge(item.EdgeID));
            Assert.Equal(edge.GeometrySegments.Count - 1, item.Location.GeometryIndex);
            Assert.Equal(RoadGeometrySegment.ParameterEnd, item.Location.Parameter);
        });
    }

    [Fact]
    public void PurePreparer_UsesFixedBevelFallbackForSameDirectionSemanticBoundary()
    {
        Vector2 boundaryPosition = Vector2.Zero;
        RoadGraph graph = CreateSemanticBoundary(true, 3, 4);
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());
        var snapshot = new RoadSurfaceSnapshot(Token(), prepared.RoadSurface);
        RoadSurfaceTriangle[] joins = Enumerable
            .Range(0, prepared.RoadSurface.TriangleCount)
            .Select(index => prepared.RoadSurface.GetPrimitive(index).Triangle)
            .Where(triangle => triangle.Owner.Kind == RoadSurfaceOwnerKind.SemanticJoin)
            .ToArray();

        Assert.Equal(2, joins.Length);
        Assert.Equal([0, 1], joins.Select(triangle => triangle.Owner.SectorOrder));
        Assert.All(joins, triangle => Assert.True(
            triangle.A.IsFinite() && triangle.B.IsFinite() && triangle.C.IsFinite()));
        Assert.Equal(
            RoadTypeStyles.Resolve(RoadType.Highway).Width * 0.5f,
            joins.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C })
                .Max(point => point.DistanceTo(boundaryPosition)),
            precision: 5);
        Assert.Equal(
            RoadSurfaceOwnerKind.SemanticJoin,
            Assert.IsType<RoadSurfaceHit>(snapshot.FindClosest(
                new Vector2(-1f, 1f),
                maxSurfaceDistance: 0f)).OwnerKind);
        Assert.DoesNotContain(
            prepared.NodeMarkers,
            marker => marker.Position == boundaryPosition);
    }

    [Fact]
    public void PurePreparer_OppositeSemanticBoundaryNeedsNoExtraPrimitiveOrMarker()
    {
        Vector2 boundaryPosition = Vector2.Zero;
        var graph = RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
            5,
            [
                new PreparedRoadNode(0, boundaryPosition),
                new PreparedRoadNode(1, new Vector2(10f, 0f)),
                new PreparedRoadNode(2, new Vector2(-10f, 0f)),
            ],
            [
                new PreparedRoadEdge(
                    RoadType.Street,
                    3,
                    0,
                    1,
                    [new LineRoadGeometrySegment(boundaryPosition, new Vector2(10f, 0f))]),
                new PreparedRoadEdge(
                    RoadType.Highway,
                    4,
                    0,
                    2,
                    [new LineRoadGeometrySegment(boundaryPosition, new Vector2(-10f, 0f))]),
            ]));
        var preparer = new RoadRenderer.RoadRendererLoadPreparer(Settings);

        RoadRendererPreparedLoad prepared = preparer.Prepare(graph.CaptureRevision());

        Assert.DoesNotContain(
            Enumerable.Range(0, prepared.RoadSurface.TriangleCount)
                .Select(index => prepared.RoadSurface.GetPrimitive(index).Triangle),
            triangle => triangle.Owner.Kind == RoadSurfaceOwnerKind.SemanticJoin);
        Assert.DoesNotContain(
            prepared.NodeMarkers,
            marker => marker.Position == boundaryPosition);
    }

    private static RoadGraph CreateRightAngleSemanticBoundary(
        int streetEdgeID,
        int highwayEdgeID) =>
        CreateSemanticBoundary(
            false,
            streetEdgeID,
            highwayEdgeID);

    private static RoadGraph CreateTerminalCapFixture(
        int streetEdgeID,
        int highwayEdgeID,
        bool reverseStoredDirections = false,
        bool reverseEdgeEnumeration = false)
    {
        Vector2 streetStart = Vector2.Zero;
        Vector2 streetMiddle = new(4f, 2f);
        Vector2 streetEnd = new(10f, 0f);
        Vector2 highwayStart = new(0f, 20f);
        Vector2 highwayMiddle = new(6f, 16f);
        Vector2 highwayEnd = new(12f, 20f);
        IReadOnlyList<RoadGeometrySegment> streetGeometry =
        [
            new LineRoadGeometrySegment(streetStart, streetMiddle),
            new LineRoadGeometrySegment(streetMiddle, streetEnd),
        ];
        IReadOnlyList<RoadGeometrySegment> highwayGeometry =
        [
            new LineRoadGeometrySegment(highwayStart, highwayMiddle),
            new LineRoadGeometrySegment(highwayMiddle, highwayEnd),
        ];
        PreparedRoadNode[] nodes = reverseStoredDirections
            ?
            [
                new PreparedRoadNode(4, streetEnd),
                new PreparedRoadNode(5, streetStart),
                new PreparedRoadNode(6, highwayEnd),
                new PreparedRoadNode(7, highwayStart),
            ]
            :
            [
                new PreparedRoadNode(0, streetStart),
                new PreparedRoadNode(1, streetEnd),
                new PreparedRoadNode(2, highwayStart),
                new PreparedRoadNode(3, highwayEnd),
            ];
        var street = new PreparedRoadEdge(
            RoadType.Street,
            streetEdgeID,
            reverseStoredDirections ? 4 : 0,
            reverseStoredDirections ? 5 : 1,
            reverseStoredDirections
                ? RoadGeometryDirection.ReverseChain(streetGeometry)
                : streetGeometry);
        var highway = new PreparedRoadEdge(
            RoadType.Highway,
            highwayEdgeID,
            reverseStoredDirections ? 6 : 2,
            reverseStoredDirections ? 7 : 3,
            reverseStoredDirections
                ? RoadGeometryDirection.ReverseChain(highwayGeometry)
                : highwayGeometry);
        PreparedRoadEdge[] edges = reverseEdgeEnumeration
            ? [highway, street]
            : [street, highway];

        return RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
            Math.Max(
                Math.Max(streetEdgeID, highwayEdgeID),
                nodes.Max(node => node.ID)) + 1,
            nodes,
            edges));
    }

    private static RoadGraph CreateSemanticBoundary(
        bool sameDirection,
        int streetEdgeID,
        int highwayEdgeID,
        bool reverseStoredDirections = false,
        bool reverseEdgeEnumeration = false)
    {
        Vector2 boundaryPosition = Vector2.Zero;
        int boundaryNodeID = reverseStoredDirections
            ? Math.Max(streetEdgeID, highwayEdgeID) + 1
            : 0;
        Vector2 streetEnd = sameDirection
            ? new Vector2(10f, 5f)
            : new Vector2(10f, 0f);
        Vector2 highwayEnd = sameDirection
            ? new Vector2(10f, -5f)
            : new Vector2(0f, 10f);
        IReadOnlyList<RoadGeometrySegment> streetGeometry = sameDirection
            ?
            [
                new LineRoadGeometrySegment(boundaryPosition, new Vector2(2f, 0f)),
                new LineRoadGeometrySegment(new Vector2(2f, 0f), streetEnd),
            ]
            : [new LineRoadGeometrySegment(boundaryPosition, streetEnd)];
        IReadOnlyList<RoadGeometrySegment> highwayGeometry = sameDirection
            ?
            [
                new LineRoadGeometrySegment(boundaryPosition, new Vector2(2f, 0f)),
                new LineRoadGeometrySegment(new Vector2(2f, 0f), highwayEnd),
            ]
            : [new LineRoadGeometrySegment(boundaryPosition, highwayEnd)];
        var street = new PreparedRoadEdge(
            RoadType.Street,
            streetEdgeID,
            reverseStoredDirections ? 1 : 0,
            reverseStoredDirections ? boundaryNodeID : 1,
            reverseStoredDirections
                ? RoadGeometryDirection.ReverseChain(streetGeometry)
                : streetGeometry);
        var highway = new PreparedRoadEdge(
            RoadType.Highway,
            highwayEdgeID,
            reverseStoredDirections ? 2 : 0,
            reverseStoredDirections ? boundaryNodeID : 2,
            reverseStoredDirections
                ? RoadGeometryDirection.ReverseChain(highwayGeometry)
                : highwayGeometry);
        PreparedRoadEdge[] edges = reverseEdgeEnumeration
            ? [highway, street]
            : [street, highway];

        return RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
            Math.Max(
                Math.Max(streetEdgeID, highwayEdgeID),
                boundaryNodeID) + 1,
            [
                new PreparedRoadNode(boundaryNodeID, boundaryPosition),
                new PreparedRoadNode(1, streetEnd),
                new PreparedRoadNode(2, highwayEnd),
            ],
            edges));
    }

    private static RoadGraph CreateAcuteJunction(bool reverseEdges)
    {
        Vector2[] endpoints =
        [
            new Vector2(20f, 0f),
            new Vector2(20f, 4f),
            new Vector2(-20f, 0f),
        ];
        RoadType[] roadTypes =
        [
            RoadType.Dirt,
            RoadType.Highway,
            RoadType.Street,
        ];
        int junctionNodeID = reverseEdges ? 3 : 0;
        int[] endpointNodeIDs = reverseEdges ? [0, 1, 2] : [1, 2, 3];
        var edges = new PreparedRoadEdge[endpoints.Length];
        for (int index = 0; index < endpoints.Length; index++)
        {
            int endpointNodeID = endpointNodeIDs[index];
            Vector2 endpoint = endpoints[index];
            edges[index] = reverseEdges
                ? new PreparedRoadEdge(
                    roadTypes[index],
                    10 + index,
                    endpointNodeID,
                    junctionNodeID,
                    [new LineRoadGeometrySegment(endpoint, Vector2.Zero)])
                : new PreparedRoadEdge(
                    roadTypes[index],
                    10 + index,
                    junctionNodeID,
                    endpointNodeID,
                    [new LineRoadGeometrySegment(Vector2.Zero, endpoint)]);
        }

        PreparedRoadNode[] nodes = reverseEdges
            ?
            [
                new PreparedRoadNode(0, endpoints[0]),
                new PreparedRoadNode(1, endpoints[1]),
                new PreparedRoadNode(2, endpoints[2]),
                new PreparedRoadNode(3, Vector2.Zero),
            ]
            :
            [
                new PreparedRoadNode(0, Vector2.Zero),
                new PreparedRoadNode(1, endpoints[0]),
                new PreparedRoadNode(2, endpoints[1]),
                new PreparedRoadNode(3, endpoints[2]),
            ];

        return RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
            13,
            nodes,
            edges));
    }

    private static (
        Vector2 A,
        Vector2 B,
        Vector2 C,
        Vector2 CenterlineStart,
        Vector2 CenterlineEnd,
        Color Color,
        int Sector)[]
        ExtractSemanticJoinVisual(RoadRendererPreparedLoad prepared) =>
        Enumerable.Range(0, prepared.RoadSurface.TriangleCount)
            .Select(index => (
                Index: index,
                Triangle: prepared.RoadSurface.GetPrimitive(index).Triangle))
            .Where(item => item.Triangle.Owner.Kind == RoadSurfaceOwnerKind.SemanticJoin)
            .Select(item => (
                item.Triangle.A,
                item.Triangle.B,
                item.Triangle.C,
                item.Triangle.CenterlineStart,
                item.Triangle.CenterlineEnd,
                prepared.RoadColors[prepared.RoadIndices[item.Index * 3]],
                item.Triangle.Owner.SectorOrder))
            .ToArray();

    private static SemanticJoinOwnership[] ExtractSemanticJoinOwnership(
        RoadRendererPreparedLoad prepared) =>
        SurfaceTriangles(prepared, RoadSurfaceOwnerKind.SemanticJoin)
            .Select(triangle => new SemanticJoinOwnership(
                triangle.Owner.EdgeID,
                triangle.Owner.NodeID,
                triangle.Owner.Endpoint,
                triangle.Owner.SectorOrder,
                Assert.IsType<RoadLocation>(triangle.FixedLocation)))
            .ToArray();

    private static TerminalCapVisual[] ExtractTerminalCapVisual(
        RoadRendererPreparedLoad prepared) =>
        SurfaceDiscs(prepared)
            .Select(disc =>
            {
                RoadRendererNodeMarker marker = Assert.Single(
                    prepared.NodeMarkers,
                    candidate => candidate.Position == disc.Center);
                return new TerminalCapVisual(
                    disc.Center,
                    disc.Radius,
                    disc.CenterlineStart,
                    disc.CenterlineEnd,
                    marker.Diameter,
                    marker.Color);
            })
            .OrderBy(item => item.Center.X)
            .ThenBy(item => item.Center.Y)
            .ToArray();

    private static TerminalCapOwnership[] ExtractTerminalCapOwnership(
        RoadRendererPreparedLoad prepared) =>
        SurfaceDiscs(prepared)
            .Select(disc => new TerminalCapOwnership(
                disc.Center,
                disc.Owner.EdgeID,
                Assert.IsType<int>(disc.Owner.NodeID),
                Assert.IsType<EdgeEndpoint>(disc.Owner.Endpoint),
                disc.Owner.SectorOrder,
                Assert.IsType<RoadLocation>(disc.Location)))
            .OrderBy(item => item.Center.X)
            .ThenBy(item => item.Center.Y)
            .ToArray();

    private static (
        Vector2 A,
        Vector2 B,
        Vector2 C,
        Vector2 CenterlineStart,
        Vector2 CenterlineEnd,
        Color Color,
        int Sector)[] ExtractJunctionPatchVisual(RoadRendererPreparedLoad prepared) =>
        Enumerable.Range(0, prepared.RoadSurface.TriangleCount)
            .Select(index => (
                Index: index,
                Triangle: prepared.RoadSurface.GetPrimitive(index).Triangle))
            .Where(item => item.Triangle.Owner.Kind == RoadSurfaceOwnerKind.JunctionPatch)
            .Select(item => (
                item.Triangle.A,
                item.Triangle.B,
                item.Triangle.C,
                item.Triangle.CenterlineStart,
                item.Triangle.CenterlineEnd,
                prepared.RoadColors[prepared.RoadIndices[item.Index * 3]],
                item.Triangle.Owner.SectorOrder))
            .ToArray();

    private static RoadSurfaceTriangle[] SurfaceTriangles(
        RoadRendererPreparedLoad prepared,
        RoadSurfaceOwnerKind ownerKind) =>
        Enumerable.Range(0, prepared.RoadSurface.TriangleCount)
            .Select(index => prepared.RoadSurface.GetPrimitive(index).Triangle)
            .Where(triangle => triangle.Owner.Kind == ownerKind)
            .ToArray();

    private static RoadSurfaceDisc[] SurfaceDiscs(RoadRendererPreparedLoad prepared) =>
        Enumerable.Range(prepared.RoadSurface.TriangleCount, prepared.RoadSurface.DiscCount)
            .Select(index => prepared.RoadSurface.GetPrimitive(index).Disc)
            .ToArray();

    private static RoadTypeStyleDefinition Style(
        RoadType roadType,
        string displayName,
        string color,
        float width) =>
        new(roadType, displayName, new Color(color), width);

    private static RoadRenderToken Token() => new(
        SceneGeneration: 1,
        GraphFacadeID: 2,
        GraphFacadeGeneration: 3,
        ChangeSequence: 4,
        RoadStyleRevision: 5,
        RenderRequestID: 6);

    private readonly record struct SemanticJoinOwnership(
        int EdgeID,
        int? NodeID,
        EdgeEndpoint? Endpoint,
        int SectorOrder,
        RoadLocation Location);

    private readonly record struct TerminalCapVisual(
        Vector2 Center,
        float Radius,
        Vector2 CenterlineStart,
        Vector2 CenterlineEnd,
        float Diameter,
        Color Color);

    private readonly record struct TerminalCapOwnership(
        Vector2 Center,
        int EdgeID,
        int NodeID,
        EdgeEndpoint Endpoint,
        int SectorOrder,
        RoadLocation Location);
}
