using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphRegressionTests
{
    [Fact]
    public void AddRoad_CollinearRoadsFromSeparateOperations_PreserveBothGroupsAndTypes()
    {
        var graph = new RoadGraph();

        int firstGroupID = graph.AddRoad(new Vector2(-100, 0), Vector2.Zero, [], RoadType.Dirt);
        int secondGroupID = graph.AddRoad(Vector2.Zero, new Vector2(100, 0), [], RoadType.Highway);

        var firstGroup = graph.GetGroup(firstGroupID);
        var secondGroup = graph.GetGroup(secondGroupID);

        Assert.NotNull(firstGroup);
        Assert.NotNull(secondGroup);
        Assert.Equal(RoadType.Dirt, firstGroup!.Type);
        Assert.Equal(RoadType.Highway, secondGroup!.Type);
        Assert.Equal(2, graph.GetAllGroups().Count());
    }

    [Fact]
    public void AddRoad_CollinearSameTypeRoadsFromSeparateOperations_PreserveBothGroups()
    {
        var graph = new RoadGraph();

        int firstGroupID = graph.AddRoad(new Vector2(-100, 0), Vector2.Zero, [], RoadType.Street);
        int secondGroupID = graph.AddRoad(Vector2.Zero, new Vector2(100, 0), [], RoadType.Street);

        Assert.NotEqual(firstGroupID, secondGroupID);
        Assert.Single(Assert.IsType<RoadGroup>(graph.GetGroup(firstGroupID)).EdgeIDs);
        Assert.Single(Assert.IsType<RoadGroup>(graph.GetGroup(secondGroupID)).EdgeIDs);
        Assert.Equal(2, graph.GetAllEdges().Count());
        Assert.Equal(2, graph.GetAllGroups().Count());
    }

    [Fact]
    public void FindClosestEdge_LongStraightEdge_HitsItsMiddle()
    {
        var graph = new RoadGraph();
        graph.AddRoad(new Vector2(-500, 0), new Vector2(500, 0), []);
        var edge = Assert.Single(graph.GetAllEdges());

        var closest = graph.FindClosestEdge(Vector2.Zero, 1f);

        Assert.NotNull(closest);
        Assert.Equal(edge.ID, closest!.ID);
    }

    [Fact]
    public void FindClosestEdge_EdgeAtRadiusBoundary_IsIncluded()
    {
        var graph = new RoadGraph();
        graph.AddRoad(new Vector2(-10, 0), new Vector2(10, 0), []);

        var closest = graph.FindClosestEdge(new Vector2(0, 1), 1f);

        Assert.NotNull(closest);
    }

    [Fact]
    public void FindClosestEdge_LongDiagonalEdge_HitsItsMiddle()
    {
        var graph = new RoadGraph();
        graph.AddRoad(new Vector2(-500, -500), new Vector2(500, 500), []);
        var edge = Assert.Single(graph.GetAllEdges());

        var closest = graph.FindClosestEdge(new Vector2(0, 0.5f), 1f);

        Assert.NotNull(closest);
        Assert.Equal(edge.ID, closest!.ID);
    }

    [Fact]
    public void FindClosestEdge_ChoosesTheGeometricallyNearestCandidate()
    {
        var graph = new RoadGraph();
        graph.AddRoad(new Vector2(-100, 0), new Vector2(100, 0), []);
        graph.AddRoad(new Vector2(-100, 3), new Vector2(100, 3), []);
        var upperEdge = graph.GetAllEdges().Single(edge => edge.GetFullPath(graph.GetNode)[0].Y == 3f);

        var closest = graph.FindClosestEdge(new Vector2(0, 2), 3f);

        Assert.NotNull(closest);
        Assert.Equal(upperEdge.ID, closest!.ID);
    }

    [Fact]
    public void FindClosestEdge_OutsideRadius_ReturnsNull()
    {
        var graph = new RoadGraph();
        graph.AddRoad(new Vector2(-10, 0), new Vector2(10, 0), []);

        var closest = graph.FindClosestEdge(new Vector2(0, 2), 1f);

        Assert.Null(closest);
    }

    [Fact]
    public void AddRoad_CrossingLongUnsegmentedEdge_CreatesConnectedIntersectionNode()
    {
        var graph = new RoadGraph();
        graph.AddRoad(new Vector2(-500, 0), new Vector2(500, 0), []);

        graph.AddRoad(new Vector2(0, -10), new Vector2(0, 10), []);

        var crossing = graph.FindClosestNode(Vector2.Zero, 0.01f);
        Assert.NotNull(crossing);
        Assert.Equal(4, crossing!.EdgeCount);
        Assert.Equal(4, graph.GetAllEdges().Count());
        Assert.Single(graph.GetAllNodes(), node => node.Position.DistanceSquaredTo(Vector2.Zero) < 1e-4f);
        AssertGraphInvariants(graph);
    }

    [Fact]
    public void AddRoad_CrossingDiagonalRoads_CreatesOneFourWayIntersection()
    {
        var graph = new RoadGraph();
        graph.AddRoad(new Vector2(-100, -100), new Vector2(100, 100), []);

        graph.AddRoad(new Vector2(-100, 100), new Vector2(100, -100), []);

        var crossing = graph.FindClosestNode(Vector2.Zero, 0.01f);
        Assert.NotNull(crossing);
        Assert.Equal(4, crossing!.EdgeCount);
        Assert.Equal(4, graph.GetAllEdges().Count());
        Assert.Single(graph.GetAllNodes(), node => node.Position.DistanceSquaredTo(Vector2.Zero) < 1e-4f);
        AssertGraphInvariants(graph);
    }

    [Fact]
    public void AddRoad_CrossingExistingWaypoint_SplitsTheExistingEdgeAtTheWaypoint()
    {
        var graph = new RoadGraph();
        graph.AddRoad(new Vector2(-100, 0), new Vector2(100, 0), [Vector2.Zero]);

        graph.AddRoad(new Vector2(0, -100), new Vector2(0, 100), []);

        var crossing = graph.FindClosestNode(Vector2.Zero, 0.01f);
        Assert.NotNull(crossing);
        Assert.Equal(4, crossing!.EdgeCount);
        Assert.Equal(4, graph.GetAllEdges().Count());
        Assert.Single(graph.GetAllNodes(), node => node.Position.DistanceSquaredTo(Vector2.Zero) < 1e-4f);
        AssertGraphInvariants(graph);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.005f)]
    public void AddRoad_EndpointWithinGeometryEpsilon_DoesNotSplitExistingEdge(float endpointOffset)
    {
        var graph = new RoadGraph();
        int existingGroupID = graph.AddRoad(Vector2.Zero, new Vector2(10, 0), []);
        int existingEdgeID = Assert.Single(
            Assert.IsType<RoadGroup>(graph.GetGroup(existingGroupID)).EdgeIDs);

        graph.AddRoad(new Vector2(0, endpointOffset), new Vector2(1, -1), []);

        Assert.NotNull(graph.GetEdge(existingEdgeID));
        Assert.Single(Assert.IsType<RoadGroup>(graph.GetGroup(existingGroupID)).EdgeIDs);
        AssertGraphInvariants(graph);
    }

    [Fact]
    public void AddRoad_IntersectionOutsideEndpointEpsilon_CreatesFourWayNode()
    {
        var graph = new RoadGraph();
        int existingGroupID = graph.AddRoad(Vector2.Zero, new Vector2(10, 0), []);
        int existingEdgeID = Assert.Single(
            Assert.IsType<RoadGroup>(graph.GetGroup(existingGroupID)).EdgeIDs);

        graph.AddRoad(new Vector2(0, 1), new Vector2(2, -1), []);

        Assert.Null(graph.GetEdge(existingEdgeID));
        var crossing = graph.FindClosestNode(new Vector2(1, 0), 0.001f);
        Assert.NotNull(crossing);
        Assert.Equal(4, crossing!.EdgeCount);
        AssertGraphInvariants(graph);
    }

    [Theory]
    [InlineData(8f)]
    [InlineData(64f)]
    [InlineData(256f)]
    public void AddRoad_CrossingLongEdge_RemainsConnectedAcrossIndexBucketSizes(float bucketSize)
    {
        var graph = new RoadGraph(bucketSize);
        graph.AddRoad(new Vector2(-500, 0), new Vector2(500, 0), []);

        graph.AddRoad(new Vector2(0, -10), new Vector2(0, 10), []);

        var crossing = graph.FindClosestNode(Vector2.Zero, 0.01f);
        Assert.NotNull(crossing);
        Assert.Equal(4, crossing!.EdgeCount);
        AssertGraphInvariants(graph);
    }

    [Fact]
    public void RemoveRoadGroup_CrossingRoad_DoesNotMergeRemainingSegments()
    {
        var graph = new RoadGraph();
        int horizontalGroupID = graph.AddRoad(new Vector2(-100, 0), new Vector2(100, 0), []);
        int verticalGroupID = graph.AddRoad(new Vector2(0, -100), new Vector2(0, 100), []);
        var edgeIDsBeforeRemoval = graph.GetAllEdges().Select(edge => edge.ID).ToHashSet();
        var removedEdgeIDs = Assert.IsType<RoadGroup>(graph.GetGroup(verticalGroupID)).EdgeIDs.ToHashSet();

        Assert.True(graph.RemoveRoadGroup(verticalGroupID));

        var remainingEdgeIDs = graph.GetAllEdges().Select(edge => edge.ID).ToHashSet();
        var crossing = graph.FindClosestNode(Vector2.Zero, 0.01f);
        Assert.NotNull(crossing);
        Assert.Equal(2, crossing!.EdgeCount);
        Assert.Equal(2, graph.GetAllEdges().Count());
        Assert.NotNull(graph.GetGroup(horizontalGroupID));
        Assert.Null(graph.GetGroup(verticalGroupID));
        Assert.All(remainingEdgeIDs, edgeID => Assert.Contains(edgeID, edgeIDsBeforeRemoval));
        Assert.All(removedEdgeIDs, edgeID => Assert.DoesNotContain(edgeID, remainingEdgeIDs));
        AssertGraphInvariants(graph);
    }

    [Fact]
    public void AddRoad_ArbitraryAngleCollinearSegments_MergeWithinTheSameGroup()
    {
        var graph = new RoadGraph();

        graph.AddRoad(
            Vector2.Zero,
            new Vector2(60, 40),
            [new Vector2(30, 20)]);

        var edge = Assert.Single(graph.GetAllEdges());
        Assert.Equal(new Vector2(30, 20), Assert.Single(edge.Points));
    }

    [Fact]
    public void AddRoad_SlightlyBentSegments_DoNotMergeAtTheBend()
    {
        var graph = new RoadGraph();

        graph.AddRoad(
            Vector2.Zero,
            new Vector2(20, 0.01f),
            [new Vector2(10, 0)]);

        var bend = graph.FindClosestNode(new Vector2(10, 0), 0.01f);
        Assert.NotNull(bend);
        Assert.Equal(2, bend!.EdgeCount);
        Assert.Equal(2, graph.GetAllEdges().Count());
    }

    [Fact]
    public void GraphEdgePoints_CannotMutateGraphStateOutsideRoadGraphApi()
    {
        var graph = new RoadGraph();
        graph.AddRoad(
            new Vector2(-200, 0),
            new Vector2(200, 0),
            [new Vector2(-100, 0), Vector2.Zero, new Vector2(100, 0)]);
        var edge = Assert.Single(graph.GetAllEdges());
        string stateBeforeMutation = SaveJson.Serialize(graph.CaptureState());

        var exposedPoints = edge.Points;
        exposedPoints[0] = new Vector2(0, 100);

        Assert.Equal(stateBeforeMutation, SaveJson.Serialize(graph.CaptureState()));
    }

    [Fact]
    public void RemoveEdge_CrossingRoad_DoesNotMergeTheRemainingSegments()
    {
        var graph = new RoadGraph();
        int horizontalGroupID = graph.AddRoad(new Vector2(-100, 0), new Vector2(100, 0), []);
        int verticalGroupID = graph.AddRoad(new Vector2(0, -100), new Vector2(0, 100), []);
        var verticalGroup = Assert.IsType<RoadGroup>(graph.GetGroup(verticalGroupID));
        int edgeToRemove = verticalGroup.EdgeIDs.First();
        var edgeIDsBeforeRemoval = graph.GetAllEdges().Select(edge => edge.ID).ToHashSet();

        Assert.True(graph.RemoveEdge(edgeToRemove));

        var remainingEdgeIDs = graph.GetAllEdges().Select(edge => edge.ID).ToHashSet();
        var crossing = graph.FindClosestNode(Vector2.Zero, 0.01f);
        Assert.NotNull(crossing);
        Assert.Equal(3, crossing!.EdgeCount);
        Assert.Equal(3, graph.GetAllEdges().Count());
        Assert.Equal(2, Assert.IsType<RoadGroup>(graph.GetGroup(horizontalGroupID)).EdgeIDs.Count);
        Assert.DoesNotContain(edgeToRemove, remainingEdgeIDs);
        Assert.All(remainingEdgeIDs, edgeID => Assert.Contains(edgeID, edgeIDsBeforeRemoval));
        AssertGraphInvariants(graph);
    }

    private static void AssertGraphInvariants(RoadGraph graph)
    {
        var nodes = graph.GetAllNodes().ToDictionary(node => node.ID);
        var edges = graph.GetAllEdges().ToDictionary(edge => edge.ID);
        var groups = graph.GetAllGroups().ToDictionary(group => group.ID);

        Assert.All(nodes.Values, node =>
        {
            Assert.True(node.EdgeCount > 0, $"Node {node.ID} is isolated.");
            var indexedNode = graph.FindClosestNode(node.Position, 0.001f);
            Assert.NotNull(indexedNode);
            Assert.Equal(node.ID, indexedNode!.ID);

            foreach (var edgeRef in node.Edges)
            {
                Assert.True(edges.TryGetValue(edgeRef.EdgeID, out var edge),
                    $"Node {node.ID} references missing edge {edgeRef.EdgeID}.");
                Assert.True(nodes.ContainsKey(edgeRef.NeighborNodeID),
                    $"Node {node.ID} references missing neighbor {edgeRef.NeighborNodeID}.");
                Assert.True(
                    (edge!.NodeA == node.ID && edge.NodeB == edgeRef.NeighborNodeID) ||
                    (edge.NodeB == node.ID && edge.NodeA == edgeRef.NeighborNodeID),
                    $"Node {node.ID} has an inconsistent reference to edge {edge.ID}.");
            }
        });

        Assert.All(edges.Values, edge =>
        {
            Assert.True(nodes.TryGetValue(edge.NodeA, out var nodeA),
                $"Edge {edge.ID} has missing endpoint {edge.NodeA}.");
            Assert.True(nodes.TryGetValue(edge.NodeB, out var nodeB),
                $"Edge {edge.ID} has missing endpoint {edge.NodeB}.");
            Assert.Single(nodeA!.Edges, edgeRef =>
                edgeRef.EdgeID == edge.ID && edgeRef.NeighborNodeID == edge.NodeB);
            Assert.Single(nodeB!.Edges, edgeRef =>
                edgeRef.EdgeID == edge.ID && edgeRef.NeighborNodeID == edge.NodeA);
            Assert.True(groups.TryGetValue(edge.GroupID, out var group),
                $"Edge {edge.ID} references missing group {edge.GroupID}.");
            Assert.Contains(edge.ID, group!.EdgeIDs);

            var path = edge.GetFullPath(graph.GetNode);
            float longestSegmentLengthSquared = -1f;
            Vector2 spatialProbe = default;
            for (int i = 0; i < path.Length - 1; i++)
            {
                float segmentLengthSquared = path[i].DistanceSquaredTo(path[i + 1]);
                if (segmentLengthSquared <= longestSegmentLengthSquared) continue;
                longestSegmentLengthSquared = segmentLengthSquared;
                spatialProbe = (path[i] + path[i + 1]) / 2f;
            }

            Assert.True(longestSegmentLengthSquared > 0f, $"Edge {edge.ID} has no non-degenerate segment.");
            var indexedEdge = graph.FindClosestEdge(spatialProbe, 0.001f);
            Assert.NotNull(indexedEdge);
            Assert.Equal(edge.ID, indexedEdge!.ID);
        });

        Assert.All(groups.Values, group =>
        {
            Assert.False(group.IsEmpty, $"Group {group.ID} is empty.");
            Assert.All(group.EdgeIDs, edgeID =>
            {
                Assert.True(edges.TryGetValue(edgeID, out var edge),
                    $"Group {group.ID} references missing edge {edgeID}.");
                Assert.Equal(group.ID, edge!.GroupID);
            });
        });
    }
}
