using System.Text;

namespace SimpleCities.RoadCore.Tests;

public sealed class PolylineCodecTests
{
    private const string TurningRoad = """
        {"formatFamily":"simple-cities-v4","payloadType":"road-network","schemaVersion":5,
        "contentRevision":3,"nextNodeId":3,"nextEdgeId":2,"profileCatalogVersion":1,
        "map":{"widthMetres":8000,"heightMetres":8000,"origin":"center","metresPerUnit":1,"grid":"square-eight","cellSizeMetres":100},
        "nodes":[{"id":1,"x":0,"y":0},{"id":2,"x":200,"y":300}],
        "edges":[{"id":1,"startNodeId":1,"endNodeId":2,"profile":"street","points":[{"x":0,"y":0},{"x":200,"y":0},{"x":200,"y":300}]}]}
        """;

    [Fact]
    public void TurningRoad_RoundTripsEveryTurnAndCanContinueAfterReload()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(200, 0), RoadProfileId.Street);
        Build(network, new(200, 0), new(200, 300), RoadProfileId.Street);
        byte[] saved = Save(network);
        var restored = Load(saved);

        Assert.Equal(2, restored.Snapshot.NodeCount);
        RoadEdge edge = Assert.Single(restored.Snapshot.Edges);
        Assert.Equal(new[] { new RoadPoint(0, 0), new RoadPoint(200, 0), new RoadPoint(200, 300) }, edge.Points);
        Assert.Equal(saved, Save(restored));
        Build(restored, new(200, 300), new(300, 400), RoadProfileId.Street);
        Assert.Equal(4, Assert.Single(restored.Snapshot.Edges).Points.Count);
        Assert.Equal(new RoadPoint(300, 400), restored.Snapshot.Edges[0].Points[^1]);
    }

    [Fact]
    public void ReverseContinuationAndProfileBoundary_RetainStableContentAfterReload()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(200, 0), RoadProfileId.Street);
        Build(network, new(0, 0), new(-100, 100), RoadProfileId.Street);
        Build(network, new(-100, 100), new(-100, 300), RoadProfileId.Dirt);
        RoadSnapshot before = network.Snapshot;
        byte[] saved = Save(network);
        RoadNetwork restored = Load(saved);

        Assert.Equal(3, restored.Snapshot.NodeCount);
        Assert.Equal(2, restored.Snapshot.EdgeCount);
        RoadEdge street = Assert.Single(restored.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Street);
        RoadEdge dirt = Assert.Single(restored.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Dirt);
        Assert.Equal(new[] { new RoadPoint(200, 0), new RoadPoint(0, 0), new RoadPoint(-100, 100) }, street.Points);
        Assert.Equal(new[] { new RoadPoint(-100, 100), new RoadPoint(-100, 300) }, dirt.Points);
        Assert.Equal(before.NextNodeId, restored.Snapshot.NextNodeId);
        Assert.Equal(before.NextEdgeId, restored.Snapshot.NextEdgeId);
        Assert.Equal(before.Nodes, restored.Snapshot.Nodes);
        Assert.Equal(before.Edges.Select(edge => edge.Id), restored.Snapshot.Edges.Select(edge => edge.Id));
        Assert.Equal(saved, Save(restored));

        Build(restored, new(-100, 300), new(-300, 300), RoadProfileId.Dirt);
        Assert.Equal(3, restored.Snapshot.NodeCount);
        Assert.Equal(2, restored.Snapshot.EdgeCount);
    }

    [Theory]
    [InlineData("\"schemaVersion\":5", "\"schemaVersion\":1")]
    [InlineData("\"schemaVersion\":5", "\"schemaVersion\":2")]
    [InlineData("\"points\":[", "\"unknownPoints\":[")]
    [InlineData("\"x\":200,\"y\":0", "\"x\":200,\"x\":200,\"y\":0")]
    [InlineData("\"x\":200,\"y\":0", "\"x\":200,\"y\":0,\"z\":0")]
    [InlineData("\"x\":200,\"y\":0", "\"x\":200")]
    [InlineData("\"x\":200,\"y\":0", "\"x\":200,\"y\":50")]
    [InlineData("\"x\":200,\"y\":0", "\"x\":0,\"y\":0")]
    [InlineData("\"x\":200,\"y\":0", "\"x\":1e999,\"y\":0")]
    [InlineData("\"startNodeId\":1,\"endNodeId\":2", "\"startNodeId\":2,\"endNodeId\":1")]
    [InlineData("\"x\":200,\"y\":300}", "\"x\":200,\"y\":300,\"extra\":0}")]
    public void InvalidPolylineContent_IsRejectedWithoutChangingPublishedRoad(string original, string replacement)
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(-200, 0), RoadProfileId.Highway);
        RoadSnapshot before = network.Snapshot;
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(TurningRoad.Replace(original, replacement)));

        Assert.Throws<InvalidDataException>(() => network.PlanLoad(RoadCodec.Read(source)));
        Assert.Same(before, network.Snapshot);
        Assert.Equal(RoadProfileId.Highway, Assert.Single(network.Snapshot.Edges).Profile);
    }

    [Fact]
    public void NonSeekablePayload_IsReadInChunksAndCannotExceedByteBudget()
    {
        using var valid = new ChunkedReadStream(Encoding.UTF8.GetBytes(TurningRoad));
        PreparedRoadState prepared = RoadCodec.Read(valid);
        Assert.Equal(3, Assert.Single(prepared.Edges).Points.Count);

        byte[] oversized = Encoding.UTF8.GetBytes(TurningRoad + new string(' ', RoadCodec.MaximumPayloadBytes));
        using var invalid = new ChunkedReadStream(oversized);
        Assert.Throws<InvalidDataException>(() => RoadCodec.Read(invalid));
        Assert.Equal(RoadCodec.MaximumPayloadBytes + 1, invalid.BytesRead);
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end, RoadProfileId profile)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, profile));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }

    private static byte[] Save(RoadNetwork network)
    {
        using var destination = new MemoryStream();
        RoadCodec.Write(destination, network.Snapshot);
        return destination.ToArray();
    }

    private static RoadNetwork Load(byte[] bytes)
    {
        using var source = new MemoryStream(bytes);
        var result = new RoadNetwork();
        Assert.True(result.TryCommit(result.PlanLoad(RoadCodec.Read(source))));
        return result;
    }

    private sealed class ChunkedReadStream(byte[] bytes) : Stream
    {
        public int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int length = Math.Min(Math.Min(count, 37), bytes.Length - BytesRead);
            bytes.AsSpan(BytesRead, length).CopyTo(buffer.AsSpan(offset, length));
            BytesRead += length;
            return length;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
