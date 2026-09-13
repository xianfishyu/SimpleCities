using System.Text.Json;
using SimpleCities.RoadCore;

internal static class SurfaceDiagnosis
{
    internal static void Run()
    {
        var commands = new List<int[]>();
        for (int line = -3; line <= 3; line++)
        {
            commands.Add([-3, line, 3, line]);
            commands.Add([line, -3, line, 3]);
        }
        for (int x = -3; x < 3; x++)
        for (int y = -3; y < 3; y++)
        {
            commands.Add([x, y, x + 1, y + 1]);
            commands.Add([x, y + 1, x + 1, y]);
        }
        for (int index = 0; index < 11; index++)
        {
            int x = -12 + index % 6 * 4, y = 5 + index / 6 * 3;
            commands.Add([x, y, x + 1, y]);
            commands.Add([x + 1, y, x + 1, y + 1]);
        }
        commands.Add([-4, -8, 4, -8]);
        var original = Failure(commands, 25);
        Console.WriteLine("ORIGINAL " + JsonSerializer.Serialize(original));
        if (original is null)
        {
            Console.WriteLine("SURFACE_CHECK_PASS: all 240-edge fixture pieces remain convex after binary32 conversion.");
            return;
        }
        bool removed;
        do
        {
            removed = false;
            for (int i = 0; i < commands.Count; i++)
            {
                var candidate = commands.Where((_, index) => index != i).ToList();
                if (Failure(candidate, 25) is null) continue;
                commands = candidate;
                removed = true;
                break;
            }
        } while (removed);
        Console.WriteLine("MINIMAL_COMMANDS " + JsonSerializer.Serialize(commands));
        foreach (int cell in new[] { 25, 50, 100, 200 })
            Console.WriteLine($"CELL_{cell} " + JsonSerializer.Serialize(Failure(commands, cell)));
        Environment.ExitCode = 1;
    }

    private static object? Failure(List<int[]> commands, int cell)
    {
        var network = new RoadNetwork(new MapDefinition(cell));
        foreach (int[] command in commands)
        {
            var result = network.PlanBuild(new(network.Snapshot.Token,
                new(command[0] * cell, command[1] * cell), new(command[2] * cell, command[3] * cell), RoadProfileId.Street));
            if (result.Plan is null || !network.TryCommit(result.Plan)) return null;
        }
        var surface = RoadPresentation.Prepare(network.Snapshot.Nodes, network.Snapshot.Edges);
        if (surface is null) return null;
        foreach (RoadSurfacePiece piece in surface.Pieces)
        {
            var points = piece.Corners.Select(point => new RoadPoint((float)point.X, (float)point.Y)).ToArray();
            double winding = 0;
            var crosses = new List<double>();
            bool invalid = false;
            for (int i = 0; i < points.Length; i++)
            {
                RoadPoint a = points[i], b = points[(i + 1) % points.Length], c = points[(i + 2) % points.Length];
                double cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                crosses.Add(cross);
                if (cross == 0 || (winding != 0 && Math.Sign(cross) != winding)) invalid = true;
                winding = Math.Sign(cross);
            }
            if (invalid) return new
            {
                Cell = cell, network.Snapshot.EdgeCount, Edge = piece.Edge.Id.Value,
                Junction = piece.JunctionNode?.Value, piece.StartParameter, piece.EndParameter,
                SourcePoints = piece.Corners.Select(point => new[] { point.X, point.Y }),
                Converted = points.Select(point => new[] { point.X, point.Y }), Crosses = crosses
            };
        }
        return null;
    }
}
