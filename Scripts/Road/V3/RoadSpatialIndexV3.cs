using Godot;
using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 空间索引骨架：按 query fragment 的保守 bounds 放入 uniform grid bucket。
/// 只保存可丢弃、可重建的派生引用；容量超限时插入失败，不回退全图扫描。
/// 后续将按指南 3.4 把长 geometry 切成参数区间 fragment。
/// </summary>
public sealed class RoadSpatialIndexV3
{
    private readonly float _bucketSize;
    private readonly RoadGraphCapacity _capacity;
    private readonly Dictionary<(int X, int Y), List<RoadQueryFragment>> _buckets = new();
    private readonly List<RoadQueryFragment> _fragments = new();
    private int _bucketReferenceCount;

    public int FragmentCount => _fragments.Count;
    public int BucketCount => _buckets.Count;
    public int BucketReferenceCount => _bucketReferenceCount;
    public int LastQueryCandidateCount { get; private set; }

    public RoadSpatialIndexV3(float bucketSize, RoadGraphCapacity capacity)
    {
        if (!float.IsFinite(bucketSize) || bucketSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(bucketSize), "Bucket size must be finite and positive.");
        capacity.Validate();
        _bucketSize = bucketSize;
        _capacity = capacity;
    }

    public bool TryInsert(RoadQueryFragment fragment)
    {
        if (!IsValidFragment(fragment))
            throw new ArgumentException("Fragment bounds/parameters are invalid.", nameof(fragment));
        if (_fragments.Count >= _capacity.MaxQueryFragments)
            return false;

        (int MinX, int MaxX, int MinY, int MaxY) cells = GetCellRange(fragment.ConservativeBounds);
        long cellCount = ((long)cells.MaxX - cells.MinX + 1L) * (cells.MaxY - cells.MinY + 1L);
        if (cellCount > _capacity.MaxBucketReferences || _bucketReferenceCount + cellCount > _capacity.MaxBucketReferences)
            return false;

        int newBucketCount = 0;
        for (int x = cells.MinX; x <= cells.MaxX; x++)
        {
            for (int y = cells.MinY; y <= cells.MaxY; y++)
            {
                if (!_buckets.ContainsKey((x, y)))
                    newBucketCount++;
            }
        }

        if (_buckets.Count + newBucketCount > _capacity.MaxBuckets)
            return false;

        for (int x = cells.MinX; x <= cells.MaxX; x++)
        {
            for (int y = cells.MinY; y <= cells.MaxY; y++)
            {
                var key = (x, y);
                if (!_buckets.TryGetValue(key, out List<RoadQueryFragment>? list))
                {
                    list = [];
                    _buckets[key] = list;
                }

                list.Add(fragment);
                _bucketReferenceCount++;
            }
        }

        _fragments.Add(fragment);
        return true;
    }

    public IReadOnlyList<RoadQueryFragment> QueryRadius(Vector2 center, float radius)
    {
        if (!RoadNumericPolicy.IsWithinCoordinateRange(center))
            throw new ArgumentOutOfRangeException(nameof(center), "Query center must be within the V3 numeric range.");
        if (!float.IsFinite(radius) || radius < 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite and non-negative.");

        var queryBounds = new Rect2(
            center.X - radius,
            center.Y - radius,
            radius * 2f,
            radius * 2f);
        return QueryRect(queryBounds);
    }

    public IReadOnlyList<RoadQueryFragment> QueryRect(Rect2 rect)
    {
        if (!RoadNumericPolicy.IsWithinCoordinateRange(rect.Position) ||
            !RoadNumericPolicy.IsWithinCoordinateRange(rect.End))
        {
            throw new ArgumentOutOfRangeException(nameof(rect), "Query rect must be within the V3 numeric range.");
        }

        var result = new HashSet<RoadQueryFragment>();
        (int MinX, int MaxX, int MinY, int MaxY) cells = GetCellRange(rect);
        for (int x = cells.MinX; x <= cells.MaxX; x++)
        {
            for (int y = cells.MinY; y <= cells.MaxY; y++)
            {
                if (!_buckets.TryGetValue((x, y), out List<RoadQueryFragment>? list))
                    continue;
                foreach (RoadQueryFragment fragment in list)
                {
                    if (fragment.ConservativeBounds.Intersects(rect))
                        result.Add(fragment);
                }
            }
        }

        LastQueryCandidateCount = result.Count;
        return [.. result];
    }

    public void Clear()
    {
        _buckets.Clear();
        _fragments.Clear();
        _bucketReferenceCount = 0;
        LastQueryCandidateCount = 0;
    }

    private (int MinX, int MaxX, int MinY, int MaxY) GetCellRange(Rect2 bounds)
    {
        int minX = Mathf.FloorToInt(bounds.Position.X / _bucketSize);
        int maxX = Mathf.FloorToInt(bounds.End.X / _bucketSize);
        int minY = Mathf.FloorToInt(bounds.Position.Y / _bucketSize);
        int maxY = Mathf.FloorToInt(bounds.End.Y / _bucketSize);
        return (minX, maxX, minY, maxY);
    }

    private static bool IsValidFragment(RoadQueryFragment fragment)
    {
        if (fragment.ParameterStart < 0f || fragment.ParameterEnd > 1f || fragment.ParameterStart > fragment.ParameterEnd)
            return false;
        if (!RoadNumericPolicy.IsWithinCoordinateRange(fragment.ConservativeBounds.Position) ||
            !RoadNumericPolicy.IsWithinCoordinateRange(fragment.ConservativeBounds.End))
        {
            return false;
        }

        return true;
    }
}
