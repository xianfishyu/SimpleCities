namespace SimpleCities.RoadCore;

/// <summary>一次变更两端的领域身份、地图和分配水位，不保留快照或派生缓存。</summary>
public sealed record RoadChangeMetadata(MapDefinition Map, RoadStateToken Token, long NextNodeId, long NextEdgeId);
public sealed record RoadNodeChange(NodeId Id, RoadNode? Before, RoadNode? After);
public sealed record RoadEdgeChange(EdgeId Id, RoadEdge? Before, RoadEdge? After);

/// <summary>一笔完整领域变更；仅保存有变化的实体，空端表示新增或删除，可供历史消费者重建内容。</summary>
public sealed class RoadChangeSet
{
    internal RoadChangeSet(RoadSnapshot before, RoadSnapshot after)
    {
        Before = new(before.Map, before.Token, before.NextNodeId, before.NextEdgeId);
        After = new(after.Map, after.Token, after.NextNodeId, after.NextEdgeId);
        var beforeNodes = before.Nodes.ToDictionary(node => node.Id);
        var afterNodes = after.Nodes.ToDictionary(node => node.Id);
        Nodes = Array.AsReadOnly(beforeNodes.Keys.Union(afterNodes.Keys).OrderBy(id => id.Value)
            .Select(id => new RoadNodeChange(id, beforeNodes.GetValueOrDefault(id), afterNodes.GetValueOrDefault(id)))
            .Where(change => change.Before != change.After).ToArray());
        var beforeEdges = before.Edges.ToDictionary(edge => edge.Id);
        var afterEdges = after.Edges.ToDictionary(edge => edge.Id);
        Edges = Array.AsReadOnly(beforeEdges.Keys.Union(afterEdges.Keys).OrderBy(id => id.Value)
            .Select(id => new RoadEdgeChange(id, beforeEdges.GetValueOrDefault(id), afterEdges.GetValueOrDefault(id)))
            .Where(change => !SameEdge(change.Before, change.After)).ToArray());
    }

    public RoadChangeMetadata Before { get; }
    public RoadChangeMetadata After { get; }
    public IReadOnlyList<RoadNodeChange> Nodes { get; }
    public IReadOnlyList<RoadEdgeChange> Edges { get; }

    private static bool SameEdge(RoadEdge? before, RoadEdge? after) =>
        ReferenceEquals(before, after) || (before is not null && after is not null && before.Id == after.Id &&
        before.Start == after.Start && before.End == after.End && before.Profile == after.Profile &&
        before.Points.SequenceEqual(after.Points));
}
