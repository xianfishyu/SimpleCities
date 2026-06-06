using Godot;
using System.Collections.Generic;

/// <summary>
/// 可被空间索引引用的实体必须实现此接口。
/// </summary>
public interface ISpatialRef
{
    Vector2 Position { get; }
    SpatialRefKind Kind { get; }
}

public enum SpatialRefKind
{
    Node,
    EdgePoint
}

/// <summary>
/// 节点的空间引用。持有节点 ID 和位置。
/// </summary>
public class NodeSpatialRef : ISpatialRef
{
    public int NodeID { get; }
    public Vector2 Position { get; }
    public SpatialRefKind Kind => SpatialRefKind.Node;

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

    public EdgePointRef(int edgeID, Vector2 position)
    {
        EdgeID = edgeID;
        Position = position;
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
        if (!_buckets.TryGetValue((bx, by), out var list))
            _buckets[(bx, by)] = list = new List<ISpatialRef>();
        list.Add(entity);
    }

    /// <summary>移除指定实体的所有条目。</summary>
    public void Remove(ISpatialRef entity)
    {
        var (bx, by) = WorldToBucket(entity.Position);
        if (_buckets.TryGetValue((bx, by), out var list))
            list.RemoveAll(r => r == entity);
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

        float radiusSq = radius * radius;

        for (int bx = minBX; bx <= maxBX; bx++)
        for (int by = minBY; by <= maxBY; by++)
        {
            if (!_buckets.TryGetValue((bx, by), out var list)) continue;
            foreach (var entity in list)
            {
                if (entity.Position.DistanceSquaredTo(center) <= radiusSq)
                    yield return entity;
            }
        }
    }

    /// <summary>清空所有索引。</summary>
    public void Clear() => _buckets.Clear();

    private (int bx, int by) WorldToBucket(Vector2 pos)
    {
        return (WorldToBucketCoord(pos.X), WorldToBucketCoord(pos.Y));
    }

    private int WorldToBucketCoord(float val)
    {
        return Mathf.FloorToInt(val / _bucketSize);
    }
}
