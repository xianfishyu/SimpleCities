namespace SimpleCities.RoadCore;

/// <summary>当前会话的不可变领域变更历史；撤销与重做共用最多 64 条记录。</summary>
public sealed class RoadEditHistory
{
    public const int Capacity = 64;
    private readonly RoadChangeSet[] _changes;

    private RoadEditHistory(RoadChangeSet[] changes, int cursor)
    {
        _changes = changes;
        UndoCount = cursor;
        EstimatedBytes = changes.Sum(Estimate);
    }

    internal static RoadEditHistory Empty { get; } = new([], 0);
    public int UndoCount { get; }
    public int RedoCount => _changes.Length - UndoCount;
    public int RetainedCount => _changes.Length;

    /// <summary>
    /// 领域载荷估算，非 .NET 堆实测：每条 192 字节元数据，每个变更槽 32 字节，
    /// 节点每端 48 字节，边每端 80 字节加每个几何点 16 字节。
    /// 跨条目共享实体重复计数；不包含活动快照、派生索引或绘图资源，不作为准入预算。
    /// </summary>
    public long EstimatedBytes { get; }

    internal RoadChangeSet? UndoChange => UndoCount == 0 ? null : _changes[UndoCount - 1];
    internal RoadChangeSet? RedoChange => RedoCount == 0 ? null : _changes[UndoCount];
    internal RoadEditHistory Move(bool undo) => new(_changes, UndoCount + (undo ? -1 : 1));

    internal RoadEditHistory Append(RoadChangeSet change)
    {
        int first = Math.Max(0, UndoCount + 1 - Capacity);
        RoadChangeSet[] retained = _changes.Skip(first).Take(UndoCount - first).Append(change).ToArray();
        return new(retained, retained.Length);
    }

    private static long Estimate(RoadChangeSet change) => 192L +
        change.Nodes.Sum(node => 32L + (node.Before is null ? 0 : 48) + (node.After is null ? 0 : 48)) +
        change.Edges.Sum(edge => 32L + EdgeBytes(edge.Before) + EdgeBytes(edge.After));

    private static long EdgeBytes(RoadEdge? edge) => edge is null ? 0 : 80L + 16L * edge.Points.Count;
}

/// <summary>预备阶段构造完整状态，提交阶段只交换这一引用。</summary>
internal sealed record RoadPublishedState(RoadSnapshot Snapshot, RoadEditHistory History, long RevisionWatermark);
