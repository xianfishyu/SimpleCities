using System.Text;
using System.Text.Json.Nodes;

namespace SimpleCities.RoadCore.Tests;

public sealed class PrimaryJunctionCodecTests
{
    [Fact]
    public void MainGridCross_RoundTripsCanonicalConnectivityWithoutDuplicatingJunctions()
    {
        var network = new RoadNetwork();
        Build(network, new(-200, 0), new(200, 0));
        Build(network, new(0, -200), new(0, 200));
        byte[] saved = Save(network);
        Assert.Equal(6, JsonNode.Parse(saved)!["schemaVersion"]!.GetValue<int>());

        RoadSnapshot before = network.Snapshot;
        Assert.Equal(5, before.NodeCount);
        Assert.Equal(4, before.EdgeCount);
        RoadNode junction = Assert.Single(before.Nodes, node => node.Position == new RoadPoint(0, 0));
        Assert.Equal(4, before.Edges.Count(edge => edge.Start == junction.Id || edge.End == junction.Id));
        RoadJunctionReadModel junctionBefore = RoadJunctionQuery.Read(before, junction.Id)!;

        var restored = new RoadNetwork();
        for (int read = 0; read < 2; read++)
        {
            using var source = new MemoryStream(saved);
            Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(source))));
            Assert.Equal(saved, Save(restored));
            Assert.Equal(before.Nodes, restored.Snapshot.Nodes);
            Assert.Equal(before.NextNodeId, restored.Snapshot.NextNodeId);
            Assert.Equal(before.NextEdgeId, restored.Snapshot.NextEdgeId);
            RoadJunctionReadModel junctionAfter = RoadJunctionQuery.Read(restored.Snapshot, junction.Id)!;
            Assert.Equal(restored.Snapshot.Token, junctionAfter.Source);
            Assert.Equal(junctionBefore.Node, junctionAfter.Node);
            Assert.Equal(junctionBefore.Incidences, junctionAfter.Incidences);
            Assert.Equal(junctionBefore.Turns, junctionAfter.Turns);
            foreach (RoadEdge edge in before.Edges)
            {
                RoadEdge reloaded = Assert.Single(restored.Snapshot.Edges, item => item.Id == edge.Id);
                Assert.Equal(edge.Start, reloaded.Start);
                Assert.Equal(edge.End, reloaded.End);
                Assert.Equal(edge.Profile, reloaded.Profile);
                Assert.Equal(edge.Points, reloaded.Points);
                Assert.Equal(before.Resolve(new(before.Token, edge.Id, 0.25)),
                    restored.Snapshot.Resolve(new(restored.Snapshot.Token, edge.Id, 0.25)));
                Assert.Null(restored.Snapshot.Resolve(new(before.Token, edge.Id, 0.25)));
            }
        }
    }

    [Fact]
    public void ReloadedTJunction_ReusesItsNodeForDiagonalBranches()
    {
        var network = new RoadNetwork();
        Build(network, new(-200, 0), new(200, 0));
        Build(network, new(0, 0), new(0, 200), RoadProfileId.Dirt);
        RoadNode junction = Assert.Single(network.Snapshot.Nodes, node => node.Position == new RoadPoint(0, 0));
        Assert.Equal(3, RoadJunctionQuery.Read(network.Snapshot, junction.Id)!.Incidences.Count);

        var restored = new RoadNetwork();
        using (var source = new MemoryStream(Save(network)))
            Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(source))));
        Build(restored, new(0, 0), new(200, 200), RoadProfileId.Highway);

        RoadNode reused = Assert.Single(restored.Snapshot.Nodes, node => node.Position == new RoadPoint(0, 0));
        Assert.Equal(junction.Id, reused.Id);
        Assert.Equal(5, restored.Snapshot.NodeCount);
        Assert.Equal(4, restored.Snapshot.EdgeCount);
        RoadJunctionReadModel read = RoadJunctionQuery.Read(restored.Snapshot, junction.Id)!;
        Assert.Equal(4, read.Incidences.Count);
        Assert.Single(read.Incidences, incidence => incidence.Profile == RoadProfileId.Dirt);
        Assert.Single(read.Incidences, incidence => incidence.Profile == RoadProfileId.Highway);
        byte[] saved = Save(restored);
        using (var source = new MemoryStream(saved))
            Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(source))));
        Assert.Equal(saved, Save(restored));
        Assert.Equal(read.Incidences, RoadJunctionQuery.Read(restored.Snapshot, junction.Id)!.Incidences);
        Assert.Equal(read.Turns, RoadJunctionQuery.Read(restored.Snapshot, junction.Id)!.Turns);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PreviousDebugSchemas_AreRejectedWithoutMigrationOrPublication(int version)
    {
        var network = new RoadNetwork();
        Build(network, new(-200, 0), new(200, 0));
        RoadSnapshot before = network.Snapshot;
        JsonNode payload = JsonNode.Parse(Save(network))!;
        payload["schemaVersion"] = version;
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(payload.ToJsonString()));

        Assert.Throws<InvalidDataException>(() => network.PlanLoad(RoadCodec.Read(source)));
        Assert.Same(before, network.Snapshot);
    }

    [Theory]
    [MemberData(nameof(InvalidTopologies))]
    public void NoncanonicalJunctionPayload_IsRejectedWithoutChangingActiveRoad(string caseName, string nodes, string edges)
    {
        var network = new RoadNetwork();
        Build(network, new(-300, -300), new(-100, -300), RoadProfileId.Highway);
        RoadSnapshot before = network.Snapshot;
        byte[] saved = Save(network);
        string payload = $$"""
            {"formatFamily":"simple-cities-v4","payloadType":"road-network","schemaVersion":6,
            "contentRevision":3,"nextNodeId":10,"nextEdgeId":10,"profileCatalogVersion":1,
            "map":{"widthMetres":8000,"heightMetres":8000,"origin":"center","metresPerUnit":1,"grid":"square-eight","cellSizeMetres":100},
            "nodes":{{nodes}},"edges":{{edges}}}
            """;
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        Exception? error = Record.Exception(() => network.PlanLoad(RoadCodec.Read(source)));
        Assert.True(error is InvalidDataException, $"{caseName} must be rejected as invalid road content; actual: {error}");
        Assert.Same(before, network.Snapshot);
        Assert.Equal(saved, Save(network));
    }

    public static IEnumerable<object[]> InvalidTopologies()
    {
        yield return ["cross-without-structural-node",
            """[{"id":1,"x":-200,"y":0},{"id":2,"x":200,"y":0},{"id":3,"x":0,"y":-200},{"id":4,"x":0,"y":200}]""",
            """[{"id":1,"startNodeId":1,"endNodeId":2,"profile":"street","points":[{"x":-200,"y":0},{"x":200,"y":0}]},{"id":2,"startNodeId":3,"endNodeId":4,"profile":"street","points":[{"x":0,"y":-200},{"x":0,"y":200}]}]"""];
        yield return ["t-junction-without-splitting-old-edge",
            """[{"id":1,"x":-200,"y":0},{"id":2,"x":200,"y":0},{"id":3,"x":0,"y":0},{"id":4,"x":0,"y":200}]""",
            """[{"id":1,"startNodeId":1,"endNodeId":2,"profile":"street","points":[{"x":-200,"y":0},{"x":200,"y":0}]},{"id":2,"startNodeId":3,"endNodeId":4,"profile":"dirt","points":[{"x":0,"y":0},{"x":0,"y":200}]}]"""];
        yield return ["unsupported-cell-center-crossing",
            """[{"id":1,"x":-100,"y":0},{"id":2,"x":0,"y":100},{"id":3,"x":-100,"y":100},{"id":4,"x":0,"y":0}]""",
            """[{"id":1,"startNodeId":1,"endNodeId":2,"profile":"street","points":[{"x":-100,"y":0},{"x":0,"y":100}]},{"id":2,"startNodeId":3,"endNodeId":4,"profile":"street","points":[{"x":-100,"y":100},{"x":0,"y":0}]}]"""];
        yield return ["two-identities-for-the-same-position",
            """[{"id":1,"x":-200,"y":0},{"id":2,"x":0,"y":0},{"id":3,"x":0,"y":0},{"id":4,"x":0,"y":200}]""",
            """[{"id":1,"startNodeId":1,"endNodeId":2,"profile":"street","points":[{"x":-200,"y":0},{"x":0,"y":0}]},{"id":2,"startNodeId":3,"endNodeId":4,"profile":"dirt","points":[{"x":0,"y":0},{"x":0,"y":200}]}]"""];
        yield return ["redundant-same-profile-degree-two-node",
            """[{"id":1,"x":-200,"y":0},{"id":2,"x":0,"y":0},{"id":3,"x":0,"y":200}]""",
            """[{"id":1,"startNodeId":1,"endNodeId":2,"profile":"street","points":[{"x":-200,"y":0},{"x":0,"y":0}]},{"id":2,"startNodeId":2,"endNodeId":3,"profile":"street","points":[{"x":0,"y":0},{"x":0,"y":200}]}]"""];
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
