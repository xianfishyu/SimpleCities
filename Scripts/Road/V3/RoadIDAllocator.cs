using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 lineage 内只前进、不复用的 checked ID allocator。
/// 失败与 NoChanges 不得推进 watermark；外部 load/full reset 创建新 allocator 并采用 payload 中的 nextID。
/// </summary>
public sealed class RoadIDAllocator
{
    private int _nextID;
    private readonly int _maxID;

    public int NextID => _nextID;
    public int MaxID => _maxID;

    public RoadIDAllocator(int nextID, int maxID)
    {
        if (nextID < 0)
            throw new ArgumentOutOfRangeException(nameof(nextID), "nextID must be non-negative.");
        if (maxID < 0)
            throw new ArgumentOutOfRangeException(nameof(maxID), "maxID must be non-negative.");
        if (nextID > maxID)
            throw new ArgumentOutOfRangeException(nameof(nextID), "nextID must not exceed maxID.");

        _nextID = nextID;
        _maxID = maxID;
    }

    public bool TryAllocate(out int id)
    {
        if (_nextID >= _maxID)
        {
            id = 0;
            return false;
        }

        id = _nextID;
        _nextID = checked(_nextID + 1);
        return true;
    }

    public bool TryReserve(int count, out int firstID)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Reservation count must be non-negative.");

        if (count == 0)
        {
            firstID = _nextID;
            return true;
        }

        long remaining = (long)_maxID - _nextID;
        if (count > remaining)
        {
            firstID = 0;
            return false;
        }

        firstID = _nextID;
        _nextID = checked((int)(_nextID + count));
        return true;
    }
}
