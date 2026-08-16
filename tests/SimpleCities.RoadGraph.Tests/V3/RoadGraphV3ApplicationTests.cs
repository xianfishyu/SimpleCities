using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.IO;
using System.Linq;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3ApplicationTests
{
    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            RoadGraphV3Revision revision = CreateRevision();
            app.Controller.ReplaceWithFullReset(revision, 1);

            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            Assert.Equal("city-001", app.CurrentSlotID);

            var loaded = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(loaded.Load("city-001"));
            Assert.Equal("city-001", loaded.CurrentSlotID);
            Assert.Equal(revision.Nodes.Count, loaded.Controller.Facade.Revision.Nodes.Count);
            Assert.Equal(revision.Edges.Count, loaded.Controller.Facade.Revision.Edges.Count);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_AppliesPreservedToolState()
    {
        string root = GetTempRoot();
        try
        {
            var source = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            RoadGraphV3Revision revision = CreateRevision();
            source.Controller.ReplaceWithFullReset(revision, 1);
            Assert.True(source.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.ToolState.SwitchTo(RoadToolType.Upgrade);
            app.ToolState.TrySelectRoadType(RoadType.Highway);

            Assert.True(app.Load("city-001"));
            Assert.Equal(RoadToolType.Upgrade, app.ToolState.CurrentTool);
            Assert.Equal(RoadType.Highway, app.ToolState.SelectedRoadType);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_AppliesPresentationPlan()
    {
        string root = GetTempRoot();
        try
        {
            var source = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            RoadGraphV3Revision revision = CreateRevision();
            source.Controller.ReplaceWithFullReset(revision, 1);
            Assert.True(source.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.Load("city-001", lineageID: 7));
            Assert.False(app.Presentation.IsStalled);
            Assert.NotNull(app.Presentation.PresentedSnapshot);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void LoadIntoCurrent_AppliesPreservedToolState()
    {
        string root = GetTempRoot();
        try
        {
            var source = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            RoadGraphV3Revision revision = CreateRevision();
            source.Controller.ReplaceWithFullReset(revision, 1);
            Assert.True(source.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.ToolState.SwitchTo(RoadToolType.Remove);
            app.ToolState.TrySelectRoadType(RoadType.Arterial);

            Assert.True(app.LoadIntoCurrent("city-001"));
            Assert.Equal(RoadToolType.Remove, app.ToolState.CurrentTool);
            Assert.Equal(RoadType.Arterial, app.ToolState.SelectedRoadType);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_MissingSlot_Fails()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.False(app.Load("missing"));
            Assert.Equal(string.Empty, app.CurrentSlotID);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryAutosave_WhenGateFree_Saves()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            RoadGraphV3Revision revision = CreateRevision();

            V3AutosaveDecision decision = app.TryAutosave(
                "city-001",
                revision,
                hasNewerSuccess: false,
                out bool saved);

            Assert.Equal(V3AutosaveDecision.RunNow, decision);
            Assert.True(saved);
            Assert.True(new V3FileSlotStore(root).Load("city-001").Success);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryRequestPresentation_ThenPublish_Succeeds()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
            session.TryAddPoint(new Vector2(1f, 0f));
            Assert.True(app.TryBuild(session, out _));
            RoadRenderToken desired = new(1, 1, 1, 1, 1, 3);

            Assert.True(app.TryRequestPresentation(desired));
            Assert.True(app.Presentation.TryPublish(desired));
            Assert.False(app.Presentation.IsStalled);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Presentation_CanRequestAndPublish()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
            session.TryAddPoint(new Vector2(1f, 0f));
            Assert.True(app.TryBuild(session, out _));

            RoadRenderToken desired = new(1, 1, 1, 1, 1, 2);
            Assert.True(app.Presentation.TryRequest(
                app.Controller.Facade.Revision,
                app.Controller.Facade.CurrentToken,
                desired));
            Assert.True(app.Presentation.TryPublish(desired));

            Assert.False(app.Presentation.IsStalled);
            Assert.NotNull(app.Presentation.PresentedSnapshot);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void HitProvider_ResolvesPublishedEdgeHit()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
            session.TryAddPoint(new Vector2(1f, 0f));
            Assert.True(app.TryBuild(session, out _));
            int edgeID = app.Revision.Edges.Keys.Single();
            int nodeAID = app.Revision.Edges[edgeID].NodeAID;

            RoadRenderToken desired = new(1, 1, 1, 1, 1, 7);
            Assert.True(app.Presentation.TryRequest(
                app.Controller.Facade.Revision,
                app.Controller.Facade.CurrentToken,
                desired));
            Assert.True(app.Presentation.TryPublish(desired));

            var hit = new RoadSurfaceHit(
                app.Controller.Facade.CurrentToken,
                RoadSurfaceOwnerKind.Ribbon,
                NodeID: nodeAID,
                EdgeID: edgeID,
                Endpoint: EdgeEndpoint.A,
                new RoadLocation(edgeID, 0, 0f),
                DistanceSquared: 1f);

            Assert.True(app.HitProvider.TryResolve(hit, out _));
            Assert.True(app.HitProvider.TryResolveEdge(hit, out int resolvedEdgeID));
            Assert.Equal(edgeID, resolvedEdgeID);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void BuildDefaultSurfaceSnapshot_ReturnsSnapshotWithOwners()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
            session.TryAddPoint(new Vector2(1f, 0f));
            Assert.True(app.TryBuild(session, out _));

            RoadSurfaceSnapshotBuildResult result = app.BuildDefaultSurfaceSnapshot();

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.Snapshot);
            Assert.Single(result.Snapshot!.Owners);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void BuildSurfaceSnapshot_ReturnsSnapshotWithOwners()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var session = new RoadPlacementSessionV3(RoadType.Street, Vector2.Zero);
            session.TryAddPoint(new Vector2(1f, 0f));
            Assert.True(app.TryBuild(session, out _));

            var styles = new RoadStyleProvider(RoadTypeStyleCatalog.CreateDefault());
            RoadSurfaceSnapshotBuildResult result = app.BuildSurfaceSnapshot(styles);

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.Snapshot);
            Assert.Equal(app.Controller.Facade.Revision.Edges.Count, result.Snapshot!.Owners.Count);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void LoadIntoCurrent_ReplacesControllerAndClearsHistory()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            var saver = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            saver.Controller.ReplaceWithFullReset(revision, 1);
            Assert.True(saver.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.Controller.TryAddNode(Vector2.Zero, out _));
            Assert.True(app.LoadIntoCurrent("city-001", newLineageID: 7));

            Assert.Equal(revision.Nodes.Count, app.Controller.Facade.Revision.Nodes.Count);
            Assert.Equal(revision.Edges.Count, app.Controller.Facade.Revision.Edges.Count);
            Assert.Equal(0, app.Controller.History.UndoCount);
            Assert.Equal("city-001", app.CurrentSlotID);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryBuildFromPolyline_CreatesEdge()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.True(app.TryBuildFromPolyline(
                [Vector2.Zero, new Vector2(1f, 0f), new Vector2(2f, 0f)],
                RoadType.Highway,
                out _));

            var edge = Assert.Single(app.Controller.Facade.Revision.Edges.Values);
            Assert.Equal(2, edge.Geometry.Count);
            Assert.Equal(RoadType.Highway, edge.RoadType);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryBuildFromPolyline_TooFewPoints_Fails()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.False(app.TryBuildFromPolyline([Vector2.Zero], RoadType.Street, out _));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryBuild_WithSnapRadius_ReusesNode()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.Controller.TryAddNode(Vector2.Zero, out _));
            var session = new RoadPlacementSessionV3(RoadType.Street, new Vector2(0.01f, 0f));
            session.TryAddPoint(new Vector2(1f, 0f));

            Assert.True(app.TryBuild(session, snapRadius: 0.1f, out _));
            Assert.Equal(2, app.Controller.Facade.Revision.Nodes.Count);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryBuild_CommitsSessionThroughApplication()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            var session = new RoadPlacementSessionV3(RoadType.Highway, Vector2.Zero);
            session.TryAddPoint(new Vector2(1f, 0f));

            Assert.True(app.TryBuild(session, out _));
            Assert.Single(app.Controller.Facade.Revision.Edges);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryUndo_WithCurrentToken_UndoesNode()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.Controller.TryAddNode(Vector2.Zero, out _));
            GraphStateToken token = app.Controller.Facade.CurrentToken;

            Assert.True(app.TryUndo(token, out _));
            Assert.Empty(app.Controller.Facade.Revision.Nodes);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void SaveAs_SetsCurrentSlot()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);

            Assert.True(app.SaveAs("city-010", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            Assert.Equal("city-010", app.CurrentSlotID);
            Assert.Contains(app.List(), summary => summary.SlotId == "city-010");
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void SaveCurrent_WhenCurrentSlotSet_Saves()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.True(app.SaveCurrent("新名称", "新城市", "2026-08-12T09:00:00.0000000Z", null, null, null));
            Assert.Equal("city-001", app.CurrentSlotID);
            V3Manifest? manifest = V3SlotManifestService.GetManifest("city-001", root);
            Assert.NotNull(manifest);
            Assert.Equal("新名称", manifest!.DisplayName);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void SaveCurrent_WhenNoCurrentSlot_Fails()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.False(app.SaveCurrent("n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryAutosaveCurrent_WhenCurrentSlotSet_Saves()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            V3AutosaveDecision decision = app.TryAutosaveCurrent(
                app.Controller.Facade.Revision,
                hasNewerSuccess: false,
                out bool saved);

            Assert.Equal(V3AutosaveDecision.RunNow, decision);
            Assert.True(saved);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryAutosaveCurrent_WhenNoCurrentSlot_Skips()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            V3AutosaveDecision decision = app.TryAutosaveCurrent(
                CreateRevision(),
                hasNewerSuccess: false,
                out bool saved);

            Assert.Equal(V3AutosaveDecision.SkipBusy, decision);
            Assert.False(saved);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentTool_SetSwitchesTool()
    {
        var app = new RoadGraphV3Application(GetTempRoot(), RoadGraphCapacity.Default, V3PayloadBudget.Default);

        app.CurrentTool = RoadToolType.Remove;

        Assert.Equal(RoadToolType.Remove, app.ToolState.CurrentTool);
    }

    [Fact]
    public void SelectedRoadType_SetChangesType()
    {
        var app = new RoadGraphV3Application(GetTempRoot(), RoadGraphCapacity.Default, V3PayloadBudget.Default);

        app.SelectedRoadType = RoadType.Arterial;

        Assert.Equal(RoadType.Arterial, app.ToolState.SelectedRoadType);
    }

    [Fact]
    public void SelectedRoadType_Invalid_Throws()
    {
        var app = new RoadGraphV3Application(GetTempRoot(), RoadGraphCapacity.Default, V3PayloadBudget.Default);

        Assert.Throws<System.ArgumentOutOfRangeException>(() => app.SelectedRoadType = (RoadType)99);
    }

    [Fact]
    public void ToolState_DefaultsToPlaceAndStreet()
    {
        var app = new RoadGraphV3Application(GetTempRoot(), RoadGraphCapacity.Default, V3PayloadBudget.Default);

        Assert.Equal(RoadToolType.Place, app.ToolState.CurrentTool);
        Assert.Equal(RoadType.Street, app.ToolState.SelectedRoadType);
    }

    [Fact]
    public void ToolState_CanSwitchAndSelectType()
    {
        var app = new RoadGraphV3Application(GetTempRoot(), RoadGraphCapacity.Default, V3PayloadBudget.Default);

        app.ToolState.SwitchTo(RoadToolType.Upgrade);
        Assert.True(app.ToolState.TrySelectRoadType(RoadType.Highway));

        Assert.Equal(RoadToolType.Upgrade, app.ToolState.CurrentTool);
        Assert.Equal(RoadType.Highway, app.ToolState.SelectedRoadType);
    }

    [Fact]
    public void TryUpgradeEdges_ChangesEdges()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.TryAddNode(Vector2.Zero, out _);
            app.Controller.TryAddNode(new Vector2(1f, 0f), out _);
            app.Controller.TryAddNode(new Vector2(1f, 1f), out _);
            var nodes = app.Controller.Facade.Revision.Nodes.Values.ToArray();
            app.Controller.TryAddEdge(nodes[0].ID, nodes[1].ID, [new LineRoadGeometrySegment(nodes[0].Position, nodes[1].Position)], RoadType.Street, out _);
            app.Controller.TryAddEdge(nodes[1].ID, nodes[2].ID, [new LineRoadGeometrySegment(nodes[1].Position, nodes[2].Position)], RoadType.Arterial, out _);
            int[] edgeIDs = app.Controller.Facade.Revision.Edges.Keys.Order().ToArray();
            app.Controller.History.Clear();

            Assert.True(app.TryUpgradeEdges(edgeIDs, RoadType.Highway, out _));
            Assert.All(app.Controller.Facade.Revision.Edges.Values, edge => Assert.Equal(RoadType.Highway, edge.RoadType));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryAddEdge_AddsEdge()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryAddNode(Vector2.Zero, out _));
            Assert.True(app.TryAddNode(new Vector2(1f, 0f), out _));
            var nodes = app.Controller.Facade.Revision.Nodes.Values.ToArray();

            Assert.True(app.TryAddEdge(
                nodes[0].ID,
                nodes[1].ID,
                [new LineRoadGeometrySegment(nodes[0].Position, nodes[1].Position)],
                RoadType.Street,
                out _));
            Assert.Single(app.Controller.Facade.Revision.Edges);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryAddNode_AddsNode()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.True(app.TryAddNode(Vector2.Zero, out _));
            Assert.Single(app.Controller.Facade.Revision.Nodes);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryChangeRoadType_ChangesSingleEdge()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline([Vector2.Zero, new Vector2(1f, 0f)], RoadType.Street, out _));
            int edgeID = app.Controller.Facade.Revision.Edges.Keys.Single();

            Assert.True(app.TryChangeRoadType(edgeID, RoadType.Highway, out _));
            Assert.Equal(RoadType.Highway, app.Controller.Facade.Revision.Edges[edgeID].RoadType);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryRemoveEdge_RemovesSingleEdge()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline([Vector2.Zero, new Vector2(1f, 0f)], RoadType.Street, out _));
            int edgeID = app.Controller.Facade.Revision.Edges.Keys.Single();

            Assert.True(app.TryRemoveEdge(edgeID, out _));
            Assert.Empty(app.Controller.Facade.Revision.Edges);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryRemoveEdges_RemovesEdges()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline(
                [Vector2.Zero, new Vector2(1f, 0f)],
                RoadType.Street,
                out _));
            int edgeID = app.Controller.Facade.Revision.Edges.Keys.Single();

            Assert.True(app.TryRemoveEdges([edgeID], out _));
            Assert.Empty(app.Controller.Facade.Revision.Edges);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryUndo_WithoutToken_Undoes()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline([Vector2.Zero, new Vector2(1f, 0f)], RoadType.Street, out _));

            Assert.True(app.TryUndo(out _));
            Assert.Empty(app.Controller.Facade.Revision.Edges);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryRedo_WithoutToken_Redoes()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline([Vector2.Zero, new Vector2(1f, 0f)], RoadType.Street, out _));
            Assert.True(app.TryUndo(out _));

            Assert.True(app.TryRedo(out _));
            Assert.Single(app.Controller.Facade.Revision.Edges);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void ClearHistory_EmptiesUndoRedo()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline([Vector2.Zero, new Vector2(1f, 0f)], RoadType.Street, out _));
            Assert.True(app.CanUndo);

            app.ClearHistory();

            Assert.False(app.CanUndo);
            Assert.False(app.CanRedo);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CanUndo_AfterEdit_IsTrue()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline([Vector2.Zero, new Vector2(1f, 0f)], RoadType.Street, out _));

            Assert.True(app.CanUndo);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CanRedo_AfterUndo_IsTrue()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline([Vector2.Zero, new Vector2(1f, 0f)], RoadType.Street, out _));
            Assert.True(app.TryUndo(app.Controller.Facade.CurrentToken, out _));

            Assert.True(app.CanRedo);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Revision_ReturnsCurrentControllerRevision()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline(
                [Vector2.Zero, new Vector2(1f, 0f)],
                RoadType.Street,
                out _));

            Assert.Same(app.Controller.Facade.Revision, app.Revision);
            Assert.Single(app.Revision.Edges);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void ListUsableSlots_FiltersCorrupt()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            Assert.True(app.Save("city-002", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(root, "city-002", "road_network.json"),
                "corrupt");

            IReadOnlyList<V3SlotSummary> usable = app.ListUsableSlots();

            V3SlotSummary summary = Assert.Single(usable);
            Assert.Equal("city-001", summary.SlotId);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotSchemaVersion_AfterSave_IsOne()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.Equal(V3SaveRoot.FormatFamily, app.CurrentSlotFormatFamily);
            Assert.Equal(1, app.CurrentSlotSchemaVersion);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void HasCurrentSlot_AfterSave_IsTrue()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.True(app.HasCurrentSlot);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotIsOperable_AfterSave_IsTrue()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.True(app.CurrentSlotIsOperable);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotIsForeign_AfterSave_IsFalse()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.False(app.CurrentSlotIsForeign);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotIsUnsafe_AfterSave_IsFalse()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.False(app.CurrentSlotIsUnsafe);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotIsAbsent_AfterSave_IsFalse()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.False(app.CurrentSlotIsAbsent);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotIsCorrupt_AfterCorruptSave_IsTrue()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(root, "city-001", "road_network.json"),
                "corrupt");

            Assert.True(app.CurrentSlotIsCorrupt);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotIsComplete_AfterSave_IsTrue()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.True(app.CurrentSlotIsComplete);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotOccupant_AfterSave_ReturnsComplete()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.Equal(V3SlotOccupant.CompleteV3, app.CurrentSlotOccupant);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotTimestamp_AfterSave_ReturnsTimestamp()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.Equal("2026-08-12T08:00:00.0000000Z", app.CurrentSlotTimestamp);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotDisplayName_AfterSave_ReturnsName()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "河湾城", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.Equal("河湾城", app.CurrentSlotDisplayName);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotSummary_AfterSave_ReturnsSummary()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            V3SlotSummary? summary = app.CurrentSlotSummary;

            Assert.NotNull(summary);
            Assert.Equal("city-001", summary!.SlotId);
            Assert.True(summary.IsUsable);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotSummary_WhenNoSlot_ReturnsNull()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.Null(app.CurrentSlotSummary);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotPayload_AfterSave_ReturnsRoadPayload()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            byte[]? payload = app.CurrentSlotPayload(V3RoadSlotFactory.RoadNetworkFileName);

            Assert.NotNull(payload);
            Assert.NotEmpty(payload);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void GetPayload_ReturnsRoadPayload()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            byte[]? payload = app.GetPayload("city-001", V3RoadSlotFactory.RoadNetworkFileName);

            Assert.NotNull(payload);
            Assert.NotEmpty(payload);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotTotalBytes_AfterSave_GreaterThanZero()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.True(app.CurrentSlotTotalBytes > 0);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentRoadTypeCounts_AfterBuild_ContainsStreet()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline([Vector2.Zero, new Vector2(1f, 0f)], RoadType.Street, out _));

            Assert.True(app.CurrentRoadTypeCounts.TryGetValue(RoadType.Street, out int count));
            Assert.Equal(1, count);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSelfLoopCount_AfterNormalBuild_IsZero()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline([Vector2.Zero, new Vector2(1f, 0f)], RoadType.Street, out _));

            Assert.Equal(0, app.CurrentSelfLoopCount);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentGeometrySegmentCount_AfterPolyline_EqualsSegments()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline([Vector2.Zero, new Vector2(1f, 0f), new Vector2(2f, 0f)], RoadType.Street, out _));

            Assert.Equal(2, app.CurrentGeometrySegmentCount);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentNodeCountAndEdgeCount_AfterBuild_ReflectGraph()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            Assert.True(app.TryBuildFromPolyline([Vector2.Zero, new Vector2(1f, 0f)], RoadType.Street, out _));

            Assert.Equal(2, app.CurrentNodeCount);
            Assert.Equal(1, app.CurrentEdgeCount);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotRoadNetworkJson_AfterSave_ContainsPayload()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            string? json = app.CurrentSlotRoadNetworkJson;

            Assert.NotNull(json);
            Assert.Contains("simple-cities-v3", json);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotHasRoadNetwork_AfterSave_IsTrue()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.True(app.CurrentSlotHasRoadNetwork);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void GetCurrentSlotFile_ReturnsRoadNetwork()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            V3ManifestFile? file = app.GetCurrentSlotFile(V3RoadSlotFactory.RoadNetworkFileName);

            Assert.NotNull(file);
            Assert.Equal(V3RoadSlotFactory.RoadNetworkFileName, file!.Name);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotFiles_AfterSave_ContainsRoadNetwork()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.Contains(app.CurrentSlotFiles, file => file.Name == V3RoadSlotFactory.RoadNetworkFileName);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotFileNames_AfterSave_ContainsRoadNetwork()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.Contains(V3RoadSlotFactory.RoadNetworkFileName, app.CurrentSlotFileNames);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotHasFiles_AfterSave_IsTrue()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.True(app.CurrentSlotHasFiles);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotFileCount_AfterSave_IsOne()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.Equal(1, app.CurrentSlotFileCount);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotHasThumbnail_AfterSaveWithThumbnail_IsTrue()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, "thumb.png"));

            Assert.True(app.CurrentSlotHasThumbnail);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotThumbnailFile_AfterSave_ReturnsFile()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, "thumb.png"));

            Assert.Equal("thumb.png", app.CurrentSlotThumbnailFile);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotPopulationAndFunds_AfterSave_ReturnValues()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", 1200, 50000m, null));

            Assert.Equal(1200, app.CurrentSlotPopulation);
            Assert.Equal(50000m, app.CurrentSlotFunds);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotCityName_AfterSave_ReturnsCityName()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "河湾城", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.Equal("河湾城", app.CurrentSlotCityName);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CurrentSlotManifest_AfterSave_ReturnsManifest()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            V3Manifest? manifest = app.CurrentSlotManifest;

            Assert.NotNull(manifest);
            Assert.Equal("city-001", manifest!.SlotId);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void GetManifest_ReturnsSavedManifest()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            V3Manifest? manifest = app.GetManifest("city-001");

            Assert.NotNull(manifest);
            Assert.Equal("city-001", manifest!.SlotId);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void GetStatus_ReturnsCompleteAfterSave()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            V3SlotSummary summary = app.GetStatus("city-001");

            Assert.Equal(V3SlotOccupant.CompleteV3, summary.Occupant);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void DeleteCurrentSlot_RemovesAndClearsSlot()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.True(app.DeleteCurrentSlot());
            Assert.Equal(string.Empty, app.CurrentSlotID);
            Assert.Empty(app.List());
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Delete_RemovesSlot()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            Assert.True(app.Delete("city-001"));
            Assert.Empty(app.List());
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void List_ReturnsSavedSlots()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            Assert.True(app.Save("city-002", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            IReadOnlyList<V3SlotSummary> list = app.List();

            Assert.Equal(2, list.Count);
            Assert.All(list, summary => Assert.True(summary.IsUsable));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void NewCity_ResetsControllerAndSlot()
    {
        string root = GetTempRoot();
        try
        {
            var app = new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
            app.Controller.ReplaceWithFullReset(CreateRevision(), 1);
            Assert.True(app.Save("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            app.NewCity(lineageID: 42);

            Assert.Empty(app.Controller.Facade.Revision.Nodes);
            Assert.Empty(app.Controller.Facade.Revision.Edges);
            Assert.Equal(42, app.Controller.Facade.LineageID);
            Assert.Equal(string.Empty, app.CurrentSlotID);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-app-{Guid.NewGuid():N}");

    private static void Cleanup(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }

    private static RoadGraphV3Revision CreateRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        return revision;
    }
}
