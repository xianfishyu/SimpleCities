using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using SimpleCities.RoadCore;

string output = args.Length == 0 ? ".scratch/v4-20-20260921/history.json" : args[0];
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
string[] scenarios = ["construction-64", "local-span-profile", "whole-32-span-edge-profile",
    "batch-256-edge-profile", "batch-4-edge-delete", "dense-junction-profile",
    "polyline-whole-profile", "polyline-local-profile"];
var rows = new List<object>();
var cancellationRows = new List<object>();
var releaseRows = new List<object>();
var fixtures = new List<object>();
foreach (int cell in new[] { 25, 50, 100, 200 })
{
    var seeds = new Dictionary<string, byte[]>();
    foreach (string kind in new[] { "empty", "straight", "disconnected", "junction", "polyline" })
    {
        byte[] seed = MakeSeed(cell, kind);
        seeds.Add(kind, seed);
        RoadSnapshot snapshot = Load(seed).Snapshot;
        fixtures.Add(new { CellMetres = cell, Kind = kind, snapshot.NodeCount, snapshot.EdgeCount,
            ChainPoints = snapshot.Edges.Sum(edge => edge.Points.Count), PayloadBytes = seed.Length,
            ConstructedThroughPublicBuild = true, ValidatedThroughPublicCodec = true });
        Console.WriteLine($"Seed {cell} m {kind}: {snapshot.NodeCount} nodes, {snapshot.EdgeCount} edges");
    }
    foreach (string scenario in scenarios)
    {
        byte[] seed = seeds[SeedKind(scenario)];
        _ = Measure(seed, scenario, -1);
        for (int repetition = 0; repetition < 5; repetition++)
            rows.Add(Measure(seed, scenario, repetition));
        Console.WriteLine($"Measured {cell} m {scenario}");
    }
    cancellationRows.AddRange(MeasureCancellation(seeds["disconnected"]));
    releaseRows.AddRange(MeasurePlanRelease(seeds["disconnected"]));
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
    HistoryBudgetIsProductionAdmission = false,
    TargetScaleMeasured = false,
    TargetScaleLimitation = "Current production capacity rejects edge 257; 10K/9680 and 100K cannot be loaded.",
    Fixtures = fixtures, Rows = rows,
    EdgeLimit = MeasureEdgeLimit(),
    Cancellation = cancellationRows,
    UncommittedPlanRelease = releaseRows
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(Path.GetFullPath(output));

static string SeedKind(string scenario) => scenario switch
{
    "construction-64" => "empty",
    "local-span-profile" or "whole-32-span-edge-profile" => "straight",
    "batch-256-edge-profile" or "batch-4-edge-delete" => "disconnected",
    "dense-junction-profile" => "junction",
    "polyline-whole-profile" or "polyline-local-profile" => "polyline",
    _ => throw new ArgumentException("Unknown scenario", nameof(scenario))
};

[MethodImpl(MethodImplOptions.NoInlining)]
static object Measure(byte[] seed, string scenario, int repetition)
{
    Roots roots = MakeHistory(seed, scenario);
    long estimated = roots.History?.EstimatedBytes ?? throw new InvalidOperationException("Missing history");
    HeapSample before = StableHeap();
    roots.History = null;
    long releaseStarted = Stopwatch.GetTimestamp();
    HeapSample after = StableHeap();
    double releaseMs = Stopwatch.GetElapsedTime(releaseStarted).TotalMilliseconds;
    bool collected = !roots.HistoryReference.IsAlive;
    Require(collected, "released history is still rooted");
    GC.KeepAlive(roots.Snapshot);
    return new
    {
        CellMetres = roots.Snapshot.Map.CellSizeMetres, Scenario = scenario, Repetition = repetition,
        Operations = 64, roots.Snapshot.NodeCount, roots.Snapshot.EdgeCount,
        ChainPoints = roots.Snapshot.Edges.Sum(edge => edge.Points.Count),
        EstimatedBytes = estimated,
        MarginalHistoryManagedBytes = before.Bytes - after.Bytes,
        BeforeHeap = before, AfterHeap = after,
        ForcedCollectionAndReleaseMs = releaseMs, ReleasedHistoryCollected = collected,
        roots.MaximumOperationEstimatedBytes, roots.Sharing, roots.OperationMs, roots.SelectedSpanCounts
    };
}

[MethodImpl(MethodImplOptions.NoInlining)]
static Roots MakeHistory(byte[] seed, string scenario)
{
    RoadNetwork network = Load(seed);
    int cell = network.Snapshot.Map.CellSizeMetres;
    var entities = new List<object>();
    var times = new List<double>();
    var spanCounts = new List<int>();
    long maximumOperation = 0;
    for (int i = 0; i < 64; i++)
    {
        long previous = network.History.EstimatedBytes;
        long started = Stopwatch.GetTimestamp();
        RoadPlan plan;
        int selectedCount = 0;
        if (scenario == "construction-64")
        {
            double x = (i % 8 * 2 - 8) * cell, y = (i / 8 * 2 - 8) * cell;
            plan = Build(network, new(x, y), new(x + cell, y));
        }
        else
        {
            RoadSnapshot source = network.Snapshot;
            RoadGridSpan[] spans;
            if (scenario == "local-span-profile")
                spans = [PickPoint(source, new(cell * 0.5, 0))];
            else if (scenario == "polyline-local-profile")
                spans = [PickPoint(source, new(-19.5 * cell, -19.5 * cell))];
            else
                spans = AllSpans(source, scenario == "batch-4-edge-delete" ? 4 : int.MaxValue);
            selectedCount = spans.Length;
            RoadEditResult result = scenario == "batch-4-edge-delete"
                ? network.PlanRemove(spans)
                : network.PlanChangeProfile(spans, i % 2 == 0 ? RoadProfileId.Highway : RoadProfileId.Street);
            plan = result.Plan ?? throw new InvalidOperationException(result.Reason);
            Require(network.TryCommit(plan), "edit commit");
        }
        times.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        spanCounts.Add(selectedCount);
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
    Require(network.History.RetainedCount == 64 && network.History.UndoCount == 64, "history capacity");
    var unique = entities.ToHashSet(ReferenceEqualityComparer.Instance);
    var current = network.Snapshot.Nodes.Cast<object>().Concat(network.Snapshot.Edges)
        .ToHashSet(ReferenceEqualityComparer.Instance);
    return new Roots(network.Snapshot, network.History, maximumOperation,
        new Sharing(entities.Count, unique.Count, unique.Count(current.Contains)), times.ToArray(), spanCounts.ToArray());
}

// Selection through the public span picker; each midpoint is strictly inside a
// primary-grid interval, including both cells of a two-cell connector.
static RoadGridSpan[] AllSpans(RoadSnapshot snapshot, int edgeCount = int.MaxValue)
{
    var spans = new List<RoadGridSpan>();
    foreach (RoadEdge edge in snapshot.Edges.Take(edgeCount))
    {
        double length = edge.Length, cumulative = 0;
        for (int i = 1; i < edge.Points.Count; i++)
        {
            RoadPoint a = edge.Points[i - 1], b = edge.Points[i];
            double segment = a.DistanceTo(b);
            int cells = checked((int)Math.Round(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)) /
                snapshot.Map.CellSizeMetres));
            Require(cells > 0, "fixture segment must contain a primary cell");
            for (int step = 0; step < cells; step++)
                spans.Add(Pick(snapshot, edge, (cumulative + segment * (step + 0.5) / cells) / length));
            cumulative += segment;
        }
    }
    return spans.DistinctBy(span => span.Key).ToArray();
}

