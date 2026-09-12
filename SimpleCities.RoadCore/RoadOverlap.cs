namespace SimpleCities.RoadCore;

/// <summary>只计算本次直线草稿与已发布道路的正长度共线覆盖。</summary>
internal static class RoadOverlap
{
    // 调用方已验证非零合法米字网格草稿；快照中的道路也已通过拓扑验证。
    // 按半格轴向间隔覆盖主格点和格心，8 km / 12.5 m 使差分数组最多含 641 项，不建立逐格道路实体。
    internal static IReadOnlyList<RoadConflictSpan> Find(RoadSnapshot snapshot, RoadPoint start, RoadPoint end,
        CancellationToken cancellationToken)
    {
        double dx = end.X - start.X, dy = end.Y - start.Y;
        bool useX = dx != 0;
        double origin = useX ? start.X : start.Y;
        double step = Math.Sign(useX ? dx : dy) * (snapshot.Map.CellSizeMetres / 2d);
        int count = (int)((useX ? dx : dy) / step);
        var changes = new int[count + 1];
        foreach (RoadEdge edge in snapshot.Edges)
        {
            for (int i = 1; i < edge.Points.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RoadPoint a = edge.Points[i - 1], b = edge.Points[i];
                // 当前有界整数或半整数米坐标的乘积可由 double 精确表达，不需扩大为容差带。
                if (dx * (b.Y - a.Y) - dy * (b.X - a.X) != 0 ||
                    dx * (a.Y - start.Y) - dy * (a.X - start.X) != 0)
                    continue;
                int aIndex = (int)(((useX ? a.X : a.Y) - origin) / step);
                int bIndex = (int)(((useX ? b.X : b.Y) - origin) / step);
                int lower = Math.Max(0, Math.Min(aIndex, bIndex));
                int upper = Math.Min(count, Math.Max(aIndex, bIndex));
                if (lower >= upper) continue;
                changes[lower]++;
                changes[upper]--;
            }
        }

        var conflicts = new List<RoadConflictSpan>();
        int coverage = 0, startIndex = 0;
        for (int i = 0; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int previous = coverage;
            coverage += changes[i];
            if (previous == 0 && coverage > 0) startIndex = i;
            if (previous > 0 && coverage == 0)
                conflicts.Add(new RoadConflictSpan((double)startIndex / count, (double)i / count));
        }
        return conflicts;
    }
}
