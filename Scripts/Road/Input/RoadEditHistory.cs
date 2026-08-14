using System;
using System.Collections.Generic;

/// <summary>以已提交 RoadGraph delta 为边界维护容量受限的撤销与重做历史。</summary>
public sealed class RoadEditHistory : IDisposable
{
    public const int DefaultCapacity = 64;
    public const long DefaultByteCapacity = 16L * 1024L * 1024L;

    private readonly RoadGraph _graph;
    private readonly int _capacity;
    private readonly long _byteCapacity;
    private readonly List<EditEntry> _undoEntries = [];
    private readonly List<EditEntry> _redoEntries = [];
    private RoadGraphDelta? _pendingDelta;
    private GraphStateToken _pendingToken;
    private long _retainedByteSize;
    private bool _isExecutingEdit;
    private bool _isApplyingHistory;
    private bool _disposed;

    public bool CanUndo => _undoEntries.Count > 0;
    public bool CanRedo => _redoEntries.Count > 0;
    public int UndoCount => _undoEntries.Count;
    public int RedoCount => _redoEntries.Count;
    public long RetainedByteSize => _retainedByteSize;
    public long ByteCapacity => _byteCapacity;

    public RoadEditHistory(
        RoadGraph graph,
        int capacity = DefaultCapacity,
        long byteCapacity = DefaultByteCapacity)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "History capacity must be positive.");
        if (byteCapacity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(byteCapacity),
                byteCapacity,
                "History byte capacity must be positive.");

        _graph = graph;
        _capacity = capacity;
        _byteCapacity = byteCapacity;
        _graph.GraphChanged += OnGraphChanged;
    }

    public bool Execute(Func<bool> edit)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(edit);
        if (_isExecutingEdit || _isApplyingHistory)
            throw new InvalidOperationException("Road edit history cannot execute recursively.");

        _pendingDelta = null;
        _pendingToken = default;
        _isExecutingEdit = true;
        bool succeeded;
        try
        {
            succeeded = _graph.ExecuteWithDeltaAdmission(edit, AdmitDelta);
        }
        catch
        {
            RollBackPendingEdit();
            throw;
        }
        finally
        {
            _isExecutingEdit = false;
        }

        if (!succeeded)
        {
            RollBackPendingEdit();
            return false;
        }
        if (_pendingDelta is null)
            return true;

        ClearEntries(_redoEntries);
        AddEntry(_undoEntries, new EditEntry(_pendingDelta, _pendingToken));
        TrimOldestUndoEntries();
        _pendingDelta = null;
        _pendingToken = default;
        return true;
    }

    public bool Undo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_undoEntries.Count == 0)
            return false;

        EditEntry entry = _undoEntries[^1];
        RoadGraphDeltaApplyResult result = ApplyEntry(
            entry,
            RoadGraphDeltaDirection.Reverse);
        if (!result.Success)
        {
            Clear();
            return false;
        }

        _undoEntries.RemoveAt(_undoEntries.Count - 1);
        _redoEntries.Add(entry with { ExpectedToken = result.StateToken });
        UpdateTopExpectedToken(_undoEntries, result.StateToken);
        return true;
    }

    public bool Redo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_redoEntries.Count == 0)
            return false;

        EditEntry entry = _redoEntries[^1];
        RoadGraphDeltaApplyResult result = ApplyEntry(
            entry,
            RoadGraphDeltaDirection.Forward);
        if (!result.Success)
        {
            Clear();
            return false;
        }

        _redoEntries.RemoveAt(_redoEntries.Count - 1);
        _undoEntries.Add(entry with { ExpectedToken = result.StateToken });
        UpdateTopExpectedToken(_redoEntries, result.StateToken);
        return true;
    }

    public void Clear()
    {
        _undoEntries.Clear();
        _redoEntries.Clear();
        _retainedByteSize = 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _graph.GraphChanged -= OnGraphChanged;
        Clear();
        _disposed = true;
    }

    private bool AdmitDelta(RoadGraphDelta delta) =>
        !delta.IsFullReset &&
        _pendingDelta is null &&
        delta.EstimatedByteSize <= _byteCapacity;

    private RoadGraphDeltaApplyResult ApplyEntry(
        EditEntry entry,
        RoadGraphDeltaDirection direction)
    {
        _isApplyingHistory = true;
        try
        {
            return _graph.ApplyDelta(entry.Delta, direction, entry.ExpectedToken);
        }
        finally
        {
            _isApplyingHistory = false;
        }
    }

    private void RollBackPendingEdit()
    {
        if (_pendingDelta is null)
            return;

        var entry = new EditEntry(_pendingDelta, _pendingToken);
        RoadGraphDeltaApplyResult rollback = ApplyEntry(
            entry,
            RoadGraphDeltaDirection.Reverse);
        _pendingDelta = null;
        _pendingToken = default;
        Clear();
        if (!rollback.Success)
            throw new InvalidOperationException("A failed road edit could not be rolled back.");
    }

    private void OnGraphChanged(RoadGraphChangedEvent change)
    {
        if (_isApplyingHistory)
            return;
        if (_isExecutingEdit)
        {
            if (_pendingDelta is null && !change.Changes.IsFullReset)
            {
                _pendingDelta = change.Delta;
                _pendingToken = change.StateToken;
            }
            return;
        }
        Clear();
    }

    private void AddEntry(List<EditEntry> entries, EditEntry entry)
    {
        entries.Add(entry);
        _retainedByteSize = checked(_retainedByteSize + entry.Delta.EstimatedByteSize);
    }

    private void ClearEntries(List<EditEntry> entries)
    {
        foreach (EditEntry entry in entries)
            _retainedByteSize -= entry.Delta.EstimatedByteSize;
        entries.Clear();
    }

    private void TrimOldestUndoEntries()
    {
        while (_undoEntries.Count > _capacity || _retainedByteSize > _byteCapacity)
        {
            EditEntry oldest = _undoEntries[0];
            _undoEntries.RemoveAt(0);
            _retainedByteSize -= oldest.Delta.EstimatedByteSize;
        }
    }

    private static void UpdateTopExpectedToken(
        List<EditEntry> entries,
        GraphStateToken token)
    {
        if (entries.Count > 0)
            entries[^1] = entries[^1] with { ExpectedToken = token };
    }

    private sealed record EditEntry(
        RoadGraphDelta Delta,
        GraphStateToken ExpectedToken);
}