static RoadGridSpan PickPoint(RoadSnapshot snapshot, RoadPoint point)
{
    foreach (RoadEdge edge in snapshot.Edges)
    {
        double cumulative = 0, length = edge.Length;
        for (int i = 1; i < edge.Points.Count; i++)
        {
            RoadPoint a = edge.Points[i - 1], b = edge.Points[i];
            double segment = a.DistanceTo(b), along = a.DistanceTo(point);
            if (Math.Abs(along + point.DistanceTo(b) - segment) < 0.000001)
                return Pick(snapshot, edge, (cumulative + along) / length);
            cumulative += segment;
        }
    }
    throw new InvalidOperationException("Fixture point not on road");
}

static RoadGridSpan Pick(RoadSnapshot snapshot, RoadEdge edge, double parameter) =>
    RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, parameter))
    ?? throw new InvalidOperationException("Missing fixture span");

static RoadPlan Build(RoadNetwork network, RoadPoint start, RoadPoint end)
{
    RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, RoadProfileId.Street));
    RoadPlan plan = result.Plan ?? throw new InvalidOperationException($"Build {start} -> {end}: {result.Reason}");
    Require(network.TryCommit(plan), "build commit");
    return plan;
}

static byte[] MakeSeed(int cell, string kind)
{
    var network = new RoadNetwork(new MapDefinition(cell));
    if (kind == "straight") Build(network, new(-16 * cell, 0), new(16 * cell, 0));
    if (kind == "disconnected") SeedDisconnected(network, cell, 256);
    if (kind == "junction")
    {
        for (int i = -5; i <= 5; i++) Build(network, new(-5 * cell, i * cell), new(5 * cell, i * cell));
        for (int i = -5; i <= 5; i++) Build(network, new(i * cell, -5 * cell), new(i * cell, 5 * cell));
    }
    if (kind == "polyline")
    {
        RoadPoint previous = new(-20 * cell, -20 * cell);
        for (int row = 0; row < 20; row++)
        {
            int direction = row % 2 == 0 ? 1 : -1;
            if (row != 0)
            {
                RoadPoint connector = new(previous.X, (-20 + 2 * row) * cell);
                Build(network, previous, connector);
                previous = connector;
            }
            for (int column = 1; column <= 40; column++)
            {
                RoadPoint next = new(previous.X + direction * cell, (-20 + 2 * row + column % 2) * cell);
                Build(network, previous, next);
                previous = next;
            }
        }
        Require(network.Snapshot.EdgeCount == 1 && network.Snapshot.Edges[0].Points.Count == 820,
            "high-chain fixture must retain every turn");
    }
    using var destination = new MemoryStream();
    RoadCodec.Write(destination, network.Snapshot);
    byte[] bytes = destination.ToArray();
    _ = Load(bytes); // Reject any fixture not accepted by the production codec.
    return bytes;
}

