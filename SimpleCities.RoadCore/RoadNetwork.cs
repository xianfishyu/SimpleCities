namespace SimpleCities.RoadCore;

public readonly record struct RoadStateToken(
    Guid NetworkInstance, Guid Lineage, long ContentRevision, long ChangeSequence);

/// <summary>只读权威内容，规范道路边可跨多个直线格段和转弯。</summary>
public sealed class RoadSnapshot
{
    internal RoadSnapshot(MapDefinition map, RoadStateToken token, long nextNodeId = 1, long nextEdgeId = 1,
        IEnumerable<RoadNode>? nodes = null, IEnumerable<RoadEdge>? edges = null)
    {
        Map = map;
        Token = token;
        NextNodeId = nextNodeId;
        NextEdgeId = nextEdgeId;
        Nodes = Array.AsReadOnly(nodes?.ToArray() ?? []);
        Edges = Array.AsReadOnly(edges?.ToArray() ?? []);
    }

    public MapDefinition Map { get; }
    public RoadStateToken Token { get; }
    public long NextNodeId { get; }
    public long NextEdgeId { get; }
    public IReadOnlyList<RoadNode> Nodes { get; }
    public IReadOnlyList<RoadEdge> Edges { get; }
    public int NodeCount => Nodes.Count;
    public int EdgeCount => Edges.Count;

    public RoadPoint? Resolve(RoadLocation location)
    {
        if (location.Source != Token || !double.IsFinite(location.Parameter) || location.Parameter < 0 || location.Parameter > 1)
            return null;
        RoadEdge? edge = Edges.FirstOrDefault(edge => edge.Id == location.Edge);
        if (edge is null) return null;
        return edge.PointAt(location.Parameter);
    }
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

    public RoadPlan PlanLoad(PreparedRoadState prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        RoadSnapshot before = Snapshot;
        var token = new RoadStateToken(before.Token.NetworkInstance, Guid.NewGuid(),
            prepared.ContentRevision, checked(before.Token.ChangeSequence + 1));
        return new RoadPlan(this, before, new RoadSnapshot(prepared.Map, token,
            prepared.NextNodeId, prepared.NextEdgeId, prepared.Nodes, prepared.Edges));
    }

    public RoadBuildResult PlanBuild(RoadBuildRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RoadSnapshot before = Snapshot;
        if (request.Source != before.Token)
            return new(RoadBuildStatus.Rejected, null, "道路来源版本已过期");
        if (!request.Profile.IsValid || !before.Map.IsPrimaryPoint(request.Start) || !before.Map.IsPrimaryPoint(request.End))
            return new(RoadBuildStatus.Rejected, null, "请选择地图内的主格点和有效道路类型");
        if (request.Start == request.End)
            return new(RoadBuildStatus.NoChange, null, "");
        if (!before.Map.IsEightDirection(request.Start, request.End))
            return new(RoadBuildStatus.Rejected, null, "道路仅支持米字网格八方向");
        IReadOnlyList<RoadConflictSpan> conflicts = RoadOverlap.Find(before, request.Start, request.End, cancellationToken);
        if (conflicts.Count != 0)
            return new(RoadBuildStatus.Rejected, null, "与已有道路重叠，整笔不可建造", conflicts);
        if (before.Token.ContentRevision >= long.MaxValue - 1 || before.Token.ChangeSequence == long.MaxValue)
            return new(RoadBuildStatus.Rejected, null, "道路身份或版本已耗尽");
        try
        {
            return RoadBuildPlanner.Plan(this, before, request, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return new(RoadBuildStatus.Rejected, null, exception.Message);
        }
    }

    public bool CanCommit(RoadPlan plan) =>
        ReferenceEquals(plan.Owner, this) && ReferenceEquals(plan.Source, Snapshot);

    /// <summary>单写者在预检后发布；过期计划无副作用，成功时仅交换引用。</summary>
    public bool TryCommit(RoadPlan plan)
    {
        if (!CanCommit(plan))
            return false;
        Snapshot = plan.Target;
        return true;
    }
}

public sealed class RoadPlan
{
    internal RoadPlan(RoadNetwork owner, RoadSnapshot source, RoadSnapshot target, int addedNodeCount = 0, int addedEdgeCount = 0)
    {
        Owner = owner;
        Source = source;
        Target = target;
        AddedNodeCount = addedNodeCount;
        AddedEdgeCount = addedEdgeCount;
    }

    internal RoadNetwork Owner { get; }
    internal RoadSnapshot Source { get; }
    public RoadSnapshot Target { get; }
    public RoadStateToken SourceToken => Source.Token;
    public int AddedNodeCount { get; }
    public int AddedEdgeCount { get; }
}
