using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>组合一次连续铺路会话中已固定的段和当前可移动末端。</summary>
public sealed class RoadPlacementSession
{
    private readonly IRoadInputStrategy _strategy;
    private readonly List<RoadPathDraft> _fixedDrafts = [];

    public Vector2 StartPosition { get; }
    public RoadType RoadType { get; }
    public int FixedCornerCount => _fixedDrafts.Count;
    public Vector2 CurrentAnchor => FixedCornerCount == 0
        ? StartPosition
        : _fixedDrafts[^1].PreviewTo;
    public RoadPathDraft CurrentDraft { get; private set; }

    public RoadPlacementSession(
        IRoadInputStrategy strategy,
        Vector2 startPosition,
        RoadType roadType)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        if (!startPosition.IsFinite())
            throw new ArgumentException("A placement session needs a finite start position.", nameof(startPosition));
        if (!RoadTypeContract.IsDefined(roadType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(roadType),
                roadType,
                "RoadType is not defined.");
        }

        _strategy = strategy;
        StartPosition = startPosition;
        RoadType = roadType;
        CurrentDraft = RoadPathDraft.Empty(startPosition);
    }

    public RoadPathDraft Update(Vector2 pointerPosition)
    {
        if (CurrentDraft.IsClosed)
            return CurrentDraft;

        RoadPathDraft movingDraft = BuildMovingDraft(pointerPosition);
        CurrentDraft = Compose(movingDraft);
        return CurrentDraft;
    }

    public bool TryAddPoint(Vector2 pointerPosition)
    {
        if (CurrentDraft.IsClosed)
            return false;

        RoadPathDraft segmentDraft = BuildMovingDraft(pointerPosition);
        if (!segmentDraft.CanCommit)
        {
            CurrentDraft = Compose(segmentDraft);
            return false;
        }

        _fixedDrafts.Add(segmentDraft);
        CurrentDraft = Compose(RoadPathDraft.Empty(CurrentAnchor));
        return true;
    }

    private RoadPathDraft BuildMovingDraft(Vector2 pointerPosition)
    {
        if (_fixedDrafts.Count == 0 ||
            RoadNumericPolicy.DistanceSquared(pointerPosition, StartPosition) >
            (double)_strategy.InteractionRadius * _strategy.InteractionRadius)
        {
            return _strategy.BuildDraft(CurrentAnchor, pointerPosition);
        }

        RoadPathDraft closure = _strategy.BuildDraft(CurrentAnchor, StartPosition);
        if (!closure.CanCommit)
        {
            if (RoadExactPredicates.SameBits(CurrentAnchor, StartPosition))
                return RoadPathDraft.Empty(CurrentAnchor);
            closure = RoadPathDraft.FromPolyline([CurrentAnchor, StartPosition]);
        }

        var previewPoints = new List<Vector2>(closure.PreviewPoints);
        previewPoints[^1] = StartPosition;
        IReadOnlyList<RoadGeometrySegment> geometry = RoadGeometryCanonicalizer.ReanchorChain(
            closure.Path!.Segments.Select(segment => segment!).ToArray(),
            CurrentAnchor,
            StartPosition);
        return new RoadPathDraft(previewPoints, new RoadPath(geometry.ToArray()));
    }

    public bool TryRemoveLastPoint(Vector2 pointerPosition)
    {
        if (_fixedDrafts.Count == 0)
            return false;

        _fixedDrafts.RemoveAt(_fixedDrafts.Count - 1);
        Update(pointerPosition);
        return true;
    }

    private RoadPathDraft Compose(RoadPathDraft movingDraft)
    {
        var previewPoints = new List<Vector2> { StartPosition };
        var geometrySegments = new List<RoadGeometrySegment?>();

        foreach (RoadPathDraft draft in _fixedDrafts)
            AppendDraft(draft, previewPoints, geometrySegments);
        AppendDraft(movingDraft, previewPoints, geometrySegments);

        RoadPath? path = geometrySegments.Count == 0 ? null : new RoadPath(geometrySegments);
        return new RoadPathDraft(previewPoints, path);
    }

    private static void AppendDraft(
        RoadPathDraft draft,
        List<Vector2> previewPoints,
        List<RoadGeometrySegment?> geometrySegments)
    {
        for (int index = 1; index < draft.PreviewPoints.Count; index++)
            previewPoints.Add(draft.PreviewPoints[index]);

        if (draft.Path == null)
            return;
        geometrySegments.AddRange(draft.Path.Segments);
    }
}
