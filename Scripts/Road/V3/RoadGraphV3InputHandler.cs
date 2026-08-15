using Godot;
using SimpleCities.Road.V3;
using System.Linq;

/// <summary>
/// V3 最小连续铺路输入处理器：左键添加拐点，右键移除最后拐点，Enter 提交当前会话。
/// </summary>
public partial class RoadGraphV3InputHandler : Node2D
{
    [Export] public float CloseRadius { get; set; } = 20f;

    private RoadPlacementSessionV3? _session;

    public bool IsPlacing => _session is not null;
    public bool IsClosed => _session?.IsClosed ?? false;
    public int FixedCornerCount => _session?.FixedCornerCount ?? 0;

    public override void _Draw()
    {
        if (_session is null)
            return;

        RoadGraphV3System? system = RoadGraphV3System.Instance;
        if (system is null)
            return;

        Vector2[] points = _session.CurrentDraft.PreviewPoints.ToArray();
        if (points.Length < 2)
            return;

        if (!system.Application.DefaultStyles.TryGet(system.ToolState.SelectedRoadType, out RoadTypeStyle? style))
            return;

        DrawPolyline(points, style.Color, style.Width, true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        QueueRedraw();
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            RoadGraphV3System? system = RoadGraphV3System.Instance;
            if (system is null)
                return;

            Vector2 position = GetGlobalMousePosition();
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (_session is null)
                {
                    _session = new RoadPlacementSessionV3(system.ToolState.SelectedRoadType, position);
                }
                else if (_session.FixedCornerCount > 0 &&
                         position.DistanceTo(_session.StartPosition) <= CloseRadius)
                {
                    _session.TryClose();
                    if (!_session.HasSelfIntersection)
                    {
                        system.TryBuild(_session, out _);
                        _session = null;
                    }
                }
                else
                {
                    _session.TryAddPoint(position);
                }
                return;
            }

            if (mouseButton.ButtonIndex == MouseButton.Right && _session is not null)
            {
                _session.TryRemoveLastPoint();
                if (_session.FixedCornerCount == 0)
                    _session = null;
            }
            return;
        }

        if (@event is InputEventKey keyEvent &&
            keyEvent.Pressed &&
            !keyEvent.Echo)
        {
            RoadGraphV3System? system = RoadGraphV3System.Instance;
            if (system is null)
                return;

            switch (keyEvent.Keycode)
            {
                case Key.Key1:
                    system.ToolState.TrySelectRoadType(RoadType.Dirt);
                    break;
                case Key.Key2:
                    system.ToolState.TrySelectRoadType(RoadType.Street);
                    break;
                case Key.Key3:
                    system.ToolState.TrySelectRoadType(RoadType.Arterial);
                    break;
                case Key.Key4:
                    system.ToolState.TrySelectRoadType(RoadType.Highway);
                    break;
                case Key.Z when keyEvent.CtrlPressed:
                    system.TryUndo(out _);
                    break;
                case Key.Y when keyEvent.CtrlPressed:
                    system.TryRedo(out _);
                    break;
                case Key.Escape:
                    _session = null;
                    break;
                case Key.Enter:
                case Key.KpEnter:
                    if (_session is not null && !_session.HasSelfIntersection)
                        system.TryBuild(_session, out _);
                    _session = null;
                    break;
            }
        }
    }
}
