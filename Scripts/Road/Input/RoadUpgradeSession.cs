using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum RoadUpgradeSelectionMode
{
    Continuous,
    Rectangle,
}

/// <summary>在提交前冻结目标类型和同代表面上的稳定 Edge ID 集。</summary>
public sealed class RoadUpgradeSession
{
    private readonly IRoadSurfaceSelectionProvider _surfaceProvider;
    private readonly float _interactionRadius;
    private readonly SortedSet<int> _selectedEdgeIDs = [];
    private bool _invalidated;

    public RoadUpgradeSelectionMode Mode { get; }
    public RoadRenderToken RenderToken { get; }
    public RoadType TargetRoadType { get; }
    public Vector2 StartPosition { get; }
    public Vector2 CurrentPosition { get; private set; }
    public bool IsCurrent => EnsureCurrent();
    public int[] SelectedEdgeIDs => EnsureCurrent() ? _selectedEdgeIDs.ToArray() : [];
    public Rect2? SelectionBounds =>
        EnsureCurrent() && Mode == RoadUpgradeSelectionMode.Rectangle
        ? CreateBounds(StartPosition, CurrentPosition)
        : null;

    internal RoadUpgradeSession(
        IRoadSurfaceSelectionProvider surfaceProvider,
        RoadRenderToken renderToken,
        RoadType targetRoadType,
        RoadUpgradeSelectionMode mode,
        Vector2 startPosition,
        float interactionRadius)
    {
        ArgumentNullException.ThrowIfNull(surfaceProvider);
        if (!RoadTypeContract.IsDefined(targetRoadType))
            throw new ArgumentOutOfRangeException(
                nameof(targetRoadType),
                targetRoadType,
                "An upgrade session needs a defined target road type.");
        if (!startPosition.IsFinite())
            throw new ArgumentException("An upgrade session needs a finite start position.", nameof(startPosition));
        if (!float.IsFinite(interactionRadius) || interactionRadius <= 0f)
            throw new ArgumentOutOfRangeException(
                nameof(interactionRadius),
                interactionRadius,
                "Interaction radius must be positive and finite.");

        _surfaceProvider = surfaceProvider;
        _interactionRadius = interactionRadius;
        RenderToken = renderToken;
        TargetRoadType = targetRoadType;
        Mode = mode;
        StartPosition = startPosition;
        CurrentPosition = startPosition;
        Update(startPosition);
    }

    public bool Update(Vector2 pointerPosition)
    {
        if (!pointerPosition.IsFinite())
            throw new ArgumentException("Pointer position must contain finite coordinates.", nameof(pointerPosition));
        if (!EnsureCurrent())
            return false;

        if (Mode == RoadUpgradeSelectionMode.Rectangle)
        {
            CurrentPosition = pointerPosition;
            if (!_surfaceProvider.TryFindEdgeIDsIntersecting(
                    RenderToken,
                    CreateBounds(StartPosition, CurrentPosition),
                    out int[] edgeIDs))
            {
                Invalidate();
                return false;
            }

            _selectedEdgeIDs.Clear();
            _selectedEdgeIDs.UnionWith(edgeIDs);
            return true;
        }

        if (!AddContinuousSelection(CurrentPosition, pointerPosition))
        {
            Invalidate();
            return false;
        }
        CurrentPosition = pointerPosition;
        return true;
    }

    private bool AddContinuousSelection(Vector2 from, Vector2 to)
    {
        float distance = from.DistanceTo(to);
        int stepCount = Math.Max(1, Mathf.CeilToInt(distance / (_interactionRadius * 0.5f)));
        for (int step = 0; step <= stepCount; step++)
        {
            Vector2 sample = from.Lerp(to, (float)step / stepCount);
            if (!_surfaceProvider.TryFindClosest(
                    RenderToken,
                    sample,
                    _interactionRadius,
                    out RoadSurfaceHit? nullableHit))
            {
                return false;
            }
            if (nullableHit is RoadSurfaceHit hit && hit.EdgeID is int edgeID)
                _selectedEdgeIDs.Add(edgeID);
        }
        return true;
    }

    private bool EnsureCurrent()
    {
        if (!_invalidated && _surfaceProvider.IsCurrent(RenderToken))
            return true;

        Invalidate();
        return false;
    }

    private void Invalidate()
    {
        _invalidated = true;
        _selectedEdgeIDs.Clear();
    }

    private static Rect2 CreateBounds(Vector2 from, Vector2 to)
    {
        Vector2 minimum = new(Mathf.Min(from.X, to.X), Mathf.Min(from.Y, to.Y));
        Vector2 maximum = new(Mathf.Max(from.X, to.X), Mathf.Max(from.Y, to.Y));
        return new Rect2(minimum, maximum - minimum);
    }
}
