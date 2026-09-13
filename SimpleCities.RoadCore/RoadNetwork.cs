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
        QueryData = new RoadSnapshotQueryData(this);
    }

    public MapDefinition Map { get; }
    public RoadStateToken Token { get; }
    public long NextNodeId { get; }
    public long NextEdgeId { get; }
    public IReadOnlyList<RoadNode> Nodes { get; }
    public IReadOnlyList<RoadEdge> Edges { get; }
    public int NodeCount => Nodes.Count;
    public int EdgeCount => Edges.Count;
    internal RoadSnapshotQueryData QueryData { get; }

    public RoadEdge? FindEdge(EdgeId id) => QueryData.FindEdge(id);

    public RoadPoint? Resolve(RoadLocation location)
    {
        if (location.Source != Token || !double.IsFinite(location.Parameter) || location.Parameter < 0 || location.Parameter > 1)
            return null;
        return QueryData.Resolve(location);
    }
}

/// <summary>核心公开操作入口；发布由单一调用线程负责，快照可跨线程读取。</summary>
public sealed class RoadNetwork
{
    private volatile RoadPublishedState _published;

    public RoadNetwork(MapDefinition? map = null)
    {
        var snapshot = new RoadSnapshot(map ?? new MapDefinition(),
            new RoadStateToken(Guid.NewGuid(), Guid.NewGuid(), 1, 0));
        _published = new(snapshot, RoadEditHistory.Empty, snapshot.Token.ContentRevision);
    }

    public RoadSnapshot Snapshot => _published.Snapshot;
    public RoadEditHistory History => _published.History;
    internal RoadPublishedState Published => _published;

    internal long NextContentRevision(RoadSnapshot source)
    {
        RoadPublishedState published = _published;
        long watermark = ReferenceEquals(source, published.Snapshot)
            ? published.RevisionWatermark : source.Token.ContentRevision;
        if (watermark >= long.MaxValue - 1)
            throw new InvalidDataException("道路身份或版本已耗尽");
        return watermark + 1;
    }

    public RoadEditResult PlanUndo(CancellationToken cancellationToken = default) => PlanHistory(true, cancellationToken);
    public RoadEditResult PlanRedo(CancellationToken cancellationToken = default) => PlanHistory(false, cancellationToken);

    private RoadEditResult PlanHistory(bool undo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RoadPublishedState published = _published;
        RoadSnapshot before = published.Snapshot;
        RoadChangeSet? change = undo ? published.History.UndoChange : published.History.RedoChange;
        if (change is null) return new(RoadEditStatus.NoChange, null, "");
        RoadChangeMetadata expected = undo ? change.After : change.Before;
        if (expected.Token.NetworkInstance != before.Token.NetworkInstance || expected.Token.Lineage != before.Token.Lineage ||
            expected.Token.ContentRevision != before.Token.ContentRevision)
            return new(RoadEditStatus.Rejected, null, "道路历史来源已过期");
        if (before.Token.ChangeSequence == long.MaxValue)
            return new(RoadEditStatus.Rejected, null, "道路身份或版本已耗尽");

        var nodes = before.Nodes.ToDictionary(node => node.Id);
        foreach (RoadNodeChange node in change.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            nodes.Remove(node.Id);
            RoadNode? restored = undo ? node.Before : node.After;
            if (restored is not null) nodes.Add(node.Id, restored);
        }
        var edges = before.Edges.ToDictionary(edge => edge.Id);
        foreach (RoadEdgeChange edge in change.Edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            edges.Remove(edge.Id);
            RoadEdge? restored = undo ? edge.Before : edge.After;
            if (restored is not null) edges.Add(edge.Id, restored);
        }
        RoadChangeMetadata desired = undo ? change.Before : change.After;
        RoadStateToken token = before.Token with
        {
            ContentRevision = desired.Token.ContentRevision,
            ChangeSequence = before.Token.ChangeSequence + 1,
        };
        var target = new RoadSnapshot(desired.Map, token,
            Math.Max(before.NextNodeId, desired.NextNodeId), Math.Max(before.NextEdgeId, desired.NextEdgeId),
            nodes.Values.OrderBy(node => node.Id.Value), edges.Values.OrderBy(edge => edge.Id.Value));
        cancellationToken.ThrowIfCancellationRequested();
        var plan = new RoadPlan(this, published, target, published.History.Move(undo), published.RevisionWatermark);
        cancellationToken.ThrowIfCancellationRequested();
        return new(RoadEditStatus.Ready, plan, "");
    }

