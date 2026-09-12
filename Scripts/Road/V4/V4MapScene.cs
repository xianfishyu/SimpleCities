using Godot;
using System;
using System.Collections.Generic;
using SimpleCities.RoadCore;

/// <summary>隔离 V4 操作场景；正式 MapTest 仍装配 V3。</summary>
public partial class V4MapScene : Node2D, ISceneToolLoadParticipant
{
    private const string Controls = "HUD/Panel/Margin/Controls/";
    private RoadSaveParticipant? _roads;
    private V4MapView _view = null!;
    private Camera2D _camera = null!;
    private OptionButton _cellChoice = null!;
    private OptionButton _slots = null!;
    private Label _status = null!;
    private Label _mapInfo = null!;
    private SaveManager _saveManager = null!;
    private string _pendingOperation = "";
    private ToolAdmission? _toolAdmission;
    private readonly List<string> _slotIDs = [];
    public string LastOperationToken { get; private set; } = "";
    public int CellSizeMetres => _roads?.Network.Snapshot.Map.CellSizeMetres ?? 0;
    public int PresentedCellSizeMetres => _view?.Presented?.Map.CellSizeMetres ?? 0;
    public bool IsPresentationCurrent => _roads is not null && _view.Presented?.Token == _roads.Network.Snapshot.Token;
    public string StateToken => _roads?.Network.Snapshot.Token.ToString() ?? "";

    public override void _Ready()
    {
        _saveManager = GetNode<SaveManager>("/root/SaveManager");
        _view = GetNode<V4MapView>("View");
        _camera = GetNode<Camera2D>("Camera2D");
        _cellChoice = GetNode<OptionButton>(Controls + "CellSize");
        _slots = GetNode<OptionButton>(Controls + "Slots");
        _status = GetNode<Label>(Controls + "Status");
        _mapInfo = GetNode<Label>(Controls + "MapInfo");
        foreach (int cell in new[] { 25, 50, 100, 200 })
            _cellChoice.AddItem($"{cell} 米", cell);
        _cellChoice.Select(2);
        GetNode<Button>(Controls + "Create").Pressed += () => CreateMap(_cellChoice.GetSelectedId());
        GetNode<Button>(Controls + "Save").Pressed += () => StartOperation(
            () => _saveManager.StartSaveAs($"V4 空地图 · {CellSizeMetres} 米"));
        GetNode<Button>(Controls + "Load").Pressed += () =>
        {
            if (_slots.Selected >= 0 && _slots.Selected < _slotIDs.Count)
                LoadSlot(_slotIDs[_slots.Selected]);
        };
        CreateMap(100);
        RefreshSlots();
    }

    public bool CreateMap(int cellSizeMetres)
    {
        if (_saveManager.IsOperationBusy || _pendingOperation.Length != 0 || _toolAdmission is not null)
            return false;
        // Validate before unregistering the current scene or replacing its immutable map.
        var replacement = new RoadSaveParticipant(new RoadNetwork(new MapDefinition(cellSizeMetres)));
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
        if (_saveManager.IsOperationBusy || _pendingOperation.Length != 0)
            return "";
        LastOperationToken = start();
        _pendingOperation = LastOperationToken;
        _status.Text = "正在处理…";
        return LastOperationToken;
    }

    public override void _Process(double delta)
    {
        if (_pendingOperation.Length != 0 && _saveManager.HasOperationResult(_pendingOperation))
        {
            Godot.Collections.Dictionary result = _saveManager.GetOperationResult(_pendingOperation);
            bool success = result["committed"].AsBool();
            _status.Text = success ? "已完成" : $"操作未完成：{result["error"].AsString()}";
            _pendingOperation = "";
            RefreshSlots();
            UpdateMapInfo();
        }
        bool busy = _saveManager.IsOperationBusy || _pendingOperation.Length != 0;
        GetNode<Button>(Controls + "Create").Disabled = busy;
        GetNode<Button>(Controls + "Save").Disabled = busy;
        GetNode<Button>(Controls + "Load").Disabled = busy || _slotIDs.Count == 0;
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
        $"8 × 8 km  ·  中心原点\n当前格长：{CellSizeMetres} 米\n每边 {8000 / CellSizeMetres} 格  ·  空路网\n1 世界单位 = 1 米";

    public override void _UnhandledInput(InputEvent @event)
    {
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
        if (_roads is not null && GodotObject.IsInstanceValid(_saveManager))
        {
            _saveManager.UnregisterSceneLoad(this);
            _saveManager.Unregister(_roads);
        }
    }

    ISceneToolLoadAdmission ISceneToolLoadParticipant.BeginSceneLoadAdmission()
    {
        if (_toolAdmission is not null)
            throw new InvalidOperationException("V4 tools are already loading.");
        return _toolAdmission = new ToolAdmission(this, _saveManager.SceneGeneration);
    }

    // Empty-map tools have no edit draft to reset. Admission still protects scene replacement.
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
        public void CommitReferences() { }
        public IReadOnlyList<string> PublishNotifications() => Array.Empty<string>();
        public void CompleteCommit() => Dispose();
        public void Dispose()
        {
            if (ReferenceEquals(owner._toolAdmission, this))
                owner._toolAdmission = null;
        }
    }
}
