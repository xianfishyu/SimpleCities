using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 Load 的 empty tool root 计划：在 Preflight 阶段捕获需要保留的工具状态，
/// 并在 non-yield commit 中一次应用到新工具根；不携带任何活动会话/选择/预览。
/// </summary>
public sealed record RoadToolFullReset(
    RoadToolType CurrentTool,
    RoadType SelectedRoadType)
{
    public bool IsValid => RoadTypeChangeValidator.IsValidRoadType(SelectedRoadType);

    public static RoadToolFullReset Prepare(RoadToolState currentState)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        return new RoadToolFullReset(currentState.CurrentTool, currentState.SelectedRoadType);
    }

    public bool TryApplyTo(RoadToolState target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!IsValid)
            return false;

        target.SwitchTo(CurrentTool);
        return target.TrySelectRoadType(SelectedRoadType);
    }
}
