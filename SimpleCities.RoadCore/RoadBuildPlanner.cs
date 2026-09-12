using static SimpleCities.RoadCore.RoadMutationDraft;

namespace SimpleCities.RoadCore;

/// <summary>在私有内容中切分点交叉并恢复规范边，完成验证后才产生可发布计划。</summary>
internal static class RoadBuildPlanner
{

    internal static RoadBuildResult Plan(RoadNetwork owner, RoadSnapshot source, RoadBuildRequest request,
        CancellationToken cancellationToken)
    {
        var draftCuts = new HashSet<RoadPoint> { request.Start, request.End };
        var edgeCuts = new Dictionary<EdgeId, HashSet<RoadPoint>>();
        foreach (RoadEdge edge in source.Edges)
        {
            var cuts = new HashSet<RoadPoint> { edge.Points[0], edge.Points[^1] };
            for (int i = 1; i < edge.Points.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RoadPoint? intersection = Intersect(request.Start, request.End, edge.Points[i - 1], edge.Points[i]);
                if (!intersection.HasValue) continue;
                if (!source.Map.IsBuildPoint(intersection.Value))
                    throw new InvalidDataException("道路交叉位置必须是地图内的主格点或格心");
                cuts.Add(intersection.Value);
                draftCuts.Add(intersection.Value);
            }
            edgeCuts.Add(edge.Id, cuts);
        }

        long nextNode = source.NextNodeId, nextEdge = source.NextEdgeId;
        var nodes = source.Nodes.ToDictionary(node => node.Position);
        // 端点优先，再沿草稿顺序分配交点身份；不依赖集合的枚举顺序。
        IEnumerable<RoadPoint> allocationOrder = new[] { request.Start, request.End }.Concat(draftCuts
            .Where(point => point != request.Start && point != request.End)
            .OrderBy(point => Parameter(point, request.Start, request.End)));
        foreach (RoadPoint position in allocationOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!nodes.ContainsKey(position))
                nodes.Add(position, new RoadNode(new NodeId(Allocate(ref nextNode)), position));
        }
        var pieces = new List<Piece>();
        foreach (RoadEdge edge in source.Edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Split(edge.Id, edge.Profile, edge.Points, edgeCuts[edge.Id], nodes, pieces, cancellationToken);
        }
        Split(null, request.Profile, [request.Start, request.End], draftCuts, nodes, pieces, cancellationToken);
        Normalize(nodes, pieces, cancellationToken);

        return new(RoadBuildStatus.Ready, Freeze(owner, source, nodes, pieces, nextNode, nextEdge, cancellationToken), "");
    }

    private static RoadPoint? Intersect(RoadPoint a, RoadPoint b, RoadPoint c, RoadPoint d)
    {
        double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y;
        double denominator = Cross(rx, ry, sx, sy);
        if (denominator == 0)
        {
            // 调用入口已优先拒绝正长度共线重叠，此处仅余端点触碰。
            foreach (RoadPoint point in new[] { a, b })
                if (OnSegment(point, c, d)) return point;
            return null;
        }
        double numerator = Cross(c.X - a.X, c.Y - a.Y, sx, sy);
        double other = Cross(c.X - a.X, c.Y - a.Y, rx, ry);
        if (denominator > 0
            ? numerator < 0 || numerator > denominator || other < 0 || other > denominator
            : numerator > 0 || numerator < denominator || other > 0 || other < denominator)
            return null;
        // 四档格长在 ±4000 m 内仅产生整数或半整数米坐标，行列式及乘加均可精确表示。
        // 先相乘相加后相除，避免 t 插值引入格点误差。
        return new RoadPoint((a.X * denominator + rx * numerator) / denominator,
            (a.Y * denominator + ry * numerator) / denominator);
    }
}
