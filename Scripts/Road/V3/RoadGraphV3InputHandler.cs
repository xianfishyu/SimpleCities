using Godot;
using SimpleCities.Road.V3;

/// <summary>
/// V3 最小输入处理器：左键点击时在鼠标位置建造一小段当前类型道路，用于场景内验证 V3 管线。
/// </summary>
public partial class RoadGraphV3InputHandler : Node2D
{
    [Export] public float SegmentLength { get; set; } = 100f;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton ||
            !mouseButton.Pressed ||
            mouseButton.ButtonIndex != MouseButton.Left)
            return;

        RoadGraphV3System? system = RoadGraphV3System.Instance;
        if (system is null)
            return;

        Vector2 start = GetGlobalMousePosition();
        system.TryBuildFromPolyline(
            [start, start + new Vector2(SegmentLength, 0f)],
            system.ToolState.SelectedRoadType,
            out _);
    }
}
