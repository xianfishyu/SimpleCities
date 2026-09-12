namespace SimpleCities.RoadCore;

public readonly record struct RoadStateToken(
    Guid NetworkInstance, Guid Lineage, long ContentRevision, long ChangeSequence);

/// <summary>只读权威内容。目前仅支持空路网，实体操作由后续切片加入。</summary>
public sealed class RoadSnapshot
{
    internal RoadSnapshot(MapDefinition map, RoadStateToken token, long nextNodeId = 1, long nextEdgeId = 1)
    {
        Map = map;
        Token = token;
        NextNodeId = nextNodeId;
        NextEdgeId = nextEdgeId;
    }

    public MapDefinition Map { get; }
    public RoadStateToken Token { get; }
    public long NextNodeId { get; }
    public long NextEdgeId { get; }
    public int NodeCount => 0;
    public int EdgeCount => 0;
}

/// <summary>核心公开操作入口；发布由单一调用线程负责，快照可跨线程读取。</summary>
public sealed class RoadNetwork
{
    public RoadNetwork(MapDefinition? map = null)
    {
        Snapshot = new RoadSnapshot(map ?? new MapDefinition(),
            new RoadStateToken(Guid.NewGuid(), Guid.NewGuid(), 1, 0));
    }

    public RoadSnapshot Snapshot { get; private set; }

    public RoadLoadPlan PlanLoad(PreparedRoadState prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        RoadSnapshot before = Snapshot;
        var token = new RoadStateToken(before.Token.NetworkInstance, Guid.NewGuid(),
            prepared.ContentRevision, checked(before.Token.ChangeSequence + 1));
        return new RoadLoadPlan(this, before, new RoadSnapshot(prepared.Map, token,
            prepared.NextNodeId, prepared.NextEdgeId));
    }

    public bool CanCommitLoad(RoadLoadPlan plan) =>
        ReferenceEquals(plan.Owner, this) && ReferenceEquals(plan.Source, Snapshot);

    /// <summary>单写者在预检后发布；过期计划无副作用，成功时仅交换引用。</summary>
    public bool TryCommitLoad(RoadLoadPlan plan)
    {
        if (!CanCommitLoad(plan))
            return false;
        Snapshot = plan.Target;
        return true;
    }
}

public sealed class RoadLoadPlan
{
    internal RoadLoadPlan(RoadNetwork owner, RoadSnapshot source, RoadSnapshot target)
    {
        Owner = owner;
        Source = source;
        Target = target;
    }

    internal RoadNetwork Owner { get; }
    internal RoadSnapshot Source { get; }
    public RoadSnapshot Target { get; }
}
