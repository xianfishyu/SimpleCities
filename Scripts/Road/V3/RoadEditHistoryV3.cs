using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

public static class RoadGraphV3DeltaSizeEstimator
{
    public static long Estimate(RoadGraphV3Delta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        long entityCount = delta.NodeChanges.Count + delta.EdgeChanges.Count;
        long geometryCount = 0;
        foreach (RoadGraphV3EntityChange<RoadGraphV3Edge> change in delta.EdgeChanges)
        {
            geometryCount += change.Before?.Geometry.Count ?? 0;
            geometryCount += change.After?.Geometry.Count ?? 0;
        }

        return 64L + entityCount * 48L + geometryCount * 32L;
    }
}

/// <summary>
/// V3 有界 delta 历史：按 entry 数与估算字节双预算淘汰最旧 undo；
/// 单条超过字节预算的编辑在入历史前失败。redo 在 push 时清空。
/// </summary>
public sealed class RoadEditHistoryV3
{
    private sealed record Entry(RoadGraphV3Delta Delta, long EstimatedBytes);

    private readonly List<Entry> _undo = new();
    private readonly List<Entry> _redo = new();
    private readonly int _maxEntries;
    private readonly long _maxBytes;
    private long _undoBytes;
    private long _redoBytes;

    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public long TotalBytes => _undoBytes + _redoBytes;

    public RoadEditHistoryV3(int maxEntries, long maxBytes)
    {
        if (maxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "Max entries must be positive.");
        if (maxBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Max bytes must be positive.");

        _maxEntries = maxEntries;
        _maxBytes = maxBytes;
    }

    public bool TryPush(RoadGraphV3Delta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        if (delta.IsEmpty)
            return false;

        long size = RoadGraphV3DeltaSizeEstimator.Estimate(delta);
        if (size > _maxBytes)
            return false;

        EvictUndoUntilFits(size);
        if (_undo.Count >= _maxEntries)
            return false;

        _undo.Add(new Entry(delta, size));
        _undoBytes += size;
        _redo.Clear();
        _redoBytes = 0;
        return true;
    }

    public bool TryUndo(out RoadGraphV3Delta delta)
    {
        if (_undo.Count == 0)
        {
            delta = null!;
            return false;
        }

        Entry entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _undoBytes -= entry.EstimatedBytes;
        _redo.Add(entry);
        _redoBytes += entry.EstimatedBytes;
        delta = entry.Delta;
        return true;
    }

    public bool TryRedo(out RoadGraphV3Delta delta)
    {
        if (_redo.Count == 0)
        {
            delta = null!;
            return false;
        }

        Entry entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _redoBytes -= entry.EstimatedBytes;
        _undo.Add(entry);
        _undoBytes += entry.EstimatedBytes;
        delta = entry.Delta;
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _undoBytes = 0;
        _redoBytes = 0;
    }

    private void EvictUndoUntilFits(long incomingSize)
    {
        while (_undo.Count > 0 &&
               (_undo.Count >= _maxEntries || _undoBytes + incomingSize > _maxBytes))
        {
            Entry oldest = _undo[0];
            _undo.RemoveAt(0);
            _undoBytes -= oldest.EstimatedBytes;
        }
    }
}
