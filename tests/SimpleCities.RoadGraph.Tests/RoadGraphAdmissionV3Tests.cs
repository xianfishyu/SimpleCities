using Godot;
using SimpleCities.Tests;

public sealed class RoadGraphAdmissionV3Tests
{
    [Fact]
    public void SubmitPolyline_CoordinateAboveLimitIsRejectedWithoutStateOrEvents()
    {
        var graph = new RoadGraph();
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;
        float outside = MathF.BitIncrement(RoadNumericPolicy.MaximumCoordinateMagnitude);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street,
            [Vector2.Zero, new Vector2(outside, 0f)]);

        Assert.Equal(RoadPathSubmissionError.NumericOutOfRange, result.Error);
        AssertGraphEmpty(graph);
        Assert.Equal(0, graph.NextIDWatermark);
        Assert.Equal(0, changedEvents);
    }

    [Fact]
    public void SubmitPath_ControlPointAboveLimitIsRejectedWithoutStateOrEvents()
    {
        var graph = new RoadGraph();
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;
        float outside = MathF.BitIncrement(RoadNumericPolicy.MaximumCoordinateMagnitude);
        var curve = new CubicBezierRoadGeometrySegment(
            Vector2.Zero,
            new Vector2(outside, 5f),
            new Vector2(10f, 5f),
            new Vector2(20f, 0f));

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([curve]), RoadType.Street));

        Assert.Equal(RoadPathSubmissionError.NumericOutOfRange, result.Error);
        AssertGraphEmpty(graph);
        Assert.Equal(0, graph.NextIDWatermark);
        Assert.Equal(0, changedEvents);
    }

    [Fact]
    public void SubmitPath_NodeCapacityIsRejectedBeforeMutation()
    {
        var capacity = new RoadGraphCapacity
        {
            MaximumNodes = 1,
        };
        var graph = new RoadGraph(capacity);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
        ]), RoadType.Street));

        Assert.Equal(RoadPathSubmissionError.CapacityExceeded, result.Error);
        AssertGraphEmpty(graph);
        Assert.Equal(0, graph.NextIDWatermark);
    }

    [Fact]
    public void SubmitPolyline_SpatialReferenceBudgetIsRejectedBeforeMutation()
    {
        var capacity = new RoadGraphCapacity
        {
            MaximumBuckets = 2,
            MaximumSpatialReferences = 2,
        };
        var graph = new RoadGraph(1f, capacity);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street,
            [Vector2.Zero, new Vector2(10f, 0f)]);

        Assert.Equal(RoadPathSubmissionError.CapacityExceeded, result.Error);
        AssertGraphEmpty(graph);
        Assert.Equal(0, graph.NextIDWatermark);
    }

    [Fact]
    public void SubmitPath_ExhaustedIDSpaceIsRejectedBeforeMutation()
    {
        var graph = new RoadGraph(RoadGraphCapacity.Default, int.MaxValue - 2);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
        ]), RoadType.Street));

        Assert.Equal(RoadPathSubmissionError.CapacityExceeded, result.Error);
        AssertGraphEmpty(graph);
        Assert.Equal(int.MaxValue - 2, graph.NextIDWatermark);
    }

    [Fact]
    public void SubmitPath_CandidateBudgetRejectsCrossingBeforeExistingGraphChanges()
    {
        var capacity = new RoadGraphCapacity
        {
            MaximumMutationCandidates = 1,
        };
        var graph = new RoadGraph(capacity);
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-10f, -2f),
            new Vector2(10f, -2f),
        ]).Success);
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-10f, 2f),
            new Vector2(10f, 2f),
        ]).Success);
        string before = RoadGraphTestCodec.CaptureJson(graph);
        int watermark = graph.NextIDWatermark;
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(new Vector2(0f, -5f), new Vector2(0f, 5f)),
        ]), RoadType.Street));

        Assert.Equal(RoadPathSubmissionError.CapacityExceeded, result.Error);
        Assert.Equal(before, RoadGraphTestCodec.CaptureJson(graph));
        Assert.Equal(watermark, graph.NextIDWatermark);
        Assert.Equal(0, changedEvents);
        graph.AssertInvariants();
    }

    [Fact]
    public void SuccessfulSubmissionStoresCanonicalPositiveZero()
    {
        float negativeZero = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            new Vector2(negativeZero, negativeZero),
            new Vector2(10f, negativeZero),
        ]);

        Assert.True(result.Success);
        Assert.All(graph.GetAllNodes(), node =>
        {
            Assert.True(RoadNumericPolicy.HasCanonicalZero(node.Position.X));
            Assert.True(RoadNumericPolicy.HasCanonicalZero(node.Position.Y));
        });
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.All(edge.GeometrySegments, geometry =>
        {
            Assert.True(RoadNumericPolicy.HasCanonicalZero(geometry.Start.X));
            Assert.True(RoadNumericPolicy.HasCanonicalZero(geometry.Start.Y));
            Assert.True(RoadNumericPolicy.HasCanonicalZero(geometry.End.X));
            Assert.True(RoadNumericPolicy.HasCanonicalZero(geometry.End.Y));
        });
    }

    [Fact]
    public void FindClosestNode_DoesNotTreatApproximatelyEqualDistancesAsATie()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-10f, 0f),
            Vector2.Zero,
        ]).Success);
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-10f, 10f),
            new Vector2(0.75f, 0f),
        ]).Success);
        GraphNode nearer = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position == new Vector2(0.75f, 0f));

        GraphNode? result = graph.FindClosestNode(new Vector2(0.3750001f, 0f), 0.5f);

        Assert.NotNull(result);
        Assert.Equal(nearer.ID, result!.ID);
    }

    [Fact]
    public void SuccessfulSubmissionReportsActualResourcesWithinCapacity()
    {
        var graph = new RoadGraph();

        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new CubicBezierRoadGeometrySegment(
                Vector2.Zero,
                new Vector2(3f, 4f),
                new Vector2(7f, 4f),
                new Vector2(10f, 0f)),
        ]), RoadType.Street)).Success);

        RoadGraphResourceCounts counts = graph.CaptureResourceCounts();
        Assert.Equal(2, counts.Nodes);
        Assert.Equal(1, counts.Edges);
        Assert.Equal(1, counts.GeometrySegments);
        Assert.True(counts.Buckets > 0);
        Assert.True(counts.SpatialReferences >= counts.Nodes + counts.GeometrySegments);
        Assert.Equal(
            RoadGraphCapacityError.None,
            RoadGraphCapacity.Default.Validate(counts));
        graph.AssertInvariants();
    }

    [Fact]
    public void MultiIntersectionSubmission_PerformsOneBatchAdmissionPass()
    {
        var graph = new RoadGraph();
        const int crossingCount = 16;
        for (int index = 0; index < crossingCount; index++)
        {
            float y = index * 4f;
            Assert.True(graph.SubmitPolyline(RoadType.Street, [
                new Vector2(-8f, y),
                new Vector2(8f, y),
            ]).Success);
        }

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            new Vector2(0f, -4f),
            new Vector2(0f, crossingCount * 4f),
        ]);

        Assert.True(result.Success);
        Assert.Equal(1, graph.LastOperationMetrics.MutationAdmissionPassCount);
        graph.AssertInvariants();
    }

    [Fact]
    public void DirectSubdivision_NodeCapacityFailureHasNoSideEffects()
    {
        var graph = new RoadGraph(new RoadGraphCapacity
        {
            MaximumNodes = 2,
        });
        RoadPathSubmissionResult submitted = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
        ]), RoadType.Street));
        int edgeID = Assert.Single(submitted.Changes.CreatedEdgeIDs);
        string before = RoadGraphTestCodec.CaptureJson(graph);
        int watermark = graph.NextIDWatermark;
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        bool result = graph.SplitEdgeAtGeometryParameters(
            edgeID,
            [new EdgeGeometrySplitPoint(0, 0.5f)]);

        Assert.False(result);
        Assert.Equal(before, RoadGraphTestCodec.CaptureJson(graph));
        Assert.Equal(watermark, graph.NextIDWatermark);
        Assert.Equal(0, changedEvents);
    }

    [Fact]
    public void DirectSubdivision_IDExhaustionHasNoSideEffects()
    {
        var graph = new RoadGraph();
        RoadGraphTestCodec.LoadJson(graph, """
        {
          "formatFamily": "simple-cities-v3",
          "payloadType": "road-network",
          "schemaVersion": 1,
          "nextID": 2147483646,
          "nodes": [
            { "id": 0, "x": 0, "y": 0 },
            { "id": 1, "x": 10, "y": 0 }
          ],
          "edges": [
            {
              "id": 2,
              "nodeAID": 0,
              "nodeBID": 1,
              "roadType": "street",
              "geometry": [
                {
                  "version": 1,
                  "kind": "line",
                  "start": { "x": 0, "y": 0 },
                  "end": { "x": 10, "y": 0 }
                }
              ]
            }
          ]
        }
        """);
        string before = RoadGraphTestCodec.CaptureJson(graph);

        bool result = graph.SplitEdgeAtGeometryParameters(
            2,
            [new EdgeGeometrySplitPoint(0, 0.5f)]);

        Assert.False(result);
        Assert.Equal(before, RoadGraphTestCodec.CaptureJson(graph));
        Assert.Equal(2147483646, graph.NextIDWatermark);
    }

    private static void AssertGraphEmpty(RoadGraph graph)
    {
        Assert.Empty(graph.GetAllNodes());
        Assert.Empty(graph.GetAllEdges());
        Assert.Equal(
            new RoadGraphResourceCounts(0, 0, 0, 0, 0, 0),
            graph.CaptureResourceCounts());
    }
}
