using System.Text;
using System.Text.Json.Nodes;

namespace SimpleCities.RoadCore.Tests;

public sealed class LoopCodecTests
{
    [Theory]
    [InlineData("pure")]
    [InlineData("branch")]
    [InlineData("parallel")]
    [InlineData("cell-center")]
    public void LoopRoundTrip_PreservesSeamIdentitiesEndRolesAndLocations(string shape)
    {
        RoadNetwork network;
        if (shape == "cell-center")
        {
            network = new RoadNetwork(new MapDefinition(25));
            LoopRoadTests.Build(network, new(0, 0), new(25, 25));
            LoopRoadTests.Build(network, new(0, 0), new(25, 0));
            LoopRoadTests.Build(network, new(25, 0), new(12.5, 12.5));
        }
        else
        {
            network = LoopRoadTests.Square();
            if (shape == "branch") LoopRoadTests.Build(network, new(100, 100), new(200, 100));
            if (shape == "parallel") LoopRoadTests.Build(network, new(0, 0), new(100, 100));
        }
        RoadSnapshot original = network.Snapshot;
        byte[] bytes = Save(network);
        Assert.Equal(6, JsonNode.Parse(bytes)!["schemaVersion"]!.GetValue<int>());
        var restored = new RoadNetwork();
        for (int i = 0; i < 2; i++)
        {
            using var stream = new MemoryStream(bytes);
            Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(stream))));
            RoadSnapshot loaded = restored.Snapshot;
            Assert.Equal(bytes, Save(restored));
            Assert.Equal(original.Nodes, loaded.Nodes);
            Assert.Equal(original.NextNodeId, loaded.NextNodeId);
            Assert.Equal(original.NextEdgeId, loaded.NextEdgeId);
            foreach (RoadEdge edge in original.Edges)
            {
                RoadEdge copy = Assert.Single(loaded.Edges, item => item.Id == edge.Id);
                Assert.Equal(edge.Start, copy.Start);
                Assert.Equal(edge.End, copy.End);
                Assert.Equal(edge.Profile, copy.Profile);
                Assert.Equal(edge.Points, copy.Points);
                foreach (double parameter in new[] { 0, 0.25, 0.5, 0.75, 1 })
                    Assert.Equal(original.Resolve(new(original.Token, edge.Id, parameter)), loaded.Resolve(new(loaded.Token, edge.Id, parameter)));
                Assert.Null(loaded.Resolve(new(original.Token, edge.Id, 0.5)));
            }
            foreach (RoadNode node in original.Nodes)
            {
                RoadJunctionReadModel before = RoadJunctionQuery.Read(original, node.Id)!;
                RoadJunctionReadModel after = RoadJunctionQuery.Read(loaded, node.Id)!;
                Assert.Equal(before.Incidences, after.Incidences);
                Assert.Equal(before.Turns, after.Turns);
                Assert.Equal(loaded.Token, after.Source);
            }
        }
    }

    [Theory]
    [InlineData("reverse")]
    [InlineData("missing-close")]
    [InlineData("wrong-seam-position")]
    [InlineData("redundant-degree-two-node")]
    [InlineData("overlapping-return")]
    public void NoncanonicalLoopPayload_IsRejectedBeforePublication(string corruption)
    {
        var network = LoopRoadTests.Square();
        RoadSnapshot before = network.Snapshot;
        byte[] bytes = Save(network);
        JsonNode payload = JsonNode.Parse(bytes)!;
        JsonNode edge = payload["edges"]![0]!;
        var points = (JsonArray)edge["points"]!;
        switch (corruption)
        {
            case "reverse":
                edge["points"] = new JsonArray(points.Reverse().Select(point => point!.DeepClone()).ToArray());
                break;
            case "missing-close":
                points.RemoveAt(points.Count - 1);
                break;
            case "wrong-seam-position":
                payload["nodes"]![0]!["x"] = -100;
                break;
            case "redundant-degree-two-node":
                payload["nodes"] = JsonNode.Parse("""[{"id":1,"x":0,"y":0},{"id":2,"x":100,"y":100}]""");
                payload["edges"] = JsonNode.Parse("""[{"id":1,"startNodeId":1,"endNodeId":2,"profile":"street","points":[{"x":0,"y":0},{"x":0,"y":100},{"x":100,"y":100}]},{"id":2,"startNodeId":1,"endNodeId":2,"profile":"street","points":[{"x":0,"y":0},{"x":100,"y":0},{"x":100,"y":100}]}]""");
                payload["nextEdgeId"] = 3;
                break;
            case "overlapping-return":
                edge["points"] = JsonNode.Parse("""[{"x":0,"y":0},{"x":100,"y":0},{"x":0,"y":0}]""");
                break;
        }
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => network.PlanLoad(RoadCodec.Read(stream)));
        Assert.Same(before, network.Snapshot);
        Assert.Equal(bytes, Save(network));
    }

    [Fact]
    public void LoopAtExhaustedNodeWatermark_RejectsANewBranchWithoutPartialChanges()
    {
        var network = LoopRoadTests.Square();
        JsonNode payload = JsonNode.Parse(Save(network))!;
        payload["nextNodeId"] = long.MaxValue - 1;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload.ToJsonString())))
            Assert.True(network.TryCommit(network.PlanLoad(RoadCodec.Read(stream))));
        RoadSnapshot before = network.Snapshot;
        byte[] saved = Save(network);
        RoadBuildResult result = network.PlanBuild(new(before.Token, new(100, 100), new(200, 100), RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Rejected, result.Status);
        Assert.Contains("耗尽", result.Reason);
        Assert.Null(result.Plan);
        Assert.Same(before, network.Snapshot);
        Assert.Equal(saved, Save(network));
        using var readable = new MemoryStream(saved);
        Assert.Single(RoadCodec.Read(readable).Edges);
    }

    private static byte[] Save(RoadNetwork network)
    {
        using var stream = new MemoryStream();
        RoadCodec.Write(stream, network.Snapshot);
        return stream.ToArray();
    }
}
