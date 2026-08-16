using SimpleCities.Road.V3;

/// <summary>
/// 第二代 UI 工具类型到 V3 工具状态的稳定映射。
/// </summary>
public static class ToolTypeExtensions
{
    public static RoadToolType ToRoadToolType(this ToolType toolType) => toolType switch
    {
        ToolType.Road => RoadToolType.Place,
        ToolType.RoadRemove => RoadToolType.Remove,
        ToolType.RoadUpgrade => RoadToolType.Upgrade,
        _ => RoadToolType.Select,
    };
}