static RoadNetwork Load(byte[] bytes)
{
    using var stream = new MemoryStream(bytes, writable: false);
    var network = new RoadNetwork();
    Require(network.TryCommit(network.PlanLoad(RoadCodec.Read(stream))), "seed load");
    Require(network.History.RetainedCount == 0, "seed history must be empty");
    return network;
}

static object MeasureEdgeLimit()
{
    var network = new RoadNetwork(new MapDefinition(25));
    SeedDisconnected(network, 25, 256);
    RoadSnapshot before = network.Snapshot;
    RoadBuildResult result = network.PlanBuild(new(before.Token, new(0, 1000), new(25, 1000), RoadProfileId.Street));
    Require(result.Plan is null && ReferenceEquals(before, network.Snapshot), "expected structural edge rejection");
    return new { RequestedEdgeCount = 257, network.Snapshot.EdgeCount, Status = result.Status.ToString(), result.Reason };
}

static void SeedDisconnected(RoadNetwork network, int cell, int count)
{
    for (int i = 0; i < count; i++)
    {
        double x = (i % 16 * 2 - 16) * cell, y = (i / 16 * 2 - 16) * cell;
        Build(network, new(x, y), new(x + cell, y));
    }
}

static IEnumerable<object> MeasureCancellation(byte[] seed)
{
    RoadNetwork network = Load(seed);
    RoadSnapshot source = network.Snapshot;
    RoadGridSpan[] spans = AllSpans(source);
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
        long delayStarted = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(delayStarted).TotalMilliseconds < 0.1) Thread.SpinWait(20);
        long signal = Stopwatch.GetTimestamp();
        cancellation.Cancel();
        worker.Join();
        Require(ReferenceEquals(source, network.Snapshot), "cancel must not publish");
        if (repetition >= 0)
            yield return new
            {
                CellMetres = source.Map.CellSizeMetres, Repetition = repetition, SelectedSpans = spans.Length,
                RequestedDelayAfterWorkerEntryMs = 0.1, Outcome = outcome,
                CompletedBeforeSignal = exitTimestamp < signal,
                SignalToWorkerExitMs = (exitTimestamp - signal) * 1000.0 / Stopwatch.Frequency,
                ActiveSnapshotUnchanged = true
            };
    }
}

