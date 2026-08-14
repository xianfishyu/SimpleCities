using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

/// <summary>
/// 可被空间索引引用的实体必须实现此接口。
/// </summary>
public interface ISpatialRef
{
    Vector2 Position { get; }
    SpatialRefKind Kind { get; }
    bool IntersectsCircle(Vector2 center, float radius);
}

public enum SpatialRefKind
{
    Node,
    EdgePoint,
    EdgeSegment,
    EdgeGeometry,
}

internal readonly record struct RoadLocation(
    int EdgeID,
    int GeometryIndex,
    float Parameter);

/// <summary>
/// 节点的空间引用。持有节点 ID 和位置。
/// </summary>
public class NodeSpatialRef : ISpatialRef
{
    public int NodeID { get; }
    public Vector2 Position { get; }
    public SpatialRefKind Kind => SpatialRefKind.Node;
    public bool IntersectsCircle(Vector2 center, float radius) =>
        RoadNumericPolicy.DistanceSquared(Position, center) <= (double)radius * radius;

    public NodeSpatialRef(int nodeID, Vector2 position)
    {
        NodeID = nodeID;
        Position = position;
    }
}

/// <summary>
/// 边途经点的空间引用。持有边 ID 和该点的位置。
/// </summary>
public class EdgePointRef : ISpatialRef
{
    public int EdgeID { get; }
    public Vector2 Position { get; }
    public SpatialRefKind Kind => SpatialRefKind.EdgePoint;
    public bool IntersectsCircle(Vector2 center, float radius) =>
        RoadNumericPolicy.DistanceSquared(Position, center) <= (double)radius * radius;

    public EdgePointRef(int edgeID, Vector2 position)
    {
        EdgeID = edgeID;
        Position = position;
    }
}

public class EdgeSegmentRef : ISpatialRef
{
    public int EdgeID { get; }
    public Vector2 Start { get; }
    public Vector2 End { get; }
    public Vector2 Position => (Start + End) * 0.5f;
    public SpatialRefKind Kind => SpatialRefKind.EdgeSegment;

    public EdgeSegmentRef(int edgeID, Vector2 start, Vector2 end)
    {
        EdgeID = edgeID;
        Start = start;
        End = end;
    }

    public bool IntersectsCircle(Vector2 center, float radius) =>
        DistanceSquaredTo(center) <= radius * radius;

    private float DistanceSquaredTo(Vector2 point)
    {
        Vector2 segment = End - Start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0f)
            return Start.DistanceSquaredTo(point);

        float t = Mathf.Clamp((point - Start).Dot(segment) / lengthSquared, 0f, 1f);
        return (Start + segment * t).DistanceSquaredTo(point);
    }
}

public sealed class EdgeGeometryRef : ISpatialRef
{
    public int EdgeID { get; }
    public int GeometryIndex { get; }
    public int FragmentIndex { get; }
    public float ParameterStart { get; }
    public float ParameterEnd { get; }
    public RoadGeometrySegment SourceGeometry { get; }
    public RoadGeometrySegment Geometry { get; }
    public bool OwnsParameterEnd { get; }
    public Rect2 Bounds => Geometry.Bounds;
    public Vector2 Position => Bounds.GetCenter();
    public SpatialRefKind Kind => SpatialRefKind.EdgeGeometry;

    internal EdgeGeometryRef(
        int edgeID,
        int geometryIndex,
        int fragmentIndex,
        float parameterStart,
        float parameterEnd,
        RoadGeometrySegment sourceGeometry,
        RoadGeometrySegment geometry,
        bool ownsParameterEnd)
    {
        EdgeID = edgeID;
        GeometryIndex = geometryIndex;
        FragmentIndex = fragmentIndex;
        ParameterStart = parameterStart;
        ParameterEnd = parameterEnd;
        SourceGeometry = sourceGeometry;
        Geometry = geometry;
        OwnsParameterEnd = ownsParameterEnd;
    }

    public bool IntersectsCircle(Vector2 center, float radius) =>
        DistanceSquaredToBounds(center, Bounds) <= (double)radius * radius;

