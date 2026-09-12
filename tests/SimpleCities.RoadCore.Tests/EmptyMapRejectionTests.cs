using System.Text;
using System.Text.Json;

namespace SimpleCities.RoadCore.Tests;

public sealed class EmptyMapRejectionTests
{
    // Authored independently of the writer: the schema example is the input oracle.
    private const string Valid = """
        {"formatFamily":"simple-cities-v4","payloadType":"road-network","schemaVersion":6,
         "contentRevision":7,"nextNodeId":9,"nextEdgeId":12,"profileCatalogVersion":1,
         "map":{"widthMetres":8000,"heightMetres":8000,"origin":"center","metresPerUnit":1,
                "grid":"square-eight","cellSizeMetres":50},"nodes":[],"edges":[]}
        """;

    [Theory]
    [InlineData("\"schemaVersion\":6", "\"schemaVersion\":1")]
    [InlineData("simple-cities-v4", "simple-cities-v3")]
    [InlineData("\"cellSizeMetres\":50", "\"cellSizeMetres\":75")]
    [InlineData("\"cellSizeMetres\":50", "\"cellSizeMetres\":50.5")]
    [InlineData("\"cellSizeMetres\":50", "\"cellSizeMetres\":50,\"cellSizeMetres\":100")]
    [InlineData("\"schemaVersion\":6", "\"schemaVersion\":6,\"unknown\":0")]
    [InlineData("\"nextNodeId\":9", "\"nextNodeId\":0")]
    [InlineData("\"nextEdgeId\":12", "\"nextEdgeId\":-1")]
    [InlineData("\"profileCatalogVersion\":1", "\"profileCatalogVersion\":2")]
    [InlineData("\"widthMetres\":8000", "\"widthMetres\":8001")]
    [InlineData("\"origin\":\"center\"", "\"origin\":\"corner\"")]
    [InlineData("\"metresPerUnit\":1", "\"metresPerUnit\":100")]
    [InlineData("\"nodes\":[]", "\"nodes\":[{}]")]
    [InlineData("\"edges\":[]", "\"edges\":[{}]")]
    public void InvalidPayload_DoesNotChangeActiveMap(string from, string to)
    {
        var network = new RoadNetwork();
        RoadSnapshot before = network.Snapshot;
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(Valid.Replace(from, to)));
        Assert.Throws<InvalidDataException>(() => network.PlanLoad(RoadCodec.Read(bytes)));
        Assert.Equal(before.Token, network.Snapshot.Token);
        Assert.Equal(100, network.Snapshot.Map.CellSizeMetres);
        Assert.Equal(1, network.Snapshot.NextNodeId);
    }

    [Fact]
    public void LoadedEmptyMap_RetainsPersistedWatermarksAndRejectsForeignPlan()
    {
        var first = new RoadNetwork();
        var second = new RoadNetwork();
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(Valid));
        RoadPlan plan = first.PlanLoad(RoadCodec.Read(bytes));
        Assert.False(second.TryCommit(plan));
        Assert.True(first.TryCommit(plan));
        Assert.Equal(7, first.Snapshot.Token.ContentRevision);
        Assert.Equal(9, first.Snapshot.NextNodeId);
        Assert.Equal(12, first.Snapshot.NextEdgeId);
        Assert.Equal(50, first.Snapshot.Map.CellSizeMetres);
        Assert.Equal(100, second.Snapshot.Map.CellSizeMetres);
    }

    [Fact]
    public void OversizedOrTruncatedPayload_IsRejected()
    {
        using var oversized = new MemoryStream(Encoding.UTF8.GetBytes(Valid + new string(' ', RoadCodec.MaximumPayloadBytes)));
        Assert.Throws<InvalidDataException>(() => RoadCodec.Read(oversized));
        using var truncated = new MemoryStream(Encoding.UTF8.GetBytes(Valid[..^1]));
        Assert.ThrowsAny<JsonException>(() => RoadCodec.Read(truncated));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapDefinition(75));
    }
}
