using Godot;
using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 类型化铺路会话：固定拐点 + 当前可移动末端，提交时生成带目标 RoadType 的建造请求。
/// </summary>
public sealed class RoadPlacementSessionV3
{
    private readonly RoadType _roadType;
    private readonly Vector2 _startPosition;
    private readonly List<Vector2> _fixedPoints = [];
    private Vector2? _currentPointer;

    public RoadType RoadType => _roadType;
    public Vector2 StartPosition => _startPosition;
    public int FixedCornerCount => _fixedPoints.Count;
    public Vector2 CurrentAnchor => _fixedPoints.Count == 0 ? _startPosition : _fixedPoints[^1];
    public Vector2? CurrentPointer => _currentPointer;

    public RoadPathDraft CurrentDraft => BuildDraft();

    public RoadPlacementSessionV3(RoadType roadType, Vector2 startPosition)
    {
        if (!RoadTypeChangeValidator.IsValidRoadType(roadType))
            throw new ArgumentOutOfRangeException(nameof(roadType), roadType, "Unknown road type.");
        if (!startPosition.IsFinite())
            throw new ArgumentException("A placement session needs a finite start position.", nameof(startPosition));

        _roadType = roadType;
        _startPosition = startPosition;
    }

    public RoadPathDraft Update(Vector2 pointerPosition)
    {
        if (!pointerPosition.IsFinite())
            throw new ArgumentException("Pointer position must be finite.", nameof(pointerPosition));

        _currentPointer = pointerPosition.DistanceTo(CurrentAnchor) > 0f ? pointerPosition : null;
        return CurrentDraft;
    }

    public bool IsClosed => _fixedPoints.Count > 0 && _currentPointer == _startPosition;

    public bool TryClose()
    {
        if (_fixedPoints.Count == 0)
            return false;

        _currentPointer = _startPosition;
        return true;
    }

    public bool TryAddPoint(Vector2 pointerPosition)
    {
        if (pointerPosition.DistanceTo(CurrentAnchor) <= 0f)
            return false;

        Update(pointerPosition);
        if (CurrentDraft.Path is null)
            return false;

        _fixedPoints.Add(pointerPosition);
        _currentPointer = null;
        return true;
    }

    public bool TryRemoveLastPoint()
    {
        if (_fixedPoints.Count == 0)
            return false;

        _fixedPoints.RemoveAt(_fixedPoints.Count - 1);
        return true;
    }

    public bool TryCommit(out RoadBuildRequest request)
    {
        request = null!;
        RoadPathDraft draft = CurrentDraft;
        if (draft.Path is null)
            return false;

        request = new RoadBuildRequest(draft.Path, _roadType);
        return true;
    }

    private RoadPathDraft BuildDraft()
    {
        var points = new List<Vector2> { _startPosition };
        points.AddRange(_fixedPoints);
        if (_currentPointer is Vector2 pointer)
            points.Add(pointer);
        return RoadPathDraft.FromPolyline(points);
    }
}