    internal bool OwnsLocalParameter(float parameter) =>
        parameter < RoadGeometrySegment.ParameterEnd || OwnsParameterEnd;

    internal float ToSourceParameter(float localParameter) =>
        Mathf.Lerp(ParameterStart, ParameterEnd, localParameter);

    internal bool TryGetRoadLocation(float localParameter, out RoadLocation location)
    {
        if (!float.IsFinite(localParameter) ||
            localParameter < RoadGeometrySegment.ParameterStart ||
            localParameter > RoadGeometrySegment.ParameterEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(localParameter));
        }

        if (!OwnsLocalParameter(localParameter))
        {
            location = default;
            return false;
        }

        location = new RoadLocation(EdgeID, GeometryIndex, ToSourceParameter(localParameter));
        return true;
    }

    private static double DistanceSquaredToBounds(Vector2 point, Rect2 bounds)
    {
        Vector2 end = bounds.End;
        double closestX = Math.Clamp((double)point.X, bounds.Position.X, end.X);
        double closestY = Math.Clamp((double)point.Y, bounds.Position.Y, end.Y);
        double dx = point.X - closestX;
        double dy = point.Y - closestY;
        return dx * dx + dy * dy;
    }
}

internal static class RoadQueryFragmentFactory
{
    private const int MaximumSubdivisionDepth = 24;

    internal static IReadOnlyList<EdgeGeometryRef> Create(
        int edgeID,
        int geometryIndex,
        RoadGeometrySegment geometry,
        float targetSpan,
        bool ownsGeometryEnd)
    {
        if (!TryCreate(
                edgeID,
                geometryIndex,
                geometry,
                targetSpan,
                ownsGeometryEnd,
                int.MaxValue,
                out IReadOnlyList<EdgeGeometryRef> fragments))
        {
            throw new InvalidOperationException("Query-fragment planning exceeded the supported fragment count.");
        }

        return fragments;
    }

    internal static bool TryCreateChain(
        int edgeID,
        IReadOnlyList<RoadGeometrySegment> geometrySegments,
        float targetSpan,
        bool ownsEdgeEnd,
        int maximumFragments,
        out IReadOnlyList<EdgeGeometryRef> fragments)
    {
        ArgumentNullException.ThrowIfNull(geometrySegments);
        if (maximumFragments < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumFragments));

        var planned = new List<EdgeGeometryRef>(Math.Min(geometrySegments.Count, maximumFragments));
        for (int geometryIndex = 0; geometryIndex < geometrySegments.Count; geometryIndex++)
        {
            RoadGeometrySegment geometry = geometrySegments[geometryIndex]
                ?? throw new ArgumentException("A geometry chain cannot contain null segments.", nameof(geometrySegments));
            int remaining = maximumFragments - planned.Count;
            if (!TryCreate(
                    edgeID,
                    geometryIndex,
                    geometry,
                    targetSpan,
                    ownsEdgeEnd && geometryIndex == geometrySegments.Count - 1,
                    remaining,
                    out IReadOnlyList<EdgeGeometryRef> geometryFragments))
            {
                fragments = Array.Empty<EdgeGeometryRef>();
                return false;
            }
            planned.AddRange(geometryFragments);
        }

