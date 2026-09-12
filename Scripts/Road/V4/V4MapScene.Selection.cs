using Godot;
using SimpleCities.RoadCore;

public partial class V4MapScene
{
    private readonly RoadSpanSelectionSession _selectionSession = new();
    private OptionButton _toolMode = null!;
    private Vector2? _selectionLastWorld;
    private bool IsSelectionTool => _toolMode.Selected == 1;

    private void InitializeSelection()
    {
        _toolMode = GetNode<OptionButton>(Controls + "ToolMode");
        _toolMode.AddItem("建造道路");
        _toolMode.AddItem("选择格段");
        _toolMode.Select(0);
        _toolMode.ItemSelected += index => SetToolMode((int)index);
    }

    public bool SetToolMode(int mode)
    {
        if (mode is not (0 or 1) || !CanEdit) return false;
        CancelDraft();
        ClearRoadSelection();
        _toolMode.Select(mode);
        _status.Text = mode == 1 ? "按住拖选格段 · Esc 清除" : "拖动建造道路";
        GetNode<Label>(Controls + "Help").Text = mode == 1
            ? "左键拖选格段 · Esc 清除\n滚轮缩放 · 中键拖动\n路口移向分支后选择"
            : "左键拖动建造 · Esc 取消\n滚轮缩放 · 中键拖动\n主格点八方向 · 格心仅对角";
        return true;
    }

    private void ClearRoadSelection()
    {
        _selectionSession.Clear();
        _selectionLastWorld = null;
        _view.ClearSelection();
    }

    public Godot.Collections.Dictionary GetSelectionState() => new()
    {
        ["mode"] = IsSelectionTool ? "Select" : "Build",
        ["selecting"] = _selectionSession.IsSelecting,
        ["sourceToken"] = _selectionSession.Source?.ToString() ?? "",
        ["selectedCount"] = _selectionSession.Selected.Count,
        ["hasHover"] = _selectionSession.Hovered is not null,
        ["strokes"] = _view.DescribeSelection(),
    };

    private Vector2 SelectionWorld(Vector2 screen) => GetCanvasTransform().AffineInverse() * screen;

    private void UpdateSelectionPointer(Vector2 screen)
    {
        RoadSnapshot snapshot = _roads!.Network.Snapshot;
        Vector2 world = SelectionWorld(screen);
        RoadGridSpan? hover = _view.PeekSpan(world);
        if (!_selectionSession.Hover(snapshot, hover))
        {
            _selectionLastWorld = null;
            _view.ClearSelection();
            return;
        }
        if (_selectionSession.IsSelecting)
        {
            _selectionSession.Accumulate(snapshot, _view.TraceSpans(_selectionLastWorld ?? world, world));
            _selectionLastWorld = world;
            _status.Text = $"已选 {_selectionSession.Selected.Count} 个道路格段";
        }
        _view.SetSelection(_selectionSession.Hovered, _selectionSession.Selected);
    }

    private bool HandleSelectionInput(InputEvent input)
    {
        if (input is InputEventMouseMotion motion && GetNode<Control>("HUD/Panel").GetGlobalRect().HasPoint(motion.Position))
        {
            _selectionLastWorld = null;
            if (_roads is not null) _selectionSession.Hover(_roads.Network.Snapshot, null);
            _view.SetSelection(null, _selectionSession.Selected);
        }
        if (!IsSelectionTool || input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release)
            return false;
        if (_selectionSession.IsSelecting)
        {
            if (CanEdit && !GetNode<Control>("HUD/Panel").GetGlobalRect().HasPoint(release.Position))
                UpdateSelectionPointer(release.Position);
            _selectionSession.End();
            _selectionLastWorld = null;
            _status.Text = $"已选 {_selectionSession.Selected.Count} 个道路格段 · Esc 清除";
        }
        return true;
    }

    private bool HandleSelectionPointerEvent(InputEvent input)
    {
        if (!IsSelectionTool || !CanEdit) return false;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press)
        {
            _selectionSession.Begin(_roads!.Network.Snapshot);
            _selectionLastWorld = null;
            UpdateSelectionPointer(press.Position);
            return true;
        }
        if (input is InputEventMouseMotion motion) UpdateSelectionPointer(motion.Position);
        return false; // Camera wheel/pan still flows through the shared input handler.
    }
}
