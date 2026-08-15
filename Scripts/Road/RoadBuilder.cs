using Godot;
using System;
using System.Linq;

public partial class RoadBuilder : Node2D
{
    [Export] public RoadConfig Config { get; set; } = null!;

    private RoadGraph? _graph;
    private RoadRenderer? _renderer;
    private IRoadInputStrategy? _inputStrategy;
    private RoadEditHistory? _editHistory;
    private RoadPlacementSession? _placementSession;
    private bool _leftPressStartedSession;
    private bool _ignoreNextLeftRelease;
    private Vector2 _lastPlacePointerPosition;

    private bool _isRemoveHoverActive;
    private bool _isUpgradeHoverActive;
    private int _lastHoveredEdgeID = -1;
    private RoadRemovalSession? _removalSession;
    private RoadUpgradeSession? _upgradeSession;
    private RoadBuilderLoadAdmission? _loadAdmission;
    private long _loadAdmissionGeneration;

    public bool IsPlacing => _placementSession != null;
    public int FixedCornerCount => _placementSession?.FixedCornerCount ?? 0;
    public RoadPathDraft? CurrentDraft => _placementSession?.CurrentDraft;
    public RoadType SelectedRoadType { get; private set; } = RoadType.Street;
    public bool IsRemoving => _removalSession != null;
    public bool IsUpgrading => _upgradeSession != null;
    public bool CanUndo => _editHistory?.CanUndo == true;
    public bool CanRedo => _editHistory?.CanRedo == true;

    public bool HasActivePlaceSession() => IsPlacing;

    public int GetFixedCornerCount() => FixedCornerCount;

    public RoadType GetSelectedRoadType() => SelectedRoadType;

    public bool SetSelectedRoadType(RoadType roadType)
    {
        if (_loadAdmission is not null || !RoadTypeContract.IsDefined(roadType))
            return false;
        if (SelectedRoadType == roadType)
            return true;

        CancelPlaceSession();
        CancelUpgradeSession();
        SelectedRoadType = roadType;
        return true;
    }

    public bool HasActiveRemoveSession() => IsRemoving;

    public int GetRemovalSelectionCount() => _removalSession?.SelectedEdgeIDs.Length ?? 0;

    public bool HasActiveUpgradeSession() => IsUpgrading;

    public int GetUpgradeSelectionCount() => _upgradeSession?.SelectedEdgeIDs.Length ?? 0;

    public RoadType GetUpgradeTargetRoadType() =>
        _upgradeSession?.TargetRoadType ?? SelectedRoadType;

    public bool CanUndoLastEdit() => CanUndo;

    public bool CanRedoLastEdit() => CanRedo;

    public int GetUndoEditCount() => _editHistory?.UndoCount ?? 0;

    public int GetRedoEditCount() => _editHistory?.RedoCount ?? 0;

    public void SetGraph(RoadGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (_loadAdmission is not null)
            throw new InvalidOperationException("RoadBuilder graph cannot change during load admission.");
        CancelPlaceSession();
        CancelRemoveSession();
        CancelUpgradeSession();
        _editHistory?.Dispose();
        _graph = graph;
        _editHistory = new RoadEditHistory(graph);
    }

    public void SetInputStrategy(IRoadInputStrategy inputStrategy)
    {
        ArgumentNullException.ThrowIfNull(inputStrategy);
        if (_loadAdmission is not null)
            throw new InvalidOperationException("RoadBuilder input strategy cannot change during load admission.");
        CancelPlaceSession();
        CancelRemoveSession();
        CancelUpgradeSession();
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
        Config.NormalizeRuntimeValues(message => GD.PushWarning($"RoadBuilder: {message}"));

        _inputStrategy ??= SquareEightRoadInputStrategy.FromConfig(Config);
    }

    public override void _ExitTree()
    {
        _editHistory?.Dispose();
        _editHistory = null;
    }

    public void HandlePlaceInput(InputEvent @event)
    {
        if (_loadAdmission is not null)
            return;
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
            CancelPlaceSession();
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
        if (_graph == null || _renderer == null || _loadAdmission is not null)
            return;

        if (_removalSession is not null && !_removalSession.IsCurrent)
            EndRemoveSession();
        if (_upgradeSession is not null && !_upgradeSession.IsCurrent)
            EndUpgradeSession();
        if ((_isRemoveHoverActive || _isUpgradeHoverActive) && !IsRemoving && !IsUpgrading)
            UpdateRoadEditHover();
    }

    public bool BeginPlace(Vector2 pointerPosition)
    {
        if (_loadAdmission is not null ||
            _graph == null ||
            _inputStrategy == null ||
            IsPlacing ||
            IsRemoving ||
            IsUpgrading)
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

        _placementSession = new RoadPlacementSession(
            _inputStrategy,
            startPosition,
            SelectedRoadType);
        ApplyPreview(_placementSession.CurrentDraft);
        return true;
    }

