using Godot;

public sealed class RoadGraphRoadTypeChangeV3Tests
{
    [Theory]
    [InlineData(RoadType.Dirt)]
    [InlineData(RoadType.Street)]
    [InlineData(RoadType.Arterial)]
    [InlineData(RoadType.Highway)]
    public void ChangeRoadType_ChangesCanonicalEdgeToEveryDomainType(RoadType targetType)
    {
        RoadType initialType = targetType == RoadType.Street
            ? RoadType.Dirt
            : RoadType.Street;
        var graph = new RoadGraph();
        int edgeID = AddLine(graph, initialType, Vector2.Zero, new Vector2(10f, 0f));
        GraphStateToken before = graph.CurrentStateToken;
        RoadGraphRevision beforeRoot = graph.CaptureRevision();
        var events = new List<RoadGraphChangedEvent>();
        graph.GraphChanged += events.Add;

        RoadTypeChangeResult result = graph.ChangeRoadType([edgeID], targetType);

        Assert.True(result.Success, result.Error.ToString());
        Assert.Equal(targetType, Assert.IsType<GraphEdge>(graph.GetEdge(edgeID)).RoadType);
        Assert.Equal([edgeID], result.Changes.UpdatedEdgeIDs);
        Assert.Empty(result.Changes.UpdatedNodeIDs);
        Assert.Empty(result.Changes.CreatedEdgeIDs);
        Assert.Empty(result.Changes.RemovedEdgeIDs);
        Assert.Equal(before.DomainRevisionID + 1, graph.CurrentStateToken.DomainRevisionID);
        Assert.Equal(before.ChangeSequence + 1, graph.CurrentStateToken.ChangeSequence);
        Assert.NotSame(beforeRoot, graph.CaptureRevision());
        Assert.Equal(initialType, beforeRoot.Edges[edgeID].RoadType);
        Assert.Single(events);
        Assert.Same(result.Delta, events[0].Delta);
        graph.AssertInvariants();
    }

    [Fact]
    public void ChangeRoadType_MergesRemovedSemanticBoundaryAndRetainsMinimumEdgeID()
    {
        var graph = new RoadGraph();
        int streetID = AddLine(
            graph,
            RoadType.Street,
            Vector2.Zero,
            new Vector2(10f, 0f));
        int arterialID = AddLine(
            graph,
            RoadType.Arterial,
            new Vector2(10f, 0f),
            new Vector2(20f, 0f));
        int boundaryNodeID = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position == new Vector2(10f, 0f)).ID;

        RoadTypeChangeResult result = graph.ChangeRoadType(
            [arterialID, arterialID],
            RoadType.Street);

        Assert.True(result.Success, result.Error.ToString());
        GraphEdge merged = Assert.Single(graph.GetAllEdges());
        Assert.Equal(Math.Min(streetID, arterialID), merged.ID);
        Assert.Equal(RoadType.Street, merged.RoadType);
        Assert.Null(graph.GetNode(boundaryNodeID));
        Assert.Equal([boundaryNodeID], result.Changes.RemovedNodeIDs);
        Assert.Contains(Math.Max(streetID, arterialID), result.Changes.RemovedEdgeIDs);
        Assert.Contains(Math.Min(streetID, arterialID), result.Changes.UpdatedEdgeIDs);
        Assert.Equal(2, graph.GetAllNodes().Count());
        graph.AssertInvariants();
    }

    [Fact]
    public void ChangeRoadType_RejectsEmptyMissingInvalidAndNoChangesWithoutSideEffects()
    {
        var graph = new RoadGraph();
        int edgeID = AddLine(
            graph,
            RoadType.Street,
            Vector2.Zero,
            new Vector2(10f, 0f));
        RoadGraphRevision root = graph.CaptureRevision();
        GraphStateToken token = graph.CurrentStateToken;
        int watermark = graph.NextIDWatermark;
        int eventCount = 0;
        graph.GraphChanged += _ => eventCount++;

        AssertRejected(null, RoadType.Highway, RoadTypeChangeError.EmptySelection);
        AssertRejected([], RoadType.Highway, RoadTypeChangeError.EmptySelection);
        AssertRejected([edgeID, int.MaxValue], RoadType.Highway, RoadTypeChangeError.MissingEdge);
        AssertRejected([edgeID], (RoadType)99, RoadTypeChangeError.InvalidRoadType);
        AssertRejected([edgeID, edgeID], RoadType.Street, RoadTypeChangeError.NoChanges);

        void AssertRejected(
            IEnumerable<int>? ids,
            RoadType type,
            RoadTypeChangeError expected)
        {
            RoadTypeChangeResult result = graph.ChangeRoadType(ids, type);
            Assert.False(result.Success);
            Assert.Equal(expected, result.Error);
            Assert.False(result.Changes.HasChanges);
            Assert.Null(result.Delta);
            Assert.Same(root, graph.CaptureRevision());
            Assert.Equal(token, graph.CurrentStateToken);
            Assert.Equal(watermark, graph.NextIDWatermark);
            Assert.Equal(0, eventCount);
        }
    }

    [Fact]
    public void ChangeRoadType_PreservesRootedLoopAndCanonicalizesItsSemanticBoundary()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(10f, 0f),
            new Vector2(10f, 10f),
            Vector2.Zero,
        ]).Success);
        GraphEdge loop = Assert.Single(graph.GetAllEdges());

        RoadTypeChangeResult result = graph.ChangeRoadType(
            [loop.ID],
            RoadType.Highway);

        Assert.True(result.Success, result.Error.ToString());
        GraphEdge changed = Assert.Single(graph.GetAllEdges());
        Assert.Equal(changed.NodeA, changed.NodeB);
        Assert.Equal(RoadType.Highway, changed.RoadType);
        Assert.Equal([changed.ID], result.Changes.UpdatedEdgeIDs);
        graph.AssertInvariants();
    }

    private static int AddLine(
        RoadGraph graph,
        RoadType type,
        Vector2 start,
        Vector2 end)
    {
        RoadPathSubmissionResult result = graph.SubmitPolyline(type, [start, end]);
        Assert.True(result.Success, result.Error.ToString());
        return Assert.Single(result.Changes.CreatedEdgeIDs);
    }
}
