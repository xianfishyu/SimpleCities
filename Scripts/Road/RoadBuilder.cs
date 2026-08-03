using Godot;
using System;
using System.Linq;

public partial class RoadBuilder : Node2D
{
    [Export] public RoadConfig Config { get; set; } = null!;

    private RoadGraph? _graph;
    private RoadRenderer? _renderer;
    private IRoadInputStrategy? _inputStrategy;
    private RoadPlacementSession? _placementSession;
    private bool _leftPressStartedSession;
    private bool _ignoreNextLeftRelease;
    private Vector2 _lastPlacePointerPosition;

    private bool _isRemoveHoverActive;
    private int _lastHoveredEdgeID = -1;

    public bool IsPlacing => _placementSession != null;
    public int FixedCornerCount => _placementSession?.FixedCornerCount ?? 0;
    public RoadPathDraft? CurrentDraft => _placementSession?.CurrentDraft;

    public bool HasActivePlaceSession() => IsPlacing;

    public int GetFixedCornerCount() => FixedCornerCount;

    public void SetGraph(RoadGraph graph) => _graph = graph;

    public void SetInputStrategy(IRoadInputStrategy inputStrategy)
    {
        ArgumentNullException.ThrowIfNull(inputStrategy);
        CancelPlaceSession();
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
        if (@event is InputEventKey keyEvent &&
            keyEvent.Pressed &&
            !keyEvent.Echo &&
            keyEvent.Keycode is Key.Enter or Key.KpEnter)
        {
            if (IsPlacing)
                ConfirmPlace(_lastPlacePointerPosition);
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (IsPlacing)
                UpdatePlace(ToWorldPosition(mouseMotion.Position));
            return;
        }

        if (@event is not InputEventMouseButton mouseButton)
            return;

        Vector2 pointerPosition = ToWorldPosition(mouseButton.Position);
        if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed && IsPlacing)
        {
            if (FixedCornerCount == 0)
                CancelPlaceSession();
            else
                RemoveLastPlacePoint(pointerPosition);
            return;
        }

        if (mouseButton.ButtonIndex != MouseButton.Left)
            return;

        if (mouseButton.Pressed)
        {
            if (mouseButton.DoubleClick && IsPlacing)
            {
                _ignoreNextLeftRelease = true;
                ConfirmPlace(pointerPosition);
                return;
            }

            _leftPressStartedSession = !IsPlacing && BeginPlace(pointerPosition);
            return;
        }

        if (_ignoreNextLeftRelease)
        {
            _ignoreNextLeftRelease = false;
            return;
        }
        if (!IsPlacing)
            return;

        UpdatePlace(pointerPosition);
        if (_leftPressStartedSession)
        {
            _leftPressStartedSession = false;
            if (CurrentDraft?.CanCommit == true)
                ConfirmPlace(pointerPosition);
            return;
        }

        AddPlacePoint(pointerPosition);
    }

    public override void _Process(double delta)
    {
        if (_graph == null || _renderer == null)
            return;

        if (_isRemoveHoverActive)
            UpdateRemoveHover();
    }

    public bool BeginPlace(Vector2 pointerPosition)
    {
        if (_graph == null || _inputStrategy == null || IsPlacing)
            return false;

        _lastPlacePointerPosition = pointerPosition;
        Vector2 startPosition = _inputStrategy.SnapPointer(pointerPosition);

        float interactionRadius = _inputStrategy.InteractionRadius;
        if (_graph.FindClosestEdge(startPosition, interactionRadius) == null &&
            _graph.FindClosestNode(startPosition, interactionRadius) == null)
        {
            (Vector2 pos, int edgeID)? nearest = FindNearestRoadPoint(pointerPosition);
            if (nearest.HasValue)
            {
                startPosition = nearest.Value.pos;
                GD.Print(
                    $"[PLACE-SNAP] fallback=({startPosition.X:F0},{startPosition.Y:F0}) " +
                    $"edgeID={nearest.Value.edgeID}");
            }
        }

        _placementSession = new RoadPlacementSession(_inputStrategy, startPosition);
        ApplyPreview(_placementSession.CurrentDraft);
        return true;
    }

    public void UpdatePlace(Vector2 pointerPosition)
    {
        if (_placementSession == null)
            return;

        _lastPlacePointerPosition = pointerPosition;
        ApplyPreview(_placementSession.Update(pointerPosition));
    }

    public bool AddPlacePoint(Vector2 pointerPosition)
    {
        if (_placementSession == null)
            return false;

        _lastPlacePointerPosition = pointerPosition;
        bool added = _placementSession.TryAddPoint(pointerPosition);
        ApplyPreview(_placementSession.CurrentDraft);
        return added;
    }

    public bool RemoveLastPlacePoint(Vector2 pointerPosition)
    {
        if (_placementSession == null)
            return false;

        _lastPlacePointerPosition = pointerPosition;
        bool removed = _placementSession.TryRemoveLastPoint(pointerPosition);
        ApplyPreview(_placementSession.CurrentDraft);
        return removed;
    }

    public bool ConfirmPlace(Vector2 pointerPosition)
    {
        if (_placementSession == null || _graph == null)
            return false;

        _lastPlacePointerPosition = pointerPosition;
        RoadPathDraft draft = _placementSession.Update(pointerPosition);
        ApplyPreview(draft);
        if (draft.Path == null)
            return false;

        RoadPathSubmissionResult result = _graph.SubmitPath(draft.Path);
        if (!result.Success)
        {
            GD.Print($"[PLACE-END] path rejected: {result.Error}");
            return false;
        }

        EndPlaceSession();
        return true;
    }

    /// <summary>兼容既有单次拖拽调用；确认当前完整铺路会话。</summary>
    public bool CommitPlace(Vector2 pointerPosition)
        => ConfirmPlace(pointerPosition);

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

    /// <summary>取消当前连续铺路会话，不修改路网。</summary>
    public void CancelPlaceSession()
    {
        if (_placementSession == null)
            return;

        EndPlaceSession();
    }

    /// <summary>兼容既有工具切换调用。</summary>
    public void CancelPlaceDrag() => CancelPlaceSession();

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

        _renderer.PreviewPoints = draft.PreviewPoints.ToArray();
        _renderer.QueueRedraw();
    }

    private void ClearPreview()
    {
        if (_renderer == null)
            return;

        _renderer.PreviewPoints = [];
        _renderer.QueueRedraw();
    }

    private void EndPlaceSession()
    {
        _placementSession = null;
        _leftPressStartedSession = false;
        _ignoreNextLeftRelease = false;
        ClearPreview();
    }

    private Vector2 ToWorldPosition(Vector2 viewportPosition) =>
        GetCanvasTransform().AffineInverse() * viewportPosition;

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
