using Godot;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

const double FrameBudgetMilliseconds = 16.67;
bool enforceBudget = args.Contains("--enforce-budget", StringComparer.Ordinal);
int[] sizes = [1_000, 10_000, 100_000];
var allResults = new List<BenchmarkResult>();

foreach (int edgeCount in sizes)
{
    Dataset dataset = Dataset.Create(edgeCount);
    string payload = BuildPayload(dataset);
    int iterations = edgeCount switch
    {
        <= 1_000 => 8,
        <= 10_000 => 5,
        _ => 1,
    };
    int warmups = edgeCount < 100_000 ? 1 : 0;

    foreach (Scenario scenario in CreateScenarios(dataset))
    {
        for (int index = 0; index < warmups; index++)
            _ = MeasureOnce(payload, scenario);

        var samples = new List<BenchmarkSample>(iterations);
        for (int index = 0; index < iterations; index++)
            samples.Add(MeasureOnce(payload, scenario));
        allResults.Add(BenchmarkResult.Create(edgeCount, scenario.Name, samples));
    }
}

Console.WriteLine("# RoadGraph V3 性能基线");
Console.WriteLine();
Console.WriteLine($"> 运行时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
Console.WriteLine($"> 环境：{RuntimeInformation.OSDescription}; {RuntimeInformation.FrameworkDescription}; {System.Environment.ProcessorCount} logical processors");
Console.WriteLine($"> 命令：`dotnet run --project tests/SimpleCities.RoadGraph.Performance/SimpleCities.RoadGraph.Performance.csproj --configuration Release --no-restore`");
Console.WriteLine("> 口径：固定 32 单位间距、8 单位直线 Edge 数据集；1k/10k 各含一次预热，分别采样 8/5 次；100k 不预热并采样 1 次。图恢复和 GC 不计入操作耗时。候选数来自空间索引，扫描数只统计覆盖、锚点和原生交点的全表几何枚举。");
Console.WriteLine();
Console.WriteLine("| Edge | 场景 | 平均 ms | P95 ms | 平均分配 KiB | 平均候选 Edge | 平均全表扫描 | 平均访问 Edge | 10k 门槛 |");
Console.WriteLine("|---:|---|---:|---:|---:|---:|---:|---:|---|");
foreach (BenchmarkResult result in allResults)
{
    string budget = result.EdgeCount == 10_000
        ? result.P95Milliseconds <= FrameBudgetMilliseconds ? "通过" : "未通过"
        : "不适用";
    Console.WriteLine(FormattableString.Invariant(
        $"| {result.EdgeCount} | {result.Scenario} | {result.MeanMilliseconds:F3} | {result.P95Milliseconds:F3} | {result.MeanAllocatedBytes / 1024d:F1} | {result.MeanCandidateEdges:F1} | {result.MeanFullScans:F1} | {result.MeanFullEdgeVisits:F1} | {budget} |"));
}

Console.WriteLine();
Console.WriteLine("## 最大连续 Edge 局部查询");
Console.WriteLine();
Console.WriteLine("> 口径：图构建不计时；单条 Edge 分别扩大 line 长度或远端 geometry 数，在首/中/尾固定局部窗口各预热 20 次、采样 200 次。时间仅记录，不作为跨机器硬门槛；结构门槛要求聚合 1 Edge、fragment/exact test 有界且不发生全 Edge 扫描。");
Console.WriteLine();
Console.WriteLine("| 数据族 | 规模 | 窗口 | 平均 ms | P95 ms | 总 bucket | 总 fragment | 候选 fragment | exact test | 聚合 Edge | 全表扫描 | 访问 Edge |");
Console.WriteLine("|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
foreach (LocalityBenchmarkResult result in RunLocalityBenchmarks())
{
    Console.WriteLine(FormattableString.Invariant(
        $"| {result.Family} | {result.Scale} | {result.Window} | {result.MeanMilliseconds:F4} | {result.P95Milliseconds:F4} | {result.Buckets} | {result.QueryFragments} | {result.FragmentCandidates} | {result.ExactTests} | {result.AggregatedEdges} | {result.FullScans} | {result.FullEdgeVisits} |"));
}

BenchmarkResult[] failed10k = allResults
    .Where(result => result.EdgeCount == 10_000 && result.P95Milliseconds > FrameBudgetMilliseconds)
    .ToArray();
Console.WriteLine();
Console.WriteLine(failed10k.Length == 0
    ? $"10k 硬门槛：全部场景 P95 不超过 {FrameBudgetMilliseconds:F2} ms。"
    : $"10k 硬门槛：{failed10k.Length} 个场景超过 {FrameBudgetMilliseconds:F2} ms：{string.Join("、", failed10k.Select(result => result.Scenario))}。");
Console.WriteLine("100k 结果仅用于压力观察，不参与退出码判定。");

Console.WriteLine();
Console.WriteLine("## 不可变 root 局部 mutation");
Console.WriteLine();
Console.WriteLine("> 口径：互不相连的单 geometry Edge 数据集；图恢复和 GC 不计时，只改造中间一条 Edge。提交后再统计跨 revision 的引用共享，统计遍历不计入提交耗时；旧 root 释放在独立 no-inline 边界后强制 GC 观察。revision 与 diagnostics capture 分别对同一已发布实例调用 100,000 次。");
Console.WriteLine();
Console.WriteLine("| Geometry | 提交 ms | 分配 KiB | delta Node | delta Edge | 复制 Node | 复制 Edge | 复制 bucket 页 | 共享 Entity | 共享 bucket 页 | revision capture ms | revision bytes | diagnostics capture ms | diagnostics bytes | 旧 root 释放 |");
Console.WriteLine("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
ImmutableMutationBenchmarkResult[] immutableMutationResults =
    RunImmutableMutationBenchmarks(sizes).ToArray();
foreach (ImmutableMutationBenchmarkResult result in immutableMutationResults)
{
    Console.WriteLine(FormattableString.Invariant(
        $"| {result.GeometryCount} | {result.CommitMilliseconds:F3} | {result.AllocatedBytes / 1024d:F1} | {result.DeltaNodes} | {result.DeltaEdges} | {result.CopiedNodes} | {result.CopiedEdges} | {result.CopiedBucketPages} | {result.SharedEntities} | {result.SharedBucketPages} | {result.CaptureMilliseconds:F3} | {result.CaptureAllocatedBytes} | {result.DiagnosticsCaptureMilliseconds:F3} | {result.DiagnosticsCaptureAllocatedBytes} | {(result.OldRootReleased ? "是" : "否")} |"));
}
ImmutableMutationBenchmarkResult[] diagnosticsAllocationFailures = immutableMutationResults
    .Where(result => result.DiagnosticsCaptureAllocatedBytes != 0)
    .ToArray();
Console.WriteLine(diagnosticsAllocationFailures.Length == 0
    ? "diagnostics capture 门禁：1k/10k/100k 均为 0 bytes。"
    : $"diagnostics capture 门禁：{diagnosticsAllocationFailures.Length} 个规模发生分配。");

HistoryBenchmarkResult historyResult = RunHistoryBenchmark();
Console.WriteLine();
Console.WriteLine("## 64 项 geometry-dense delta 历史");
Console.WriteLine();
Console.WriteLine("> 口径：单条 1,024 geometry Edge 连续交替改造 64 次；旧基线按每项 before/after 共 128 份当前完整 JSON 的 UTF-8 字节估算。编辑、全部 undo 和全部 redo 各只计自身耗时/线程分配，事件必须全为普通 delta。");
Console.WriteLine();
Console.WriteLine("| 编辑数 | delta retained KiB | 128 JSON KiB | 编辑分配 KiB | undo ms | undo KiB | redo ms | redo KiB | 事件 | full reset |");
Console.WriteLine("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
Console.WriteLine(FormattableString.Invariant(
    $"| {historyResult.EditCount} | {historyResult.DeltaRetainedBytes / 1024d:F1} | {historyResult.JsonBaselineBytes / 1024d:F1} | {historyResult.EditAllocatedBytes / 1024d:F1} | {historyResult.UndoMilliseconds:F3} | {historyResult.UndoAllocatedBytes / 1024d:F1} | {historyResult.RedoMilliseconds:F3} | {historyResult.RedoAllocatedBytes / 1024d:F1} | {historyResult.EventCount} | {historyResult.FullResetEventCount} |"));

return enforceBudget && (failed10k.Length > 0 || diagnosticsAllocationFailures.Length > 0) ? 2 : 0;

static HistoryBenchmarkResult RunHistoryBenchmark()
{
    const int editCount = RoadEditHistory.DefaultCapacity;
    RoadGraph graph = CreateZigZagGraph(1_024);
    int edgeID = AssertSingle(graph.GetAllEdges()).ID;
    string state = CaptureJson(graph);
    long jsonBaselineBytes = (long)Encoding.UTF8.GetByteCount(state) * editCount * 2L;
    using var history = new RoadEditHistory(graph);
    int eventCount = 0;
    int fullResetEventCount = 0;
    graph.GraphChanged += change =>
    {
        eventCount++;
        if (change.Changes.IsFullReset)
            fullResetEventCount++;
    };

    long editAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    for (int index = 0; index < editCount; index++)
    {
        RoadType target = index % 2 == 0 ? RoadType.Highway : RoadType.Street;
        if (!history.Execute(() => graph.ChangeRoadType([edgeID], target).Success))
            throw new InvalidOperationException($"History benchmark edit {index} was rejected.");
    }
    long editAllocated = GC.GetAllocatedBytesForCurrentThread() - editAllocatedBefore;
    if (history.UndoCount != editCount)
        throw new InvalidOperationException("History benchmark did not retain all 64 edits.");

    long undoAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    long undoStarted = Stopwatch.GetTimestamp();
    for (int index = 0; index < editCount; index++)
    {
        if (!history.Undo())
            throw new InvalidOperationException($"History benchmark undo {index} failed.");
    }
    double undoMilliseconds = Stopwatch.GetElapsedTime(undoStarted).TotalMilliseconds;
    long undoAllocated = GC.GetAllocatedBytesForCurrentThread() - undoAllocatedBefore;

    long redoAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    long redoStarted = Stopwatch.GetTimestamp();
    for (int index = 0; index < editCount; index++)
    {
        if (!history.Redo())
            throw new InvalidOperationException($"History benchmark redo {index} failed.");
    }
    double redoMilliseconds = Stopwatch.GetElapsedTime(redoStarted).TotalMilliseconds;
    long redoAllocated = GC.GetAllocatedBytesForCurrentThread() - redoAllocatedBefore;

    return new HistoryBenchmarkResult(
        editCount,
        history.RetainedByteSize,
        jsonBaselineBytes,
        editAllocated,
        undoMilliseconds,
        undoAllocated,
        redoMilliseconds,
        redoAllocated,
        eventCount,
        fullResetEventCount);
}

static T AssertSingle<T>(IEnumerable<T> values)
{
    using IEnumerator<T> enumerator = values.GetEnumerator();
    if (!enumerator.MoveNext())
        throw new InvalidOperationException("Expected exactly one value, but the sequence was empty.");
    T value = enumerator.Current;
    if (enumerator.MoveNext())
        throw new InvalidOperationException("Expected exactly one value, but the sequence had multiple values.");
    return value;
}

static IReadOnlyList<ImmutableMutationBenchmarkResult> RunImmutableMutationBenchmarks(
    IEnumerable<int> geometryCounts)
{
    var results = new List<ImmutableMutationBenchmarkResult>();
    foreach (int geometryCount in geometryCounts)
    {
        Dataset dataset = Dataset.Create(geometryCount);
        var graph = new RoadGraph();
        LoadJson(graph, BuildPayload(dataset));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        LocalMutationMeasurement measurement = MeasureLocalMutation(
            graph,
            dataset.MiddleEdgeID);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        bool oldRootReleased = !measurement.OldRoot.IsAlive;

        RoadGraphRevision captured = graph.CaptureRevision();
        long captureAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long captureStarted = Stopwatch.GetTimestamp();
        for (int index = 0; index < 100_000; index++)
        {
            if (!ReferenceEquals(captured, graph.CaptureRevision()))
                throw new InvalidOperationException("CaptureRevision changed without a graph commit.");
        }
        double captureMilliseconds = Stopwatch.GetElapsedTime(captureStarted).TotalMilliseconds;
        long captureAllocated = GC.GetAllocatedBytesForCurrentThread() - captureAllocatedBefore;

        RoadGraphDiagnosticsSnapshot diagnostics = graph.CaptureDiagnosticsSnapshot();
        long diagnosticsAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long diagnosticsStarted = Stopwatch.GetTimestamp();
        for (int index = 0; index < 100_000; index++)
        {
            if (!ReferenceEquals(diagnostics, graph.CaptureDiagnosticsSnapshot()))
                throw new InvalidOperationException(
                    "CaptureDiagnosticsSnapshot changed without a graph commit.");
        }
        double diagnosticsMilliseconds = Stopwatch.GetElapsedTime(diagnosticsStarted).TotalMilliseconds;
        long diagnosticsAllocated =
            GC.GetAllocatedBytesForCurrentThread() - diagnosticsAllocatedBefore;

        results.Add(new ImmutableMutationBenchmarkResult(
            geometryCount,
            measurement.CommitMilliseconds,
            measurement.AllocatedBytes,
            measurement.DeltaNodes,
            measurement.DeltaEdges,
            measurement.CopiedNodes,
            measurement.CopiedEdges,
            measurement.CopiedBucketPages,
            measurement.SharedEntities,
            measurement.SharedBucketPages,
            captureMilliseconds,
            captureAllocated,
            diagnosticsMilliseconds,
            diagnosticsAllocated,
            oldRootReleased));
    }
    return results;
}

[MethodImpl(MethodImplOptions.NoInlining)]
static LocalMutationMeasurement MeasureLocalMutation(RoadGraph graph, int edgeID)
{
    RoadGraphRevision before = graph.CaptureRevision();
    GC.KeepAlive(before);
    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    long started = Stopwatch.GetTimestamp();
    RoadTypeChangeResult changed = graph.ChangeRoadType([edgeID], RoadType.Highway);
    double milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    if (!changed.Success || changed.Delta is null)
        throw new InvalidOperationException($"Local RoadType mutation failed: {changed.Error}.");

    RoadGraphRevision after = graph.CaptureRevision();
    int sharedNodes = before.NodeMap.Count(pair =>
        after.NodeMap.TryGetValue(pair.Key, out GraphNode? current) &&
        ReferenceEquals(pair.Value, current));
    int sharedEdges = before.EdgeMap.Count(pair =>
        after.EdgeMap.TryGetValue(pair.Key, out GraphEdge? current) &&
        ReferenceEquals(pair.Value, current));
    int sharedBuckets = before.SpatialIndex.Buckets.Count(pair =>
        after.SpatialIndex.Buckets.TryGetValue(pair.Key, out var current) &&
        pair.Value == current);
    int copiedNodes = before.NodeMap.Count + after.NodeMap.Count - sharedNodes * 2;
    int copiedEdges = before.EdgeMap.Count + after.EdgeMap.Count - sharedEdges * 2;
    int copiedBuckets = before.SpatialIndex.Buckets.Count +
                        after.SpatialIndex.Buckets.Count -
                        sharedBuckets * 2;

    return new LocalMutationMeasurement(
        new WeakReference(before),
        milliseconds,
        allocated,
        changed.Delta.Nodes.Count,
        changed.Delta.Edges.Count,
        copiedNodes,
        copiedEdges,
        copiedBuckets,
        sharedNodes + sharedEdges,
        sharedBuckets);
}

static IReadOnlyList<Scenario> CreateScenarios(Dataset dataset) =>
[
    new("短路提交", graph =>
        graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-512f, -512f),
            new Vector2(-504f, -512f),
        ]).Success),
    new("长路提交", graph =>
        graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-512f, -448f),
            new Vector2(dataset.MaxX + 512f, -448f),
        ]).Success),
    new("原生曲线提交", graph => graph.SubmitPath(new RoadBuildRequest(new RoadPath([
        new CubicBezierRoadGeometrySegment(
            new Vector2(-512f, -384f),
            new Vector2(-504f, -368f),
            new Vector2(-496f, -400f),
            new Vector2(-488f, -384f))]), RoadType.Street)).Success),
    new("完整覆盖", graph =>
        graph.SubmitPolyline(RoadType.Street, [dataset.LastStart, dataset.LastEnd]).Error ==
            RoadPathSubmissionError.FullyCovered),
    new("多交叉提交", graph =>
        graph.SubmitPolyline(RoadType.Street, [
            new Vector2(4f, -16f),
            new Vector2(4f, dataset.MaxY + 16f),
        ]).Success),
    new("最近边命中", graph =>
        graph.FindClosestEdge(dataset.MiddlePoint, 2f)?.ID == dataset.MiddleEdgeID),
    new("单边删除", graph => graph.RemoveEdge(dataset.MiddleEdgeID)),
];

