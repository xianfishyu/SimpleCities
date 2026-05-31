using Godot;
using System;
using System.Collections.Generic;

public partial class RoadBuilder : Node2D
{
    [Export] public RoadConfig Config { get; set; } = null!;

    private RoadNetwork? _network;
    private RoadRenderer? _renderer;

    /// <summary>
    /// 拖拽语义（最终规格）：
    ///   一次拖拽 = 一条单方向直路。
    ///   按下时记录起点，每帧把鼠标向量 (mouse - start) 投影到最接近的 8 方向；
    ///   投影长度（向下取整为格数）决定预览终点。鼠标改朝另一个方向时，整条路重新沿新方向发射，
    ///   而不是在末端拐弯。释放时若投影 ≥ 1 格则提交。
    /// </summary>
    private bool _isDragging;
    private Vector2 _dragStartPos;
    private Direction _currentDir;     // 仅在 _currentLength >= 1 时有意义
    private int _currentLength;        // 沿 _currentDir 的格数；0 表示尚未确定方向

    /// <summary>拆除工具激活时每帧更新悬停高亮</summary>
    private bool _isRemoveHoverActive;
    private int _lastHoveredSegmentID = -1;

    public void SetNetwork(RoadNetwork network) => _network = network;

    public override void _Ready()
    {
        _renderer = GetNode<RoadRenderer>("../RoadRenderer");
        if (Config == null)
        {
            GD.PushError("RoadBuilder: Config (RoadConfig resource) is not assigned in the scene.");
            // 退化：用默认 Config，避免空引用崩溃
            Config = new RoadConfig();
        }
    }

    public void HandlePlaceInput(InputEvent @event)
    {
        if (_network == null) return;

        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                BeginDrag();
            }
            else
            {
                EndDragAndCommit();
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_network == null || _renderer == null) return;

