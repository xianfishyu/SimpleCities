using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3RoadLoadPipelineTests
{
    [Fact]
    public void Load_RoundTripsController()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            V3RoadLoadPipelineResult result = V3RoadLoadPipeline.Load(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default);

            Assert.True(result.Success, result.Error);
            Assert.Equal(V3LoadPhase.Completed, result.Phase);
            Assert.NotNull(result.Controller);
            Assert.Equal(revision.Nodes.Count, result.Controller!.Facade.Revision.Nodes.Count);
            Assert.Equal(revision.Edges.Count, result.Controller.Facade.Revision.Edges.Count);
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
            V3RoadLoadPipelineResult result = V3RoadLoadPipeline.Load(
                "missing",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default);

            Assert.False(result.Success);
            Assert.Equal(V3LoadPhase.Failed, result.Phase);
            Assert.Null(result.Controller);
            Assert.NotNull(result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryLoadIntoController_ReplacesRevisionAndClearsHistory()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            var controller = new RoadGraphV3Controller(
                new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default), 1),
                new RoadEditHistoryV3(100, 100000));
            Assert.True(controller.TryAddNode(Vector2.Zero, out _));
            Assert.Equal(1, controller.History.UndoCount);

            bool result = V3RoadLoadPipeline.TryLoadIntoController(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                controller,
                newLineageID: 7);

            Assert.True(result);
            Assert.Equal(revision.Nodes.Count, controller.Facade.Revision.Nodes.Count);
            Assert.Equal(revision.Edges.Count, controller.Facade.Revision.Edges.Count);
            Assert.Equal(0, controller.History.UndoCount);
            Assert.Equal(7, controller.Facade.LineageID);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_WithToolState_ReturnsToolPlan()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            var toolState = new RoadToolState();
            toolState.SwitchTo(RoadToolType.Upgrade);
            toolState.TrySelectRoadType(RoadType.Highway);

            V3RoadLoadPipelineResult result = V3RoadLoadPipeline.Load(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 1,
                preservedToolState: toolState);

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.ToolPlan);
            Assert.Equal(RoadToolType.Upgrade, result.ToolPlan!.CurrentTool);
            Assert.Equal(RoadType.Highway, result.ToolPlan.SelectedRoadType);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_WithStylesAndDesiredToken_ReturnsPresentationPlan()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
            Assert.True(catalog.Success);
            var styles = new RoadStyleProvider(catalog);
            RoadRenderToken desired = new(0, 7, 0, 0, 0, 9);

            V3RoadLoadPipelineResult result = V3RoadLoadPipeline.Load(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 7,
                styles: styles,
                desiredPresentationToken: desired);

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.PresentationPlan);
            Assert.Equal(desired, result.PresentationPlan!.DesiredToken);
            Assert.NotNull(result.PresentationPlan.Snapshot);
            Assert.True(result.PresentationPlan.HasMeshData);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_WithToolAndPresentation_ReturnsBothPlans()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
            Assert.True(catalog.Success);
            var styles = new RoadStyleProvider(catalog);
            RoadRenderToken desired = new(0, 7, 0, 0, 0, 10);
            var toolState = new RoadToolState();
            toolState.SwitchTo(RoadToolType.Upgrade);
            toolState.TrySelectRoadType(RoadType.Highway);

            V3RoadLoadPipelineResult result = V3RoadLoadPipeline.Load(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 7,
                preservedToolState: toolState,
                styles: styles,
                desiredPresentationToken: desired);

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.ToolPlan);
            Assert.NotNull(result.PresentationPlan);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_ResultCanApplyParticipants()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
            Assert.True(catalog.Success);
            var styles = new RoadStyleProvider(catalog);
            RoadRenderToken desired = new(0, 7, 0, 0, 0, 15);
            var sourceTool = new RoadToolState();
            sourceTool.SwitchTo(RoadToolType.Upgrade);
            sourceTool.TrySelectRoadType(RoadType.Highway);

            V3RoadLoadPipelineResult result = V3RoadLoadPipeline.Load(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 7,
                preservedToolState: sourceTool,
                styles: styles,
                desiredPresentationToken: desired);
            Assert.True(result.Success, result.Error);

            var toolState = new RoadToolState();
            var presentationState = new RoadPresentationState(new RoadRenderToken(0, 0, 0, 0, 0, 0));
            Assert.True(result.TryApplyParticipants(toolState, presentationState));
            Assert.Equal(RoadToolType.Upgrade, toolState.CurrentTool);
            Assert.Equal(RoadType.Highway, toolState.SelectedRoadType);
            Assert.False(presentationState.IsStalled);
            Assert.NotNull(presentationState.PresentedSnapshot);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Prepare_ReturnsPreflightPlanAndCoordinator()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
            Assert.True(catalog.Success);
            var styles = new RoadStyleProvider(catalog);
            RoadRenderToken desired = new(0, 7, 0, 0, 0, 20);
            var toolState = new RoadToolState();
            toolState.SwitchTo(RoadToolType.Upgrade);
            toolState.TrySelectRoadType(RoadType.Highway);

            V3RoadLoadPrepareResult result = V3RoadLoadPipeline.Prepare(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 7,
                preservedToolState: toolState,
                styles: styles,
                desiredPresentationToken: desired);

            Assert.True(result.Success, result.Error);
            Assert.Equal(V3LoadPhase.Preflight, result.Phase);
            Assert.NotNull(result.Plan);
            Assert.NotNull(result.Coordinator);
            Assert.NotNull(result.ToolPlan);
            Assert.NotNull(result.PresentationPlan);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Prepare_ResultCanApplyParticipants()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
            Assert.True(catalog.Success);
            var styles = new RoadStyleProvider(catalog);
            RoadRenderToken desired = new(0, 7, 0, 0, 0, 21);
            var sourceTool = new RoadToolState();
            sourceTool.SwitchTo(RoadToolType.Upgrade);
            sourceTool.TrySelectRoadType(RoadType.Highway);

            V3RoadLoadPrepareResult prepare = V3RoadLoadPipeline.Prepare(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 7,
                preservedToolState: sourceTool,
                styles: styles,
                desiredPresentationToken: desired);
            Assert.True(prepare.Success, prepare.Error);

            var toolState = new RoadToolState();
            var presentationState = new RoadPresentationState(new RoadRenderToken(0, 0, 0, 0, 0, 0));
            Assert.True(prepare.TryApplyParticipants(toolState, presentationState));
            Assert.Equal(RoadToolType.Upgrade, toolState.CurrentTool);
            Assert.Equal(RoadType.Highway, toolState.SelectedRoadType);
            Assert.False(presentationState.IsStalled);
            Assert.NotNull(presentationState.PresentedSnapshot);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Prepare_MissingSlot_Fails()
    {
        string root = GetTempRoot();
        try
        {
            V3RoadLoadPrepareResult result = V3RoadLoadPipeline.Prepare(
                "missing",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default);

            Assert.False(result.Success);
            Assert.Equal(V3LoadPhase.Failed, result.Phase);
            Assert.Null(result.Plan);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Commit_FromPrepare_ReturnsController()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            V3RoadLoadPrepareResult prepare = V3RoadLoadPipeline.Prepare(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 7);
            Assert.True(prepare.Success, prepare.Error);

            V3RoadLoadPipelineResult result = V3RoadLoadPipeline.Commit(prepare, lineageID: 7);

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.Controller);
            Assert.Equal(7, result.Controller!.Facade.LineageID);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Prepare_ResultCanCommitItself()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));

            V3RoadLoadPrepareResult prepare = V3RoadLoadPipeline.Prepare(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 7);
            Assert.True(prepare.Success, prepare.Error);

            V3RoadLoadPipelineResult result = prepare.Commit(lineageID: 7);

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.Controller);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Commit_WithToolAndPresentationStates_AppliesAllInsideCommit()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
            Assert.True(catalog.Success);
            var styles = new RoadStyleProvider(catalog);
            RoadRenderToken desired = new(0, 7, 0, 0, 0, 30);
            var sourceTool = new RoadToolState();
            sourceTool.SwitchTo(RoadToolType.Upgrade);
            sourceTool.TrySelectRoadType(RoadType.Highway);

            V3RoadLoadPrepareResult prepare = V3RoadLoadPipeline.Prepare(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 7,
                preservedToolState: sourceTool,
                styles: styles,
                desiredPresentationToken: desired);
            Assert.True(prepare.Success, prepare.Error);

            var toolState = new RoadToolState();
            var presentationState = new RoadPresentationState(new RoadRenderToken(0, 0, 0, 0, 0, 0));
            V3RoadLoadPipelineResult result = prepare.Commit(7, toolState, presentationState, "city-001");

            Assert.True(result.Success, result.Error);
            Assert.Equal("city-001", result.SlotId);
            Assert.Equal(RoadToolType.Upgrade, toolState.CurrentTool);
            Assert.Equal(RoadType.Highway, toolState.SelectedRoadType);
            Assert.False(presentationState.IsStalled);
            Assert.NotNull(presentationState.PresentedSnapshot);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_WithCommitStates_AppliesInsideCommit()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
            Assert.True(catalog.Success);
            var styles = new RoadStyleProvider(catalog);
            RoadRenderToken desired = new(0, 7, 0, 0, 0, 31);
            var sourceTool = new RoadToolState();
            sourceTool.SwitchTo(RoadToolType.Remove);
            sourceTool.TrySelectRoadType(RoadType.Arterial);

            var toolState = new RoadToolState();
            var presentationState = new RoadPresentationState(new RoadRenderToken(0, 0, 0, 0, 0, 0));
            V3RoadLoadPipelineResult result = V3RoadLoadPipeline.Load(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 7,
                preservedToolState: sourceTool,
                styles: styles,
                desiredPresentationToken: desired,
                commitToolState: toolState,
                commitPresentationState: presentationState);

            Assert.True(result.Success, result.Error);
            Assert.Equal("city-001", result.SlotId);
            Assert.Equal(RoadToolType.Remove, toolState.CurrentTool);
            Assert.Equal(RoadType.Arterial, toolState.SelectedRoadType);
            Assert.False(presentationState.IsStalled);
            Assert.NotNull(presentationState.PresentedSnapshot);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Commit_InsideCommitAction_RunsWithinCommit()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
            Assert.True(catalog.Success);
            var styles = new RoadStyleProvider(catalog);
            RoadRenderToken desired = new(0, 7, 0, 0, 0, 32);
            var sourceTool = new RoadToolState();
            sourceTool.SwitchTo(RoadToolType.Upgrade);
            sourceTool.TrySelectRoadType(RoadType.Highway);

            V3RoadLoadPrepareResult prepare = V3RoadLoadPipeline.Prepare(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 7,
                preservedToolState: sourceTool,
                styles: styles,
                desiredPresentationToken: desired);
            Assert.True(prepare.Success, prepare.Error);

            var toolState = new RoadToolState();
            var presentationState = new RoadPresentationState(new RoadRenderToken(0, 0, 0, 0, 0, 0));
            bool ran = false;
            V3RoadLoadPipelineResult result = prepare.Commit(
                7,
                toolState,
                presentationState,
                "city-001",
                () => ran = true);

            Assert.True(result.Success, result.Error);
            Assert.True(ran);
            Assert.Equal(RoadToolType.Upgrade, toolState.CurrentTool);
            Assert.Equal(RoadType.Highway, toolState.SelectedRoadType);
            Assert.False(presentationState.IsStalled);
            Assert.NotNull(presentationState.PresentedSnapshot);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Commit_InsideCommitFailure_RestoresToolAndPresentation()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
            Assert.True(catalog.Success);
            var styles = new RoadStyleProvider(catalog);
            RoadRenderToken desired = new(0, 7, 0, 0, 0, 33);
            var sourceTool = new RoadToolState();
            sourceTool.SwitchTo(RoadToolType.Upgrade);
            sourceTool.TrySelectRoadType(RoadType.Highway);

            V3RoadLoadPrepareResult prepare = V3RoadLoadPipeline.Prepare(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                lineageID: 7,
                preservedToolState: sourceTool,
                styles: styles,
                desiredPresentationToken: desired);
            Assert.True(prepare.Success, prepare.Error);

            var toolState = new RoadToolState();
            var presentationState = new RoadPresentationState(new RoadRenderToken(0, 0, 0, 0, 0, 0));
            V3RoadLoadPipelineResult result = prepare.Commit(
                7,
                toolState,
                presentationState,
                "city-001",
                () => throw new InvalidOperationException("SwapFailed"));

            Assert.False(result.Success);
            Assert.Equal(RoadToolType.Place, toolState.CurrentTool);
            Assert.Equal(RoadType.Street, toolState.SelectedRoadType);
            Assert.Equal(new RoadRenderToken(0, 0, 0, 0, 0, 0), presentationState.DesiredToken);
            Assert.Equal(new RoadRenderToken(0, 0, 0, 0, 0, 0), presentationState.PresentedToken);
            Assert.Null(presentationState.PresentedSnapshot);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Commit_PresentationFailure_RestoresToolAndPresentation()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            var validToolPlan = new RoadToolFullReset(RoadToolType.Remove, RoadType.Arterial);
            var invalidPresentation = new RoadPresentationFullReset(
                new RoadRenderToken(0, 7, 0, 0, 0, 1),
                new RoadSurfaceSnapshot(new GraphStateToken(-1, 0, 0), []));
            var plan = new V3LoadPreflightPlan(
                revision,
                V3ToolLoadParticipant.Prepare(validToolPlan),
                V3RendererLoadParticipant.Prepare(invalidPresentation));
            var coordinator = new V3LoadAggregateCoordinator(["tool", "renderer"]);
            Assert.True(coordinator.TryBegin());
            Assert.True(coordinator.TryEnterPrepare());
            Assert.True(coordinator.TryPrepare("tool"));
            Assert.True(coordinator.TryPrepare("renderer"));
            Assert.True(coordinator.TryEnterPreflight());

            var prepare = new V3RoadLoadPrepareResult(true, V3LoadPhase.Preflight, plan, null, coordinator)
            {
                SlotId = "city-001",
                ToolPlan = validToolPlan,
                PresentationPlan = invalidPresentation,
            };

            var toolState = new RoadToolState();
            toolState.SwitchTo(RoadToolType.Place);
            toolState.TrySelectRoadType(RoadType.Street);
            var presentationState = new RoadPresentationState(new RoadRenderToken(0, 0, 0, 0, 0, 0));

            V3RoadLoadPipelineResult result = prepare.Commit(7, toolState, presentationState, "city-001");

            Assert.False(result.Success);
            Assert.Equal(RoadToolType.Place, toolState.CurrentTool);
            Assert.Equal(RoadType.Street, toolState.SelectedRoadType);
            Assert.Equal(new RoadRenderToken(0, 0, 0, 0, 0, 0), presentationState.DesiredToken);
            Assert.Equal(new RoadRenderToken(0, 0, 0, 0, 0, 0), presentationState.PresentedToken);
            Assert.Null(presentationState.PresentedSnapshot);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void TryLoadIntoController_WithToolState_AppliesEmptyToolRoot()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            Assert.True(V3RoadSavePipeline.Save("city-001", root, revision, "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null));
            var controller = new RoadGraphV3Controller(
                new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default), 1),
                new RoadEditHistoryV3(100, 100000));
            var toolState = new RoadToolState();
            toolState.SwitchTo(RoadToolType.Upgrade);
            toolState.TrySelectRoadType(RoadType.Highway);

            bool result = V3RoadLoadPipeline.TryLoadIntoController(
                "city-001",
                root,
                RoadGraphCapacity.Default,
                V3PayloadBudget.Default,
                controller,
                toolState,
                newLineageID: 7);

            Assert.True(result);
            Assert.Equal(revision.Nodes.Count, controller.Facade.Revision.Nodes.Count);
            Assert.Equal(0, controller.History.UndoCount);
            Assert.Equal(7, controller.Facade.LineageID);
            Assert.Equal(RoadToolType.Upgrade, toolState.CurrentTool);
            Assert.Equal(RoadType.Highway, toolState.SelectedRoadType);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-loadpipe-{Guid.NewGuid():N}");

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
