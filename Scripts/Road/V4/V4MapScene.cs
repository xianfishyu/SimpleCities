using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SimpleCities.RoadCore;
using CoreRoadBuildRequest = SimpleCities.RoadCore.RoadBuildRequest;
using CoreRoadBuildResult = SimpleCities.RoadCore.RoadBuildResult;

/// <summary>隔离 V4 操作场景；正式 MapTest 仍装配 V3。</summary>
public partial class V4MapScene : Node2D, ISceneToolLoadParticipant
{
    private const string Controls = "HUD/Panel/Margin/Controls/";
    private RoadSaveParticipant? _roads;
    private V4MapView _view = null!;
    private Camera2D _camera = null!;
    private ShaderMaterial _backgroundMaterial = null!;
    private OptionButton _cellChoice = null!;
    private OptionButton _slots = null!;
    private Label _status = null!;
    private Label _mapInfo = null!;
    private SaveManager _saveManager = null!;
    private string _pendingOperation = "";
    private ToolAdmission? _toolAdmission;
    private readonly List<string> _slotIDs = [];
    private OptionButton _profileChoice = null!;
    private RoadPoint? _draftStart;
    private RoadStateToken _draftSource;
    private RoadProfileId _draftProfile;
    private CoreRoadBuildRequest? _previewRequest;
    private CoreRoadBuildRequest? _queuedPreview;
    private CoreRoadBuildResult? _previewResult;
    private CancellationTokenSource? _previewCancellation;
    private bool _previewWorkerRunning;
    private readonly V4RoadOperation _buildOperation = new();
    private CancellationTokenSource? _buildCancellation;
    private string _buildSourceToken = "";
    private string _buildPresentedToken = "";
    private string _operationCompletedText = "道路已建造";
#if DEBUG
    internal Action? BeforeBuildWork { get; set; }
    internal Action? BeforePreviewWork { get; set; }
#endif
    public bool HasBuildPreview => _draftStart.HasValue;
    public bool IsBuildBusy => _buildOperation.IsBusy;
    public int RoadCount => _roads?.Network.Snapshot.EdgeCount ?? 0;
    public string LastOperationToken { get; private set; } = "";
    public int CellSizeMetres => _roads?.Network.Snapshot.Map.CellSizeMetres ?? 0;
    public int PresentedCellSizeMetres => _view?.Presented?.Map.CellSizeMetres ?? 0;
    public bool IsPresentationCurrent => _roads is not null && _view.Presented?.Token == _roads.Network.Snapshot.Token;
    public string StateToken => _roads?.Network.Snapshot.Token.ToString() ?? "";
    public string BuildPhase => _buildOperation.Phase;
    public double BuildElapsedMilliseconds => _buildOperation.ElapsedMilliseconds;
    public double BuildDrawnElapsedMilliseconds => _buildOperation.DrawnElapsedMilliseconds;
    public string BuildSourceToken => _buildSourceToken;
    public string BuildPresentedToken => _buildPresentedToken;

    public override void _Ready()
    {
        _saveManager = GetNode<SaveManager>("/root/SaveManager");
        _view = GetNode<V4MapView>("View");
        _camera = GetNode<Camera2D>("Camera2D");
        _backgroundMaterial = (ShaderMaterial)GetNode<ColorRect>("Background/ColorRect").Material;
        _cellChoice = GetNode<OptionButton>(Controls + "CellSize");
        _slots = GetNode<OptionButton>(Controls + "Slots");
        _status = GetNode<Label>(Controls + "Status");
        _mapInfo = GetNode<Label>(Controls + "MapInfo");
        InitializeSelection();
        _profileChoice = GetNode<OptionButton>(Controls + "Profile");
        foreach (string label in new[] { "土路 · 8 米", "街道 · 12 米", "干道 · 24 米", "公路 · 32 米" })
            _profileChoice.AddItem(label);
        _profileChoice.Select(1);
        foreach (int cell in new[] { 25, 50, 100, 200 })
            _cellChoice.AddItem($"{cell} 米", cell);
        _cellChoice.Select(2);
        GetNode<Button>(Controls + "Create").Pressed += () => CreateMap(_cellChoice.GetSelectedId());
        GetNode<Button>(Controls + "Save").Pressed += () => StartOperation(
            () => _saveManager.StartSaveAs($"V4 地图 · {CellSizeMetres} 米"));
        GetNode<Button>(Controls + "Load").Pressed += () =>
        {
            if (_slots.Selected >= 0 && _slots.Selected < _slotIDs.Count)
                LoadSlot(_slotIDs[_slots.Selected]);
        };
        CreateMap(100);
        RefreshSlots();
        RenderingServer.FramePostDraw += OnFramePostDraw;
    }

