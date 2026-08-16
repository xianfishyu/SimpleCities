using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3LoadPreflightPlanTests
{
    [Fact]
    public void CanCommit_TrueWhenAllRequiredReady()
    {
        RoadGraphV3Revision revision = CreateRevision();
        var tool = V3ToolLoadParticipant.Prepare(new RoadToolFullReset(RoadToolType.Place, RoadType.Street));
        var renderer = V3RendererLoadParticipant.Prepare(CreateRendererPlan(includeMesh: true));

        var plan = new V3LoadPreflightPlan(revision, tool, renderer);

        Assert.True(plan.CanCommit);
    }

    [Fact]
    public void CanCommit_FalseWhenRendererMissingMesh()
    {
        RoadGraphV3Revision revision = CreateRevision();
        var tool = V3ToolLoadParticipant.Prepare(new RoadToolFullReset(RoadToolType.Place, RoadType.Street));
        var renderer = V3RendererLoadParticipant.Prepare(CreateRendererPlan(includeMesh: false));

        var plan = new V3LoadPreflightPlan(revision, tool, renderer);

        Assert.False(plan.CanCommit);
    }

    [Fact]
    public void CreateController_ReturnsControllerWithRevision()
    {
        RoadGraphV3Revision revision = CreateRevision();
        var plan = new V3LoadPreflightPlan(revision, null, null);

        RoadGraphV3Controller controller = plan.CreateController(lineageID: 7);

        Assert.Equal(7, controller.Facade.LineageID);
        Assert.Equal(revision.Nodes.Count, controller.Facade.Revision.Nodes.Count);
        Assert.Equal(revision.Edges.Count, controller.Facade.Revision.Edges.Count);
    }

    private static RoadGraphV3Revision CreateRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        return revision;
    }

    private static RoadPresentationFullReset CreateRendererPlan(bool includeMesh)
    {
        var snapshot = new RoadSurfaceSnapshot(
            new GraphStateToken(1, 3, 4),
            [
                new RoadSurfaceOwner(
                    RoadSurfaceOwnerKind.Ribbon,
                    NodeID: 10,
                    EdgeID: 20,
                    Endpoint: EdgeEndpoint.A,
                    new RoadLocation(20, 0, 0.5f)),
            ]);
        var token = new RoadRenderToken(1, 2, 3, 4, 5, 6);
        if (!includeMesh)
            return RoadPresentationFullReset.Create(token, snapshot);

        var ribbon = new RoadRibbonMeshData(
            [new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(10f, 1f), new Vector2(10f, -1f)],
            [0, 1, 2, 1, 3, 2],
            [Colors.White, Colors.White, Colors.White, Colors.White]);
        return RoadPresentationFullReset.Create(token, snapshot, [ribbon], []);
    }
}
