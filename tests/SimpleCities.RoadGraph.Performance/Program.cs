using Godot;
using System.Diagnostics;
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

Console.WriteLine("# RoadGraph V2 性能基线");
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

BenchmarkResult[] failed10k = allResults
    .Where(result => result.EdgeCount == 10_000 && result.P95Milliseconds > FrameBudgetMilliseconds)
    .ToArray();
Console.WriteLine();
Console.WriteLine(failed10k.Length == 0
    ? $"10k 硬门槛：全部场景 P95 不超过 {FrameBudgetMilliseconds:F2} ms。"
    : $"10k 硬门槛：{failed10k.Length} 个场景超过 {FrameBudgetMilliseconds:F2} ms：{string.Join("、", failed10k.Select(result => result.Scenario))}。");
Console.WriteLine("100k 结果仅用于压力观察，不参与退出码判定。");

return enforceBudget && failed10k.Length > 0 ? 2 : 0;

static IReadOnlyList<Scenario> CreateScenarios(Dataset dataset) =>
[
    new("短路提交", graph =>
        graph.AddRoad(new Vector2(-512f, -512f), new Vector2(-504f, -512f), []) >= 0),
    new("长路提交", graph =>
        graph.AddRoad(
            new Vector2(-512f, -448f),
            new Vector2(dataset.MaxX + 512f, -448f),
            []) >= 0),
    new("原生曲线提交", graph => graph.SubmitPath(new RoadPath([
        new CubicBezierRoadGeometrySegment(
            new Vector2(-512f, -384f),
            new Vector2(-504f, -368f),
            new Vector2(-496f, -400f),
            new Vector2(-488f, -384f))])).Success),
    new("完整覆盖", graph =>
        graph.AddRoad(dataset.LastStart, dataset.LastEnd, []) == -1),
    new("多交叉提交", graph =>
        graph.AddRoad(
            new Vector2(4f, -16f),
            new Vector2(4f, dataset.MaxY + 16f),
            []) >= 0),
    new("最近边命中", graph =>
        graph.FindClosestEdge(dataset.MiddlePoint, 2f)?.ID == dataset.MiddleEdgeID),
    new("单边删除", graph => graph.RemoveEdge(dataset.MiddleEdgeID)),
];

static BenchmarkSample MeasureOnce(string payload, Scenario scenario)
{
    var graph = new RoadGraph();
    graph.RestoreState(payload);
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
        metrics.FullEdgeScanPassCount,
        metrics.FullEdgeVisitCount);
}

static string BuildPayload(Dataset dataset)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
        writer.WriteStartObject();
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
            writer.WriteNumber("groupID", dataset.GroupID);
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

        writer.WriteStartArray("groups");
        writer.WriteStartObject();
        writer.WriteNumber("id", dataset.GroupID);
        writer.WriteStartArray("edgeIDs");
        for (int index = 0; index < dataset.EdgeCount; index++)
            writer.WriteNumberValue(dataset.EdgeID(index));
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
    return Encoding.UTF8.GetString(stream.ToArray());
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

    public int GroupID => EdgeCount * 3;
    public int NextID => GroupID + 1;
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