        fragments = planned;
        return true;
    }

    private static bool TryCreate(
        int edgeID,
        int geometryIndex,
        RoadGeometrySegment geometry,
        float targetSpan,
        bool ownsGeometryEnd,
        int maximumFragments,
        out IReadOnlyList<EdgeGeometryRef> fragments)
    {
        if (!float.IsFinite(targetSpan) || targetSpan <= 0f)
            throw new ArgumentOutOfRangeException(nameof(targetSpan));
        if (maximumFragments < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumFragments));

        var planned = new List<EdgeGeometryRef>(Math.Min(maximumFragments, 256));
        var pending = new Stack<PendingFragment>();
        pending.Push(new PendingFragment(
            geometry,
            RoadGeometrySegment.ParameterStart,
            RoadGeometrySegment.ParameterEnd,
            0));

        while (pending.TryPop(out PendingFragment candidate))
        {
            Rect2 bounds = candidate.Geometry.Bounds;
            float span = Mathf.Max(Mathf.Abs(bounds.Size.X), Mathf.Abs(bounds.Size.Y));
            if (span <= targetSpan || candidate.Depth == MaximumSubdivisionDepth)
            {
                if (planned.Count == maximumFragments)
                {
                    fragments = Array.Empty<EdgeGeometryRef>();
                    return false;
                }
                planned.Add(new EdgeGeometryRef(
                    edgeID,
                    geometryIndex,
                    planned.Count,
                    candidate.ParameterStart,
                    candidate.ParameterEnd,
                    geometry,
                    candidate.Geometry,
                    ownsGeometryEnd && candidate.ParameterEnd == RoadGeometrySegment.ParameterEnd));
                continue;
            }

            RoadGeometrySplit split = candidate.Geometry.Split(0.5f);
            float midpoint = (candidate.ParameterStart + candidate.ParameterEnd) * 0.5f;
            pending.Push(new PendingFragment(
                split.After,
                midpoint,
                candidate.ParameterEnd,
                candidate.Depth + 1));
            pending.Push(new PendingFragment(
                split.Before,
                candidate.ParameterStart,
                midpoint,
                candidate.Depth + 1));
        }

        fragments = planned;
        return true;
    }

    private readonly record struct PendingFragment(
        RoadGeometrySegment Geometry,
        float ParameterStart,
        float ParameterEnd,
        int Depth);
}

/// <summary>
/// 基于均匀网格的空间哈希索引。
/// 世界空间被划分为 BucketSize × BucketSize 的方形桶。
/// 每个桶存储落入该范围的所有 ISpatialRef 引用。
///
/// 性能：
///   - 插入/移除：O(1)（哈希表 + List 操作）
///   - 半径查询：O(1 + k) 其中 k = 命中桶内的实体数（小常数）
///   - 适用于城市规模（数千节点 + 数万边）的场景
/// </summary>
public class UniformGrid
{
    private readonly float _bucketSize;
    private ImmutableDictionary<(int bx, int by), ImmutableArray<ISpatialRef>>.Builder _buckets;
    private long _referenceEntryCount;

    public UniformGrid(float bucketSize)
    {
        _bucketSize = Mathf.Max(bucketSize, 1f);
        _buckets = ImmutableDictionary.CreateBuilder<
            (int bx, int by),
            ImmutableArray<ISpatialRef>>();
    }

    internal UniformGrid(UniformGridSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _bucketSize = snapshot.BucketSize;
        _buckets = snapshot.Buckets.ToBuilder();
        _referenceEntryCount = snapshot.ReferenceEntryCount;
    }

    internal int BucketCount => _buckets.Count;
    internal long ReferenceEntryCount => _referenceEntryCount;
    internal float BucketSize => _bucketSize;

    internal UniformGridSnapshot CaptureSnapshot() => new(
        _bucketSize,
        _buckets.ToImmutable(),
        _referenceEntryCount);

    /// <summary>插入一个空间引用。同一实体可多次插入（如一个边插入其所有途经点）。</summary>
    public void Insert(ISpatialRef entity)
    {
        var (bx, by) = WorldToBucket(entity.Position);
        InsertIntoBucket(bx, by, entity);
    }

    public void InsertSegment(EdgeSegmentRef segment)
    {
        foreach (var (bx, by) in GetCoveredBuckets(segment.Start, segment.End))
            InsertIntoBucket(bx, by, segment);
    }

    public void InsertGeometry(EdgeGeometryRef geometry)
    {
        foreach (var (bx, by) in GetCoveredBuckets(geometry.Bounds))
            InsertIntoBucket(bx, by, geometry);
    }

    /// <summary>移除指定实体的所有条目。</summary>
    public void Remove(ISpatialRef entity)
    {
        var (bx, by) = WorldToBucket(entity.Position);
        RemoveFromBucket(bx, by, reference => reference == entity);
    }

    public void RemoveSegment(EdgeSegmentRef segment)
    {
        foreach (var (bx, by) in GetCoveredBuckets(segment.Start, segment.End))
            RemoveFromBucket(bx, by, reference => reference == segment);
    }

