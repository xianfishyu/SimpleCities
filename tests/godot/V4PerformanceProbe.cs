using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SimpleCities.RoadCore;

// Debug-only fixture setup. Every road uses the public planner, then a strict codec roundtrip.
public partial class V4MapScene
{
    public bool PerformanceFixtureBusy { get; private set; }
    public string PerformanceFixtureError { get; private set; } = "";
    private Godot.Collections.Dictionary _performanceFixture = new();
    public Godot.Collections.Dictionary GetPerformanceFixture() => _performanceFixture;

    public async void PreparePerformanceFixture(int cell)
    {
        if (PerformanceFixtureBusy || !CreateMap(cell)) return;
        PerformanceFixtureBusy = true;
        PerformanceFixtureError = "";
        try
        {
            PreparedRoadState prepared = await Task.Run(() => BuildPerformanceFixture(cell));
            if (!IsSceneAlive) return;
            InstallPerformanceFixture(prepared);
        }
        catch (Exception error) { PerformanceFixtureError = error.Message; }
        finally { PerformanceFixtureBusy = false; }
    }

    public async void LoadPerformanceFixture(string resourcePath)
    {
        if (PerformanceFixtureBusy || IsBuildBusy) return;
        PerformanceFixtureBusy = true;
        PerformanceFixtureError = "";
        string path = ProjectSettings.GlobalizePath(resourcePath);
        try
        {
            PreparedRoadState prepared = await Task.Run(() =>
            {
                using var source = File.OpenRead(path);
                return RoadCodec.Read(source);
            });
            if (!IsSceneAlive) return;
            if (!CreateMap(prepared.Map.CellSizeMetres)) throw new InvalidOperationException("Fixture map creation was refused.");
            InstallPerformanceFixture(prepared);
        }
        catch (Exception error) { PerformanceFixtureError = error.Message; }
        finally { PerformanceFixtureBusy = false; }
    }

    private void InstallPerformanceFixture(PreparedRoadState prepared)
    {
        RoadPlan plan = _roads!.Network.PlanLoad(prepared);
        _view.ShowNewMap(plan.Target);
        if (!_roads.Network.TryCommit(plan)) throw new InvalidOperationException("Fixture load became stale.");
        ResetDisplayRecoveryReferences();
        UpdateMapInfo();
        RoadSnapshot snapshot = _roads.Network.Snapshot;
        RoadSurfaceData surface = RoadPresentation.Prepare(snapshot.Nodes, snapshot.Edges)
            ?? throw new InvalidOperationException("Fixture surface is missing.");
        _performanceFixture = new()
        {
            ["cell"] = snapshot.Map.CellSizeMetres, ["edges"] = snapshot.EdgeCount, ["nodes"] = snapshot.NodeCount,
            ["points"] = snapshot.Edges.Sum(edge => edge.Points.Count),
            ["surfacePieces"] = surface.Pieces.Count,
            ["vertices"] = surface.Pieces.Sum(piece => piece.Corners.Count),
            ["indices"] = surface.Pieces.Sum(piece => (piece.Corners.Count - 2) * 3),
            ["historyEntries"] = _roads.Network.History.UndoCount,
        };
    }

    private static PreparedRoadState BuildPerformanceFixture(int cell)
    {
        var network = new RoadNetwork(new MapDefinition(cell));
        void Build(int x1, int y1, int x2, int y2)
        {
            RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token,
                new(x1 * cell, y1 * cell), new(x2 * cell, y2 * cell), RoadProfileId.Street));
            if (result.Plan is null || !network.TryCommit(result.Plan)) throw new InvalidOperationException(result.Reason);
        }
        for (int line = -3; line <= 3; line++)
        {
            Build(-3, line, 3, line);
            Build(line, -3, line, 3);
        }
        for (int x = -3; x < 3; x++)
            for (int y = -3; y < 3; y++)
            {
                Build(x, y, x + 1, y + 1);
                Build(x, y + 1, x + 1, y);
            }
        for (int index = 0; index < 11; index++)
        {
            int x = -12 + index % 6 * 4, y = 5 + index / 6 * 3;
            Build(x, y, x + 1, y);
            Build(x + 1, y, x + 1, y + 1);
        }
        Build(-4, -8, 4, -8);
        if (network.Snapshot.EdgeCount != 240) throw new InvalidOperationException("Unexpected canonical fixture edge count.");
        using var bytes = new MemoryStream();
        RoadCodec.Write(bytes, network.Snapshot);
        bytes.Position = 0;
        return RoadCodec.Read(bytes);
    }
}
