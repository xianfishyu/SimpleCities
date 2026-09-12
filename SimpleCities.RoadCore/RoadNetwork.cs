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
        RoadNode? startNode = before.Nodes.FirstOrDefault(node => node.Position == request.Start);
        RoadNode? endNode = before.Nodes.FirstOrDefault(node => node.Position == request.End);
        RoadNode? connector = startNode ?? endNode;
        if (before.EdgeCount != 0 && (connector is null || (startNode is not null && endNode is not null)))
            return new(RoadBuildStatus.Rejected, null, "请从已有开放端点续建；闭环和独立分支尚未接入");
        RoadEdge? previous = null;
        if (connector is not null)
        {
            RoadEdge[] incident = before.Edges.Where(edge => edge.Start == connector.Id || edge.End == connector.Id).ToArray();
            if (incident.Length != 1)
                return new(RoadBuildStatus.Rejected, null, "当前仅支持从开放端点续建");
            previous = incident[0];
        }
        bool merge = previous?.Profile == request.Profile;
        int addedNodes = connector is null ? 2 : 1;
        int addedEdges = merge ? 0 : 1;
        if (before.NextNodeId >= long.MaxValue - addedNodes ||
            (addedEdges != 0 && before.NextEdgeId >= long.MaxValue - addedEdges) ||
            before.Token.ContentRevision >= long.MaxValue - 1 || before.Token.ChangeSequence == long.MaxValue)
            return new(RoadBuildStatus.Rejected, null, "道路身份或版本已耗尽");
        var nodes = before.Nodes.ToList();
        var edges = before.Edges.ToList();
        if (connector is null)
        {
            var start = new RoadNode(new NodeId(before.NextNodeId), request.Start);
            var end = new RoadNode(new NodeId(before.NextNodeId + 1), request.End);
            nodes.AddRange([start, end]);
            edges.Add(new RoadEdge(new EdgeId(before.NextEdgeId), start.Id, end.Id, request.Profile, [request.Start, request.End]));
        }
        else
        {
            var free = new RoadNode(new NodeId(before.NextNodeId), startNode is null ? request.Start : request.End);
            nodes.Add(free);
            if (merge)
            {
                RoadEdge old = previous!;
                var points = old.End == connector.Id ? old.Points.ToList() : old.Points.Reverse().ToList();
                points.Add(free.Position);
                int n = points.Count;
                if (RoadTopology.IsForwardCollinear(points[n - 3], points[n - 2], points[n - 1]))
                    points.RemoveAt(n - 2);
                NodeId kept = old.End == connector.Id ? old.Start : old.End;
                edges.Remove(old);
                nodes.Remove(connector);
                edges.Add(Orient(old.Id, kept, free.Id, old.Profile, points));
            }
            else
                edges.Add(Orient(new EdgeId(before.NextEdgeId), connector.Id, free.Id, request.Profile, [connector.Position, free.Position]));
        }
        nodes.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        edges.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        long nextNode = before.NextNodeId + addedNodes;
        long nextEdge = before.NextEdgeId + addedEdges;
        try { RoadTopology.Validate(before.Map, nodes, edges, nextNode, nextEdge, cancellationToken); }
        catch (InvalidDataException exception) { return new(RoadBuildStatus.Rejected, null, exception.Message); }
        RoadStateToken token = before.Token with
        {
            ContentRevision = before.Token.ContentRevision + 1,
            ChangeSequence = before.Token.ChangeSequence + 1,
        };
        var target = new RoadSnapshot(before.Map, token, nextNode, nextEdge, nodes, edges);
        cancellationToken.ThrowIfCancellationRequested();
        return new(RoadBuildStatus.Ready, new RoadPlan(this, before, target, addedNodes, addedEdges), "");
    }

    private static RoadEdge Orient(EdgeId id, NodeId a, NodeId b, RoadProfileId profile, IEnumerable<RoadPoint> points) =>
        a.Value < b.Value ? new(id, a, b, profile, points) : new(id, b, a, profile, points.Reverse());

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
