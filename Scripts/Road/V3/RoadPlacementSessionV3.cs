using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

    public bool HasSelfIntersection
    {
        get
        {
            RoadPath? path = CurrentDraft.Path;
            if (path is null)
                return false;

            var segments = path.Segments.Where(segment => segment is not null).Select(segment => segment!).ToList();
            for (int index = 0; index < segments.Count; index++)
            {
                for (int other = index + 1; other < segments.Count; other++)
                {
                    if (other == index + 1)
                        continue;
                    if (SegmentsIntersect(
                            segments[index].Start,
                            segments[index].End,
                            segments[other].Start,
                            segments[other].End))
                        return true;
                }
            }

            return false;
        }
    }

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

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float orientationAB_C = Cross(b - a, c - a);
        float orientationAB_D = Cross(b - a, d - a);
        float orientationCD_A = Cross(d - c, a - c);
        float orientationCD_B = Cross(d - c, b - c);

        if (Mathf.Abs(orientationAB_C) < 1e-6f && IsOnSegment(a, b, c))
            return true;
        if (Mathf.Abs(orientationAB_D) < 1e-6f && IsOnSegment(a, b, d))
            return true;
        if (Mathf.Abs(orientationCD_A) < 1e-6f && IsOnSegment(c, d, a))
            return true;
        if (Mathf.Abs(orientationCD_B) < 1e-6f && IsOnSegment(c, d, b))
            return true;

        return (orientationAB_C * orientationAB_D < 0f) &&
               (orientationCD_A * orientationCD_B < 0f);
    }

    private static float Cross(Vector2 left, Vector2 right) =>
        left.X * right.Y - left.Y * right.X;

    private static bool IsOnSegment(Vector2 a, Vector2 b, Vector2 point)
    {
        return point.X >= Mathf.Min(a.X, b.X) - 1e-6f &&
               point.X <= Mathf.Max(a.X, b.X) + 1e-6f &&
               point.Y >= Mathf.Min(a.Y, b.Y) - 1e-6f &&
               point.Y <= Mathf.Max(a.Y, b.Y) + 1e-6f;
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
