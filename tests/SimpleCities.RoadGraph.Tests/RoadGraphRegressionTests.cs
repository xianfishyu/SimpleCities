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
    }

    [Fact]
    public void RemoveRoadGroup_CrossingRoad_DoesNotMergeRemainingSegments()
    {
        var graph = new RoadGraph();
        int horizontalGroupID = graph.AddRoad(new Vector2(-100, 0), new Vector2(100, 0), []);
        int verticalGroupID = graph.AddRoad(new Vector2(0, -100), new Vector2(0, 100), []);

        Assert.True(graph.RemoveRoadGroup(verticalGroupID));

        var crossing = graph.FindClosestNode(Vector2.Zero, 0.01f);
        Assert.NotNull(crossing);
        Assert.Equal(2, crossing!.EdgeCount);
        Assert.Equal(2, graph.GetAllEdges().Count());
        Assert.NotNull(graph.GetGroup(horizontalGroupID));
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

        Assert.True(graph.RemoveEdge(edgeToRemove));

        var crossing = graph.FindClosestNode(Vector2.Zero, 0.01f);
        Assert.NotNull(crossing);
        Assert.Equal(3, crossing!.EdgeCount);
        Assert.Equal(3, graph.GetAllEdges().Count());
        Assert.Equal(2, Assert.IsType<RoadGroup>(graph.GetGroup(horizontalGroupID)).EdgeIDs.Count);
    }
}
