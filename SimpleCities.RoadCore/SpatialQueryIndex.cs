namespace SimpleCities.RoadCore;

public enum SpatialQueryStatus { Ready, BudgetExceeded, InvalidParameters }
/// <summary>一次公开查询的累计工作上限；零或负数为非法参数。</summary>
public readonly record struct SpatialQueryBudget(int MaxBuckets = 4096, int MaxCandidates = 4096, int MaxExactTests = 16384)
{
    public static SpatialQueryBudget Default => new(4096, 4096, 16384);
    public bool IsValid => MaxBuckets > 0 && MaxCandidates > 0 && MaxExactTests > 0;
}
/// <summary>桶访问、消费的粗筛片段、精确距离/相交/表面检查、整边遍历和最终不同 Edge 数。
/// 复合拖选累计各次实际查询工作；候选和命中数分别统计。</summary>
public readonly record struct SpatialQueryMetrics(int BucketsVisited, int FragmentCandidates,
    int ExactGeometryTests, int FullEdgeVisits, int Hits);

/// <summary>非 Ready 时 Results 为空，不能解释为一次成功的空查询。</summary>
public sealed record SpatialQueryResult<T>(RoadStateToken Source, SpatialQueryStatus Status,
    IReadOnlyList<T> Results, SpatialQueryMetrics Metrics, string Reason);

public readonly record struct SpatialBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    public bool IsValid => double.IsFinite(MinX) && double.IsFinite(MinY) && double.IsFinite(MaxX) &&
        double.IsFinite(MaxY) && MinX <= MaxX && MinY <= MaxY;
    internal bool Intersects(SpatialBounds other) => MinX <= other.MaxX && MaxX >= other.MinX &&
        MinY <= other.MaxY && MaxY >= other.MinY;
    public static SpatialBounds Between(RoadPoint start, RoadPoint end) => new(
        Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));
}
public readonly record struct SpatialFragment<T>(T Value, SpatialBounds Bounds);

/// <summary>不可变、有界片段的粗筛索引。直线路径逐格遍历，不扫描路径包围盒或全表。</summary>
public sealed class SpatialQueryIndex<T>
{
    private readonly Dictionary<(int X, int Y), int[]> _buckets;
    private readonly SpatialFragment<T>[] _fragments;
    private readonly double _bucketSize;
    private readonly RoadStateToken _source;

    public SpatialQueryIndex(RoadStateToken source, IEnumerable<SpatialFragment<T>> fragments, double bucketSize = 100)
    {
        ArgumentNullException.ThrowIfNull(fragments);
        if (!double.IsFinite(bucketSize) || bucketSize <= 0) throw new ArgumentOutOfRangeException(nameof(bucketSize));
        _source = source;
        _bucketSize = bucketSize;
        _fragments = fragments.ToArray();
        var buckets = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < _fragments.Length; i++)
        {
            SpatialBounds bounds = _fragments[i].Bounds;
            if (!ValidBounds(bounds)) throw new ArgumentException("Invalid fragment bounds.", nameof(fragments));
            int left = Bucket(bounds.MinX), right = Bucket(bounds.MaxX), top = Bucket(bounds.MinY), bottom = Bucket(bounds.MaxY);
            // Every caller must fragment long geometry before indexing; never expand an unbounded rectangle.
            if (((long)right - left + 1) * ((long)bottom - top + 1) > 64)
                throw new ArgumentException("Spatial fragment exceeds its bounded bucket coverage.", nameof(fragments));
            for (int x = left; x <= right; x++)
                for (int y = top; y <= bottom; y++)
                {
                    if (!buckets.TryGetValue((x, y), out List<int>? entries)) buckets.Add((x, y), entries = []);
                    entries.Add(i);
                }
        }
        _buckets = buckets.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    public SpatialQueryResult<T> QueryBounds(SpatialBounds bounds, SpatialQueryBudget? budget = null)
    {
        var query = new Query(this, budget ?? SpatialQueryBudget.Default, bounds);
        if (!query.IsValid) return query.Result();
        for (int x = Bucket(bounds.MinX); x <= Bucket(bounds.MaxX); x++)
            for (int y = Bucket(bounds.MinY); y <= Bucket(bounds.MaxY); y++)
                if (!query.Visit(x, y)) return query.Result();
        return query.Result();
    }

