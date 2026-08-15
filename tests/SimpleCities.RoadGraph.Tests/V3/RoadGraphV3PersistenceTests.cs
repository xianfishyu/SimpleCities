using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3PersistenceTests
{
    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(
            a,
            b,
            [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))],
            RoadType.Street,
            out revision,
            out _);

        string json = RoadGraphV3Persistence.Serialize(revision);
        RoadGraphV3PersistenceResult result = RoadGraphV3Persistence.Deserialize(json, RoadGraphCapacity.Default);

        Assert.True(result.Success, result.Error);
        Assert.Equal(revision.Nodes.Count, result.Revision!.Nodes.Count);
        Assert.Equal(revision.Edges.Count, result.Revision.Edges.Count);
        Assert.Equal(RoadType.Street, result.Revision.Edges.Values.Single().RoadType);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsFailure()
    {
        RoadGraphV3PersistenceResult result = RoadGraphV3Persistence.Deserialize(
            "not json",
            RoadGraphCapacity.Default);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Deserialize_RejectsWrongFamily()
    {
        const string json = """
            {
              "formatFamily": "wrong",
              "payloadType": "road-network",
              "schemaVersion": 1,
              "nextID": 1,
              "nodes": [],
              "edges": []
            }
            """;

        RoadGraphV3PersistenceResult result = RoadGraphV3Persistence.Deserialize(json, RoadGraphCapacity.Default);

        Assert.False(result.Success);
        Assert.Equal("InvalidFormatFamily", result.Error);
    }

    [Fact]
    public void Serialize_EmptyRevision_RoundTrips()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);

        string json = RoadGraphV3Persistence.Serialize(revision);
        RoadGraphV3PersistenceResult result = RoadGraphV3Persistence.Deserialize(json, RoadGraphCapacity.Default);

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Revision!.Nodes);
        Assert.Empty(result.Revision.Edges);
    }
}