    public void UpdatePlace(Vector2 pointerPosition)
    {
        if (_loadAdmission is not null || _placementSession == null)
            return;

        _lastPlacePointerPosition = pointerPosition;
        ApplyPreview(_placementSession.Update(pointerPosition));
    }

    public bool AddPlacePoint(Vector2 pointerPosition)
    {
        if (_loadAdmission is not null || _placementSession == null)
            return false;

        _lastPlacePointerPosition = pointerPosition;
        bool added = _placementSession.TryAddPoint(pointerPosition);
        ApplyPreview(_placementSession.CurrentDraft);
        return added;
    }

    public bool RemoveLastPlacePoint(Vector2 pointerPosition)
    {
        if (_loadAdmission is not null || _placementSession == null)
            return false;

        _lastPlacePointerPosition = pointerPosition;
        bool removed = _placementSession.TryRemoveLastPoint(pointerPosition);
        ApplyPreview(_placementSession.CurrentDraft);
        return removed;
    }

    public bool ConfirmPlace(Vector2 pointerPosition)
    {
        if (_loadAdmission is not null || _placementSession == null || _graph == null)
            return false;

        RoadPlacementSession session = _placementSession;
        _lastPlacePointerPosition = pointerPosition;
        RoadPathDraft draft = session.Update(pointerPosition);
        ApplyPreview(draft);
        if (draft.Path == null)
            return false;

        RoadPathSubmissionResult? result = null;
        bool submitted = ExecuteRoadEdit(() =>
        {
            result = _graph.SubmitPath(new RoadBuildRequest(draft.Path, session.RoadType));
            return result.Success;
        });
        if (!submitted)
        {
            GD.Print($"[PLACE-END] path rejected: {result?.Error}");
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
        if (_loadAdmission is not null || _graph == null || _inputStrategy == null)
            return;

        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (IsRemoving)
                UpdateRemove(ToWorldPosition(mouseMotion.Position));
            return;
        }

        if (@event is not InputEventMouseButton mouseButton)
            return;

        Vector2 pointerPosition = ToWorldPosition(mouseButton.Position);
        if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed && IsRemoving)
        {
            CancelRemoveSession();
            return;
        }
        if (mouseButton.ButtonIndex != MouseButton.Left)
            return;