static BenchmarkSample MeasureOnce(string payload, Scenario scenario)
{
    var graph = new RoadGraph();
    LoadJson(graph, payload);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    long started = Stopwatch.GetTimestamp();
    bool valid = scenario.Action(graph);
    long elapsed = Stopwatch.GetTimestamp() - started;
    long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    if (!valid)
        throw new InvalidOperationException($"Scenario '{scenario.Name}' did not produce its expected result.");

    RoadGraphOperationMetrics metrics = graph.LastOperationMetrics;
    return new BenchmarkSample(
        elapsed * 1000d / Stopwatch.Frequency,
        allocated,
        metrics.SpatialCandidateEdgeCount,
        metrics.QueryFragmentCandidateCount,
        metrics.ExactGeometryTestCount,
        metrics.FullEdgeScanPassCount,
        metrics.FullEdgeVisitCount);
}

static IReadOnlyList<LocalityBenchmarkResult> RunLocalityBenchmarks()
{
    var cases = new List<LocalityCase>();
    foreach (float length in new[] { 4_096f, 65_536f })
    {
        RoadGraph graph = CreateStraightLineGraph(length);
        cases.Add(new LocalityCase("line 长度", (int)length, graph, [
            ("首", new Vector2(17f, 0f)),
            ("中", new Vector2(length * 0.5f + 17f, 0f)),
            ("尾", new Vector2(length - 17f, 0f)),
        ]));
    }

    foreach (int geometryCount in new[] { 64, 1_024 })
    {
        RoadGraph graph = CreateZigZagGraph(geometryCount);
        int[] indices = [0, geometryCount / 2, geometryCount - 1];
        cases.Add(new LocalityCase(
            "geometry 数",
            geometryCount,
            graph,
            indices.Select((index, position) => (
                new[] { "首", "中", "尾" }[position],
                ZigZagMidpoint(index))).ToArray()));
    }

    var results = new List<LocalityBenchmarkResult>();
    foreach (LocalityCase benchmarkCase in cases)
    {
        RoadGraphResourceCounts resources = benchmarkCase.Graph.CaptureResourceCounts();
        foreach ((string window, Vector2 point) in benchmarkCase.Points)
        {
            for (int index = 0; index < 20; index++)
                RequireLocalQuery(benchmarkCase.Graph, point);

            var samples = new double[200];
            for (int index = 0; index < samples.Length; index++)
            {
                long started = Stopwatch.GetTimestamp();
                RequireLocalQuery(benchmarkCase.Graph, point);
                samples[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            }

            Array.Sort(samples);
            RoadGraphOperationMetrics metrics = benchmarkCase.Graph.LastOperationMetrics;
            results.Add(new LocalityBenchmarkResult(
                benchmarkCase.Family,
                benchmarkCase.Scale,
                window,
                samples.Average(),
                samples[(int)Math.Ceiling(samples.Length * 0.95d) - 1],
                resources.Buckets,
                resources.QueryFragments,
                metrics.QueryFragmentCandidateCount,
                metrics.ExactGeometryTestCount,
                metrics.SpatialCandidateEdgeCount,
                metrics.FullEdgeScanPassCount,
                metrics.FullEdgeVisitCount));
        }
    }

    return results;
}

static void RequireLocalQuery(RoadGraph graph, Vector2 point)
{
    if (graph.FindClosestEdge(point, 0.01f)?.ID != 2)
        throw new InvalidOperationException($"Locality query at {point} missed the maximal Edge.");
    RoadGraphOperationMetrics metrics = graph.LastOperationMetrics;
    if (metrics.SpatialCandidateEdgeCount != 1 ||
        metrics.QueryFragmentCandidateCount is < 1 or > 8 ||
        metrics.ExactGeometryTestCount is < 1 or > 8 ||
        metrics.FullEdgeScanPassCount != 0 ||
        metrics.FullEdgeVisitCount != 0)
    {
        throw new InvalidOperationException($"Locality query at {point} exceeded its structural bounds: {metrics}.");
    }
}

static RoadGraph CreateStraightLineGraph(float length)
{
    var end = new Vector2(length, 0f);
    return RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
        4,
        [new PreparedRoadNode(0, Vector2.Zero), new PreparedRoadNode(1, end)],
        [new PreparedRoadEdge(RoadType.Street, 2, 0, 1, [new LineRoadGeometrySegment(Vector2.Zero, end)])]));
}