    public void RemoveGeometry(EdgeGeometryRef geometry)
    {
        foreach (var (bx, by) in GetCoveredBuckets(geometry.Bounds))
            RemoveFromBucket(bx, by, reference => reference == geometry);
    }

    /// <summary>
    /// 查询以 center 为圆心、radius 为半径范围内的所有实体。
    /// 桶级预过滤 + 精确距离检查。
    /// </summary>
    public IEnumerable<ISpatialRef> QueryRadius(Vector2 center, float radius)
    {
        int minBX = WorldToBucketCoord(center.X - radius);
        int maxBX = WorldToBucketCoord(center.X + radius);
        int minBY = WorldToBucketCoord(center.Y - radius);
        int maxBY = WorldToBucketCoord(center.Y + radius);

        var returned = new HashSet<ISpatialRef>();

        for (int bx = minBX; bx <= maxBX; bx++)
        for (int by = minBY; by <= maxBY; by++)
        {
            if (!_buckets.TryGetValue((bx, by), out ImmutableArray<ISpatialRef> list)) continue;
            foreach (var entity in list)
            {
                if (returned.Add(entity) && entity.IntersectsCircle(center, radius))
                    yield return entity;
            }
        }
    }

    /// <summary>
    /// 返回与矩形覆盖同一批 bucket 的去重引用。调用方负责权威几何过滤。
    /// </summary>
    public IEnumerable<ISpatialRef> QueryBounds(Rect2 bounds)
    {
        var returned = new HashSet<ISpatialRef>();
        foreach ((int bx, int by) in GetCoveredBuckets(bounds))
        {
            if (!_buckets.TryGetValue((bx, by), out ImmutableArray<ISpatialRef> list)) continue;
            foreach (ISpatialRef entity in list)
                if (returned.Add(entity))
                    yield return entity;
        }
    }

    /// <summary>清空所有索引。</summary>
    public void Clear()
    {
        _buckets = ImmutableDictionary.CreateBuilder<
            (int bx, int by),
            ImmutableArray<ISpatialRef>>();
        _referenceEntryCount = 0;
    }

    internal bool TryCountCoveredBuckets(Rect2 bounds, out long count)
    {
        if (!TryGetBucketCoverage(bounds, out BucketCoverage coverage))
        {
            count = 0;
            return false;
        }

        count = coverage.Count;
        return true;
    }

    internal bool HasExactCoverage(IReadOnlyDictionary<ISpatialRef, Rect2> expectedBounds)
    {
        ArgumentNullException.ThrowIfNull(expectedBounds);
        var coverageByReference = new Dictionary<ISpatialRef, BucketCoverage>(
            ReferenceEqualityComparer.Instance);
        var remainingEntries = new Dictionary<ISpatialRef, long>(
            ReferenceEqualityComparer.Instance);
        foreach ((ISpatialRef reference, Rect2 bounds) in expectedBounds)
        {
            if (!TryGetBucketCoverage(bounds, out BucketCoverage coverage) ||
                !coverageByReference.TryAdd(reference, coverage) ||
                !remainingEntries.TryAdd(reference, coverage.Count))
            {
                return false;
            }
        }

        long observedEntryCount = 0L;
        foreach (((int bx, int by) bucket, ImmutableArray<ISpatialRef> references) in _buckets)
        {
            var referencesInBucket = new HashSet<ISpatialRef>(ReferenceEqualityComparer.Instance);
            foreach (ISpatialRef reference in references)
            {
                observedEntryCount++;
                if (!referencesInBucket.Add(reference) ||
                    !coverageByReference.TryGetValue(reference, out BucketCoverage coverage) ||
                    bucket.bx < coverage.MinBX || bucket.bx > coverage.MaxBX ||
                    bucket.by < coverage.MinBY || bucket.by > coverage.MaxBY ||
                    --remainingEntries[reference] < 0L)
                {
                    return false;
                }
            }
        }

        return observedEntryCount == _referenceEntryCount &&
               remainingEntries.Values.All(remaining => remaining == 0L);
    }

