namespace SimpleCities.Road.V3;

/// <summary>
/// V3 工具类型。
/// </summary>
public enum RoadToolType
{
    Place,
    Remove,
    Upgrade,
}

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
}