static IEnumerable<object> MeasurePlanRelease(byte[] seed)
{
    for (int repetition = -1; repetition < 5; repetition++)
    {
        PendingRoots roots = MakePendingPlan(seed);
        HeapSample before = StableHeap();
        roots.Plan = null;
        long releaseStarted = Stopwatch.GetTimestamp();
        HeapSample after = StableHeap();
        double releaseMs = Stopwatch.GetElapsedTime(releaseStarted).TotalMilliseconds;
        bool collected = !roots.PlanReference.IsAlive;
        Require(collected, "uncommitted plan remains rooted");
        GC.KeepAlive(roots.Network);
        if (repetition >= 0)
            yield return new
            {
                CellMetres = roots.Network.Snapshot.Map.CellSizeMetres, Repetition = repetition,
                SelectedSpans = 256, MarginalPreparedPlanManagedBytes = before.Bytes - after.Bytes,
                ForcedCollectionAndReleaseMs = releaseMs, ReleasedPlanCollected = collected,
                BeforeHeap = before, AfterHeap = after
            };
    }
}

[MethodImpl(MethodImplOptions.NoInlining)]
static PendingRoots MakePendingPlan(byte[] seed)
{
    RoadNetwork network = Load(seed);
    RoadSnapshot source = network.Snapshot;
    RoadPlan plan = network.PlanChangeProfile(AllSpans(source), RoadProfileId.Highway).Plan
        ?? throw new InvalidOperationException("Missing pending plan");
    Require(ReferenceEquals(source, network.Snapshot), "pending plan must not publish");
    return new(network, plan);
}

[MethodImpl(MethodImplOptions.NoInlining)]
static HeapSample StableHeap()
{
    long minimum = long.MaxValue, maximum = 0, last = 0;
    for (int i = 0; i < 3; i++)
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        last = GC.GetTotalMemory(false);
        minimum = Math.Min(minimum, last);
        maximum = Math.Max(maximum, last);
    }
    // Context only: working set includes runtime/native memory and retained GC pages.
    using Process process = Process.GetCurrentProcess();
    return new(last, maximum - minimum, process.WorkingSet64);
}

static void Require(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
}

sealed class Roots(RoadSnapshot snapshot, RoadEditHistory history, long maximumOperationEstimatedBytes,
    Sharing sharing, double[] operationMs, int[] selectedSpanCounts)
{
    public RoadSnapshot Snapshot { get; } = snapshot;
    public RoadEditHistory? History { get; set; } = history;
    public WeakReference HistoryReference { get; } = new(history);
    public long MaximumOperationEstimatedBytes { get; } = maximumOperationEstimatedBytes;
    public Sharing Sharing { get; } = sharing;
    public double[] OperationMs { get; } = operationMs;
    public int[] SelectedSpanCounts { get; } = selectedSpanCounts;
}
sealed record Sharing(int EntityEndpointOccurrences, int UniqueEntityObjects, int UniqueObjectsAlsoInCurrentSnapshot);
readonly record struct HeapSample(long Bytes, long StabilitySpreadBytes, long ProcessWorkingSetBytes);
sealed class PendingRoots(RoadNetwork network, RoadPlan plan)
{
    public RoadNetwork Network { get; } = network;
    public RoadPlan? Plan { get; set; } = plan;
    public WeakReference PlanReference { get; } = new(plan);
}
