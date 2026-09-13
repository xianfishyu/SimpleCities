using System.Text;

namespace SimpleCities.RoadCore.Tests;

public sealed class V4LoadValidationTests
{
    // 独立编写的规范载荷；非法输入直接变更原始 JSON，以保留重复字段。
    private const string Valid = """
        {"formatFamily":"simple-cities-v4","payloadType":"road-network","schemaVersion":6,
        "contentRevision":2,"nextNodeId":3,"nextEdgeId":2,"profileCatalogVersion":1,
        "map":{"widthMetres":8000,"heightMetres":8000,"origin":"center","metresPerUnit":1,"grid":"square-eight","cellSizeMetres":100},
        "nodes":[{"id":1,"x":0,"y":0},{"id":2,"x":100,"y":0}],
        "edges":[{"id":1,"startNodeId":1,"endNodeId":2,"profile":"street","points":[{"x":0,"y":0},{"x":100,"y":0}]}]}
        """;

    [Theory]
    [InlineData("\"x\":100,\"y\":0", "\"x\":4100,\"y\":0")]
    [InlineData("\"x\":100,\"y\":0", "\"x\":100,\"y\":200")]
    [InlineData("\"nextEdgeId\":2", "\"nextEdgeId\":1")]
    [InlineData("\"profile\":\"street\"", "\"profile\":\"street\",\"profile\":\"street\"")]
    [InlineData("\"profile\":\"street\"", "\"profile\":\"street\",\"geometryKind\":\"bezier\"")]
    public void InvalidLoadPreservesActiveRoadContentAndBothHistoryDirections(string original, string replacement)
    {
        var network = new RoadNetwork(new MapDefinition(25));
        Build(network, new(-200, 0), new(-100, 0));
        Build(network, new(-200, 100), new(-100, 100));
        Assert.True(network.TryCommit(network.PlanUndo().Plan!));
        RoadSnapshot before = network.Snapshot;
        RoadEditHistory history = network.History;
        byte[] saved = Save(before);
        Assert.Equal(1, history.UndoCount);
        Assert.Equal(1, history.RedoCount);

        using (var validSource = new MemoryStream(Encoding.UTF8.GetBytes(Valid)))
            Assert.Single(RoadCodec.Read(validSource).Edges);
        Assert.Contains(original, Valid);
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(Valid.Replace(original, replacement)));

        Assert.Throws<InvalidDataException>(() => network.PlanLoad(RoadCodec.Read(source)));

        Assert.Same(before, network.Snapshot);
        Assert.Same(history, network.History);
        Assert.Equal(saved, Save(network.Snapshot));
        Assert.Equal(1, network.History.UndoCount);
        Assert.Equal(1, network.History.RedoCount);
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }

    private static byte[] Save(RoadSnapshot snapshot)
    {
        using var stream = new MemoryStream();
        RoadCodec.Write(stream, snapshot);
        return stream.ToArray();
    }
}
