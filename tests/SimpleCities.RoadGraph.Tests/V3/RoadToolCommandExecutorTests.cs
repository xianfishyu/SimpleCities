using Godot;
using SimpleCities.Road.V3;
using System.Linq;

namespace SimpleCities.Tests.V3;

public sealed class RoadToolCommandExecutorTests
{
    [Fact]
    public void TryBuild_CommitsSessionToController()
    {
        var controller = CreateController();
        var executor = new RoadToolCommandExecutor(controller);
        var session = new RoadPlacementSessionV3(RoadType.Highway, Vector2.Zero);
        session.TryAddPoint(new Vector2(1f, 0f));
        session.TryAddPoint(new Vector2(2f, 0f));

        Assert.True(executor.TryBuild(session, out _));

        var edge = Assert.Single(controller.Facade.Revision.Edges.Values);
        Assert.Equal(RoadType.Highway, edge.RoadType);
        Assert.Equal(2, edge.Geometry.Count);
    }

    [Fact]
    public void TryBuild_EmptySession_Fails()
    {
        var controller = CreateController();
        var executor = new RoadToolCommandExecutor(controller);
        var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);

        Assert.False(executor.TryBuild(session, out _));
    }

    [Fact]
    public void TryUpgrade_ChangesSelectedEdges()
    {
        var controller = CreateController();
        controller.TryAddNode(Vector2.Zero, out _);
        controller.TryAddNode(new Vector2(1f, 0f), out _);
        controller.TryAddNode(new Vector2(1f, 1f), out _);
        var nodes = controller.Facade.Revision.Nodes.Values.ToArray();
        controller.TryAddEdge(nodes[0].ID, nodes[1].ID, [new LineRoadGeometrySegment(nodes[0].Position, nodes[1].Position)], RoadType.Street, out _);
        controller.TryAddEdge(nodes[1].ID, nodes[2].ID, [new LineRoadGeometrySegment(nodes[1].Position, nodes[2].Position)], RoadType.Arterial, out _);
        int[] edgeIDs = controller.Facade.Revision.Edges.Keys.Order().ToArray();
        var session = new RoadUpgradeSessionV3(RoadType.Highway);
        session.TrySelectEdge(edgeIDs[0]);
        var executor = new RoadToolCommandExecutor(controller);

        Assert.True(executor.TryUpgrade(session, out IReadOnlyList<int> changed));
        Assert.Equal([edgeIDs[0]], changed);
        Assert.Equal(RoadType.Highway, controller.Facade.Revision.Edges[edgeIDs[0]].RoadType);
        Assert.Equal(RoadType.Arterial, controller.Facade.Revision.Edges[edgeIDs[1]].RoadType);
    }

    [Fact]
    public void TryUpgrade_MissingEdge_FailsWithoutChanges()
    {
        var controller = CreateController();
        controller.TryAddNode(Vector2.Zero, out _);
        controller.TryAddNode(new Vector2(1f, 0f), out _);
        var nodes = controller.Facade.Revision.Nodes.Values.ToArray();
        controller.TryAddEdge(nodes[0].ID, nodes[1].ID, [new LineRoadGeometrySegment(nodes[0].Position, nodes[1].Position)], RoadType.Street, out _);
        int edgeID = controller.Facade.Revision.Edges.Keys.Single();
        var session = new RoadUpgradeSessionV3(RoadType.Highway);
        session.TrySelectEdge(999);
        var executor = new RoadToolCommandExecutor(controller);

        Assert.False(executor.TryUpgrade(session, out _));
        Assert.Equal(RoadType.Street, controller.Facade.Revision.Edges[edgeID].RoadType);
    }

    private static RoadGraphV3Controller CreateController() =>
        new(
            new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default)),
            new RoadEditHistoryV3(100, 100000));
}