static RoadGraph CreateZigZagGraph(int geometryCount)
{
    var geometry = new RoadGeometrySegment[geometryCount];
    Vector2 start = Vector2.Zero;
    for (int index = 0; index < geometry.Length; index++)
    {
        Vector2 end = new((index + 1) * 16f, index % 2 == 0 ? 16f : 0f);
        geometry[index] = new LineRoadGeometrySegment(start, end);
        start = end;
    }

    return RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
        4,
        [new PreparedRoadNode(0, Vector2.Zero), new PreparedRoadNode(1, start)],
        [new PreparedRoadEdge(RoadType.Street, 2, 0, 1, geometry)]));
}

static Vector2 ZigZagMidpoint(int index)
{
    Vector2 start = new(index * 16f, index % 2 == 0 ? 0f : 16f);
    Vector2 end = new((index + 1) * 16f, index % 2 == 0 ? 16f : 0f);
    return (start + end) * 0.5f;
}

static string BuildPayload(Dataset dataset)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
        writer.WriteStartObject();
        writer.WriteString("formatFamily", "simple-cities-v3");
        writer.WriteString("payloadType", "road-network");
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteNumber("nextID", dataset.NextID);

        writer.WriteStartArray("nodes");
        for (int index = 0; index < dataset.EdgeCount; index++)
        {
            (Vector2 start, Vector2 end) = dataset.GetEdge(index);
            WriteNode(writer, index * 2, start);
            WriteNode(writer, index * 2 + 1, end);
        }
        writer.WriteEndArray();

        writer.WriteStartArray("edges");
        for (int index = 0; index < dataset.EdgeCount; index++)
        {
            (Vector2 start, Vector2 end) = dataset.GetEdge(index);
            writer.WriteStartObject();
            writer.WriteNumber("id", dataset.EdgeID(index));
            writer.WriteNumber("nodeAID", index * 2);
            writer.WriteNumber("nodeBID", index * 2 + 1);
            writer.WriteString("roadType", "street");
            writer.WriteStartArray("geometry");
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteString("kind", "line");
            WritePoint(writer, "start", start);
            WritePoint(writer, "end", end);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
    return Encoding.UTF8.GetString(stream.ToArray());
}

