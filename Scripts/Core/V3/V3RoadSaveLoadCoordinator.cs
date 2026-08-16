using System;
using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

/// <summary>
/// 道路保存/加载协调器：在进程内 gate 持有期间执行道路槽 Save/Load，
/// 为真实应用装配提供统一的同步入口。
/// </summary>
public sealed class V3RoadSaveLoadCoordinator
{
    private readonly V3CoordinatorGate _gate;

    public V3RoadSaveLoadCoordinator() : this(new V3CoordinatorGate())
    {
    }

    public V3RoadSaveLoadCoordinator(V3CoordinatorGate gate)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public bool Save(
        string slotId,
        string root,
        RoadGraphV3Revision revision,
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile)
    {
        if (!_gate.TryAcquire(out Guid operationId))
            return false;

        try
        {
            return V3RoadSavePipeline.Save(
                slotId,
                root,
                revision,
                displayName,
                cityName,
                timestamp,
                population,
                funds,
                thumbnailFile);
        }
        finally
        {
            _gate.Release(operationId);
        }
    }

    public RoadGraphV3Controller? Load(
        string slotId,
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget,
        long lineageID = 1)
    {
        if (!_gate.TryAcquire(out Guid operationId))
            return null;

        try
        {
            V3RoadLoadPipelineResult result = V3RoadLoadPipeline.Load(
                slotId,
                root,
                capacity,
                budget,
                lineageID);
            return result.Success ? result.Controller : null;
        }
        finally
        {
            _gate.Release(operationId);
        }
    }

    public V3RoadLoadPipelineResult? LoadResult(
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
        if (!_gate.TryAcquire(out Guid operationId))
            return null;

        try
        {
            V3RoadLoadPrepareResult prepare = V3RoadLoadPipeline.Prepare(
                slotId,
                root,
                capacity,
                budget,
                lineageID,
                preservedToolState,
                styles,
                desiredPresentationToken);
            if (!prepare.Success)
                return V3RoadLoadPipelineResult.Failure(prepare.Phase, prepare.Error ?? "PrepareFailed");
            return V3RoadLoadPipeline.Commit(
                prepare,
                lineageID,
                commitToolState,
                commitPresentationState,
                slotId,
                insideCommit);
        }
        finally
        {
            _gate.Release(operationId);
        }
    }

    public V3RoadLoadPrepareResult? Prepare(
        string slotId,
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget,
        long lineageID = 1,
        RoadToolState? preservedToolState = null,
        RoadStyleProvider? styles = null,
        RoadRenderToken? desiredPresentationToken = null)
    {
        if (!_gate.TryAcquire(out Guid operationId))
            return null;

        try
        {
            return V3RoadLoadPipeline.Prepare(
                slotId,
                root,
                capacity,
                budget,
                lineageID,
                preservedToolState,
                styles,
                desiredPresentationToken);
        }
        finally
        {
            _gate.Release(operationId);
        }
    }
}
