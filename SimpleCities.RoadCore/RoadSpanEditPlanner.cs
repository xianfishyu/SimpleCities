using static SimpleCities.RoadCore.RoadMutationDraft;

namespace SimpleCities.RoadCore;

/// <summary>只在来源快照的私有草稿中编辑一个格段，提交前恢复完整规范拓扑。</summary>
internal static class RoadSpanEditPlanner
{
    internal static RoadEditResult Plan(RoadNetwork owner, RoadSnapshot source, RoadGridSpan span, RoadProfileId? profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(span);
        cancellationToken.ThrowIfCancellationRequested();
        if (span.Source != source.Token)
            return new(RoadEditStatus.Rejected, null, "道路来源版本已过期");
        RoadEdge? selected = source.Edges.FirstOrDefault(edge => edge.Id == span.Edge);
        if (selected is null)
            return new(RoadEditStatus.Rejected, null, "道路格段不存在");
        // 同源候选计划的目标 token 可以相同；区间必须同时与当前内容的规范格段一致。
        RoadSpanRange first = span.Ranges[0];
        RoadGridSpan? current = RoadSpanQuery.Pick(source,
            new(source.Token, selected.Id, (first.StartParameter + first.EndParameter) / 2));
        if (current is null || current.Key != span.Key || !current.Ranges.SequenceEqual(span.Ranges) ||
            !current.Points.SequenceEqual(span.Points))
            return new(RoadEditStatus.Rejected, null, "道路格段与当前内容不一致");
        if (profile.HasValue && !profile.Value.IsValid)
            return new(RoadEditStatus.Rejected, null, "请选择有效道路类型");
        if (profile == selected.Profile)
            return new(RoadEditStatus.NoChange, null, "");
        if (source.Token.ContentRevision >= long.MaxValue - 1 || source.Token.ChangeSequence == long.MaxValue)
            return new(RoadEditStatus.Rejected, null, "道路身份或版本已耗尽");
        try
        {
            return Prepare(owner, source, selected, span, profile, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return new(RoadEditStatus.Rejected, null, exception.Message);
        }
    }

    private static RoadEditResult Prepare(RoadNetwork owner, RoadSnapshot source, RoadEdge selected,
        RoadGridSpan span, RoadProfileId? profile, CancellationToken cancellationToken)
    {
        long nextNode = source.NextNodeId, nextEdge = source.NextEdgeId;
        var nodes = source.Nodes.ToDictionary(node => node.Position);
        var cuts = new HashSet<RoadPoint> { selected.Points[0], selected.Points[^1], span.Points[0], span.Points[^1] };
        foreach (RoadPoint point in new[] { span.Points[0], span.Points[^1] })
            if (!nodes.ContainsKey(point)) nodes.Add(point, new(new(Allocate(ref nextNode)), point));

        var split = new List<Piece>();
        Split(selected.Id, selected.Profile, selected.Points, cuts, nodes, split, cancellationToken);
        var pieces = source.Edges.Where(edge => edge.Id != selected.Id)
            .Select(edge => new Piece(edge.Id, edge.Start, edge.End, edge.Profile, edge.Points.ToList())).ToList();
        double distance = 0;
        foreach (Piece piece in split)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double length = piece.Points.Zip(piece.Points.Skip(1), (a, b) => a.DistanceTo(b)).Sum();
            double middle = (distance + length / 2) / selected.Length;
            distance += length;
            if (!span.Ranges.Any(range => middle > range.StartParameter && middle < range.EndParameter))
                pieces.Add(piece);
            else if (profile.HasValue)
                pieces.Add(piece with { Profile = profile.Value });
        }
        var used = pieces.SelectMany(piece => new[] { piece.Start, piece.End }).ToHashSet();
        foreach (RoadNode node in nodes.Values.Where(node => !used.Contains(node.Id)).ToArray())
            nodes.Remove(node.Position);
        Normalize(nodes, pieces, cancellationToken);

        return new(RoadEditStatus.Ready, Freeze(owner, source, nodes, pieces, nextNode, nextEdge, cancellationToken), "");
    }
}