static string CaptureJson(RoadGraph graph)
{
    using var stream = new MemoryStream();
    graph.WriteSnapshot(stream, graph.CaptureSnapshot());
    return Encoding.UTF8.GetString(stream.ToArray());
}

static void LoadJson(RoadGraph graph, string json)
{
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
    graph.CommitPreparedLoad(graph.CaptureLoadReader().PrepareLoad(stream));
}

static void WriteNode(Utf8JsonWriter writer, int id, Vector2 position)
{
    writer.WriteStartObject();
    writer.WriteNumber("id", id);
    writer.WriteNumber("x", position.X);
    writer.WriteNumber("y", position.Y);
    writer.WriteEndObject();
}

static void WritePoint(Utf8JsonWriter writer, string propertyName, Vector2 point)
{
    writer.WriteStartObject(propertyName);
    writer.WriteNumber("x", point.X);
    writer.WriteNumber("y", point.Y);
    writer.WriteEndObject();
}

internal sealed record Scenario(string Name, Func<RoadGraph, bool> Action);

internal readonly record struct BenchmarkSample(
    double Milliseconds,
    long AllocatedBytes,
    int CandidateEdges,
    int FragmentCandidates,
    long ExactTests,
    int FullScans,
    long FullEdgeVisits);

internal readonly record struct LocalMutationMeasurement(
    WeakReference OldRoot,
    double CommitMilliseconds,
    long AllocatedBytes,
    int DeltaNodes,
    int DeltaEdges,
    int CopiedNodes,
    int CopiedEdges,
    int CopiedBucketPages,
    int SharedEntities,
    int SharedBucketPages);

