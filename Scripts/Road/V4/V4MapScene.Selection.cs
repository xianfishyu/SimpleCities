using Godot;
using System.Diagnostics;
using System.Linq;
using SimpleCities.RoadCore;

public partial class V4MapScene
{
    private RoadSpanSelectionSession _selectionSession = new();
    private OptionButton _toolMode = null!;
    private Vector2? _selectionLastWorld;
    private bool IsSelectionTool => _toolMode.Selected != 0;
    private bool IsSpanEditTool => _toolMode.Selected is 2 or 3;
    private RoadProfileId _selectionProfile;

    private void InitializeSelection()
    {
        _toolMode = GetNode<OptionButton>(Controls + "ToolMode");
        _toolMode.AddItem("建造道路");
        _toolMode.AddItem("选择格段");
        _toolMode.AddItem("删除格段");
        _toolMode.AddItem("改造格段");
        _toolMode.Select(0);
        _toolMode.ItemSelected += index => SetToolMode((int)index);
    }

    public bool SetToolMode(int mode)
    {
        if (mode is < 0 or > 3 || !CanEdit) return false;
        CancelDraft();
        ClearRoadSelection();
        _toolMode.Select(mode);
        _status.Text = mode switch
        {
            1 => "按住拖选格段 · Esc 清除",
            2 => "按住拖选格段，松开删除",
            3 => "选择目标类型并拖选格段，松开改造",
            _ => "拖动建造道路",
        };
        GetNode<Label>(Controls + "Help").Text = mode switch
        {
            1 => "左键拖选格段 · Esc 清除\n滚轮缩放 · 中键拖动\n路口移向分支后选择",
            2 => "左键拖选格段 · 松开删除\n滚轮缩放 · 中键拖动\nEsc 取消 · 路口不扩散",
            3 => "左键拖选格段 · 松开改造\n滚轮缩放 · 中键拖动\nEsc 取消 · 四种类型互换",
            _ => "左键拖动建造 · Esc 取消\n滚轮缩放 · 中键拖动\n主格点八方向 · 格心仅对角",
        };
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
        ["mode"] = _toolMode.Selected switch { 1 => "Select", 2 => "Remove", 3 => "ChangeProfile", _ => "Build" },
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
        var hoverQuery = _view.QuerySpan(world);
        if (hoverQuery.Status != SpatialQueryStatus.Ready)
        {
            RejectSelectionQuery(hoverQuery.Reason);
            return;
        }
        RoadGridSpan? hover = hoverQuery.Results.FirstOrDefault();
        if (_toolMode.Selected == 3 && hover is not null)
        {
            RoadProfileId target = _selectionSession.IsSelecting ? _selectionProfile : RoadProfiles.All[_profileChoice.Selected].Id;
            if (snapshot.FindEdge(hover.Edge)?.Profile == target) hover = null;
        }
        if (!_selectionSession.Hover(snapshot, hover))
        {
            _selectionLastWorld = null;
            _view.ClearSelection();
            return;
        }
        if (_selectionSession.IsSelecting)
        {
            var trace = _view.QuerySpans(_selectionLastWorld ?? world, world);
            if (trace.Status != SpatialQueryStatus.Ready)
            {
                RejectSelectionQuery(trace.Reason);
                return;
            }
            var crossed = trace.Results;
            _selectionSession.Accumulate(snapshot, _toolMode.Selected == 3
                ? crossed.Where(span => snapshot.FindEdge(span.Edge)?.Profile != _selectionProfile)
                : crossed);
            _selectionLastWorld = world;
            _status.Text = $"已选 {_selectionSession.Selected.Count} 个道路格段";
        }
        _view.SetSelection(_selectionSession.Hovered, _selectionSession.Selected);
    }

    private void RejectSelectionQuery(string reason)
    {
        ClearRoadSelection();
        _status.Text = reason + " · 请重新选择";
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
            long inputTimestamp = Stopwatch.GetTimestamp();
            bool canSubmit = CanEdit && !GetNode<Control>("HUD/Panel").GetGlobalRect().HasPoint(release.Position);
            if (canSubmit)
                UpdateSelectionPointer(release.Position);
            if (!_selectionSession.IsSelecting) return true;
            _selectionSession.End();
            _selectionLastWorld = null;
            if (IsSpanEditTool)
            {
                RoadGridSpan[] spans = canSubmit ? _selectionSession.Selected.ToArray() : [];
                int mode = _toolMode.Selected;
                if (spans.Length != 0) SubmitSpanEdit(spans, mode, _selectionProfile, inputTimestamp);
                else ClearRoadSelection();
                return true;
            }
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
            _selectionProfile = RoadProfiles.All[_profileChoice.Selected].Id;
            _selectionLastWorld = null;
            UpdateSelectionPointer(press.Position);
            return true;
        }
        if (input is InputEventMouseMotion motion) UpdateSelectionPointer(motion.Position);
        return false; // Camera wheel/pan still flows through the shared input handler.
    }

    private void SubmitSpanEdit(RoadGridSpan[] spans, int mode, RoadProfileId profile, long inputTimestamp) =>
        SubmitRoadOperation((network, token) =>
        {
            RoadEditResult result = mode == 2 ? network.PlanRemove(spans, token) : network.PlanChangeProfile(spans, profile, token);
            return new PlannedRoadOperation(result.Plan, result.Status == RoadEditStatus.NoChange, result.Reason);
        }, inputTimestamp, mode == 2 ? "格段已删除" : "格段已改造", "选择道路格段");
}
