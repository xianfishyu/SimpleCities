using Godot;

public sealed class RoadGraphIncidenceV3Tests
{
    [Fact]
    public void PreparedSelfLoopRegistersAAndBIncidencesWithoutJunctionMarker()
    {
        RoadGraph graph = CreateSelfLoopGraph();
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        GraphNode seam = Assert.Single(graph.GetAllNodes());

        Assert.Equal(seam.ID, edge.NodeA);
        Assert.Equal(seam.ID, edge.NodeB);
        Assert.Equal(2, seam.IncidenceCount);
        Assert.Equal(2, seam.Degree);
        Assert.Equal(1, seam.IncidentEdgeCount);
        Assert.Collection(
            seam.Incidences.OrderBy(incidence => incidence.Endpoint),
            incidence => Assert.Equal(
                new EdgeIncidence(edge.ID, EdgeEndpoint.A, seam.ID), incidence),
            incidence => Assert.Equal(
                new EdgeIncidence(edge.ID, EdgeEndpoint.B, seam.ID), incidence));
        Assert.Equal([seam.ID], seam.GetNeighborIDs());
        Assert.False(RoadRenderer.IsJunctionNode(graph, seam));
        graph.AssertInvariants();
    }

    [Fact]
    public void RemovingSelfLoopRemovesBothIncidencesAndIsolatedSeam()
    {
        RoadGraph graph = CreateSelfLoopGraph();
        int edgeID = Assert.Single(graph.GetAllEdges()).ID;

        Assert.True(graph.RemoveEdge(edgeID));

        Assert.Empty(graph.GetAllNodes());
        Assert.Empty(graph.GetAllEdges());
        graph.AssertInvariants();
    }

    [Fact]
    public void SelfLoopAndBranchContributeThreeIncidencesAtJunction()
    {
        Vector2 seamPosition = new(5f, 0f);
        var topology = new PreparedRoadGraphTopology(
            6,
            [
                new PreparedRoadNode(0, seamPosition),
                new PreparedRoadNode(3, new Vector2(12f, 0f)),
            ],
            [
                new PreparedRoadEdge(RoadType.Street,
                    1, 0, 0,
                    [new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, Mathf.Tau)]),
                new PreparedRoadEdge(RoadType.Street,
                    4, 0, 3,
                    [new LineRoadGeometrySegment(seamPosition, new Vector2(12f, 0f))]),
            ]);
        RoadGraph graph = RoadGraph.FromPreparedTopology(topology);
        GraphNode junction = Assert.IsType<GraphNode>(graph.GetNode(0));

