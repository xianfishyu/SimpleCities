using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 Load 的 presentation full-reset 计划：在 Preflight 阶段绑定目标 token、已构建 surface snapshot
/// 以及 ribbon/junction mesh 数据，在 non-yield commit 中一次设置 desired/presented，
/// 避免图已交换但表现未就绪的窗口。
/// </summary>
public sealed record RoadPresentationFullReset(
    RoadRenderToken DesiredToken,
    RoadSurfaceSnapshot Snapshot)
{
    public IReadOnlyList<RoadRibbonMeshData> RibbonMeshes { get; init; } = [];
    public IReadOnlyList<RoadJunctionPatchData> JunctionPatches { get; init; } = [];
    public IReadOnlyList<RoadCapMeshData> CapMeshes { get; init; } = [];

    public bool IsValid => Snapshot.IsValid;
    public bool HasMeshData => RibbonMeshes.Count > 0 || JunctionPatches.Count > 0 || CapMeshes.Count > 0;

    public static RoadPresentationFullReset Create(RoadRenderToken desiredToken, RoadSurfaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new RoadPresentationFullReset(desiredToken, snapshot);
    }

    public static RoadPresentationFullReset Create(
        RoadRenderToken desiredToken,
        RoadSurfaceSnapshot snapshot,
        IReadOnlyList<RoadRibbonMeshData> ribbonMeshes,
        IReadOnlyList<RoadJunctionPatchData> junctionPatches)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(ribbonMeshes);
        ArgumentNullException.ThrowIfNull(junctionPatches);
        return new RoadPresentationFullReset(desiredToken, snapshot)
        {
            RibbonMeshes = ribbonMeshes,
            JunctionPatches = junctionPatches,
            CapMeshes = [],
        };
    }

    public static RoadPresentationFullReset Create(
        RoadRenderToken desiredToken,
        RoadSurfaceSnapshot snapshot,
        IReadOnlyList<RoadRibbonMeshData> ribbonMeshes,
        IReadOnlyList<RoadJunctionPatchData> junctionPatches,
        IReadOnlyList<RoadCapMeshData> capMeshes)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(ribbonMeshes);
        ArgumentNullException.ThrowIfNull(junctionPatches);
        ArgumentNullException.ThrowIfNull(capMeshes);
        return new RoadPresentationFullReset(desiredToken, snapshot)
        {
            RibbonMeshes = ribbonMeshes,
            JunctionPatches = junctionPatches,
            CapMeshes = capMeshes,
        };
    }

    public static RoadPresentationFullReset Prepare(RoadPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.PresentedSnapshot is null)
            throw new InvalidOperationException("Cannot prepare a presentation full reset without a presented snapshot.");

        return new RoadPresentationFullReset(state.DesiredToken, state.PresentedSnapshot);
    }

    public bool TryApplyTo(RoadPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!IsValid)
            return false;

        state.SetDesired(DesiredToken);
        return state.TryPublish(DesiredToken, Snapshot);
    }
}