    private bool TryGetBucketCoverage(Rect2 bounds, out BucketCoverage coverage)
    {
        coverage = default;
        if (!bounds.Position.IsFinite() || !bounds.End.IsFinite())
            return false;

        int minBX = WorldToBucketCoord(Mathf.Min(bounds.Position.X, bounds.End.X));
        int maxBX = WorldToBucketCoord(Mathf.Max(bounds.Position.X, bounds.End.X));
        int minBY = WorldToBucketCoord(Mathf.Min(bounds.Position.Y, bounds.End.Y));
        int maxBY = WorldToBucketCoord(Mathf.Max(bounds.Position.Y, bounds.End.Y));
        long width = (long)maxBX - minBX + 1L;
        long height = (long)maxBY - minBY + 1L;
        if (width <= 0L || height <= 0L || width > long.MaxValue / height)
            return false;

        coverage = new BucketCoverage(minBX, maxBX, minBY, maxBY, width * height);
        return true;
    }

    private (int bx, int by) WorldToBucket(Vector2 pos)
    {
        return (WorldToBucketCoord(pos.X), WorldToBucketCoord(pos.Y));
    }

    private int WorldToBucketCoord(float val)
    {
        return Mathf.FloorToInt(val / _bucketSize);
    }

    private void InsertIntoBucket(int bx, int by, ISpatialRef entity)
    {
        (int bx, int by) key = (bx, by);
        ImmutableArray<ISpatialRef> page = _buckets.GetValueOrDefault(
            key,
            ImmutableArray<ISpatialRef>.Empty);
        _buckets[key] = page.Add(entity);
        _referenceEntryCount++;
    }

    private void RemoveFromBucket(
        int bx,
        int by,
        Func<ISpatialRef, bool> predicate)
    {
        (int bx, int by) key = (bx, by);
        if (!_buckets.TryGetValue(key, out ImmutableArray<ISpatialRef> page))
            return;

        ImmutableArray<ISpatialRef>.Builder retained = page.ToBuilder();
        int beforeCount = retained.Count;
        retained.RemoveAll(reference => predicate(reference));
        int removed = beforeCount - retained.Count;
        if (removed == 0)
            return;

        _referenceEntryCount -= removed;
        if (retained.Count == 0)
            _buckets.Remove(key);
        else
            _buckets[key] = retained.ToImmutable();
    }

    private IEnumerable<(int bx, int by)> GetCoveredBuckets(Vector2 start, Vector2 end)
    {
        int minBX = WorldToBucketCoord(Mathf.Min(start.X, end.X));
        int maxBX = WorldToBucketCoord(Mathf.Max(start.X, end.X));
        int minBY = WorldToBucketCoord(Mathf.Min(start.Y, end.Y));
        int maxBY = WorldToBucketCoord(Mathf.Max(start.Y, end.Y));

        for (int bx = minBX; bx <= maxBX; bx++)
        for (int by = minBY; by <= maxBY; by++)
            yield return (bx, by);
    }

    private IEnumerable<(int bx, int by)> GetCoveredBuckets(Rect2 bounds)
    {
        int minBX = WorldToBucketCoord(bounds.Position.X);
        int maxBX = WorldToBucketCoord(bounds.End.X);
        int minBY = WorldToBucketCoord(bounds.Position.Y);
        int maxBY = WorldToBucketCoord(bounds.End.Y);

        for (int bx = minBX; bx <= maxBX; bx++)
        for (int by = minBY; by <= maxBY; by++)
            yield return (bx, by);
    }

    private readonly record struct BucketCoverage(
        int MinBX,
        int MaxBX,
        int MinBY,
        int MaxBY,
        long Count);
}

internal sealed class UniformGridSnapshot
{
    internal float BucketSize { get; }
    internal ImmutableDictionary<(int bx, int by), ImmutableArray<ISpatialRef>> Buckets { get; }
    internal long ReferenceEntryCount { get; }

    internal UniformGridSnapshot(
        float bucketSize,
        ImmutableDictionary<(int bx, int by), ImmutableArray<ISpatialRef>> buckets,
        long referenceEntryCount)
    {
        BucketSize = bucketSize;
        Buckets = buckets ?? throw new ArgumentNullException(nameof(buckets));
        ReferenceEntryCount = referenceEntryCount;
    }
}