        Assert.Equal(3, junction.IncidenceCount);
        Assert.Equal(2, junction.IncidentEdgeCount);
        Assert.True(RoadRenderer.IsJunctionNode(graph, junction));
        graph.AssertInvariants();
    }

    [Fact]
    public void PreparedParallelEdgesAreRejectedAsNonCanonicalDegreeTwoBoundaries()
    {
        Vector2 start = Vector2.Zero;
        Vector2 end = new(10f, 0f);
        var topology = new PreparedRoadGraphTopology(
            5,
            [new PreparedRoadNode(0, start), new PreparedRoadNode(1, end)],
            [
                new PreparedRoadEdge(RoadType.Street,
                    2, 0, 1,
                    [new LineRoadGeometrySegment(start, end)]),
                new PreparedRoadEdge(RoadType.Street,
                    3, 0, 1,
                    [new CubicBezierRoadGeometrySegment(
                        start, new Vector2(2f, 4f), new Vector2(8f, 4f), end)]),
            ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => RoadGraph.FromPreparedTopology(topology));

        Assert.Contains("non-canonical same-type degree-2 boundary", exception.Message);
    }

    [Fact]
    public void PreparedNonLoopIsOrientedByNodeIDWithNativeGeometryReversal()
    {
        Vector2 highIDPosition = new(-4f, 1f);
        Vector2 lowIDPosition = new(7f, 3f);
        var topology = new PreparedRoadGraphTopology(
            10,
            [
                new PreparedRoadNode(9, highIDPosition),
                new PreparedRoadNode(3, lowIDPosition),
            ],
            [
                new PreparedRoadEdge(RoadType.Street,
                    4, 9, 3,
                    [new CubicBezierRoadGeometrySegment(
                        highIDPosition,
                        new Vector2(-1f, 8f),
                        new Vector2(5f, -2f),
                        lowIDPosition)]),
            ]);

        RoadGraph graph = RoadGraph.FromPreparedTopology(topology);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        var geometry = Assert.IsType<CubicBezierRoadGeometrySegment>(
            Assert.Single(edge.GeometrySegments));

        Assert.Equal(3, edge.NodeA);
        Assert.Equal(9, edge.NodeB);
        Assert.True(RoadExactPredicates.SameBits(lowIDPosition, geometry.Start));
        Assert.True(RoadExactPredicates.SameBits(highIDPosition, geometry.End));
        Assert.Equal(EdgeEndpoint.A, Assert.Single(graph.GetNode(3)!.Incidences).Endpoint);
        Assert.Equal(EdgeEndpoint.B, Assert.Single(graph.GetNode(9)!.Incidences).Endpoint);
        graph.AssertInvariants();
    }

    [Fact]
    public void PreparedMixedNativeChainRemainsBitwiseContinuousAfterIDReversal()
    {
        var arc = new CircularArcRoadGeometrySegment(
            new Vector2(2f, 1f),
            4f,
            0.37f,
            1.2f);
        var clothoid = new ClothoidRoadGeometrySegment(
            arc.End,
            1.57f,
            0.01f,
            0.04f,
            7f);
        var tail = new CubicBezierRoadGeometrySegment(
            clothoid.End,
            clothoid.End + new Vector2(2f, 1f),
            clothoid.End + new Vector2(4f, -1f),
            clothoid.End + new Vector2(6f, 0f));
        var topology = new PreparedRoadGraphTopology(
            12,
            [
                new PreparedRoadNode(9, arc.Start),
                new PreparedRoadNode(3, tail.End),
            ],
            [new PreparedRoadEdge(RoadType.Street, 4, 9, 3, [arc, clothoid, tail])]);

        RoadGraph graph = RoadGraph.FromPreparedTopology(topology);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());

        Assert.Equal(3, edge.NodeA);
        Assert.Equal(9, edge.NodeB);
        Assert.True(RoadExactPredicates.SameBits(
            graph.GetNode(edge.NodeA)!.Position,
            edge.GeometrySegments[0].Start));
        Assert.True(RoadExactPredicates.SameBits(
            graph.GetNode(edge.NodeB)!.Position,
            edge.GeometrySegments[^1].End));
        for (int index = 1; index < edge.GeometrySegments.Count; index++)
        {
            Assert.True(RoadExactPredicates.SameBits(
                edge.GeometrySegments[index - 1].End,
                edge.GeometrySegments[index].Start));
        }

        RoadGeometrySegment[] restoredGeometry = edge.GeometrySegments
            .Select(segment => Assert.IsAssignableFrom<RoadGeometrySegment>(
                RoadGeometrySerializer.Deserialize(
                    RoadGeometrySerializer.Serialize(segment)).Geometry))
            .ToArray();
        for (int index = 1; index < restoredGeometry.Length; index++)
        {
            Assert.True(RoadExactPredicates.SameBits(
                restoredGeometry[index - 1].End,
                restoredGeometry[index].Start));
        }
    }

    [Fact]
    public void PreparedTopologyRejectsMissingEndpointBeforePublishingGraph()
    {
        var topology = new PreparedRoadGraphTopology(
            4,
            [new PreparedRoadNode(0, Vector2.Zero)],
            [
                new PreparedRoadEdge(RoadType.Street,
                    1,
                    0,
                    3,
                    [new LineRoadGeometrySegment(Vector2.Zero, Vector2.Right)]),
            ]);

        Assert.Throws<ArgumentException>(() => RoadGraph.FromPreparedTopology(topology));
    }

    [Fact]
    public void ImmutableNodeTransformDoesNotMutatePublishedGraphAndRejectsDuplicateEndpointRole()
    {
        RoadGraph graph = CreateSelfLoopGraph();
        GraphNode published = Assert.Single(graph.GetAllNodes());
        GraphEdge edge = Assert.Single(graph.GetAllEdges());

        GraphNode transformed = published.WithRemovedIncidence(
            edge.ID,
            EdgeEndpoint.A,
            out bool removed);
        Assert.True(removed);
        Assert.NotSame(published, transformed);
        Assert.Equal(1, transformed.IncidenceCount);
        Assert.Same(published, graph.GetNode(published.ID));
        Assert.Equal(2, published.IncidenceCount);
        graph.AssertInvariants();

        GraphNode restored = transformed.WithAddedIncidence(
            edge.ID,
            EdgeEndpoint.A,
            transformed.ID);
        Assert.Equal(2, restored.IncidenceCount);
        Assert.Throws<InvalidOperationException>(() =>
            restored.WithAddedIncidence(edge.ID, EdgeEndpoint.A, restored.ID));
    }

    private static RoadGraph CreateSelfLoopGraph()
    {
        Vector2 seam = new(5f, 0f);
        var topology = new PreparedRoadGraphTopology(
            3,
            [new PreparedRoadNode(0, seam)],
            [
                new PreparedRoadEdge(RoadType.Street,
                    1,
                    0,
                    0,
                    [new CircularArcRoadGeometrySegment(Vector2.Zero, 5f, 0f, Mathf.Tau)]),
            ]);
        return RoadGraph.FromPreparedTopology(topology);
    }
}