internal readonly record struct ImmutableMutationBenchmarkResult(
    int GeometryCount,
    double CommitMilliseconds,
    long AllocatedBytes,
    int DeltaNodes,
    int DeltaEdges,
    int CopiedNodes,
    int CopiedEdges,
    int CopiedBucketPages,
    int SharedEntities,
    int SharedBucketPages,
    double CaptureMilliseconds,
    long CaptureAllocatedBytes,
    double DiagnosticsCaptureMilliseconds,
    long DiagnosticsCaptureAllocatedBytes,
    bool OldRootReleased);

internal readonly record struct HistoryBenchmarkResult(
    int EditCount,
    long DeltaRetainedBytes,
    long JsonBaselineBytes,
    long EditAllocatedBytes,
    double UndoMilliseconds,
    long UndoAllocatedBytes,
    double RedoMilliseconds,
    long RedoAllocatedBytes,
    int EventCount,
    int FullResetEventCount);

internal sealed record LocalityCase(
    string Family,
    int Scale,
    RoadGraph Graph,
    IReadOnlyList<(string Window, Vector2 Point)> Points);

internal sealed record LocalityBenchmarkResult(
    string Family,
    int Scale,
    string Window,
    double MeanMilliseconds,
    double P95Milliseconds,
    long Buckets,
    long QueryFragments,
    int FragmentCandidates,
    long ExactTests,
    int AggregatedEdges,
    int FullScans,
    long FullEdgeVisits);

