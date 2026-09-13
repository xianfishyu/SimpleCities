using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using SimpleCities.RoadCore;

if (args.Length != 0 && args[0] == "--surface-diagnosis")
{
    SurfaceDiagnosis.Run();
    return;
}

string output = args.Length == 0 ? ".scratch/v4-20-qa/history.json" : args[0];
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
var rows = new List<object>();
foreach (int cell in new[] { 25, 50, 100, 200 })
foreach (string scenario in new[] { "construction", "single-span-profile", "batch-32-span-profile", "batch-64-disconnected-profile", "batch-256-disconnected-profile" })
{
    _ = Measure(cell, scenario, -1); // Warm up exactly the same path; excluded from evidence.
    for (int repetition = 0; repetition < 5; repetition++)
        rows.Add(Measure(cell, scenario, repetition));
    Console.WriteLine($"Measured {cell} m {scenario}");
}
File.WriteAllText(output, JsonSerializer.Serialize(new
{
    CapturedUtc = DateTimeOffset.UtcNow,
    Runtime = RuntimeInformation.FrameworkDescription,
    OS = RuntimeInformation.OSDescription,
    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    ProcessorCount = Environment.ProcessorCount,
    ServerGC = System.Runtime.GCSettings.IsServerGC,
    TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime default",
    HistoryCapacity = RoadEditHistory.Capacity,
    Rows = rows,
    EdgeLimit = MeasureEdgeLimit(),
    Cancellation = new[] { 25, 50, 100, 200 }.SelectMany(MeasureCancellation).ToArray(),
    UncommittedPlanRelease = new[] { 25, 50, 100, 200 }.SelectMany(MeasurePlanRelease).ToArray()
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(Path.GetFullPath(output));

[MethodImpl(MethodImplOptions.NoInlining)]
static object Measure(int cell, string scenario, int repetition)
{
    var roots = MakeHistory(cell, scenario);
    // The network and all RoadPlans have left their stack frames. Only these two
    // public roots remain. The exact same snapshot stays alive across both GCs.
    long estimated = roots.History!.EstimatedBytes;
    var before = StableHeap();
    roots.History = null;
    long releaseStarted = Stopwatch.GetTimestamp();
    var after = StableHeap();
    double releaseMs = Stopwatch.GetElapsedTime(releaseStarted).TotalMilliseconds;
    GC.KeepAlive(roots.Snapshot);
    return new
    {
        CellMetres = cell, Scenario = scenario, Repetition = repetition,
        Operations = 64, roots.Snapshot.NodeCount, roots.Snapshot.EdgeCount,
        EstimatedBytes = estimated,
        MarginalHistoryManagedBytes = before.Bytes - after.Bytes,
        BeforeHeap = before, AfterHeap = after,
        ForcedCollectionAndReleaseMs = releaseMs,
        roots.MaximumOperationEstimatedBytes, roots.Sharing,
        OperationMs = roots.OperationMs
    };
}

[MethodImpl(MethodImplOptions.NoInlining)]
static Roots MakeHistory(int cell, string scenario)
{
    var network = new RoadNetwork(new MapDefinition(cell));
    if (scenario != "construction")
    {
        if (scenario.EndsWith("disconnected-profile", StringComparison.Ordinal))
            SeedDisconnected(network, cell, scenario.StartsWith("batch-256", StringComparison.Ordinal) ? 256 : 64);
        else
            Build(network, new(-16 * cell, 0), new(16 * cell, 0));
        using var stream = new MemoryStream();
        RoadCodec.Write(stream, network.Snapshot);
        stream.Position = 0;
        Require(network.TryCommit(network.PlanLoad(RoadCodec.Read(stream))), "seed reload");
    }
    var entities = new List<object>();
    var times = new List<double>();
    long maximumOperation = 0;
    for (int i = 0; i < 64; i++)
    {
        long previous = network.History.EstimatedBytes;
        var watch = Stopwatch.StartNew();
        RoadPlan plan;
        if (scenario == "construction")
        {
            double x = (i % 8 * 2 - 8) * cell, y = (i / 8 * 2 - 8) * cell;
            plan = Build(network, new(x, y), new(x + cell, y));
        }
        else
        {
            RoadSnapshot source = network.Snapshot;
            int count = scenario == "single-span-profile" ? 1 : 32;
            var spans = scenario.EndsWith("disconnected-profile", StringComparison.Ordinal)
                ? source.Edges.Select(edge => RoadSpanQuery.Pick(source, new(source.Token, edge.Id, 0.5))
                    ?? throw new InvalidOperationException("Missing batch span")).ToArray()
                : Enumerable.Range(0, count).Select(index =>
                    Pick(source, new((-16 + (count == 1 ? 16 : index) + 0.5) * cell, 0))).ToArray();
            RoadEditResult result = network.PlanChangeProfile(spans,
                i % 2 == 0 ? RoadProfileId.Highway : RoadProfileId.Street);
            plan = result.Plan ?? throw new InvalidOperationException(result.Reason);
            Require(network.TryCommit(plan), "profile commit");
        }
        watch.Stop();
        times.Add(watch.Elapsed.TotalMilliseconds);
        maximumOperation = Math.Max(maximumOperation, network.History.EstimatedBytes - previous);
        foreach (RoadNodeChange change in plan.ChangeSet.Nodes)
        {
            if (change.Before is not null) entities.Add(change.Before);
            if (change.After is not null) entities.Add(change.After);
        }
        foreach (RoadEdgeChange change in plan.ChangeSet.Edges)
        {
            if (change.Before is not null) entities.Add(change.Before);
            if (change.After is not null) entities.Add(change.After);
        }
    }
    Require(network.History.RetainedCount == 64, "history capacity");
    var unique = entities.ToHashSet(ReferenceEqualityComparer.Instance);
    var current = network.Snapshot.Nodes.Cast<object>().Concat(network.Snapshot.Edges)
        .ToHashSet(ReferenceEqualityComparer.Instance);
    return new Roots(network.Snapshot, network.History, maximumOperation,
        new Sharing(entities.Count, unique.Count, unique.Count(current.Contains)), times.ToArray());
}

static RoadGridSpan Pick(RoadSnapshot snapshot, RoadPoint point)
{
    RoadEdge edge = snapshot.Edges.Single(edge => edge.Points[0].X <= point.X && edge.Points[^1].X >= point.X);
    double parameter = (point.X - edge.Points[0].X) / (edge.Points[^1].X - edge.Points[0].X);
    return RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, parameter))
        ?? throw new InvalidOperationException("Missing span");
}

static RoadPlan Build(RoadNetwork network, RoadPoint start, RoadPoint end)
{
    RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, RoadProfileId.Street));
    RoadPlan plan = result.Plan ?? throw new InvalidOperationException(result.Reason);
    Require(network.TryCommit(plan), "build commit");
    return plan;
}