    public SpatialQueryResult<T> QuerySegment(RoadPoint from, RoadPoint to, SpatialQueryBudget? budget = null)
    {
        var query = new Query(this, budget ?? SpatialQueryBudget.Default, SpatialBounds.Between(from, to));
        if (!from.IsFinite || !to.IsFinite || !query.IsValid) return query.Invalid();
        int x = Bucket(from.X), y = Bucket(from.Y), endX = Bucket(to.X), endY = Bucket(to.Y);
        int stepX = Math.Sign(to.X - from.X), stepY = Math.Sign(to.Y - from.Y);
        double dx = to.X - from.X, dy = to.Y - from.Y;
        double nextX = stepX == 0 ? double.PositiveInfinity : ((x + (stepX > 0 ? 1d : 0d)) * _bucketSize - from.X) / dx;
        double nextY = stepY == 0 ? double.PositiveInfinity : ((y + (stepY > 0 ? 1d : 0d)) * _bucketSize - from.Y) / dy;
        double deltaX = stepX == 0 ? double.PositiveInfinity : _bucketSize / Math.Abs(dx);
        double deltaY = stepY == 0 ? double.PositiveInfinity : _bucketSize / Math.Abs(dy);
        while (true)
        {
            if (!query.Visit(x, y) || (x == endX && y == endY)) return query.Result();
            // The terminal point belongs to floor(coordinate). A negative direction must not
            // cross its final grid boundary again when the other axis reaches the endpoint.
            if (x == endX) nextX = double.PositiveInfinity;
            if (y == endY) nextY = double.PositiveInfinity;
            if (nextX < nextY) { x += stepX; nextX += deltaX; }
            else if (nextY < nextX) { y += stepY; nextY += deltaY; }
            else
            {
                // A corner touch includes both side buckets, so surface endpoint ownership is retained.
                if (!query.Visit(x + stepX, y) || !query.Visit(x, y + stepY)) return query.Result();
                x += stepX; y += stepY; nextX += deltaX; nextY += deltaY;
            }
        }
    }

    private int Bucket(double coordinate) => (int)Math.Floor(coordinate / _bucketSize);
    private bool ValidBounds(SpatialBounds bounds) => bounds.IsValid &&
        Math.Abs(bounds.MinX / _bucketSize) < int.MaxValue / 2d && Math.Abs(bounds.MaxX / _bucketSize) < int.MaxValue / 2d &&
        Math.Abs(bounds.MinY / _bucketSize) < int.MaxValue / 2d && Math.Abs(bounds.MaxY / _bucketSize) < int.MaxValue / 2d;

    private sealed class Query(SpatialQueryIndex<T> owner, SpatialQueryBudget budget, SpatialBounds bounds)
    {
        private readonly HashSet<(int, int)> _visited = [];
        private readonly HashSet<int> _seen = [];
        private readonly List<T> _results = [];
        private SpatialQueryStatus _status = SpatialQueryStatus.Ready;
        internal bool IsValid => budget.IsValid && owner.ValidBounds(bounds);
        internal bool Visit(int x, int y)
        {
            if (_visited.Contains((x, y))) return true;
            if (_visited.Count >= budget.MaxBuckets) { _status = SpatialQueryStatus.BudgetExceeded; return false; }
            _visited.Add((x, y));
            if (!owner._buckets.TryGetValue((x, y), out int[]? entries)) return true;
            foreach (int index in entries)
            {
                if (_seen.Contains(index)) continue;
                if (_seen.Count >= budget.MaxCandidates) { _status = SpatialQueryStatus.BudgetExceeded; return false; }
                _seen.Add(index);
                SpatialFragment<T> fragment = owner._fragments[index];
                if (fragment.Bounds.Intersects(bounds)) _results.Add(fragment.Value);
            }
            return true;
        }
        internal SpatialQueryResult<T> Invalid() => new(owner._source, SpatialQueryStatus.InvalidParameters, [], default, "空间查询参数非法");
        internal SpatialQueryResult<T> Result()
        {
            if (!IsValid) return Invalid();
            return new(owner._source, _status, _status == SpatialQueryStatus.Ready ? _results.AsReadOnly() : [],
                new(_visited.Count, _seen.Count, 0, 0, 0), _status == SpatialQueryStatus.Ready ? "" : "空间查询候选或分桶预算已耗尽");
        }
    }
}
