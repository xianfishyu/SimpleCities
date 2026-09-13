using System.Text.Json;

namespace SimpleCities.RoadCore.Tests;

public sealed class TopologyCapacityTests
{
    [Fact]
    public void CodecAcceptsMoreThanTheFormer256EdgeLimit()
    {
        RoadNetwork network = LoadLines(DisconnectedLines(257));
        Assert.Equal(257, network.Snapshot.EdgeCount);
        Assert.Equal(514, network.Snapshot.NodeCount);
    }

    public static IEnumerable<object[]> MissingJunctions()
    {
        yield return [new RoadPoint[][] { [new(-100, 0), new(100, 0)], [new(0, -100), new(0, 100)] }];
        yield return [new RoadPoint[][] { [new(-100, 100), new(100, 100)], [new(0, 0), new(0, 200)] }];
        yield return [new RoadPoint[][] { [new(0, 0), new(25, 25)], [new(0, 25), new(25, 0)] }];
        yield return [new RoadPoint[][] { [new(-4000, -4000), new(4000, 4000)], [new(-4000, 4000), new(4000, -4000)] }];
        yield return [new RoadPoint[][] { [new(0, 0), new(100, 0)], [new(50, 25), new(50, 0)] }];
        yield return [new RoadPoint[][] { [new(0, 0), new(100, 0), new(0, 100), new(100, 100), new(0, 0)] }];
    }

    [Theory]
    [MemberData(nameof(MissingJunctions))]
    public void CodecRejectsUnsplitCrossingsIncludingHalfCellsAndClosedEndpoints(RoadPoint[][] lines)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => LoadLines(lines));
        Assert.Contains("缺少共同结构节点", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CodecRejectsPositiveLengthOverlapAlongAxisOrLongDiagonal(bool diagonal)
    {
        RoadPoint[][] lines = diagonal
            ? [[new(-4000, -4000), new(4000, 4000)], [new(-25, -25), new(25, 25)]]
            : [[new(-4000, 0), new(4000, 0)], [new(-25, 0), new(25, 0)]];
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => LoadLines(lines));
        Assert.Contains("共线重叠", exception.Message);
    }

    [Fact]
    public void CodecAcceptsAdjacentPolylineSegmentsRootedLoopAndMapDiagonal()
    {
        RoadNetwork network = LoadLines([
            [new(-4000, -4000), new(4000, 4000)],
            [new(-100, 100), new(-100, 200), new(0, 200)],
            [new(200, -200), new(200, -100), new(300, -100), new(300, -200), new(200, -200)]
        ]);
        Assert.Equal(3, network.Snapshot.EdgeCount);
        Assert.Single(network.Snapshot.Edges, edge => edge.Start == edge.End);
    }

    [Fact]
    public void PublicBuildSplitsHalfCellCrossingAndCancelledBuildDoesNotPublish()
    {
        RoadNetwork network = LoadLines([[new(0, 0), new(25, 25)]]);
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, new(0, 25), new(25, 0), RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
        Assert.Equal(4, network.Snapshot.EdgeCount);
        Assert.Contains(network.Snapshot.Nodes, node => node.Position == new RoadPoint(12.5, 12.5));
        RoadSnapshot before = network.Snapshot;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => network.PlanBuild(new(before.Token,
            new(-4000, -4000), new(-25, -25), RoadProfileId.Street), cancellation.Token));
        Assert.Same(before, network.Snapshot);
    }

    internal static IEnumerable<RoadPoint[]> DisconnectedLines(int count)
    {
        for (int index = 0; index < count; index++)
        {
            int x = -4000 + index % 160 * 50, y = -4000 + index / 160 * 25;
            yield return [new(x, y), new(x + 25, y)];
        }
    }

    internal static RoadNetwork LoadLines(IEnumerable<RoadPoint[]> lines, int cell = 25)
    {
        var nodes = new List<object>();
        var edges = new List<object>();
        var ids = new Dictionary<RoadPoint, int>();
        int Node(RoadPoint point)
        {
            if (ids.TryGetValue(point, out int id)) return id;
            id = ids.Count + 1;
            ids.Add(point, id);
            nodes.Add(new { id, x = point.X, y = point.Y });
            return id;
        }
        foreach (RoadPoint[] original in lines)
        {
            int start = Node(original[0]), end = Node(original[^1]);
            RoadPoint[] points = start <= end ? original : original.Reverse().ToArray();
            edges.Add(new { id = edges.Count + 1, startNodeId = Math.Min(start, end), endNodeId = Math.Max(start, end),
                profile = "street", points = points.Select(point => new { x = point.X, y = point.Y }) });
        }
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatFamily = "simple-cities-v4", payloadType = "road-network", schemaVersion = 6,
            contentRevision = 1, nextNodeId = nodes.Count + 1, nextEdgeId = edges.Count + 1, profileCatalogVersion = 1,
            map = new { widthMetres = 8000, heightMetres = 8000, origin = "center", metresPerUnit = 1, grid = "square-eight", cellSizeMetres = cell },
            nodes, edges
        });
        using var stream = new MemoryStream(payload);
        var network = new RoadNetwork();
        Assert.True(network.TryCommit(network.PlanLoad(RoadCodec.Read(stream))));
        return network;
    }
}
