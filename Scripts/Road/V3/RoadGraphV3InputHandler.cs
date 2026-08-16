using Godot;
using SimpleCities.Road.V3;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// V3 最小连续铺路/改造/拆除输入处理器：左键按当前工具添加拐点、闭合或选择表面命中，
/// 右键移除最后拐点，Enter 提交当前会话，Esc 取消，P/R/U 切换工具。
/// </summary>
public partial class RoadGraphV3InputHandler : Node2D
{
    [Export] public float CloseRadius { get; set; } = 20f;
    [Export] public float HitRadius { get; set; } = 20f;

    private RoadToolInputRouter? _router;

    public bool IsPlacing => Router?.IsPlacing ?? false;
    public bool IsClosed => Router?.IsPlacementClosed ?? false;
    public int FixedCornerCount => Router?.PlacementSession?.FixedCornerCount ?? 0;

    private RoadToolInputRouter? Router
    {
        get
        {
            if (_router is not null)
                return _router;

            RoadGraphV3System? system = RoadGraphV3System.Instance;
            if (system is null)
                return null;

            _router = new RoadToolInputRouter(
                system.ToolState,
                (point, radius) =>
                {
                    if (system.TryFindClosestSurfaceHit(point, radius, out RoadSurfaceHit hit))
                        return hit;
                    return null;
                });
            return _router;
        }
    }

    public override void _Draw()
    {
        RoadToolInputRouter? router = Router;
        RoadGraphV3System? system = RoadGraphV3System.Instance;
        if (router is null || system is null)
            return;

        if (router.PlacementSession is RoadPlacementSessionV3 session)
        {
            Vector2[] points = session.CurrentDraft.PreviewPoints.ToArray();
            if (points.Length >= 2 &&
                system.Application.DefaultStyles.TryGet(system.ToolState.SelectedRoadType, out RoadTypeStyle? style))
            {
                DrawPolyline(points, style.Color, style.Width, true);

                if (session.TryGetClosedDraft(GetGlobalMousePosition(), CloseRadius, out _))
                {
                    Color closeColor = new(style.Color.R, style.Color.G, style.Color.B, 0.5f);
                    DrawLine(session.CurrentAnchor, session.StartPosition, closeColor, style.Width * 0.75f, true);
                }
            }
        }

        if (router.UpgradeSession is RoadUpgradeSessionV3 upgrade)
            DrawSelectionHighlights(system, upgrade.SelectedEdgeIDs, new Color(1f, 1f, 0f, 0.8f));
        if (router.RemovalSession is RoadRemovalSessionV3 removal)
            DrawSelectionHighlights(system, removal.SelectedEdgeIDs, new Color(1f, 0.2f, 0.2f, 0.8f));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        QueueRedraw();
        RoadGraphV3System? system = RoadGraphV3System.Instance;
        if (system is null)
            return;

        RoadToolInputRouter? router = Router;
        if (router is null)
            return;

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            Vector2 position = GetGlobalMousePosition();
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                router.HandleLeftClick(position, CloseRadius, HitRadius);
                if (router.IsPlacementClosed &&
                    router.TryTakePlacementSession(out RoadPlacementSessionV3 session))
                {
                    system.TryBuild(session, out _);
                }

                return;
            }

            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                router.HandleRightClick();
                return;
            }
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            switch (keyEvent.Keycode)
            {
                case Key.Key1:
                    router.TrySelectRoadType(RoadType.Dirt);
                    break;
                case Key.Key2:
                    router.TrySelectRoadType(RoadType.Street);
                    break;
                case Key.Key3:
                    router.TrySelectRoadType(RoadType.Arterial);
                    break;
                case Key.Key4:
                    router.TrySelectRoadType(RoadType.Highway);
                    break;
                case Key.P:
                    router.SwitchTool(RoadToolType.Place);
                    break;
                case Key.R:
                    router.SwitchTool(RoadToolType.Remove);
                    break;
                case Key.U:
                    router.SwitchTool(RoadToolType.Upgrade);
                    break;
                case Key.Z when keyEvent.CtrlPressed:
                    system.TryUndo(out _);
                    break;
                case Key.Y when keyEvent.CtrlPressed:
                    system.TryRedo(out _);
                    break;
                case Key.Escape:
                    router.Cancel();
                    break;
                case Key.Enter:
                case Key.KpEnter:
                    CommitActive(router, system);
                    break;
            }
        }
    }

    private void DrawSelectionHighlights(
        RoadGraphV3System system,
        IEnumerable<int> edgeIDs,
        Color color)
    {
        foreach (int edgeID in edgeIDs)
        {
            if (!system.Revision.Edges.TryGetValue(edgeID, out RoadGraphV3Edge? edge))
                continue;

            Vector2[] points = RoadGeometryDisplaySampler.SampleSegments(edge.Geometry);
            if (points.Length >= 2)
                DrawPolyline(points, color, 4f, true);
        }
    }

    private static void CommitActive(RoadToolInputRouter router, RoadGraphV3System system)
    {
        if (router.TryTakePlacementSession(out RoadPlacementSessionV3 placement) &&
            !placement.HasSelfIntersection)
        {
            system.TryBuild(placement, out _);
            return;
        }

        if (router.TryTakeUpgradeSession(out RoadUpgradeSessionV3 upgrade))
        {
            system.TryUpgrade(upgrade, out _);
            return;
        }

        if (router.TryTakeRemovalSession(out RoadRemovalSessionV3 removal))
        {
            system.TryRemove(removal, out _);
        }
    }
}
