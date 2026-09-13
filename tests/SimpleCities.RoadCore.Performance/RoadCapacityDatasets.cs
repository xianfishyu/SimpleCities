using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SimpleCities.RoadCore;

/// <summary>Standalone public-planner dataset generation; never loaded by the game.</summary>
internal static class RoadCapacityDatasets
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // --capacity-datasets [output-directory] [cell-metres]
    public static void Run(string[] args)
    {
        string directory = Path.GetFullPath(args.Length > 0 ? args[0] : ".scratch/v4-24-qa/datasets");
        int[] cells = args.Length > 1 ? [int.Parse(args[1])] : [25, 50, 100, 200];
        if (cells.Any(cell => cell is not (25 or 50 or 100 or 200)))
            throw new ArgumentException("Cell size must be 25, 50, 100, or 200 metres.");
        Directory.CreateDirectory(directory);
        foreach (int cell in cells)
        {
            Generate(directory, cell, false);
            if (cell == 200) Generate(directory, cell, true);
        }
    }

    private static void Generate(string directory, int cell, bool polylinesOnly)
    {
        string name = $"cell-{cell}-{(polylinesOnly ? "dense-polylines" : "target")}";
        var stages = new List<Stage>();
        using var memory = new MemorySampler();
        var network = new RoadNetwork(new MapDefinition(cell));
        int strokes = 0;
        var operations = new List<EditEvidence>();
        try
        {
            Measure("public-plan-build-and-commit", stages, () =>
            {
                void Build(double x1, double y1, double x2, double y2)
                {
                    RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token,
                        new(x1 * cell, y1 * cell), new(x2 * cell, y2 * cell), RoadProfileId.Street));
                    Require(result.Plan is not null && network.TryCommit(result.Plan),
                        $"Build ({x1},{y1})→({x2},{y2}) failed at stroke {strokes + 1}: {result.Reason}");
                    strokes++;
                    if (strokes % 40 == 0)
                        Console.WriteLine($"{name}: {strokes} strokes, {network.Snapshot.EdgeCount} canonical edges");
                }

                if (!polylinesOnly)
                {
                    for (int line = -20; line <= 20; line++)
                    {
                        Build(-20, line, 20, line);
                        Build(line, -20, line, 20);
                    }
                    // y-x=d and x+y=s clipped to [-20,20]^2. The extreme
                    // d/s=±40 only touch a corner and are not positive roads.
                    for (int diagonal = -39; diagonal <= 39; diagonal++)
                    {
                        int start = Math.Max(-20, -20 - diagonal);
                        int end = Math.Min(20, 20 - diagonal);
                        Build(start, start + diagonal, end, end + diagonal);
                        start = Math.Max(-20, diagonal - 20);
                        end = Math.Min(20, diagonal + 20);
                        Build(start, diagonal - start, end, diagonal - end);
                    }
                    Require(network.Snapshot.EdgeCount == 9680, "Complete 40×40 lattice must have 9,680 edges.");
                }

                if (polylinesOnly || cell != 200)
                    for (int index = 0; index < 320; index++)
                    {
                        // Target supplements occupy x≤-25, strictly outside
                        // the lattice x≥-20. Each L is separated by a full cell.
                        int columns = polylinesOnly ? 20 : 8;
                        int origin = polylinesOnly ? -20 : -40;
                        int x = origin + index % columns * 2;
                        int y = origin + index / columns * 2;
                        Build(x, y, x + 1, y);
                        Build(x + 1, y, x + 1, y + 1);
                    }
            });

            int expectedEdges = polylinesOnly ? 320 : cell == 200 ? 9680 : 10000;
            Require(network.Snapshot.EdgeCount == expectedEdges, "Unexpected canonical edge count.");
            DatasetCounts counts = Measure("count-spans-and-entities", stages, () => Count(network.Snapshot));
            Require(counts.StructuralNodes == (polylinesOnly ? 640 : cell == 200 ? 3281 : 3921), "Unexpected structural node count.");
            Require(counts.ChainPoints == (polylinesOnly ? 960 : cell == 200 ? 19360 : 20320), "Unexpected chain point count.");
            Require(counts.SelectableGridSpans == (polylinesOnly ? 640 : cell == 200 ? 9680 : 10320), "Unexpected selectable span count.");
            Require(counts.PolylineEdges == (polylinesOnly || cell != 200 ? 320 : 0), "L chains did not canonicalize as expected.");
            Require(counts.CellCenterJunctions == (polylinesOnly ? 0 : 1600), "Unexpected cell-center junction count.");
            string payloadPath = Path.Combine(directory, name + ".road.json");
            Measure("codec-write", stages, () =>
            {
                using var destination = File.Create(payloadPath);
                RoadCodec.Write(destination, network.Snapshot);
            });
            long payloadBytes = new FileInfo(payloadPath).Length;
            Require(payloadBytes <= RoadCodec.MaximumPayloadBytes, "Written payload exceeds the production reader budget.");
            PreparedRoadState prepared = Measure("codec-read", stages, () =>
            {
                using var source = File.OpenRead(payloadPath);
                return RoadCodec.Read(source);
            });
            string originalContent = ContentHash(network.Snapshot);
            var loaded = new RoadNetwork();
            RoadPlan load = Measure("plan-load", stages, () => loaded.PlanLoad(prepared));
            Measure("load-reference-commit", stages, () => Require(loaded.TryCommit(load), "Load commit failed."));
            Require(loaded.History.RetainedCount == 0 && ContentHash(loaded.Snapshot) == originalContent,
                "Load changed content or retained history.");
            Measure("codec-roundtrip-compare", stages, () =>
            {
                using var roundtrip = new MemoryStream();
                RoadCodec.Write(roundtrip, loaded.Snapshot);
                Require(File.ReadAllBytes(payloadPath).SequenceEqual(roundtrip.ToArray()), "Codec roundtrip is not byte-identical.");
            });
            SurfaceCounts surface = Measure("pure-presentation-prepare", stages, () =>
            {
                RoadSurfaceData data = RoadPresentation.Prepare(loaded.Snapshot.Nodes, loaded.Snapshot.Edges)
                    ?? throw new InvalidOperationException("Nonempty dataset has no surface.");
                return new SurfaceCounts(data.Pieces.Count, data.Pieces.Sum(piece => (long)piece.Corners.Count),
                    data.Pieces.Sum(piece => (long)(piece.Corners.Count - 2) * 3));
            });
            Measure("representative-edit-and-history-checks", stages, () =>
            {
                Exercise(loaded, false, operations);
                Exercise(loaded, true, operations);
            });
            Require(ContentHash(loaded.Snapshot) == originalContent, "Representative operations failed to restore original content.");
            memory.Sample();
            WriteEvidence(true, null, counts, surface, payloadBytes);
            Console.WriteLine($"{name}: complete, {counts.CanonicalEdges} edges, {payloadBytes} serialized bytes");
        }
        catch (Exception error)
        {
            memory.Sample();
            WriteEvidence(false, error.ToString(), null, null, null);
            throw;
        }

        void WriteEvidence(bool passed, string? error, DatasetCounts? counts, SurfaceCounts? surface, long? bytes) =>
            File.WriteAllText(Path.Combine(directory, name + ".measurements.json"), JsonSerializer.Serialize(new
            {
                CapturedUtc = DateTimeOffset.UtcNow, Name = name, CellMetres = cell, Passed = passed, Error = error,
                Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Strokes = strokes,
                Counts = counts, Presentation = surface, SerializedBytes = bytes, Stages = stages, Operations = operations,
                Memory = memory.Evidence(),
                Scope = "Public core planning/codec/load/history and pure presentation only; no GPU upload, Godot input, or frame timing."
            }, JsonOptions));
    }

    private static void Exercise(RoadNetwork network, bool remove, List<EditEvidence> evidence)
    {
        string original = ContentHash(network.Snapshot);
        RoadEdge edge = network.Snapshot.Edges[0];
        RoadGridSpan span = RoadSpanQuery.Pick(network.Snapshot, new(network.Snapshot.Token, edge.Id, 0.5))
            ?? throw new InvalidOperationException("Representative span cannot be selected.");
        Apply(remove ? "remove-one-span" : "change-one-span", () => remove
            ? network.PlanRemove(span) : network.PlanChangeProfile([span], RoadProfileId.Highway));
        string edited = ContentHash(network.Snapshot);
        Require(edited != original, "Representative edit was not effective.");
        Apply("undo", () => network.PlanUndo());
        Require(ContentHash(network.Snapshot) == original, "Undo did not restore original content.");
        Apply("redo", () => network.PlanRedo());
        Require(ContentHash(network.Snapshot) == edited, "Redo did not restore edited content.");
        Apply("undo-restore", () => network.PlanUndo());
        Require(ContentHash(network.Snapshot) == original, "Final undo did not restore original content.");

        void Apply(string operation, Func<RoadEditResult> plan)
        {
            long start = Stopwatch.GetTimestamp();
            RoadEditResult result = plan();
            long planned = Stopwatch.GetTimestamp();
            Require(result.Plan is not null && network.TryCommit(result.Plan), $"{operation} failed: {result.Reason}");
            long committed = Stopwatch.GetTimestamp();
            evidence.Add(new(operation, Stopwatch.GetElapsedTime(start, planned).TotalMilliseconds,
                Stopwatch.GetElapsedTime(planned, committed).TotalMilliseconds, network.Snapshot.NodeCount,
                network.Snapshot.EdgeCount, network.History.UndoCount, network.History.RedoCount));
        }
    }

    private static DatasetCounts Count(RoadSnapshot snapshot)
    {
        var degree = snapshot.Nodes.ToDictionary(node => node.Id, _ => 0);
        var spans = new HashSet<RoadSpanKey>();
        foreach (RoadEdge edge in snapshot.Edges)
        {
            degree[edge.Start]++;
            degree[edge.End]++;
            double length = 0;
            for (int point = 1; point < edge.Points.Count; point++) length += edge.Points[point - 1].DistanceTo(edge.Points[point]);
            double offset = 0;
            for (int point = 1; point < edge.Points.Count; point++)
            {
                RoadPoint a = edge.Points[point - 1], b = edge.Points[point];
                double segment = a.DistanceTo(b);
                int divisions = (int)Math.Ceiling(2 * Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)) / snapshot.Map.CellSizeMetres);
                for (int sample = 0; sample < divisions; sample++)
                {
                    double parameter = (offset + segment * (sample + 0.5) / divisions) / length;
                    RoadGridSpan selected = RoadSpanQuery.Pick(snapshot, new(snapshot.Token, edge.Id, parameter))
                        ?? throw new InvalidOperationException("Interior span sample is not selectable.");
                    spans.Add(selected.Key);
                }
                offset += segment;
            }
        }
        return new(snapshot.EdgeCount, snapshot.NodeCount, snapshot.Edges.Sum(edge => edge.Points.Count), spans.Count,
            snapshot.Nodes.Count(node => snapshot.Map.IsCellCenter(node.Position) && degree[node.Id] >= 3),
            snapshot.Nodes.Count(node => snapshot.Map.IsPrimaryPoint(node.Position) && degree[node.Id] >= 3),
            snapshot.Edges.Count(edge => edge.Points.Count > 2),
            snapshot.Edges.Sum(edge => Math.Max(0, edge.Points.Count - 2)));
    }

    private static string ContentHash(RoadSnapshot snapshot) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        Cell = snapshot.Map.CellSizeMetres,
        Nodes = snapshot.Nodes.Select(node => new { Id = node.Id.Value, node.Position }),
        Edges = snapshot.Edges.Select(edge => new { Id = edge.Id.Value, Start = edge.Start.Value, End = edge.End.Value,
            Profile = edge.Profile.Value, edge.Points })
    })));

    private static T Measure<T>(string name, List<Stage> stages, Func<T> action)
    {
        long start = Stopwatch.GetTimestamp();
        try { return action(); }
        finally { stages.Add(new(name, Stopwatch.GetElapsedTime(start).TotalMilliseconds)); }
    }

    private static void Measure(string name, List<Stage> stages, Action action) => Measure(name, stages, () => { action(); return true; });
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private sealed record Stage(string Name, double Milliseconds);
    private sealed record DatasetCounts(int CanonicalEdges, int StructuralNodes, int ChainPoints, int SelectableGridSpans,
        int CellCenterJunctions, int PrimaryJunctions, int PolylineEdges, int InternalChainPoints);
    private sealed record SurfaceCounts(int Pieces, long Vertices, long Indices);
    private sealed record EditEvidence(string Operation, double PlanMilliseconds, double CommitMilliseconds,
        int Nodes, int Edges, int UndoCount, int RedoCount);

    private sealed class MemorySampler : IDisposable
    {
        private readonly object _sync = new();
        private readonly Process _process = Process.GetCurrentProcess();
        private readonly Timer _timer;
        private long _peakManaged, _peakWorking, _processLifetimePeakWorking;
        private int _samples;
        public MemorySampler()
        {
            Sample();
            _timer = new Timer(_ => Sample(), null, 50, 50);
        }
        public void Sample()
        {
            lock (_sync)
            {
                _process.Refresh();
                _peakManaged = Math.Max(_peakManaged, GC.GetTotalMemory(false));
                _peakWorking = Math.Max(_peakWorking, _process.WorkingSet64);
                _processLifetimePeakWorking = _process.PeakWorkingSet64;
                _samples++;
            }
        }
        public object Evidence()
        {
            lock (_sync) return new { PollIntervalMs = 50, Samples = _samples, SampledPeakManagedBytes = _peakManaged,
                SampledPeakWorkingSetBytes = _peakWorking, ProcessLifetimePeakWorkingSetBytes = _processLifetimePeakWorking,
                Definition = "Process-wide 50 ms samples; managed value includes live and not-yet-collected allocations, not retained dataset bytes. Working set includes runtime/native pages; lifetime peak may include previous datasets. No forced GC; sampling can miss shorter peaks." };
        }
        public void Dispose()
        {
            _timer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _process.Dispose();
        }
    }
}
