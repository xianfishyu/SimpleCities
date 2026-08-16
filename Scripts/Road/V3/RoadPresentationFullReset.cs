using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 Load 的 presentation full-reset 计划：在 Preflight 阶段绑定目标 token 与已构建 surface snapshot，
/// 在 non-yield commit 中一次设置 desired/presented，避免图已交换但表现未就绪的窗口。
/// </summary>
public sealed record RoadPresentationFullReset(
    RoadRenderToken DesiredToken,
    RoadSurfaceSnapshot Snapshot)
{
    public bool IsValid => Snapshot.IsValid;

    public static RoadPresentationFullReset Create(RoadRenderToken desiredToken, RoadSurfaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new RoadPresentationFullReset(desiredToken, snapshot);
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
