using Godot;
using SimpleCities.RoadCore;

public partial class V4MapView
{
    private SpatialQueryStatus _queryStatus = SpatialQueryStatus.Ready;
    private SpatialQueryMetrics _queryMetrics;
    private string _querySource = "";
    private string _queryReason = "";

    private SpatialQueryResult<T> RecordQuery<T>(SpatialQueryResult<T> result)
    {
        _queryStatus = result.Status;
        _queryMetrics = result.Metrics;
        _querySource = result.Source.ToString();
        _queryReason = result.Reason;
        return result;
    }

    private static SpatialQueryResult<T> Unavailable<T>() =>
        new(default, SpatialQueryStatus.InvalidParameters, [], default, "道路显示尚未就绪");

    private SpatialQueryResult<V4RoadDisplay.HitResult> QueryHit(Vector2 world, SpatialQueryBudget? budget) =>
        RecordQuery(_display?.QueryHit(world, budget) ?? Unavailable<V4RoadDisplay.HitResult>());

    internal SpatialQueryResult<RoadGridSpan> QuerySpan(Vector2 world) =>
        RecordQuery(_display?.QuerySpan(world) ?? Unavailable<RoadGridSpan>());

    internal SpatialQueryResult<RoadGridSpan> QuerySpans(Vector2 from, Vector2 to, SpatialQueryBudget? budget = null) =>
        RecordQuery(_display?.QueryTraceSpans(from, to, budget) ?? Unavailable<RoadGridSpan>());

    internal Godot.Collections.Dictionary DescribeQuery() => new()
    {
        ["status"] = _queryStatus.ToString(), ["sourceToken"] = _querySource, ["reason"] = _queryReason,
        ["bucketsVisited"] = _queryMetrics.BucketsVisited,
        ["fragmentCandidates"] = _queryMetrics.FragmentCandidates,
        ["exactGeometryTests"] = _queryMetrics.ExactGeometryTests,
        ["fullEdgeVisits"] = _queryMetrics.FullEdgeVisits, ["hits"] = _queryMetrics.Hits,
    };

    internal Godot.Collections.Dictionary QueryRoad(Vector2 world, SpatialQueryBudget budget)
    {
        var result = QueryHit(world, budget);
        var description = DescribeQuery();
        description["hit"] = result.Results.Count == 0 ? new Godot.Collections.Dictionary() : DescribeHit(result.Results[0]);
        return description;
    }

    internal Godot.Collections.Dictionary TraceRoadSpans(Vector2 from, Vector2 to, SpatialQueryBudget budget)
    {
        var result = QuerySpans(from, to, budget);
        var description = DescribeQuery();
        description["spanCount"] = result.Results.Count;
        return description;
    }
}
