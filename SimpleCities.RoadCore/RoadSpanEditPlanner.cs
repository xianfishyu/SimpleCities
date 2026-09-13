using static SimpleCities.RoadCore.RoadMutationDraft;

namespace SimpleCities.RoadCore;

/// <summary>冻结整笔格段选择，在来源快照的私有草稿中完成编辑和规范化。</summary>
internal static class RoadSpanEditPlanner
{
    internal static RoadEditResult Plan(RoadNetwork owner, RoadSnapshot source, IReadOnlyList<RoadGridSpan> spans, RoadProfileId? profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spans);
        cancellationToken.ThrowIfCancellationRequested();
        if (profile.HasValue && !profile.Value.IsValid)
            return new(RoadEditStatus.Rejected, null, "请选择有效道路类型");
        var selection = new Dictionary<RoadSpanKey, RoadGridSpan>();
        foreach (RoadGridSpan span in spans.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (span is null || span.Source != source.Token)
                return new(RoadEditStatus.Rejected, null, "道路来源版本已过期");
            RoadEdge? selected = source.FindEdge(span.Edge);
            if (selected is null)
                return new(RoadEditStatus.Rejected, null, "道路格段不存在");
            // 先核对每个原始条目，再去重或跳过同类型；同 token 的候选内容也必须一致。
            RoadSpanRange first = span.Ranges[0];
            RoadGridSpan? current = RoadSpanQuery.Pick(source,
                new(source.Token, selected.Id, (first.StartParameter + first.EndParameter) / 2));
            if (current is null || current.Key != span.Key || !current.Ranges.SequenceEqual(span.Ranges) ||
                !current.Points.SequenceEqual(span.Points))
                return new(RoadEditStatus.Rejected, null, "道路格段与当前内容不一致");
            if (profile != selected.Profile) selection.TryAdd(span.Key, span);
        }
        if (selection.Count == 0)
            return new(RoadEditStatus.NoChange, null, "");
        if (source.Token.ContentRevision >= long.MaxValue - 1 || source.Token.ChangeSequence == long.MaxValue)
            return new(RoadEditStatus.Rejected, null, "道路身份或版本已耗尽");
        try
        {
            return Prepare(owner, source, selection.Values, profile, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return new(RoadEditStatus.Rejected, null, exception.Message);
        }
    }

    private static RoadEditResult Prepare(RoadNetwork owner, RoadSnapshot source, IEnumerable<RoadGridSpan> selection,
        RoadProfileId? profile, CancellationToken cancellationToken)
    {
        long nextNode = source.NextNodeId, nextEdge = source.NextEdgeId;
        var nodes = source.Nodes.ToDictionary(node => node.Position);
        var byEdge = selection.GroupBy(span => span.Edge).ToDictionary(group => group.Key, group => group.ToArray());
        var pieces = new List<Piece>();
        foreach (RoadEdge selected in source.Edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!byEdge.TryGetValue(selected.Id, out RoadGridSpan[]? spans))
            {
                pieces.Add(new(selected.Id, selected.Start, selected.End, selected.Profile, selected.Points.ToList()));
                continue;
            }
            var cuts = new HashSet<RoadPoint>();
            foreach (RoadGridSpan span in spans)
                foreach (RoadPoint point in new[] { span.Points[0], span.Points[^1] })
                    // 相邻已选格段的共同边界不需要分配节点；端点用查询给出的精确格坐标。
                    if (!cuts.Add(point)) cuts.Remove(point);
            cuts.Add(selected.Points[0]);
            cuts.Add(selected.Points[^1]);
            foreach (RoadPoint point in cuts.OrderBy(point => point.X).ThenBy(point => point.Y))
                if (!nodes.ContainsKey(point)) nodes.Add(point, new(new(Allocate(ref nextNode)), point));

            var split = new List<Piece>();
            Split(selected.Id, selected.Profile, selected.Points, cuts, nodes, split, cancellationToken);
            RoadSpanRange[] ranges = spans.SelectMany(span => span.Ranges).ToArray();
            double distance = 0, total = selected.Length;
            foreach (Piece piece in split)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double length = piece.Points.Zip(piece.Points.Skip(1), (a, b) => a.DistanceTo(b)).Sum();
                double middle = (distance + length / 2) / total;
                distance += length;
                if (!ranges.Any(range => middle >= range.StartParameter && middle <= range.EndParameter))
                    pieces.Add(piece);
                else if (profile.HasValue)
                    pieces.Add(piece with { Profile = profile.Value });
            }
        }
        var used = pieces.SelectMany(piece => new[] { piece.Start, piece.End }).ToHashSet();
        foreach (RoadNode node in nodes.Values.Where(node => !used.Contains(node.Id)).ToArray())
            nodes.Remove(node.Position);
        Normalize(nodes, pieces, cancellationToken);

        return new(RoadEditStatus.Ready, Freeze(owner, source, nodes, pieces, nextNode, nextEdge, cancellationToken), "");
    }
}
