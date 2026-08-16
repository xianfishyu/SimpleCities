using System;
using System.Collections.Generic;
using System.Linq;
using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

public sealed record V3RoadLoadPipelineResult(
    bool Success,
    RoadGraphV3Controller? Controller,
    V3LoadPhase Phase,
    string? Error)
{
    public RoadToolFullReset? ToolPlan { get; init; }
    public RoadPresentationFullReset? PresentationPlan { get; init; }
    public string SlotId { get; init; } = string.Empty;

    public bool TryApplyParticipants(RoadToolState toolState, RoadPresentationState presentationState)
    {
        ArgumentNullException.ThrowIfNull(toolState);
        ArgumentNullException.ThrowIfNull(presentationState);

        if (ToolPlan is not null && !ToolPlan.TryApplyTo(toolState))
            return false;
        if (PresentationPlan is not null && !PresentationPlan.TryApplyTo(presentationState))
            return false;
        return true;
    }

    public static V3RoadLoadPipelineResult Failure(V3LoadPhase phase, string error) =>
        new(false, null, phase, error);
}

public sealed record V3RoadLoadPrepareResult(
    bool Success,
    V3LoadPhase Phase,
    V3LoadPreflightPlan? Plan,
    string? Error,
    V3LoadAggregateCoordinator? Coordinator)
{
    public string SlotId { get; init; } = string.Empty;
    public RoadToolFullReset? ToolPlan { get; init; }
    public RoadPresentationFullReset? PresentationPlan { get; init; }

    public bool TryApplyParticipants(RoadToolState toolState, RoadPresentationState presentationState)
    {
        ArgumentNullException.ThrowIfNull(toolState);
        ArgumentNullException.ThrowIfNull(presentationState);

        if (ToolPlan is not null && !ToolPlan.TryApplyTo(toolState))
            return false;
        if (PresentationPlan is not null && !PresentationPlan.TryApplyTo(presentationState))
            return false;
        return true;
    }

    public V3RoadLoadPipelineResult Commit(
        long lineageID,
        RoadToolState? toolState = null,
        RoadPresentationState? presentationState = null,
        string? slotId = null,
        Action? insideCommit = null) =>
        V3RoadLoadPipeline.Commit(this, lineageID, toolState, presentationState, slotId, insideCommit);

    public static V3RoadLoadPrepareResult Failure(V3LoadPhase phase, string error) =>
        new(false, phase, null, error, null);
}

/// <summary>
/// 道路 Load 管线：按 Admission -> Prepare -> Preflight -> Commit 四阶段加载槽，
/// 在 Commit 阶段构造新的不可变 facade/controller，失败时协议进入 Failed。
/// </summary>
public static class V3RoadLoadPipeline
{
    public const string RequiredParticipant = "road-graph";
    public const string ToolParticipant = "tool";
    public const string RendererParticipant = "renderer";

    public static V3RoadLoadPipelineResult Load(
        string slotId,
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget,
        long lineageID = 1,
        RoadToolState? preservedToolState = null,
        RoadStyleProvider? styles = null,
        RoadRenderToken? desiredPresentationToken = null,
        RoadToolState? commitToolState = null,
        RoadPresentationState? commitPresentationState = null,
        Action? insideCommit = null)
    {
        V3RoadLoadPrepareResult prepare = Prepare(
            slotId,
            root,
            capacity,
            budget,
            lineageID,
            preservedToolState,
            styles,
            desiredPresentationToken);
        return Commit(prepare, lineageID, commitToolState, commitPresentationState, slotId, insideCommit);
    }

    public static V3RoadLoadPipelineResult Commit(
        V3RoadLoadPrepareResult prepare,
        long lineageID,
        RoadToolState? toolState = null,
        RoadPresentationState? presentationState = null,
        string? slotId = null,
        Action? insideCommit = null)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        if (!prepare.Success || prepare.Plan is null || prepare.Coordinator is null)
            return V3RoadLoadPipelineResult.Failure(prepare.Phase, prepare.Error ?? "PrepareFailed");

        RoadGraphV3Controller? createdController = null;
        bool committed = prepare.Coordinator.TryCommit(() =>
        {
            createdController = prepare.Plan.CreateController(lineageID);

            RoadToolStateSnapshot? toolSnapshot = null;
            RoadPresentationStateSnapshot? presentationSnapshot = null;
            try
            {
                if (toolState is not null && prepare.ToolPlan is not null)
                {
                    toolSnapshot = toolState.Capture();
                    if (!prepare.ToolPlan.TryApplyTo(toolState))
                        throw new InvalidOperationException("ToolPlanRejected");
                }

                if (presentationState is not null && prepare.PresentationPlan is not null)
                {
                    presentationSnapshot = presentationState.Capture();
                    if (!prepare.PresentationPlan.TryApplyTo(presentationState))
                        throw new InvalidOperationException("PresentationPlanRejected");
                }

                insideCommit?.Invoke();
            }
            catch
            {
                if (toolSnapshot is not null)
                    toolState!.Restore(toolSnapshot.Value);
                if (presentationSnapshot is not null)
                    presentationState!.Restore(presentationSnapshot.Value);
                throw;
            }
        });
        if (!committed || createdController is null)
            return V3RoadLoadPipelineResult.Failure(prepare.Coordinator.Phase, "CommitFailed");