internal sealed record BenchmarkResult(
    int EdgeCount,
    string Scenario,
    double MeanMilliseconds,
    double P95Milliseconds,
    double MeanAllocatedBytes,
    double MeanCandidateEdges,
    double MeanFullScans,
    double MeanFullEdgeVisits)
{
    public static BenchmarkResult Create(
        int edgeCount,
        string scenario,
        IReadOnlyList<BenchmarkSample> samples)
    {
        double[] durations = samples.Select(sample => sample.Milliseconds).Order().ToArray();
        int p95Index = Math.Max(0, (int)Math.Ceiling(durations.Length * 0.95d) - 1);
        return new BenchmarkResult(
            edgeCount,
            scenario,
            samples.Average(sample => sample.Milliseconds),
            durations[p95Index],
            samples.Average(sample => (double)sample.AllocatedBytes),
            samples.Average(sample => (double)sample.CandidateEdges),
            samples.Average(sample => (double)sample.FullScans),
            samples.Average(sample => (double)sample.FullEdgeVisits));
    }
}

internal sealed record Dataset(int EdgeCount, int Columns)
{
    private const float Spacing = 32f;
    private const float EdgeLength = 8f;

    public int NextID => EdgeCount * 3;
    public float MaxX => (Columns - 1) * Spacing + EdgeLength;
    public float MaxY => ((EdgeCount - 1) / Columns) * Spacing;
    public int MiddleIndex => EdgeCount / 2;
    public int MiddleEdgeID => EdgeID(MiddleIndex);
    public Vector2 MiddlePoint
    {
        get
        {
            (Vector2 start, Vector2 end) = GetEdge(MiddleIndex);
            return (start + end) * 0.5f;
        }
    }
    public Vector2 LastStart => GetEdge(EdgeCount - 1).Start;
    public Vector2 LastEnd => GetEdge(EdgeCount - 1).End;

    public static Dataset Create(int edgeCount) =>
        new(edgeCount, (int)Math.Ceiling(Math.Sqrt(edgeCount)));

    public int EdgeID(int index) => EdgeCount * 2 + index;

    public (Vector2 Start, Vector2 End) GetEdge(int index)
    {
        float x = index % Columns * Spacing;
        float y = index / Columns * Spacing;
        var start = new Vector2(x, y);
        return (start, start + new Vector2(EdgeLength, 0f));
    }
}
