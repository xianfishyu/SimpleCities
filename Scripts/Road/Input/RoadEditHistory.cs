using System;
using System.Collections.Generic;

/// <summary>以已提交 RoadGraph 状态为边界维护容量受限的撤销与重做历史。</summary>
public sealed class RoadEditHistory : IDisposable
{
    public const int DefaultCapacity = 64;

    private readonly RoadGraph _graph;
    private readonly int _capacity;
    private readonly List<EditEntry> _undoEntries = [];
    private readonly List<EditEntry> _redoEntries = [];
    private bool _isApplyingHistory;
    private bool _disposed;

    public bool CanUndo => _undoEntries.Count > 0;
    public bool CanRedo => _redoEntries.Count > 0;
    public int UndoCount => _undoEntries.Count;
    public int RedoCount => _redoEntries.Count;

    public RoadEditHistory(RoadGraph graph, int capacity = DefaultCapacity)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "History capacity must be positive.");

        _graph = graph;
        _capacity = capacity;
        _graph.GraphCleared += OnGraphCleared;
    }

    public bool Execute(Func<bool> edit)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(edit);

        string beforeState = CaptureState();
        DiscardDivergedHistory(beforeState);

        bool succeeded;
        try
        {
            succeeded = edit();
        }
        catch
        {
            RestoreIfChanged(beforeState);
            throw;
        }

        string afterState = CaptureState();
        if (!succeeded)
        {
            if (!StatesMatch(beforeState, afterState))
            {
                RestoreState(beforeState);
                throw new InvalidOperationException("A failed road edit changed the graph state.");
            }
            return false;
        }

        _redoEntries.Clear();
        if (StatesMatch(beforeState, afterState))
            return true;

        _undoEntries.Add(new EditEntry(beforeState, afterState));
        if (_undoEntries.Count > _capacity)
            _undoEntries.RemoveAt(0);
        return true;
    }

    public bool Undo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_undoEntries.Count == 0)
            return false;

        EditEntry entry = _undoEntries[^1];
        if (!StatesMatch(CaptureState(), entry.AfterState))
        {
            Clear();
            return false;
        }

        RestoreState(entry.BeforeState);
        _undoEntries.RemoveAt(_undoEntries.Count - 1);
        _redoEntries.Add(entry);
        return true;
    }

    public bool Redo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_redoEntries.Count == 0)
            return false;

        EditEntry entry = _redoEntries[^1];
        if (!StatesMatch(CaptureState(), entry.BeforeState))
        {
            Clear();
            return false;
        }

        RestoreState(entry.AfterState);
        _redoEntries.RemoveAt(_redoEntries.Count - 1);
        _undoEntries.Add(entry);
        return true;
    }

    public void Clear()
    {
        _undoEntries.Clear();
        _redoEntries.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _graph.GraphCleared -= OnGraphCleared;
        Clear();
        _disposed = true;
    }

    private void DiscardDivergedHistory(string currentState)
    {
        string? expectedState = _undoEntries.Count > 0
            ? _undoEntries[^1].AfterState
            : _redoEntries.Count > 0
                ? _redoEntries[^1].BeforeState
                : null;
        if (expectedState != null && !StatesMatch(expectedState, currentState))
            Clear();
    }

    private void RestoreIfChanged(string expectedState)
    {
        if (!StatesMatch(CaptureState(), expectedState))
            RestoreState(expectedState);
    }

    private string CaptureState() => SaveJson.Serialize(_graph.CaptureState());

    private void RestoreState(string state)
    {
        _isApplyingHistory = true;
        try
        {
            _graph.RestoreState(state);
        }
        finally
        {
            _isApplyingHistory = false;
        }
    }

    private void OnGraphCleared()
    {
        if (!_isApplyingHistory)
            Clear();
    }

    private static bool StatesMatch(string first, string second) =>
        string.Equals(first, second, StringComparison.Ordinal);

    private sealed record EditEntry(string BeforeState, string AfterState);
}