static object MeasureEdgeLimit()
{
    var network = new RoadNetwork(new MapDefinition(25));
    for (int i = 0; i < 257; i++)
    {
        double x = (i % 20 * 2 - 20) * 25, y = (i / 20 * 2 - 20) * 25;
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, new(x, y), new(x + 25, y), RoadProfileId.Street));
        if (result.Plan is null)
            return new { RequestedEdgeCount = i + 1, network.Snapshot.EdgeCount, result.Status, result.Reason };
        Require(network.TryCommit(result.Plan), "edge-limit fixture commit");
    }
    throw new InvalidOperationException("Expected structural edge limit was not observed");
}

static void SeedDisconnected(RoadNetwork network, int cell, int count)
{
    for (int i = 0; i < count; i++)
    {
        double x = (i % 16 * 2 - 16) * cell, y = (i / 16 * 2 - 16) * cell;
        Build(network, new(x, y), new(x + cell, y));
    }
}

static IEnumerable<object> MeasureCancellation(int cell)
{
    var network = new RoadNetwork(new MapDefinition(cell));
    SeedDisconnected(network, cell, 256);
    RoadSnapshot source = network.Snapshot;
    RoadGridSpan[] spans = source.Edges.Select(edge => RoadSpanQuery.Pick(source,
        new(source.Token, edge.Id, 0.5)) ?? throw new InvalidOperationException("Missing cancellation span")).ToArray();
    for (int repetition = -1; repetition < 10; repetition++)
    {
        using var cancellation = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        string outcome = "unset";
        long exitTimestamp = 0;
        var worker = new Thread(() =>
        {
            entered.Set();
            try { outcome = network.PlanChangeProfile(spans, RoadProfileId.Highway, cancellation.Token).Status.ToString(); }
            catch (OperationCanceledException) { outcome = "Cancelled"; }
            finally { exitTimestamp = Stopwatch.GetTimestamp(); }
        });
        worker.Start();
        entered.Wait();
        // Measured delay from worker entry; no internal hook claims the exact
        // planner checkpoint at which the signal arrives.
        var delay = Stopwatch.StartNew();
        while (delay.Elapsed.TotalMilliseconds < 0.1) Thread.SpinWait(20);
        long signal = Stopwatch.GetTimestamp();
        cancellation.Cancel();
        worker.Join();
        Require(ReferenceEquals(source, network.Snapshot), "cancel must not publish");
        if (repetition >= 0)
            yield return new
            {
                CellMetres = cell, Repetition = repetition, SelectedSpans = spans.Length,
                RequestedDelayAfterWorkerEntryMs = 0.1, Outcome = outcome,
                CompletedBeforeSignal = exitTimestamp < signal,
                SignalToWorkerExitMs = (exitTimestamp - signal) * 1000.0 / Stopwatch.Frequency
            };
    }
}