    public RoadEditResult PlanRemove(RoadGridSpan span, CancellationToken cancellationToken = default) =>
        PlanRemove(new[] { span }, cancellationToken);

    public RoadEditResult PlanRemove(IReadOnlyList<RoadGridSpan> spans, CancellationToken cancellationToken = default) =>
        RoadSpanEditPlanner.Plan(this, Snapshot, spans, null, cancellationToken);

    public RoadEditResult PlanChangeProfile(RoadGridSpan span, RoadProfileId profile, CancellationToken cancellationToken = default) =>
        PlanChangeProfile(new[] { span }, profile, cancellationToken);

    public RoadEditResult PlanChangeProfile(IReadOnlyList<RoadGridSpan> spans, RoadProfileId profile, CancellationToken cancellationToken = default) =>
        RoadSpanEditPlanner.Plan(this, Snapshot, spans, profile, cancellationToken);

    public RoadPlan PlanLoad(PreparedRoadState prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        RoadPublishedState published = _published;
        RoadSnapshot before = published.Snapshot;
        var token = new RoadStateToken(before.Token.NetworkInstance, Guid.NewGuid(),
            prepared.ContentRevision, checked(before.Token.ChangeSequence + 1));
        return new RoadPlan(this, published, new RoadSnapshot(prepared.Map, token,
            prepared.NextNodeId, prepared.NextEdgeId, prepared.Nodes, prepared.Edges),
            RoadEditHistory.Empty, prepared.ContentRevision);
    }

    public RoadBuildResult PlanBuild(RoadBuildRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RoadSnapshot before = Snapshot;
        if (request.Source != before.Token)
            return new(RoadBuildStatus.Rejected, null, "道路来源版本已过期");
        if (!request.Profile.IsValid || !before.Map.IsBuildPoint(request.Start) || !before.Map.IsBuildPoint(request.End))
            return new(RoadBuildStatus.Rejected, null, "请选择地图内的主格点或格心和有效道路类型");
        if (request.Start == request.End)
            return new(RoadBuildStatus.NoChange, null, "");
        if (!before.Map.IsBuildSegment(request.Start, request.End))
            return new(RoadBuildStatus.Rejected, null, "主格点道路仅支持米字网格八方向，格心仅允许对角方向");
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
        ReferenceEquals(plan.Owner, this) && ReferenceEquals(plan.Source, Snapshot) &&
        ReferenceEquals(plan.SourceState, _published);

    /// <summary>单写者在预检后发布；过期计划无副作用，成功时仅交换引用。</summary>
    public bool TryCommit(RoadPlan plan)
    {
        if (!CanCommit(plan))
            return false;
        _published = plan.TargetState;
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
        ChangeSet = new RoadChangeSet(source, target);
        SourceState = owner.Published;
        TargetState = new(target, SourceState.History.Append(ChangeSet),
            Math.Max(SourceState.RevisionWatermark, target.Token.ContentRevision));
    }

    internal RoadPlan(RoadNetwork owner, RoadPublishedState source, RoadSnapshot target,
        RoadEditHistory history, long revisionWatermark)
    {
        Owner = owner;
        Source = source.Snapshot;
        SourceState = source;
        Target = target;
        ChangeSet = new RoadChangeSet(Source, target);
        TargetState = new(target, history, revisionWatermark);
    }

    internal RoadNetwork Owner { get; }
    internal RoadSnapshot Source { get; }
    internal RoadPublishedState SourceState { get; }
    internal RoadPublishedState TargetState { get; }
    public RoadSnapshot Target { get; }
    public RoadChangeSet ChangeSet { get; }
    public RoadStateToken SourceToken => Source.Token;
    public int AddedNodeCount { get; }
    public int AddedEdgeCount { get; }
}
