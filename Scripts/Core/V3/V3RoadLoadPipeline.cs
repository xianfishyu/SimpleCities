using System;
using System.Collections.Generic;
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

    public static V3RoadLoadPipelineResult Failure(V3LoadPhase phase, string error) =>
        new(false, null, phase, error);
}

/// <summary>
/// 道路 Load 管线：按 Admission -> Prepare -> Preflight -> Commit 四阶段加载槽，
/// 在 Commit 阶段构造新的不可变 facade/controller，失败时协议进入 Failed。
/// </summary>
public static class V3RoadLoadPipeline
{
    public const string RequiredParticipant = "road-graph";

    public static V3RoadLoadPipelineResult Load(
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

        var protocol = new V3LoadProtocol();
        if (!protocol.TryEnterAdmission())
            return V3RoadLoadPipelineResult.Failure(protocol.Phase, "AdmissionRejected");

        var required = new HashSet<string>(StringComparer.Ordinal) { RequiredParticipant };
        var prepared = new HashSet<string>(StringComparer.Ordinal);

        if (!protocol.TryEnterPrepare())
        {
            protocol.Fail();
            return V3RoadLoadPipelineResult.Failure(protocol.Phase, "PrepareRejected");
        }

        V3SlotLoadServiceResult load = V3SlotLoadService.Load(slotId, root, capacity, budget);
        if (!load.Success || load.Revision is null)
        {
            protocol.Fail();
            return V3RoadLoadPipelineResult.Failure(protocol.Phase, load.Error ?? "LoadFailed");
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
                protocol.Fail();
                return V3RoadLoadPipelineResult.Failure(protocol.Phase, surface.Error ?? "PresentationPreflightFailed");
            }

            presentationPlan = RoadPresentationFullReset.Create(desiredPresentationToken.Value, surface.Snapshot);
        }

        prepared.Add(RequiredParticipant);
        var aggregate = new V3PreparedAggregate(required, prepared, []);
        if (!protocol.TryEnterPreflight() || !aggregate.AllPrepared)
        {
            protocol.Fail();
            return V3RoadLoadPipelineResult.Failure(protocol.Phase, "PreflightRejected");
        }

        if (!protocol.TryEnterCommit())
        {
            protocol.Fail();
            return V3RoadLoadPipelineResult.Failure(protocol.Phase, "CommitRejected");
        }

        var facade = new RoadGraphV3Facade(load.Revision, lineageID);
        var controller = new RoadGraphV3Controller(facade, new RoadEditHistoryV3(100, 100000));
        if (!protocol.Complete())
        {
            protocol.Fail();
            return V3RoadLoadPipelineResult.Failure(protocol.Phase, "CompleteRejected");
        }

        return new V3RoadLoadPipelineResult(true, controller, protocol.Phase, null)
        {
            ToolPlan = preservedToolState is null ? null : RoadToolFullReset.Prepare(preservedToolState),
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
