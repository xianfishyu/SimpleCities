namespace SimpleCities.Road.V3;

/// <summary>
/// V3 工具类型。
/// </summary>
public enum RoadToolType
{
    Place,
    Remove,
    Upgrade,
    Select,
}

/// <summary>
/// 工具状态的可回滚快照，供 non-yield Load commit 在异常时恢复旧会话。
/// </summary>
public readonly record struct RoadToolStateSnapshot(
    RoadToolType CurrentTool,
    RoadType SelectedRoadType);

/// <summary>
/// V3 工具状态：当前工具与已选 RoadType。
/// </summary>
public sealed class RoadToolState
{
    public RoadToolType CurrentTool { get; private set; } = RoadToolType.Place;
    public RoadType SelectedRoadType { get; private set; } = RoadType.Street;

    public void SwitchTo(RoadToolType tool) => CurrentTool = tool;

    public bool TrySelectRoadType(RoadType roadType)
    {
        if (!RoadTypeChangeValidator.IsValidRoadType(roadType))
            return false;

        SelectedRoadType = roadType;
        return true;
    }

    public RoadToolStateSnapshot Capture() => new(CurrentTool, SelectedRoadType);

    public void Restore(RoadToolStateSnapshot snapshot)
    {
        CurrentTool = snapshot.CurrentTool;
        SelectedRoadType = snapshot.SelectedRoadType;
    }
}
