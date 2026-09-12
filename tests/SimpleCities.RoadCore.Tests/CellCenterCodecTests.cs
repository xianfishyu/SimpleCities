using System.Text;
using System.Text.Json.Nodes;

namespace SimpleCities.RoadCore.Tests;

public sealed class CellCenterCodecTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void DiagonalCross_RoundTripsOneCellCenterAndItsBranchSources(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        Build(network, new(0, 0), new(cellSize, cellSize));
        Build(network, new(0, cellSize), new(cellSize, 0));
        RoadSnapshot before = network.Snapshot;
        var center = new RoadPoint(cellSize / 2.0, cellSize / 2.0);
        RoadNode junction = Assert.Single(before.Nodes, node => node.Position == center);
        Assert.Equal(5, before.NodeCount);
        Assert.Equal(4, before.EdgeCount);
        RoadJunctionReadModel original = RoadJunctionQuery.Read(before, junction.Id)!;
        Assert.Equal(4, original.Incidences.Count);
        byte[] saved = Save(network);
        Assert.Equal(5, JsonNode.Parse(saved)!["schemaVersion"]!.GetValue<int>());

        var restored = new RoadNetwork();
        for (int read = 0; read < 2; read++)
        {
            using var source = new MemoryStream(saved);
            Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(source))));
            Assert.Equal(saved, Save(restored));
            Assert.Equal(before.Nodes, restored.Snapshot.Nodes);
            Assert.Equal(before.NextNodeId, restored.Snapshot.NextNodeId);
            Assert.Equal(before.NextEdgeId, restored.Snapshot.NextEdgeId);
            RoadJunctionReadModel current = RoadJunctionQuery.Read(restored.Snapshot, junction.Id)!;
            Assert.Equal(restored.Snapshot.Token, current.Source);
            Assert.Equal(original.Incidences, current.Incidences);
            Assert.Equal(original.Turns, current.Turns);
            foreach (RoadEdge edge in before.Edges)
            {
                double parameter = edge.Start == junction.Id ? 0 : 1;
                Assert.Equal(center, restored.Snapshot.Resolve(new(restored.Snapshot.Token, edge.Id, parameter)));
                Assert.Null(restored.Snapshot.Resolve(new(before.Token, edge.Id, parameter)));
            }
        }
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void ReloadedCellCenterTJunction_ReusesItsNodeWhenTheFourthDiagonalJoins(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        var center = new RoadPoint(cellSize / 2.0, cellSize / 2.0);
        Build(network, new(0, 0), new(cellSize, cellSize));
        Build(network, center, new(0, cellSize), RoadProfileId.Dirt);
        RoadNode junction = Assert.Single(network.Snapshot.Nodes, node => node.Position == center);
        Assert.Equal(3, RoadJunctionQuery.Read(network.Snapshot, junction.Id)!.Incidences.Count);

        RoadNetwork restored = Load(Save(network));
        Build(restored, center, new(cellSize, 0), RoadProfileId.Highway);
        Assert.Equal(junction, Assert.Single(restored.Snapshot.Nodes, node => node.Position == center));
        RoadJunctionReadModel connected = RoadJunctionQuery.Read(restored.Snapshot, junction.Id)!;
        Assert.Equal(4, connected.Incidences.Count);
        Assert.Single(connected.Incidences, incidence => incidence.Profile == RoadProfileId.Dirt);
        Assert.Single(connected.Incidences, incidence => incidence.Profile == RoadProfileId.Highway);
        byte[] saved = Save(restored);
        RoadNetwork again = Load(saved);
        Assert.Equal(saved, Save(again));
        Assert.Equal(connected.Incidences, RoadJunctionQuery.Read(again.Snapshot, junction.Id)!.Incidences);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PreviousDebugSchemas_AreRejectedWithoutPublishingOrChangingSavedContent(int version)
    {
        var network = new RoadNetwork(new MapDefinition(25));
        Build(network, new(0, 0), new(25, 25));
        byte[] saved = Save(network);
        JsonNode payload = JsonNode.Parse(saved)!;
        payload["schemaVersion"] = version;
        AssertRejectedWithoutPublication(network, Encoding.UTF8.GetBytes(payload.ToJsonString()));
    }

    [Theory]
    [MemberData(nameof(InvalidCellCenterRoads))]
    public void IllegalCellCenterSegmentsAndFractionalPoints_AreRejectedBeforePublication(
        string caseName, string nodes, string points)
    {
        var network = new RoadNetwork(new MapDefinition(25));
        Build(network, new(-100, -100), new(-25, -100), RoadProfileId.Highway);
        string payload = $$"""
            {"formatFamily":"simple-cities-v4","payloadType":"road-network","schemaVersion":5,
            "contentRevision":2,"nextNodeId":3,"nextEdgeId":2,"profileCatalogVersion":1,
            "map":{"widthMetres":8000,"heightMetres":8000,"origin":"center","metresPerUnit":1,"grid":"square-eight","cellSizeMetres":25},
            "nodes":{{nodes}},"edges":[{"id":1,"startNodeId":1,"endNodeId":2,"profile":"street","points":{{points}}}]}
            """;
        AssertRejectedWithoutPublication(network, Encoding.UTF8.GetBytes(payload), caseName);
    }

    public static IEnumerable<object[]> InvalidCellCenterRoads()
    {
        yield return ["horizontal-between-legal-cell-centers",
            """[{"id":1,"x":12.5,"y":12.5},{"id":2,"x":37.5,"y":12.5}]""",
            """[{"x":12.5,"y":12.5},{"x":37.5,"y":12.5}]"""];
        yield return ["vertical-between-legal-cell-centers",
            """[{"id":1,"x":12.5,"y":12.5},{"id":2,"x":12.5,"y":37.5}]""",
            """[{"x":12.5,"y":12.5},{"x":12.5,"y":37.5}]"""];
        yield return ["quarter-cell-endpoints-on-a-diagonal",
            """[{"id":1,"x":6.25,"y":6.25},{"id":2,"x":31.25,"y":31.25}]""",
            """[{"x":6.25,"y":6.25},{"x":31.25,"y":31.25}]"""];
        yield return ["mixed-integer-and-half-cell-coordinates",
            """[{"id":1,"x":12.5,"y":0},{"id":2,"x":37.5,"y":25}]""",
            """[{"x":12.5,"y":0},{"x":37.5,"y":25}]"""];
        yield return ["horizontal-through-an-interior-cell-center-bend",
            """[{"id":1,"x":0,"y":0},{"id":2,"x":50,"y":25}]""",
            """[{"x":0,"y":0},{"x":12.5,"y":12.5},{"x":37.5,"y":12.5},{"x":50,"y":25}]"""];
        yield return ["quarter-cell-interior-bend-with-legal-primary-endpoints",
            """[{"id":1,"x":0,"y":0},{"id":2,"x":0,"y":25}]""",
            """[{"x":0,"y":0},{"x":6.25,"y":6.25},{"x":6.25,"y":18.75},{"x":0,"y":25}]"""];
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void OverlappingFromAReloadedCellCenter_LeavesItsSavedContentUntouched(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        Build(network, new(0, 0), new(cellSize, cellSize));
        Build(network, new(0, cellSize), new(cellSize, 0));
        RoadNetwork restored = Load(Save(network));
        RoadSnapshot before = restored.Snapshot;
        byte[] saved = Save(restored);

        RoadBuildResult overlap = restored.PlanBuild(new(before.Token,
            new(cellSize / 2.0, cellSize / 2.0), new(-cellSize, -cellSize), RoadProfileId.Highway));

        Assert.Equal(RoadBuildStatus.Rejected, overlap.Status);
        Assert.Null(overlap.Plan);
        Assert.NotEmpty(overlap.Conflicts);
        Assert.Same(before, restored.Snapshot);
        Assert.Equal(saved, Save(restored));
        Assert.Equal(saved, Save(Load(saved)));
    }

    private static void AssertRejectedWithoutPublication(RoadNetwork network, byte[] payload, string? caseName = null)
    {
        RoadSnapshot before = network.Snapshot;
        byte[] saved = Save(network);
        using var source = new MemoryStream(payload);
        Exception? error = Record.Exception(() => network.PlanLoad(RoadCodec.Read(source)));
        Assert.True(error is InvalidDataException, $"{caseName ?? "old-schema"} must reject invalid content; actual: {error}");
        Assert.Same(before, network.Snapshot);
        Assert.Equal(saved, Save(network));
    }

    private static RoadNetwork Load(byte[] saved)
    {
        var network = new RoadNetwork();
        using var source = new MemoryStream(saved);
        Assert.True(network.TryCommit(network.PlanLoad(RoadCodec.Read(source))));
        return network;
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end, RoadProfileId? profile = null)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, profile ?? RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }

    private static byte[] Save(RoadNetwork network)
    {
        using var destination = new MemoryStream();
        RoadCodec.Write(destination, network.Snapshot);
        return destination.ToArray();
    }
}