    public bool CreateMap(int cellSizeMetres)
    {
        if (_saveManager.IsOperationBusy || _pendingOperation.Length != 0 || _toolAdmission is not null || _buildOperation.IsBusy)
            return false;
        // Validate before unregistering the current scene or replacing its immutable map.
        var replacement = new RoadSaveParticipant(new RoadNetwork(new MapDefinition(cellSizeMetres)));
        CancelDraft();
        ClearRoadSelection();
        if (_roads is not null)
        {
            _saveManager.UnregisterSceneLoad(this);
            _saveManager.Unregister(_roads);
        }
        _roads = replacement;
        if (!_saveManager.Register(_roads) || !_saveManager.RegisterSceneLoad(
                new SceneLoadParticipants(_roads, this, _view, RoadSaveParticipant.Storage)))
            throw new InvalidOperationException("Cannot register the isolated V4 scene.");
        _view.ShowNewMap(_roads.Network.Snapshot);
        _status.Text = "已创建空地图";
        UpdateMapInfo();
        return true;
    }

    public string LoadSlot(string slotID) => StartOperation(() => _saveManager.StartLoad(slotID));

    private string StartOperation(Func<string> start)
    {
        if (_saveManager.IsOperationBusy || _pendingOperation.Length != 0 || _buildOperation.IsBusy)
            return "";
        LastOperationToken = start();
        _pendingOperation = LastOperationToken;
        _status.Text = "正在处理…";
        return LastOperationToken;
    }

    public override void _Process(double delta)
    {
        // Share the established grid style, driven by the V4 presented map and camera.
        int cellSize = PresentedCellSizeMetres;
        _backgroundMaterial.SetShaderParameter("minor_grid_size", cellSize);
        _backgroundMaterial.SetShaderParameter("major_grid_size", cellSize * 10);
        _backgroundMaterial.SetShaderParameter("camera_pos", _camera.GetScreenCenterPosition());
        _backgroundMaterial.SetShaderParameter("camera_zoom", _camera.Zoom.X);
        _backgroundMaterial.SetShaderParameter("viewport_size", GetViewport().GetVisibleRect().Size);
        if (_pendingOperation.Length != 0 && _saveManager.HasOperationResult(_pendingOperation))
        {
            Godot.Collections.Dictionary result = _saveManager.GetOperationResult(_pendingOperation);
            bool success = result["committed"].AsBool();
            _status.Text = success ? "已完成" : $"操作未完成：{result["error"].AsString()}";
            _pendingOperation = "";
            RefreshSlots();
            UpdateMapInfo();
        }
        bool busy = _saveManager.IsOperationBusy || _pendingOperation.Length != 0 || _buildOperation.IsBusy;
        _toolMode.Disabled = busy;
        if (_selectionSession.Source is RoadStateToken selectionSource && _roads is not null && selectionSource != _roads.Network.Snapshot.Token)
            ClearRoadSelection();
        GetNode<Button>(Controls + "Create").Disabled = busy;
        GetNode<Button>(Controls + "Save").Disabled = busy;
        GetNode<Button>(Controls + "Load").Disabled = busy || _slotIDs.Count == 0;
        _profileChoice.Disabled = busy || _draftStart.HasValue || _selectionSession.IsSelecting;
        if (_buildOperation.IsWaiting)
            _status.Text = "道路计算仍在进行，请继续等待… Esc 取消";
    }

    private void RefreshSlots()
    {
        _slotIDs.Clear();
        _slots.Clear();
        foreach (SaveSlotSummary slot in _saveManager.ListSlots())
        {
            _slotIDs.Add(slot.SlotID);
            _slots.AddItem(slot.DisplayName + (slot.IsValid ? "" : "（损坏）"));
        }
        int selected = _slotIDs.IndexOf(_saveManager.CurrentSlotID);
        if (selected >= 0)
            _slots.Select(selected);
    }