static IEnumerable<object> MeasurePlanRelease(int cell)
{
    for (int repetition = -1; repetition < 5; repetition++)
    {
        PendingRoots roots = MakePendingPlan(cell);
        var before = StableHeap();
        roots.Plan = null;
        long releaseStarted = Stopwatch.GetTimestamp();
        var after = StableHeap();
        double releaseMs = Stopwatch.GetElapsedTime(releaseStarted).TotalMilliseconds;
        GC.KeepAlive(roots.Network);
        if (repetition >= 0)
            yield return new
            {
                CellMetres = cell, Repetition = repetition,
                SelectedSpans = 256, MarginalPreparedPlanManagedBytes = before.Bytes - after.Bytes,
                ForcedCollectionAndReleaseMs = releaseMs,
                BeforeHeap = before, AfterHeap = after
            };
    }
}

[MethodImpl(MethodImplOptions.NoInlining)]
static PendingRoots MakePendingPlan(int cell)
{
    var network = new RoadNetwork(new MapDefinition(cell));
    SeedDisconnected(network, cell, 256);
    RoadSnapshot source = network.Snapshot;
    var spans = source.Edges.Select(edge => RoadSpanQuery.Pick(source, new(source.Token, edge.Id, 0.5))
        ?? throw new InvalidOperationException("Missing plan-release span")).ToArray();
    RoadPlan plan = network.PlanChangeProfile(spans, RoadProfileId.Highway).Plan
        ?? throw new InvalidOperationException("Missing pending plan");
    Require(ReferenceEquals(source, network.Snapshot), "pending plan must not publish");
    return new(network, plan);
}

[MethodImpl(MethodImplOptions.NoInlining)]
static HeapSample StableHeap()
{
    long[] samples = new long[3];
    for (int i = 0; i < samples.Length; i++)
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        samples[i] = GC.GetTotalMemory(false);
    }
    // Working set includes runtime/JIT/native memory and retained GC pages; it is
    // context only and must never be interpreted as the history's memory cost.
    using Process process = Process.GetCurrentProcess();
    return new(samples[^1], samples.Max() - samples.Min(), process.WorkingSet64);
}

static void Require(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
}

sealed class Roots(RoadSnapshot snapshot, RoadEditHistory history, long maximumOperationEstimatedBytes,
    Sharing sharing, double[] operationMs)
{
    public RoadSnapshot Snapshot { get; } = snapshot;
    public RoadEditHistory? History { get; set; } = history;
    public long MaximumOperationEstimatedBytes { get; } = maximumOperationEstimatedBytes;
    public Sharing Sharing { get; } = sharing;
    public double[] OperationMs { get; } = operationMs;
}
sealed record Sharing(int EntityEndpointOccurrences, int UniqueEntityObjects, int UniqueObjectsAlsoInCurrentSnapshot);
readonly record struct HeapSample(long Bytes, long StabilitySpreadBytes, long ProcessWorkingSetBytes);
sealed class PendingRoots(RoadNetwork network, RoadPlan plan)
{
    public RoadNetwork Network { get; } = network;
    public RoadPlan? Plan { get; set; } = plan;
}
