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
