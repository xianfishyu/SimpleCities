using Godot;
using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 工具输入路由器：根据当前工具维护铺路/改造/拆除会话，并把点击解析为会话动作。
/// 它不直接提交图变更；调用方通过 TryTake*Session 取走已完成的会话后交给执行器提交。
/// </summary>
public sealed class RoadToolInputRouter
{
    private readonly RoadToolState _toolState;
    private readonly Func<Vector2, float, RoadSurfaceHit?> _resolveHit;
    private RoadPlacementSessionV3? _placement;
    private RoadUpgradeSessionV3? _upgrade;
    private RoadRemovalSessionV3? _removal;

    public RoadToolType CurrentTool => _toolState.CurrentTool;
    public bool IsPlacing => _placement is not null;
    public bool IsSelecting => _upgrade is not null || _removal is not null;
    public bool HasActiveSession => IsPlacing || IsSelecting;
    public bool IsPlacementClosed => _placement?.IsClosed ?? false;
    public RoadPlacementSessionV3? PlacementSession => _placement;
    public RoadUpgradeSessionV3? UpgradeSession => _upgrade;
    public RoadRemovalSessionV3? RemovalSession => _removal;

    public RoadToolInputRouter(
        RoadToolState toolState,
        Func<Vector2, float, RoadSurfaceHit?> resolveHit)
    {
        _toolState = toolState ?? throw new ArgumentNullException(nameof(toolState));
        _resolveHit = resolveHit ?? throw new ArgumentNullException(nameof(resolveHit));
    }

    public void SwitchTool(RoadToolType tool)
    {
        _toolState.SwitchTo(tool);
        ClearSessions();
    }

    public bool TrySelectRoadType(RoadType roadType)
    {
        if (!RoadTypeChangeValidator.IsValidRoadType(roadType))
            return false;
        if (_toolState.SelectedRoadType == roadType)
            return true;

        _placement = null;
        _upgrade?.TrySetTargetType(roadType);
        return _toolState.TrySelectRoadType(roadType);
    }

    public bool HandleLeftClick(Vector2 point, float closeRadius, float hitRadius)
    {
        if (!point.IsFinite())
            throw new ArgumentException("Pointer position must be finite.", nameof(point));

        return CurrentTool switch
        {
            RoadToolType.Place => HandlePlacementClick(point, closeRadius),
            RoadToolType.Upgrade => HandleSelectionClick(point, hitRadius, upgrade: true),
            RoadToolType.Remove => HandleSelectionClick(point, hitRadius, upgrade: false),
            _ => false,
        };
    }

    public bool HandleRightClick()
    {
        if (_placement is not null)
        {
            if (!_placement.TryRemoveLastPoint() || _placement.FixedCornerCount == 0)
                _placement = null;
            return true;
        }

        return false;
    }

    public int HandleSelectionHits(IEnumerable<RoadSurfaceHit> hits, bool upgrade)
    {
        ArgumentNullException.ThrowIfNull(hits);
        if (upgrade)
        {
            _upgrade ??= new RoadUpgradeSessionV3(_toolState.SelectedRoadType);
            return _upgrade.TrySelectHits(hits);
        }

        _removal ??= new RoadRemovalSessionV3();
        return _removal.TrySelectHits(hits);
    }

    public int HandleSelectionRect(
        Rect2 rect,
        Func<Rect2, IReadOnlyList<RoadSurfaceHit>> resolveRect,
        bool upgrade)
    {
        ArgumentNullException.ThrowIfNull(resolveRect);
        if (!rect.HasArea())
            return 0;

        return HandleSelectionHits(resolveRect(rect), upgrade);
    }

    public void Cancel() => ClearSessions();

    public bool TryTakePlacementSession(out RoadPlacementSessionV3 session)
    {
        if (_placement is null)
        {
            session = null!;
            return false;
        }

        session = _placement;
        _placement = null;
        return true;
    }

    public bool TryTakeUpgradeSession(out RoadUpgradeSessionV3 session)
    {
        if (_upgrade is null)
        {
            session = null!;
            return false;
        }

        session = _upgrade;
        _upgrade = null;
        return true;
    }

    public bool TryTakeRemovalSession(out RoadRemovalSessionV3 session)
    {
        if (_removal is null)
        {
            session = null!;
            return false;
        }

        session = _removal;
        _removal = null;
        return true;
    }

    private bool HandlePlacementClick(Vector2 point, float closeRadius)
    {
        if (_placement is null)
        {
            _placement = new RoadPlacementSessionV3(_toolState.SelectedRoadType, point);
            return true;
        }

        if (_placement.TryGetClosedDraft(point, closeRadius, out _) &&
            !_placement.HasClosedSelfIntersection(point, closeRadius))
        {
            _placement.TryClose(point, closeRadius);
            return true;
        }

        return _placement.TryAddPoint(point);
    }

    private bool HandleSelectionClick(Vector2 point, float hitRadius, bool upgrade)
    {
        RoadSurfaceHit? hit = _resolveHit(point, hitRadius);
        if (hit is null)
            return false;

        if (upgrade)
        {
            _upgrade ??= new RoadUpgradeSessionV3(_toolState.SelectedRoadType);
            return _upgrade.TrySelectHit(hit);
        }

        _removal ??= new RoadRemovalSessionV3();
        return _removal.TrySelectHit(hit);
    }

    private void ClearSessions()
    {
        _placement = null;
        _upgrade = null;
        _removal = null;
    }
}