        if (mouseButton.Pressed)
            BeginRemove(pointerPosition, mouseButton.ShiftPressed);
        else if (IsRemoving)
            ConfirmRemove(pointerPosition);
    }

    public bool BeginRemove(Vector2 pointerPosition, bool rectangleSelection = false)
    {
        if (_loadAdmission is not null ||
            _graph == null ||
            _renderer is not IRoadSurfaceSelectionProvider surfaceProvider ||
            _inputStrategy == null ||
            IsPlacing ||
            IsRemoving ||
            IsUpgrading ||
            !TryCaptureCurrentRoadSurfaceToken(surfaceProvider, out RoadRenderToken renderToken))
        {
            return false;
        }

        _removalSession = new RoadRemovalSession(
            surfaceProvider,
            renderToken,
            rectangleSelection
                ? RoadRemovalSelectionMode.Rectangle
                : RoadRemovalSelectionMode.Continuous,
            pointerPosition,
            _inputStrategy.InteractionRadius);
        if (!_removalSession.IsCurrent)
        {
            _removalSession = null;
            return false;
        }
        ClearRoadEditHover();
        ApplyRemovePreview();
        return true;
    }

    public void UpdateRemove(Vector2 pointerPosition)
    {
        if (_loadAdmission is not null || _removalSession == null)
            return;

        if (!_removalSession.Update(pointerPosition))
        {
            EndRemoveSession();
            return;
        }
        ApplyRemovePreview();
    }

    public bool ConfirmRemove(Vector2 pointerPosition)
    {
        if (_loadAdmission is not null || _removalSession == null || _graph == null)
            return false;

        if (!_removalSession.Update(pointerPosition) ||
            !IsRemovalCommandAdmitted(_removalSession))
        {
            EndRemoveSession();
            return false;
        }

        int[] selectedEdgeIDs = _removalSession.SelectedEdgeIDs;
        EndRemoveSession();
        if (selectedEdgeIDs.Length == 0)
            return false;
        return ExecuteRoadEdit(() => _graph.RemoveEdges(selectedEdgeIDs));
    }

    public void CancelRemoveSession()
    {
        if (_loadAdmission is not null || _removalSession == null)
            return;

        EndRemoveSession();
    }

    public void HandleUpgradeInput(InputEvent @event)
    {
        if (_loadAdmission is not null || _graph == null || _inputStrategy == null)
            return;

        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (IsUpgrading)
                UpdateUpgrade(ToWorldPosition(mouseMotion.Position));
            return;
        }

        if (@event is not InputEventMouseButton mouseButton)
            return;

        Vector2 pointerPosition = ToWorldPosition(mouseButton.Position);
        if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed && IsUpgrading)
        {
            CancelUpgradeSession();
            return;
        }
        if (mouseButton.ButtonIndex != MouseButton.Left)
            return;

        if (mouseButton.Pressed)
            BeginUpgrade(pointerPosition, mouseButton.ShiftPressed);
        else if (IsUpgrading)
            ConfirmUpgrade(pointerPosition);
    }

    public bool BeginUpgrade(Vector2 pointerPosition, bool rectangleSelection = false)
    {
        if (_loadAdmission is not null ||
            _graph == null ||
            _renderer is not IRoadSurfaceSelectionProvider surfaceProvider ||
            _inputStrategy == null ||
            IsPlacing ||
            IsRemoving ||
            IsUpgrading ||
            !TryCaptureCurrentRoadSurfaceToken(surfaceProvider, out RoadRenderToken renderToken))
        {
            return false;
        }

        _upgradeSession = new RoadUpgradeSession(
            surfaceProvider,
            renderToken,
            SelectedRoadType,
            rectangleSelection
                ? RoadUpgradeSelectionMode.Rectangle
                : RoadUpgradeSelectionMode.Continuous,
            pointerPosition,
            _inputStrategy.InteractionRadius);
        if (!_upgradeSession.IsCurrent)
        {
            _upgradeSession = null;
            return false;
        }
        ClearRoadEditHover();
        ApplyUpgradePreview();
        return true;
    }

    public void UpdateUpgrade(Vector2 pointerPosition)
    {
        if (_loadAdmission is not null || _upgradeSession == null)
            return;

        if (!_upgradeSession.Update(pointerPosition))
        {
            EndUpgradeSession();
            return;
        }
        ApplyUpgradePreview();
    }

    public bool ConfirmUpgrade(Vector2 pointerPosition)
    {
        if (_loadAdmission is not null || _upgradeSession == null || _graph == null)
            return false;

        if (!_upgradeSession.Update(pointerPosition) ||
            !IsUpgradeCommandAdmitted(_upgradeSession))
        {
            EndUpgradeSession();
            return false;
        }

        int[] selectedEdgeIDs = _upgradeSession.SelectedEdgeIDs;
        RoadType targetRoadType = _upgradeSession.TargetRoadType;
        EndUpgradeSession();
        if (selectedEdgeIDs.Length == 0)
            return false;

        RoadTypeChangeResult? result = null;
        bool changed = ExecuteRoadEdit(() =>
        {
            result = _graph.ChangeRoadType(selectedEdgeIDs, targetRoadType);
            return result.Success;
        });
        if (!changed)
            GD.Print($"[UPGRADE-END] selection rejected: {result?.Error}");
        return changed;
    }

    public void CancelUpgradeSession()
    {
        if (_loadAdmission is not null || _upgradeSession == null)
            return;

        EndUpgradeSession();
    }

    public bool UndoLastEdit()
    {
        if (_loadAdmission is not null)
            return false;
        CancelPlaceSession();
        CancelRemoveSession();
        CancelUpgradeSession();
        return _editHistory?.Undo() == true;
    }

    public bool RedoLastEdit()
    {
        if (_loadAdmission is not null)
            return false;
        CancelPlaceSession();
        CancelRemoveSession();
        CancelUpgradeSession();
        return _editHistory?.Redo() == true;
    }

    /// <summary>取消当前连续铺路会话，不修改路网。</summary>
    public void CancelPlaceSession()
    {
        if (_loadAdmission is not null || _placementSession == null)
            return;

        EndPlaceSession();
    }

    /// <summary>兼容既有工具切换调用。</summary>
    public void CancelPlaceDrag() => CancelPlaceSession();

    public void SetRemoveHoverActive(bool active)
    {
        if (_loadAdmission is not null)
            return;
        _isRemoveHoverActive = active;
        if (!active)
        {
            CancelRemoveSession();
            ClearRoadEditHover();
        }
    }

    public void SetUpgradeHoverActive(bool active)
    {
        if (_loadAdmission is not null)
            return;
        _isUpgradeHoverActive = active;
        if (!active)
        {
            CancelUpgradeSession();
            ClearRoadEditHover();
        }
    }

    private void ApplyPreview(RoadPathDraft draft)
    {
        if (_renderer == null)
            return;

        float tolerance = float.IsFinite(Config.CurveDisplayTolerance) && Config.CurveDisplayTolerance > 0f
            ? Config.CurveDisplayTolerance
            : RoadGeometryDisplaySampler.DefaultTolerance;
        _renderer.PreviewPoints = draft.Path == null
            ? draft.PreviewPoints.ToArray()
            : RoadGeometryDisplaySampler.SampleSegments(draft.Path.Segments, tolerance);
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

    private void ApplyRemovePreview()
    {
        if (_renderer == null || _removalSession == null)
            return;

        _renderer.RemovalPreviewEdgeIDs = _removalSession.SelectedEdgeIDs;
        _renderer.RemovalSelectionBounds = _removalSession.SelectionBounds;
        _renderer.QueueRedraw();
    }

    private void EndRemoveSession()
    {
        _removalSession = null;
        if (_renderer == null)
            return;

        _renderer.RemovalPreviewEdgeIDs = [];
        _renderer.RemovalSelectionBounds = null;
        _renderer.QueueRedraw();
    }

    private void ApplyUpgradePreview()
    {
        if (_renderer == null || _upgradeSession == null)
            return;

        _renderer.UpgradePreviewEdgeIDs = _upgradeSession.SelectedEdgeIDs;
        _renderer.UpgradeSelectionBounds = _upgradeSession.SelectionBounds;
        _renderer.QueueRedraw();
    }

    private void EndUpgradeSession()
    {
        _upgradeSession = null;
        if (_renderer == null)
            return;

        _renderer.UpgradePreviewEdgeIDs = [];
        _renderer.UpgradeSelectionBounds = null;
        _renderer.QueueRedraw();
    }

    private bool ExecuteRoadEdit(Func<bool> edit) =>
        _editHistory?.Execute(edit) ?? edit();

    private Vector2 ToWorldPosition(Vector2 viewportPosition) =>
        GetCanvasTransform().AffineInverse() * viewportPosition;

    private void UpdateRoadEditHover()
    {
        int? edgeID = null;
        if (_renderer is IRoadSurfaceSelectionProvider surfaceProvider &&
            _inputStrategy is not null &&
            TryCaptureCurrentRoadSurfaceToken(surfaceProvider, out RoadRenderToken renderToken) &&
            surfaceProvider.TryFindClosest(
                renderToken,
                GetGlobalMousePosition(),
                _inputStrategy.InteractionRadius,
                out RoadSurfaceHit? nullableHit) &&
            nullableHit is RoadSurfaceHit hit)
        {
            edgeID = hit.EdgeID;
        }
        int hoveredEdgeID = edgeID ?? -1;
        if (hoveredEdgeID == _lastHoveredEdgeID)
            return;

        _lastHoveredEdgeID = hoveredEdgeID;
        _renderer!.HoveredEdgeID = edgeID;
        _renderer.QueueRedraw();
    }

    private bool TryCaptureCurrentRoadSurfaceToken(
        IRoadSurfaceSelectionProvider surfaceProvider,
        out RoadRenderToken renderToken)
    {
        renderToken = default;
        if (_graph is not RoadGraph graph ||
            !surfaceProvider.TryCaptureCurrentToken(out RoadRenderToken captured))
        {
            return false;
        }

        if (captured.GraphFacadeID != graph.FacadeID ||
            captured.ChangeSequence != graph.CurrentStateToken.ChangeSequence)
        {
            return false;
        }

        renderToken = captured;
        return true;
    }

    private bool IsRemovalCommandAdmitted(RoadRemovalSession session)
    {
        if (_graph is not RoadGraph graph || !session.IsCurrent)
            return false;

        RoadRenderToken renderToken = session.RenderToken;
        return renderToken.GraphFacadeID == graph.FacadeID &&
               renderToken.ChangeSequence == graph.CurrentStateToken.ChangeSequence;
    }

    private bool IsUpgradeCommandAdmitted(RoadUpgradeSession session)
    {
        if (_graph is not RoadGraph graph || !session.IsCurrent)
            return false;

        RoadRenderToken renderToken = session.RenderToken;
        return renderToken.GraphFacadeID == graph.FacadeID &&
               renderToken.ChangeSequence == graph.CurrentStateToken.ChangeSequence;
    }

    private void ClearRoadEditHover()
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

        RoadGeometryClosestPoint? bestPoint = null;
        foreach (RoadGeometrySegment segment in edge.GeometrySegments)
        {
            RoadGeometryClosestPoint candidate = segment.FindClosestPoint(pointerPosition);
            if (bestPoint.HasValue && candidate.DistanceSquared >= bestPoint.Value.DistanceSquared)
                continue;

            bestPoint = candidate;
        }

        return bestPoint.HasValue ? (bestPoint.Value.Position, edge.ID) : null;
    }
}