        if (_isDragging)
        {
            UpdateProjection();
        }
        else if (_isRemoveHoverActive)
        {
            UpdateRemoveHover();
        }
    }

    private void BeginDrag()
    {
        _isDragging = true;
        var mouseWorld = GetGlobalMousePosition();
        _dragStartPos = RoadNetwork.SnapToGrid(mouseWorld, Config.CellSize);
        _currentLength = 0;
        GD.Print($"[DRAG-START] mouse=({mouseWorld.X:F0},{mouseWorld.Y:F0}) snap=({_dragStartPos.X:F0},{_dragStartPos.Y:F0})");

        // 半格点吸附：若 snap 位置无 Segment，回退到几何最近点（waypoint/Junction）。
        if (_network != null && _network.FindSegmentAt(_dragStartPos) < 0)
        {
            var nearest = FindNearestRoadPoint(mouseWorld);
            if (nearest.HasValue)
            {
                _dragStartPos = nearest.Value.pos;
                GD.Print($"[DRAG-SNAP] half-grid fallback to ({_dragStartPos.X:F0},{_dragStartPos.Y:F0}) segID={nearest.Value.segmentID}");
            }
            else
                GD.Print("[DRAG-SNAP] no nearby road point found");
        }

        if (_renderer != null)
        {
            _renderer.PreviewFrom = _dragStartPos;
            _renderer.PreviewTo = _dragStartPos;
            _renderer.QueueRedraw();
        }
    }

    private void EndDragAndCommit()
    {
        if (!_isDragging) return;
        if (_network == null) { _isDragging = false; ClearPreview(); return; }

        // 用最新鼠标位置做一次投影，避免最后一帧鼠标移动未被 _Process 捕获
        UpdateProjection();

        _isDragging = false;
        ClearPreview();

        if (_currentLength <= 0) { GD.Print("[DRAG-END] length<=0, skip"); return; }

        // 半格起点：锚定到 dragging 反方向的整格，终/waypoints 全落整格
        Vector2 anchor;
        if (IsHalfGridStart)
        {
            var halfDisp = DirectionUtil.GetDisplacement(_currentDir);
            anchor = _dragStartPos - new Vector2(halfDisp.X, halfDisp.Y) * Config.CellSize / 2f;
        }
        else
            anchor = _dragStartPos;
        var endPos = ComputeEndPosFrom(anchor, _currentDir, _currentLength);
        GD.Print($"[DRAG-END] from=({_dragStartPos.X:F0},{_dragStartPos.Y:F0}) halfGrid={IsHalfGridStart} anchor=({anchor.X:F0},{anchor.Y:F0}) dir={_currentDir} len={_currentLength} to=({endPos.X:F0},{endPos.Y:F0}) wps={_currentLength-1}");

        var waypoints = new Vector2[_currentLength - 1];
        var disp = DirectionUtil.GetDisplacement(_currentDir);
        for (int i = 1; i < _currentLength; i++)
        {
            waypoints[i - 1] = new Vector2(
                anchor.X + disp.X * i * Config.CellSize,
                anchor.Y + disp.Y * i * Config.CellSize
            );
        }

        _network.AddRoad(_dragStartPos, endPos, waypoints, Config.CellSize);
        _currentLength = 0;
    }

    private void UpdateProjection()
    {
        var mouseWorld = GetGlobalMousePosition();
        var v = mouseWorld - _dragStartPos;

        // 半格起点仅允许对角延伸
        bool halfGridStart = !GridSystem.IsSnapGrid(_dragStartPos);

        Direction bestDir = Direction.E;
        float bestProj = 0f;
        foreach (var d in DirectionUtil.All)
        {
            if (halfGridStart && !IsDiagonal(d)) continue;
            var disp = DirectionUtil.GetDisplacement(d);
            float ux = disp.X;
            float uy = disp.Y;
            float invLen = 1f / Mathf.Sqrt(ux * ux + uy * uy);
            ux *= invLen; uy *= invLen;
            float proj = v.X * ux + v.Y * uy;
            if (proj > bestProj)
            {
                bestProj = proj;
                bestDir = d;
            }
        }

        float stepLen = DirectionUtil.Length(bestDir, Config.CellSize);
        int cells = Mathf.RoundToInt(bestProj / stepLen);
        if (cells < 0) cells = 0;

        _currentDir = bestDir;
        _currentLength = cells;

        if (_renderer != null)
        {
            _renderer.PreviewFrom = _dragStartPos;
            var bestDisp = DirectionUtil.GetDisplacement(bestDir);
            var anchor = IsHalfGridStart
                ? _dragStartPos - new Vector2(bestDisp.X, bestDisp.Y) * Config.CellSize / 2f
                : _dragStartPos;
            _renderer.PreviewTo = (cells > 0) ? ComputeEndPosFrom(anchor, bestDir, cells) : _dragStartPos;
            _renderer.QueueRedraw();
        }
    }

    private bool IsHalfGridStart => !GridSystem.IsSnapGrid(_dragStartPos);

    private Vector2 ComputeEndPosFrom(Vector2 anchor, Direction dir, int cells)
    {
        var disp = DirectionUtil.GetDisplacement(dir);
        return new Vector2(anchor.X + disp.X * cells * Config.CellSize,
                           anchor.Y + disp.Y * cells * Config.CellSize);
    }

    private static bool IsDiagonal(Direction d)
    {
        var disp = DirectionUtil.GetDisplacement(d);
        return Math.Abs(disp.X) == 1 && Math.Abs(disp.Y) == 1;
    }

    private Vector2 ComputeEndPos(Direction dir, int cells)
    {
        var disp = DirectionUtil.GetDisplacement(dir);
        return new Vector2(
            _dragStartPos.X + disp.X * cells * Config.CellSize,
            _dragStartPos.Y + disp.Y * cells * Config.CellSize
        );
    }

    public void HandleRemoveInput(InputEvent @event)
    {
        if (_network == null) return;

        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            // 点击 Segment 的任一格点（端点或 waypoint）→ 只拆这一段 Segment。
            // Road（连续路径聚合）会自动随 Segment 增删调整：若该 Road 因此变空则一并清掉，
            // 否则保留剩下的 Segment 仍归属同一 Road。整条 Road 拆除请用 RemoveRoad(roadID)。
            var snapped = RoadNetwork.SnapToGrid(GetGlobalMousePosition(), Config.CellSize);
            int segmentID = _network.FindSegmentAt(snapped);
            if (segmentID >= 0)
                _network.RemoveSegment(segmentID);
        }
    }

    /// <summary>
    /// 取消当前正在进行的铺路拖拽（被 ToolManager 在切换工具时调用）。
    /// </summary>
    public void CancelPlaceDrag()
    {
        if (!_isDragging) return;
        _isDragging = false;
        _currentLength = 0;
        ClearPreview();
    }

    private void ClearPreview()
    {
        if (_renderer != null)
        {
            _renderer.PreviewFrom = null;
            _renderer.PreviewTo = null;
            _renderer.QueueRedraw();
        }
    }

    // ── 拆除工具悬停高亮 ──

    /// <summary>
    /// 由 ToolManager 在切换到/离开拆除工具时调用。
    /// 切换离开时清除当前高亮状态。
    /// </summary>
    public void SetRemoveHoverActive(bool active)
    {
        _isRemoveHoverActive = active;
        if (!active)
            ClearRemoveHover();
    }

    private void UpdateRemoveHover()
    {
        var mouseWorld = GetGlobalMousePosition();
        var snapped = RoadNetwork.SnapToGrid(mouseWorld, Config.CellSize);
        int segmentID = _network!.FindSegmentAt(snapped);

        // 半格点吸附：若 snap 位置无 Segment，回退到几何最近点
        if (segmentID < 0)
        {
            var nearest = FindNearestRoadPoint(mouseWorld);
            if (nearest.HasValue)
                segmentID = nearest.Value.segmentID;
        }

        if (segmentID != _lastHoveredSegmentID)
        {
            _lastHoveredSegmentID = segmentID;
            _renderer!.HoveredSegmentID = segmentID >= 0 ? segmentID : null;
            _renderer!.QueueRedraw();
        }
    }

    private void ClearRemoveHover()
    {
        _lastHoveredSegmentID = -1;
        if (_renderer != null)
        {
            _renderer.HoveredSegmentID = null;
            _renderer.QueueRedraw();
        }
    }

    /// <summary>
    /// 几何最近点搜索：扫所有 Segment 的 waypoint 和 Junction，找距离鼠标最近的点及其所属 Segment。
    /// 用于半格点吸附——当 SnapToGrid 位置无 Segment 时，回退到实际路网上的最近点。
    /// 返回 (位置, SegmentID)；若范围内无路网点则 null。
    /// </summary>
    private (Vector2 pos, int segmentID)? FindNearestRoadPoint(Vector2 mousePos)
    {
        float bestDistSq = (Config.CellSize * 0.8f) * (Config.CellSize * 0.8f);
        Vector2? bestPos = null;
        int bestSegID = -1;

        if (_network == null) return null;

        foreach (var seg in _network.GetAllSegments())
        {
            var fj = _network.GetJunction(seg.FromJunctionID);
            var tj = _network.GetJunction(seg.ToJunctionID);
            if (fj == null || tj == null) continue;

            Check(fj.Position, seg.ID);
            Check(tj.Position, seg.ID);
            foreach (var wp in seg.Waypoints) Check(wp, seg.ID);
        }

        void Check(Vector2 pt, int sid)
        {
            float d2 = mousePos.DistanceSquaredTo(pt);
            if (d2 < bestDistSq) { bestDistSq = d2; bestPos = pt; bestSegID = sid; }
        }

        return bestPos.HasValue ? (bestPos.Value, bestSegID) : null;
    }
}
