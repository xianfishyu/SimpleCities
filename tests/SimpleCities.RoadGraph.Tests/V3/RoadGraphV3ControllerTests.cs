using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3ControllerTests
{
    [Fact]
    public void Undo_RestoresPreviousStateAndRedo_Reapplies()
    {
        var controller = CreateController();

        Assert.True(controller.TryAddNode(Vector2.Zero, out _));
        Assert.Single(controller.Facade.Revision.Nodes);

        Assert.True(controller.TryUndo(out _));
        Assert.Empty(controller.Facade.Revision.Nodes);

        Assert.True(controller.TryRedo(out _));
        Assert.Single(controller.Facade.Revision.Nodes);
    }

    [Fact]
    public void RemoveEdge_Undo_RestoresEdge()
    {
        var controller = CreateController();
        controller.TryAddNode(Vector2.Zero, out _);
        controller.TryAddNode(new Vector2(1f, 0f), out _);
        var nodes = controller.Facade.Revision.Nodes.Values.ToArray();
        controller.TryAddEdge(
            nodes[0].ID,
            nodes[1].ID,
            [new LineRoadGeometrySegment(nodes[0].Position, nodes[1].Position)],
            RoadType.Street,
            out _);
        int edgeID = controller.Facade.Revision.Edges.Keys.Single();

        Assert.True(controller.TryRemoveEdge(edgeID, out _));
        Assert.Empty(controller.Facade.Revision.Edges);

        Assert.True(controller.TryUndo(out _));
        Assert.Single(controller.Facade.Revision.Edges);
    }

    [Fact]
    public void ChangeRoadType_Undo_RestoresOldType()
    {
        var controller = CreateController();
        controller.TryAddNode(Vector2.Zero, out _);
        controller.TryAddNode(new Vector2(1f, 0f), out _);
        var nodes = controller.Facade.Revision.Nodes.Values.ToArray();
        controller.TryAddEdge(
            nodes[0].ID,
            nodes[1].ID,
            [new LineRoadGeometrySegment(nodes[0].Position, nodes[1].Position)],
            RoadType.Street,
            out _);
        int edgeID = controller.Facade.Revision.Edges.Keys.Single();

        Assert.True(controller.TryChangeRoadType(edgeID, RoadType.Highway, out _));
        Assert.Equal(RoadType.Highway, controller.Facade.Revision.Edges[edgeID].RoadType);

        Assert.True(controller.TryUndo(out _));
        Assert.Equal(RoadType.Street, controller.Facade.Revision.Edges[edgeID].RoadType);
    }

    [Fact]
    public void NewMutation_ClearsRedo()
    {
        var controller = CreateController();
        controller.TryAddNode(Vector2.Zero, out _);
        controller.TryUndo(out _);

        Assert.True(controller.TryAddNode(new Vector2(5f, 5f), out _));

        Assert.False(controller.TryRedo(out _));
    }

    [Fact]
    public void TryUndo_WhenEmpty_ReturnsFalse()
    {
        var controller = CreateController();

        Assert.False(controller.TryUndo(out _));
    }

    [Fact]
    public void ReplaceWithFullReset_ClearsHistoryAndChangesLineage()
    {
        var controller = CreateController();
        controller.TryAddNode(Vector2.Zero, out _);
        controller.TryUndo(out _);
        Assert.Equal(1, controller.History.RedoCount);

        RoadGraphV3ChangeSummary summary = controller.ReplaceWithFullReset(
            RoadGraphV3Revision.Empty(RoadGraphCapacity.Default),
            newLineageID: 99);

        Assert.True(summary.IsFullReset);
        Assert.Equal(99, controller.Facade.LineageID);
        Assert.Equal(0, controller.History.UndoCount);
        Assert.Equal(0, controller.History.RedoCount);
    }

    [Fact]
    public void Normalize_MergesChainBuiltThroughController()
    {
        var controller = CreateController();
        controller.TryAddNode(Vector2.Zero, out _);
        controller.TryAddNode(new Vector2(1f, 0f), out _);
        controller.TryAddNode(new Vector2(2f, 0f), out _);
        var nodes = controller.Facade.Revision.Nodes.Values.ToArray();
        controller.TryAddEdge(nodes[0].ID, nodes[1].ID, [new LineRoadGeometrySegment(nodes[0].Position, nodes[1].Position)], RoadType.Street, out _);
        controller.TryAddEdge(nodes[1].ID, nodes[2].ID, [new LineRoadGeometrySegment(nodes[1].Position, nodes[2].Position)], RoadType.Street, out _);

        Assert.Equal(2, controller.Facade.Revision.Nodes.Count);
        Assert.Single(controller.Facade.Revision.Edges);
    }

    [Fact]
    public void TryAddEdge_AutoNormalizesChain()
    {
        var controller = CreateController();
        controller.TryAddNode(Vector2.Zero, out _);
        controller.TryAddNode(new Vector2(1f, 0f), out _);
        controller.TryAddNode(new Vector2(2f, 0f), out _);
        var nodes = controller.Facade.Revision.Nodes.Values.ToArray();
        controller.TryAddEdge(nodes[0].ID, nodes[1].ID, [new LineRoadGeometrySegment(nodes[0].Position, nodes[1].Position)], RoadType.Street, out _);
        controller.TryAddEdge(nodes[1].ID, nodes[2].ID, [new LineRoadGeometrySegment(nodes[1].Position, nodes[2].Position)], RoadType.Street, out _);

        Assert.Single(controller.Facade.Revision.Edges);
    }

    [Fact]
    public void NormalizeAndRecord_RecordsHistoryAndUndoRestoresUnmergedChain()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out int a);
        facade.TryAddNode(new Vector2(1f, 0f), out _, out int b);
        facade.TryAddNode(new Vector2(2f, 0f), out _, out int c);
        facade.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out _, out _);
        facade.TryAddEdge(b, c, [new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f))], RoadType.Street, out _, out _);
        var controller = new RoadGraphV3Controller(facade, new RoadEditHistoryV3(100, 100000));

        Assert.True(controller.NormalizeAndRecord(out _));
        Assert.Single(controller.Facade.Revision.Edges);

        Assert.True(controller.TryUndo(out _));
        Assert.Equal(2, controller.Facade.Revision.Edges.Count);
    }

    [Fact]
    public void TryAddEdge_RollsBackWholeOperationWhenNormalizeHistoryRejected()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out int a);
        facade.TryAddNode(new Vector2(1f, 0f), out _, out int b);
        facade.TryAddNode(new Vector2(2f, 0f), out _, out int c);
        var history = new RoadEditHistoryV3(10, 300);
        var controller = new RoadGraphV3Controller(facade, history);

        Assert.True(controller.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out _));
        Assert.False(controller.TryAddEdge(b, c, [new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f))], RoadType.Street, out _));

        Assert.Single(controller.Facade.Revision.Edges);
        Assert.Equal(1, controller.History.UndoCount);
    }

    [Fact]
    public void TryAddNode_WhenHistoryRejects_RollsBackAndReturnsFalse()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        var history = new RoadEditHistoryV3(10, 100);
        var controller = new RoadGraphV3Controller(facade, history);
        GraphStateToken before = facade.CurrentToken;

        Assert.False(controller.TryAddNode(Vector2.Zero, out _));

        Assert.Empty(facade.Revision.Nodes);
        Assert.Equal(before, facade.CurrentToken);
    }

    private static RoadGraphV3Controller CreateController()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        var history = new RoadEditHistoryV3(100, 100000);
        return new RoadGraphV3Controller(facade, history);
    }
}
