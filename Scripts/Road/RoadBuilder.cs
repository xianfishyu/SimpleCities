using Godot;
using System;

public partial class RoadBuilder : Node2D
{
    [Export] public RoadConfig Config { get; set; } = null!;

    private RoadGraph? _graph;
    private RoadRenderer? _renderer;
    private IRoadInputStrategy? _inputStrategy;
    private RoadPathDraft? _currentDraft;
    private bool _isDragging;
    private Vector2 _dragStartPos;

    private bool _isRemoveHoverActive;
    private int _lastHoveredEdgeID = -1;

    public void SetGraph(RoadGraph graph) => _graph = graph;

    public void SetInputStrategy(IRoadInputStrategy inputStrategy)
    {
        ArgumentNullException.ThrowIfNull(inputStrategy);
        CancelPlaceDrag();
        _inputStrategy = inputStrategy;
    }

    public override void _Ready()
    {
        _renderer = GetNode<RoadRenderer>("../RoadRenderer");
        if (Config == null)
        {
            GD.PushError("RoadBuilder: Config (RoadConfig resource) is not assigned in the scene.");
            Config = new RoadConfig();
        }

        _inputStrategy ??= SquareEightRoadInputStrategy.FromConfig(Config);
    }

    public void HandlePlaceInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left)
            return;

        Vector2 pointerPosition = GetGlobalMousePosition();
        if (mouseButton.Pressed)
            BeginPlace(pointerPosition);
        else
            CommitPlace(pointerPosition);
    }

    public override void _Process(double delta)
    {
        if (_graph == null || _renderer == null)
            return;

        if (_isDragging)
            UpdatePlace(GetGlobalMousePosition());
        else if (_isRemoveHoverActive)
            UpdateRemoveHover();
    }

    public bool BeginPlace(Vector2 pointerPosition)
    {
        if (_graph == null || _inputStrategy == null)
            return false;

        _isDragging = true;
        _dragStartPos = _inputStrategy.SnapPointer(pointerPosition);

        float interactionRadius = _inputStrategy.InteractionRadius;
        if (_graph.FindClosestEdge(_dragStartPos, interactionRadius) == null &&
            _graph.FindClosestNode(_dragStartPos, interactionRadius) == null)
        {
            (Vector2 pos, int edgeID)? nearest = FindNearestRoadPoint(pointerPosition);
            if (nearest.HasValue)
            {
                _dragStartPos = nearest.Value.pos;
                GD.Print(
                    $"[DRAG-SNAP] fallback=({_dragStartPos.X:F0},{_dragStartPos.Y:F0}) " +
                    $"edgeID={nearest.Value.edgeID}");
            }
        }

        _currentDraft = RoadPathDraft.Empty(_dragStartPos);
        ApplyPreview(_currentDraft);
        return true;
    }

    public void UpdatePlace(Vector2 pointerPosition)
    {
        if (!_isDragging || _inputStrategy == null)
            return;

        _currentDraft = _inputStrategy.BuildDraft(_dragStartPos, pointerPosition);
        ApplyPreview(_currentDraft);
    }

    public bool CommitPlace(Vector2 pointerPosition)
    {
        if (!_isDragging)
            return false;

        UpdatePlace(pointerPosition);
        RoadPathDraft? draft = _currentDraft;
        _isDragging = false;
        _currentDraft = null;
        ClearPreview();

        if (_graph == null || draft?.Path == null)
            return false;

        RoadPathSubmissionResult result = _graph.SubmitPath(draft.Path);
        if (!result.Success)
            GD.Print($"[DRAG-END] path rejected: {result.Error}");
        return result.Success;
    }

    public void HandleRemoveInput(InputEvent @event)
    {
        if (_graph == null || _inputStrategy == null)
            return;

        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            mouseButton.Pressed)
        {
            Vector2 pointerPosition = GetGlobalMousePosition();
            GraphEdge? edge = FindEdgeForRemoval(pointerPosition);
            if (edge != null)
                _graph.RemoveEdge(edge.ID);
        }
    }

    /// <summary>取消当前铺路拖拽，不修改路网。</summary>
    public void CancelPlaceDrag()
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        _currentDraft = null;
        ClearPreview();
    }

    public void SetRemoveHoverActive(bool active)
    {
        _isRemoveHoverActive = active;
        if (!active)
            ClearRemoveHover();
    }

    private void ApplyPreview(RoadPathDraft draft)
    {
        if (_renderer == null)
            return;

        _renderer.PreviewFrom = draft.PreviewFrom;
        _renderer.PreviewTo = draft.PreviewTo;
        _renderer.QueueRedraw();
    }

    private void ClearPreview()
    {
        if (_renderer == null)
            return;

        _renderer.PreviewFrom = null;
        _renderer.PreviewTo = null;
        _renderer.QueueRedraw();
    }

    private void UpdateRemoveHover()
    {
        int? edgeID = FindEdgeForRemoval(GetGlobalMousePosition())?.ID;
        int hoveredEdgeID = edgeID ?? -1;
        if (hoveredEdgeID == _lastHoveredEdgeID)
            return;

        _lastHoveredEdgeID = hoveredEdgeID;
        _renderer!.HoveredEdgeID = edgeID;
        _renderer.QueueRedraw();
    }

    private GraphEdge? FindEdgeForRemoval(Vector2 pointerPosition)
    {
        if (_graph == null || _inputStrategy == null)
            return null;

        float interactionRadius = _inputStrategy.InteractionRadius;
        Vector2 snappedPosition = _inputStrategy.SnapPointer(pointerPosition);
        return _graph.FindClosestEdge(snappedPosition, interactionRadius)
            ?? _graph.FindClosestEdge(pointerPosition, interactionRadius);
    }

    private void ClearRemoveHover()
    {
        _lastHoveredEdgeID = -1;
        if (_renderer == null)
            return;

        _renderer.HoveredEdgeID = null;
        _renderer.QueueRedraw();
    }

    /// <summary>在命中半径内返回最接近指针的道路折线锚点。</summary>
    private (Vector2 pos, int edgeID)? FindNearestRoadPoint(Vector2 pointerPosition)
    {
        if (_graph == null || _inputStrategy == null)
            return null;

        GraphEdge? edge = _graph.FindClosestEdge(pointerPosition, _inputStrategy.InteractionRadius);
        if (edge == null)
            return null;

        Vector2[] fullPath = edge.GetFullPath(id => _graph.GetNode(id));
        Vector2? bestPosition = null;
        float bestDistanceSquared = float.MaxValue;
        foreach (Vector2 point in fullPath)
        {
            float distanceSquared = pointerPosition.DistanceSquaredTo(point);
            if (distanceSquared >= bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            bestPosition = point;
        }

        return bestPosition.HasValue ? (bestPosition.Value, edge.ID) : null;
    }
}