        return new V3RoadLoadPipelineResult(true, createdController, prepare.Coordinator.Phase, null)
        {
            ToolPlan = prepare.ToolPlan,
            PresentationPlan = prepare.PresentationPlan,
            SlotId = slotId ?? prepare.SlotId,
        };
    }

    public static V3RoadLoadPrepareResult Prepare(
        string slotId,
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget,
        long lineageID = 1,
        RoadToolState? preservedToolState = null,
        RoadStyleProvider? styles = null,
        RoadRenderToken? desiredPresentationToken = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        var required = new List<string> { RequiredParticipant };
        if (preservedToolState is not null)
            required.Add(ToolParticipant);
        if (styles is not null && desiredPresentationToken is not null)
            required.Add(RendererParticipant);
        var coordinator = new V3LoadAggregateCoordinator(required);
        if (!coordinator.TryBegin())
            return V3RoadLoadPrepareResult.Failure(coordinator.Phase, "AdmissionRejected");

        if (!coordinator.TryEnterPrepare())
        {
            coordinator.Fail();
            return V3RoadLoadPrepareResult.Failure(coordinator.Phase, "PrepareRejected");
        }

        V3SlotLoadServiceResult load = V3SlotLoadService.Load(slotId, root, capacity, budget);
        if (!load.Success || load.Revision is null)
        {
            coordinator.Fail();
            return V3RoadLoadPrepareResult.Failure(coordinator.Phase, load.Error ?? "LoadFailed");
        }

        RoadPresentationFullReset? presentationPlan = null;
        if (styles is not null && desiredPresentationToken is not null)
        {
            RoadSurfaceSnapshotBuildResult surface = RoadSurfaceSnapshotBuilder.Build(
                load.Revision,
                new GraphStateToken(lineageID, 0, 0),
                styles);
            if (!surface.Success || surface.Snapshot is null)
            {
                coordinator.Fail();
                return V3RoadLoadPrepareResult.Failure(coordinator.Phase, surface.Error ?? "PresentationPreflightFailed");
            }

            var ribbonMeshes = new List<RoadRibbonMeshData>();
            foreach (RoadGraphV3Edge edge in load.Revision.Edges.Values.OrderBy(edge => edge.ID))
            {
                if (styles.TryGet(edge.RoadType, out RoadTypeStyle? edgeStyle) &&
                    RoadRibbonBuilder.TryBuild(edge, edgeStyle, RoadGeometryDisplaySampler.DefaultTolerance, out RoadRibbonMeshData ribbonMesh))
                {
                    ribbonMeshes.Add(ribbonMesh);
                }
            }

            var junctionPatches = new List<RoadJunctionPatchData>();
            foreach (int nodeID in load.Revision.Nodes.Keys.Order())
            {
                if (RoadJunctionPatchBuilder.TryBuild(load.Revision, styles, nodeID, RoadJunctionPatchBuilder.DefaultRadius, out RoadJunctionPatchData patch))
                    junctionPatches.Add(patch);
            }

            presentationPlan = RoadPresentationFullReset.Create(
                desiredPresentationToken.Value,
                surface.Snapshot,
                ribbonMeshes,
                junctionPatches);
        }

        V3ToolLoadParticipant? toolParticipant = null;
        if (preservedToolState is not null)
            toolParticipant = V3ToolLoadParticipant.Prepare(RoadToolFullReset.Prepare(preservedToolState));

        V3RendererLoadParticipant? rendererParticipant = null;
        if (presentationPlan is not null)
            rendererParticipant = V3RendererLoadParticipant.Prepare(presentationPlan);

        var preflightPlan = new V3LoadPreflightPlan(load.Revision, toolParticipant, rendererParticipant);
        if (!preflightPlan.CanCommit)
        {
            coordinator.Fail();
            return V3RoadLoadPrepareResult.Failure(coordinator.Phase, "PreflightPlanRejected");
        }

        if (toolParticipant is not null && !coordinator.TryPrepare(toolParticipant))
        {
            coordinator.Fail();
            return V3RoadLoadPrepareResult.Failure(coordinator.Phase, "ToolParticipantRejected");
        }

        if (rendererParticipant is not null && !coordinator.TryPrepare(rendererParticipant))
        {
            coordinator.Fail();
            return V3RoadLoadPrepareResult.Failure(coordinator.Phase, "RendererParticipantRejected");
        }

        if (!coordinator.TryPrepare(RequiredParticipant))
        {
            coordinator.Fail();
            return V3RoadLoadPrepareResult.Failure(coordinator.Phase, "PrepareParticipantRejected");
        }

        if (!coordinator.TryEnterPreflight())
        {
            coordinator.Fail();
            return V3RoadLoadPrepareResult.Failure(coordinator.Phase, "PreflightRejected");
        }

        return new V3RoadLoadPrepareResult(true, coordinator.Phase, preflightPlan, null, coordinator)
        {
            SlotId = slotId,
            ToolPlan = toolParticipant?.Plan,
            PresentationPlan = presentationPlan,
        };
    }

    public static bool TryLoadIntoController(
        string slotId,
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget,
        RoadGraphV3Controller controller,
        long newLineageID = 1)
    {
        ArgumentNullException.ThrowIfNull(controller);

        V3RoadLoadPipelineResult result = Load(slotId, root, capacity, budget, newLineageID);
        if (!result.Success || result.Controller is null)
            return false;

        controller.ReplaceWithFullReset(result.Controller.Facade.Revision, newLineageID);
        return true;
    }

    public static bool TryLoadIntoController(
        string slotId,
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget,
        RoadGraphV3Controller controller,
        RoadToolState toolState,
        long newLineageID = 1)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(toolState);

        V3RoadLoadPipelineResult result = Load(slotId, root, capacity, budget, newLineageID, toolState);
        if (!result.Success || result.Controller is null || result.ToolPlan is null)
            return false;

        controller.ReplaceWithFullReset(result.Controller.Facade.Revision, newLineageID);
        return result.ToolPlan.TryApplyTo(toolState);
    }
}
