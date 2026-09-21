using Godot;
using System.Diagnostics;
using SimpleCities.RoadCore;

public partial class V4MapScene
{
    private Button _undoRoad = null!;
    private Button _redoRoad = null!;
    private bool CanUseHistory => CanEdit && !_draftStart.HasValue && !_selectionSession.IsSelecting;

    private void InitializeHistory()
    {
        _undoRoad = GetNode<Button>(Controls + "History/Undo");
        _redoRoad = GetNode<Button>(Controls + "History/Redo");
        _undoRoad.Pressed += () => UndoRoadEdit();
        _redoRoad.Pressed += () => RedoRoadEdit();
    }

    private void UpdateHistoryControls()
    {
        _undoRoad.Disabled = !CanUseHistory || _roads!.Network.History.UndoCount == 0;
        _redoRoad.Disabled = !CanUseHistory || _roads!.Network.History.RedoCount == 0;
    }

    public bool UndoRoadEdit() => SubmitHistory(redo: false);
    public bool RedoRoadEdit() => SubmitHistory(redo: true);

    private bool SubmitHistory(bool redo)
    {
        long inputTimestamp = Stopwatch.GetTimestamp();
        if (!CanUseHistory || (redo ? _roads!.Network.History.RedoCount : _roads!.Network.History.UndoCount) == 0)
            return false;
        ClearRoadSelection();
        SubmitRoadOperation((network, token) =>
        {
            RoadEditResult result = redo ? network.PlanRedo(token) : network.PlanUndo(token);
            return new PlannedRoadOperation(result.Plan, result.Status == RoadEditStatus.NoChange, result.Reason);
        }, inputTimestamp, redo ? "道路操作已重做" : "道路操作已撤销", "");
        return true;
    }

    private bool HandleHistoryInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false, CtrlPressed: true } key ||
            key.Keycode is not (Key.Z or Key.Y)) return false;
        if (key.Keycode == Key.Y || key.ShiftPressed) RedoRoadEdit();
        else UndoRoadEdit();
        GetViewport().SetInputAsHandled();
        return true;
    }

    public Godot.Collections.Dictionary GetHistoryState()
    {
        if (_roads is null) return new();
        var history = _roads.Network.History;
        var snapshot = _roads.Network.Snapshot;
        return new()
        {
            ["undoCount"] = history.UndoCount, ["redoCount"] = history.RedoCount,
            ["retainedCount"] = history.RetainedCount, ["estimatedRetainedBytes"] = history.EstimatedBytes,
            ["sourceToken"] = snapshot.Token.ToString(), ["contentRevision"] = snapshot.Token.ContentRevision,
            ["changeSequence"] = snapshot.Token.ChangeSequence,
            ["nextNodeId"] = snapshot.NextNodeId, ["nextEdgeId"] = snapshot.NextEdgeId,
        };
    }
}