    private void UpdateMapInfo() => _mapInfo.Text =
        $"8 × 8 km  ·  中心原点\n当前格长：{CellSizeMetres} 米\n每边 {8000 / CellSizeMetres} 格  ·  {RoadCount} 条道路\n1 世界单位 = 1 米";

    private bool CanEdit => _roads is not null && !_buildOperation.IsBusy && !_saveManager.IsOperationBusy &&
        _pendingOperation.Length == 0 && _toolAdmission is null && IsPresentationCurrent;

    private RoadPoint WorldPoint(Vector2 screen)
    {
        Vector2 world = GetCanvasTransform().AffineInverse() * screen;
        return new RoadPoint(world.X, world.Y);
    }

    private void CancelDraft()
    {
        _draftStart = null;
        _previewRequest = null;
        _queuedPreview = null;
        _previewResult = null;
        _previewCancellation?.Cancel();
        _view.ShowPreview(null, null, null);
    }

    public Godot.Collections.Dictionary GetBuildPreview() => new()
    {
        ["phase"] = _draftStart is null ? "None" : _previewResult?.Status.ToString() ?? "Pending",
        ["sourceToken"] = _previewRequest?.Source.ToString() ?? "",
        ["reason"] = _previewResult?.Reason ?? "",
        ["segments"] = _view.DescribePreview(),
    };

    private void UpdateDraftPreview(RoadPoint start, RoadPoint end)
    {
        var request = new CoreRoadBuildRequest(_draftSource, start, end, _draftProfile);
        if (request == _previewRequest) return;
        _previewCancellation?.Cancel();
        _previewRequest = request;
        _previewResult = null;
        _queuedPreview = request;
        _view.ShowPreview(start, end, null);
        _status.Text = "正在检查建造范围…";
        RunPreviewWorker();
    }

