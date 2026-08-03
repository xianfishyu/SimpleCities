using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum RoadRemovalSelectionMode
{
    Continuous,
    Rectangle,
}

/// <summary>在提交前以稳定 Edge ID 集合描述一次连续或矩形拆除选择。</summary>
public sealed class RoadRemovalSession
{
    private readonly RoadGraph _graph;
    private readonly float _interactionRadius;
    private readonly SortedSet<int> _selectedEdgeIDs = [];

    public RoadRemovalSelectionMode Mode { get; }
    public Vector2 StartPosition { get; }
    public Vector2 CurrentPosition { get; private set; }
    public int[] SelectedEdgeIDs => _selectedEdgeIDs.ToArray();
    public Rect2? SelectionBounds => Mode == RoadRemovalSelectionMode.Rectangle
        ? CreateBounds(StartPosition, CurrentPosition)
        : null;

    public RoadRemovalSession(
        RoadGraph graph,
        RoadRemovalSelectionMode mode,
        Vector2 startPosition,
        float interactionRadius)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (!startPosition.IsFinite())
            throw new ArgumentException("A removal session needs a finite start position.", nameof(startPosition));
        if (!float.IsFinite(interactionRadius) || interactionRadius <= 0f)
            throw new ArgumentOutOfRangeException(
                nameof(interactionRadius),
                interactionRadius,
                "Interaction radius must be positive and finite.");

        _graph = graph;
        _interactionRadius = interactionRadius;
        Mode = mode;
        StartPosition = startPosition;
        CurrentPosition = startPosition;
        Update(startPosition);
    }

    public void Update(Vector2 pointerPosition)
    {
        if (!pointerPosition.IsFinite())
            throw new ArgumentException("Pointer position must contain finite coordinates.", nameof(pointerPosition));

        if (Mode == RoadRemovalSelectionMode.Rectangle)
        {
            CurrentPosition = pointerPosition;
            _selectedEdgeIDs.Clear();
            _selectedEdgeIDs.UnionWith(_graph.FindEdgeIDsIntersecting(
                CreateBounds(StartPosition, CurrentPosition)));
            return;
        }

        AddContinuousSelection(CurrentPosition, pointerPosition);
        CurrentPosition = pointerPosition;
    }

    private void AddContinuousSelection(Vector2 from, Vector2 to)
    {
        float distance = from.DistanceTo(to);
        int stepCount = Math.Max(1, Mathf.CeilToInt(distance / (_interactionRadius * 0.5f)));
        for (int step = 0; step <= stepCount; step++)
        {
            Vector2 sample = from.Lerp(to, (float)step / stepCount);
            _selectedEdgeIDs.UnionWith(_graph.FindEdgeIDsNear(sample, _interactionRadius));
        }
    }

    private static Rect2 CreateBounds(Vector2 from, Vector2 to)
    {
        Vector2 minimum = new(Mathf.Min(from.X, to.X), Mathf.Min(from.Y, to.Y));
        Vector2 maximum = new(Mathf.Max(from.X, to.X), Mathf.Max(from.Y, to.Y));
        return new Rect2(minimum, maximum - minimum);
    }
}
