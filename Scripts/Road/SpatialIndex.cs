using Godot;
using System.Collections.Generic;
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

/// <summary>
/// 节点的空间引用。持有节点 ID 和位置。
/// </summary>
public class NodeSpatialRef : ISpatialRef
{
    public int NodeID { get; }
    public Vector2 Position { get; }
    public SpatialRefKind Kind => SpatialRefKind.Node;
    public bool IntersectsCircle(Vector2 center, float radius) =>
        Position.DistanceSquaredTo(center) <= radius * radius;

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
        Position.DistanceSquaredTo(center) <= radius * radius;

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
    private const float QueryTolerance = 1e-4f;

    public int EdgeID { get; }
    public RoadGeometrySegment Geometry { get; }
    public Rect2 Bounds => Geometry.Bounds;
    public Vector2 Position => Bounds.GetCenter();
    public SpatialRefKind Kind => SpatialRefKind.EdgeGeometry;

    public EdgeGeometryRef(int edgeID, RoadGeometrySegment geometry)
    {
        EdgeID = edgeID;
        Geometry = geometry;
    }

    public bool IntersectsCircle(Vector2 center, float radius)
    {
        RoadGeometryClosestPoint closest = Geometry.FindClosestPoint(center, QueryTolerance);
        float inclusiveRadius = radius + QueryTolerance;
        return closest.DistanceSquared <= inclusiveRadius * inclusiveRadius;
    }
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
    private readonly Dictionary<(int bx, int by), List<ISpatialRef>> _buckets = new();

    public UniformGrid(float bucketSize)
    {
        _bucketSize = Mathf.Max(bucketSize, 1f);
    }

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
        if (_buckets.TryGetValue((bx, by), out var list))
            list.RemoveAll(r => r == entity);
    }

    public void RemoveSegment(EdgeSegmentRef segment)
    {
        foreach (var (bx, by) in GetCoveredBuckets(segment.Start, segment.End))
        {
            if (_buckets.TryGetValue((bx, by), out var list))
                list.RemoveAll(reference => reference == segment);
        }
    }

    public void RemoveGeometry(EdgeGeometryRef geometry)
    {
        foreach (var (bx, by) in GetCoveredBuckets(geometry.Bounds))
        {
            if (_buckets.TryGetValue((bx, by), out var list))
                list.RemoveAll(reference => reference == geometry);
        }
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
            if (!_buckets.TryGetValue((bx, by), out var list)) continue;
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
            if (!_buckets.TryGetValue((bx, by), out var list)) continue;
            foreach (ISpatialRef entity in list)
                if (returned.Add(entity))
                    yield return entity;
        }
    }

    /// <summary>清空所有索引。</summary>
    public void Clear() => _buckets.Clear();

    internal HashSet<ISpatialRef> CaptureDistinctReferences() =>
        _buckets.Values.SelectMany(bucket => bucket).ToHashSet();

    internal bool HasExactCoverage(ISpatialRef reference, Rect2 bounds)
    {
        HashSet<(int bx, int by)> expected = GetCoveredBuckets(bounds).ToHashSet();
        var actual = new HashSet<(int bx, int by)>();

        foreach (((int bx, int by) bucket, List<ISpatialRef> references) in _buckets)
        {
            int occurrences = references.Count(candidate => ReferenceEquals(candidate, reference));
            if (occurrences > 1)
                return false;
            if (occurrences == 1)
                actual.Add(bucket);
        }

        return expected.SetEquals(actual);
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
        if (!_buckets.TryGetValue((bx, by), out var list))
            _buckets[(bx, by)] = list = new List<ISpatialRef>();
        list.Add(entity);
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
}