    // At most one preview worker plus one replaceable latest request. Superseded work cannot publish.
    private async void RunPreviewWorker()
    {
        if (_previewWorkerRunning) return;
        _previewWorkerRunning = true;
        try
        {
            while (IsSceneAlive && _queuedPreview is CoreRoadBuildRequest request)
            {
                _queuedPreview = null;
                RoadNetwork network = _roads!.Network;
                using var cancellation = new CancellationTokenSource();
                _previewCancellation = cancellation;
                CancellationToken token = cancellation.Token;
#if DEBUG
                Action? beforeWork = BeforePreviewWork;
#endif
                try
                {
                    CoreRoadBuildResult result = await Task.Run(() =>
                    {
#if DEBUG
                        beforeWork?.Invoke();
#endif
                        return network.PlanBuild(request, token);
                    });
                    if (token.IsCancellationRequested || !IsSceneAlive || !CanEdit || request != _previewRequest ||
                        !_draftStart.HasValue || request.Source != _roads!.Network.Snapshot.Token)
                        continue;
                    _previewResult = result;
                    _view.ShowPreview(request.Start, request.End, result);
                    _status.Text = result.Status switch
                    {
                        RoadBuildStatus.Ready => "松开以建造道路",
                        RoadBuildStatus.NoChange => "拖动以预览道路",
                        _ => result.Reason,
                    };
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception exception)
                {
                    if (IsSceneAlive && request == _previewRequest && _draftStart.HasValue)
                    {
                        _previewResult = new CoreRoadBuildResult(RoadBuildStatus.Rejected, null, "建造预览检查失败");
                        _view.ShowPreview(request.Start, request.End, _previewResult);
                        _status.Text = $"建造预览检查失败：{exception.Message}";
                    }
                }
                finally
                {
                    if (ReferenceEquals(_previewCancellation, cancellation)) _previewCancellation = null;
                }
            }
        }
        finally { _previewWorkerRunning = false; }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            CancelDraft();
            ClearRoadSelection();
            if (_buildOperation.TryCancel())
            {
                _buildCancellation?.Cancel();
                _status.Text = "正在取消…";
            }
        }
        if (HandleSelectionInput(@event)) return;
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release && _draftStart is RoadPoint start)
        {
            long inputTimestamp = Stopwatch.GetTimestamp();
            RoadPoint end = _roads!.Network.Snapshot.Map.SnapDragEnd(start, WorldPoint(release.Position));
            var request = new CoreRoadBuildRequest(_draftSource, start, end, _draftProfile);
            CancelDraft();
            // Release over UI ends the captured gesture without constructing behind a control.
            if (CanEdit && !GetNode<Control>("HUD/Panel").GetGlobalRect().HasPoint(release.Position))
                SubmitBuild(request, inputTimestamp);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (HandleSelectionPointerEvent(@event)) return;
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press && CanEdit && !IsSelectionTool)
        {
            MapDefinition map = _roads!.Network.Snapshot.Map;
            RoadPoint cursor = WorldPoint(press.Position);
            if (cursor.X < -4000 || cursor.X > 4000 || cursor.Y < -4000 || cursor.Y > 4000) return;
            _draftStart = map.SnapBuildPoint(cursor);
            _draftSource = _roads.Network.Snapshot.Token;
            _draftProfile = RoadProfiles.All[_profileChoice.Selected].Id;
            ClearRoadSelection();
            UpdateDraftPreview(_draftStart.Value, _draftStart.Value);
        }
        if (@event is InputEventMouseMotion move && _draftStart is RoadPoint draft && CanEdit)
        {
            RoadPoint end = _roads!.Network.Snapshot.Map.SnapDragEnd(draft, WorldPoint(move.Position));
            UpdateDraftPreview(draft, end);
        }
        else if (@event is InputEventMouseMotion hover && CanEdit && !IsSelectionTool)
        {
            RoadPoint point = WorldPoint(hover.Position);
            Godot.Collections.Dictionary hit = PickRoad(new Vector2((float)point.X, (float)point.Y));
            UpdateSelectionPointer(hover.Position);
            if (hit.TryGetValue("junctionNodeId", out Variant junctionId) && junctionId.AsInt64() > 0 &&
                RoadJunctionQuery.Read(_roads!.Network.Snapshot, new NodeId(junctionId.AsInt64())) is RoadJunctionReadModel junction)
                _status.Text = $"{(_roads.Network.Snapshot.Map.IsCellCenter(junction.Node.Position) ? "格心路口" : "路口")} · {junction.Incidences.Count} 个方向";
            else if (hit.Count != 0) _status.Text = $"道路位置：{hit["parameter"].AsDouble():P0}";
        }
        if (@event is InputEventMouseButton { Pressed: true } button &&
            button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            float zoom = Mathf.Clamp(_camera.Zoom.X * (button.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1 / 1.15f), 0.04f, 2f);
            _camera.Zoom = new Vector2(zoom, zoom);
        }
        if (@event is InputEventMouseMotion motion && motion.ButtonMask.HasFlag(MouseButtonMask.Middle))
            _camera.Position -= motion.Relative / _camera.Zoom;
    }

    public override void _ExitTree()
    {
        _queuedPreview = null;
        _previewRequest = null;
        _previewCancellation?.Cancel();
        RenderingServer.FramePostDraw -= OnFramePostDraw;
        if (_buildOperation.TryCancel()) _buildCancellation?.Cancel();
        if (_buildOperation.Phase == "Committed") _buildOperation.Finish("SceneClosed");
        if (_roads is not null && GodotObject.IsInstanceValid(_saveManager))
        {
            _saveManager.UnregisterSceneLoad(this);
            _saveManager.Unregister(_roads);
        }
    }

    ISceneToolLoadAdmission ISceneToolLoadParticipant.BeginSceneLoadAdmission()
    {
        if (_toolAdmission is not null || _buildOperation.IsBusy)
            throw new InvalidOperationException("V4 tools are already loading.");
        return _toolAdmission = new ToolAdmission(this, _saveManager.SceneGeneration);
    }

    public Godot.Collections.Dictionary PickRoad(Vector2 world) =>
        IsPresentationCurrent ? _view.PickRoad(world) : new();

    public Godot.Collections.Dictionary GetRoadState()
    {
        if (_roads is null || RoadCount == 0) return new();
        RoadSnapshot snapshot = _roads.Network.Snapshot;
        RoadEdge edge = snapshot.Edges[0];
        return new()
        {
            ["start"] = new Vector2((float)edge.Points[0].X, (float)edge.Points[0].Y),
            ["end"] = new Vector2((float)edge.Points[^1].X, (float)edge.Points[^1].Y),
            ["profile"] = edge.Profile.Value,
            ["edgeId"] = edge.Id.Value,
            ["sourceToken"] = snapshot.Token.ToString(),
            ["meshSurfaces"] = _view.MeshSurfaceCount,
            ["nodeCount"] = snapshot.NodeCount,
            ["edges"] = DescribeEdges(snapshot),
            ["junctions"] = DescribeJunctions(snapshot),
        };
    }

    public Godot.Collections.Dictionary GetJunctionState(long nodeId)
    {
        if (_roads is null || nodeId <= 0) return new();
        RoadJunctionReadModel? junction = RoadJunctionQuery.Read(_roads.Network.Snapshot, new NodeId(nodeId));
        return junction is null ? new() : DescribeJunction(junction);
    }

    private static Godot.Collections.Array<Godot.Collections.Dictionary> DescribeJunctions(RoadSnapshot snapshot)
    {
        var junctions = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (RoadNode node in snapshot.Nodes)
        {
            RoadJunctionReadModel? junction = RoadJunctionQuery.Read(snapshot, node.Id);
            if (junction is not null && junction.Incidences.Count >= 3) junctions.Add(DescribeJunction(junction));
        }
        return junctions;
    }

    private static Godot.Collections.Dictionary DescribeJunction(RoadJunctionReadModel junction)
    {
        var incidences = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (RoadIncidence incidence in junction.Incidences)
            incidences.Add(new()
            {
                ["edgeId"] = incidence.Key.Edge.Value, ["role"] = incidence.Key.Role.ToString(),
                ["profile"] = incidence.Profile.Value,
                ["outward"] = new Vector2((float)incidence.Outward.X, (float)incidence.Outward.Y),
                ["bearingRadians"] = incidence.BearingRadians,
            });
        var turns = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (RoadTurnMovement turn in junction.Turns)
            turns.Add(new()
            {
                ["fromEdgeId"] = turn.From.Edge.Value, ["fromRole"] = turn.From.Role.ToString(),
                ["toEdgeId"] = turn.To.Edge.Value, ["toRole"] = turn.To.Role.ToString(),
                ["signedAngleRadians"] = turn.SignedAngleRadians,
            });
        return new()
        {
            ["nodeId"] = junction.Node.Id.Value,
            ["position"] = new Vector2((float)junction.Node.Position.X, (float)junction.Node.Position.Y),
            ["sourceToken"] = junction.Source.ToString(), ["incidences"] = incidences, ["turns"] = turns,
        };
    }

    private static Godot.Collections.Array<Godot.Collections.Dictionary> DescribeEdges(RoadSnapshot snapshot)
    {
        var edges = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (RoadEdge edge in snapshot.Edges)
        {
            var points = new Godot.Collections.Array<Vector2>();
            foreach (RoadPoint point in edge.Points)
                points.Add(new Vector2((float)point.X, (float)point.Y));
            edges.Add(new()
            {
                ["edgeId"] = edge.Id.Value,
                ["startNodeId"] = edge.Start.Value,
                ["endNodeId"] = edge.End.Value,
                ["profile"] = edge.Profile.Value,
                ["points"] = points,
            });
        }
        return edges;
    }

    private sealed record PlannedRoadOperation(RoadPlan? Plan, bool NoChange, string Reason);

    private void SubmitBuild(CoreRoadBuildRequest request, long inputTimestamp) =>
        SubmitRoadOperation((network, token) =>
        {
            CoreRoadBuildResult result = network.PlanBuild(request, token);
            return new PlannedRoadOperation(result.Plan, result.Status == RoadBuildStatus.NoChange, result.Reason);
        }, inputTimestamp, "道路已建造", "未形成有效道路");

    private async void SubmitRoadOperation(Func<RoadNetwork, CancellationToken, PlannedRoadOperation> prepare,
        long inputTimestamp, string completedText, string noChangeText)
    {
        RoadNetwork network = _roads!.Network;
        if (!_buildOperation.TryBegin(inputTimestamp)) return;
        using var cancellation = new CancellationTokenSource();
        _buildCancellation = cancellation;
        CancellationToken token = cancellation.Token;
        _buildSourceToken = network.Snapshot.Token.ToString();
        _buildPresentedToken = "";
        _operationCompletedText = completedText;
        _status.Text = "正在准备道路… Esc 取消";
        bool committed = false;
        V4RoadDisplay? display = null;
#if DEBUG
        Action? beforeWork = BeforeBuildWork;
#endif
        try
        {
            (PlannedRoadOperation Result, RoadSurfaceData? Surface) prepared = await Task.Run(() =>
            {
#if DEBUG
                beforeWork?.Invoke();
#endif
                token.ThrowIfCancellationRequested();
                PlannedRoadOperation result = prepare(network, token);
                RoadSurfaceData? surface = result.Plan is RoadPlan target
                    ? RoadPresentation.Prepare(target.Target.Nodes, target.Target.Edges, token) : null;
                token.ThrowIfCancellationRequested();
                return (result, surface);
            });
            if (!IsSceneAlive || !_buildOperation.CanPublish)
            {
                if (IsSceneAlive) _status.Text = "已取消";
                return;
            }
            if (prepared.Result.Plan is not RoadPlan plan)
            {
                _status.Text = prepared.Result.NoChange ? noChangeText : prepared.Result.Reason;
                return;
            }
            display = _view.PrepareDisplay(plan.Target, prepared.Surface);
            if (!network.CanCommit(plan) || !_buildOperation.CanPublish) return;
            // No await or callbacks between the validated reference publications.
            committed = _buildOperation.TryCommit(() => network.TryCommit(plan));
            if (!committed) return;
            V4RoadDisplay? previous = _view.CommitDisplay(display);
            display = null;
            _view.QueueRedraw();
            previous?.Dispose();
            _buildPresentedToken = plan.Target.Token.ToString();
            _status.Text = "道路已提交，正在更新显示…";
            UpdateMapInfo();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (IsSceneAlive) _status.Text = "已取消";
        }
        catch (Exception exception)
        {
            if (IsSceneAlive) _status.Text = committed ? $"道路已提交，显示更新失败：{exception.Message}" : $"道路操作未完成：{exception.Message}";
        }
        finally
        {
            display?.Dispose();
            if (IsSceneAlive) ClearRoadSelection();
            if (ReferenceEquals(_buildCancellation, cancellation)) _buildCancellation = null;
            if (!committed || !IsSceneAlive || _buildPresentedToken.Length == 0)
                _buildOperation.Finish(committed ? "DisplayFailed" : "Finished");
        }
    }

    private void OnFramePostDraw()
    {
        // This is the first completed render after the committed display was published.
        // Input remains gated until this point; no new write can replace the measured target.
        if (_buildOperation.Phase != "Committed" || !IsPresentationCurrent ||
            _view.Presented?.Token.ToString() != _buildPresentedToken ||
            _view.DrawSubmittedToken != _buildPresentedToken)
            return;
        if (_buildOperation.RecordFirstDraw())
            _status.Text = $"{_operationCompletedText} · {_buildOperation.DrawnElapsedMilliseconds:F1} ms";
    }

    private bool IsSceneAlive => GodotObject.IsInstanceValid(this) && IsInsideTree();

    private sealed class ToolAdmission(V4MapScene owner, long generation) : ISceneToolLoadAdmission, INonThrowingLoadCommitPlan
    {
        public string ParticipantID => "v4-tools";
        public bool IsGenerationCurrent => ReferenceEquals(owner._toolAdmission, this) &&
            owner._saveManager.SceneGeneration == generation;
        public INonThrowingLoadCommitPlan PreflightFullReset()
        {
            if (!IsGenerationCurrent)
                throw new LoadPreflightInvalidException("V4 tool admission is stale.");
            return this;
        }
        public void CommitReferences() => owner._draftStart = null;
        public IReadOnlyList<string> PublishNotifications()
        {
            owner.CancelDraft();
            owner.ClearRoadSelection();
            return Array.Empty<string>();
        }
        public void CompleteCommit() => Dispose();
        public void Dispose()
        {
            if (ReferenceEquals(owner._toolAdmission, this))
                owner._toolAdmission = null;
        }
    }
}
