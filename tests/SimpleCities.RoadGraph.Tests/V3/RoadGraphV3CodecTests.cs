using Godot;
using SimpleCities.Road.V3;
using System.Text.Json;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3CodecTests
{
    [Fact]
    public void Serialize_RoundTripsCanonicalGraph()
    {
        var graph = new RoadCanonicalGraph(
            [
                new RoadCanonicalNode(1, Vector2.Zero),
                new RoadCanonicalNode(2, new Vector2(1f, 0f)),
            ],
            [
                new RoadCanonicalEdge(
                    10,
                    1,
                    2,
                    [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))],
                    "street"),
                new RoadCanonicalEdge(
                    20,
                    2,
                    2,
                    [
                        new LineRoadGeometrySegment(new Vector2(1f, 0f), new Vector2(2f, 0f)),
                        new LineRoadGeometrySegment(new Vector2(2f, 0f), new Vector2(1f, 0f)),
                    ],
                    "dirt"),
            ]);

        string json = RoadGraphV3Codec.Serialize(graph);
        RoadGraphV3CodecResult result = RoadGraphV3Codec.Deserialize(json);

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Graph!.Nodes.Count);
        Assert.Equal(2, result.Graph.Edges.Count);
        Assert.Equal(21, result.NextID);
        Assert.Equal("street", result.Graph.Edges[0].MergeKey);
        Assert.Equal("dirt", result.Graph.Edges[1].MergeKey);
        Assert.True(result.Graph.Edges[1].IsSelfLoop);
    }

    [Fact]
    public void Deserialize_RejectsWrongFamily()
    {
        var data = new RoadGraphV3Data
        {
            FormatFamily = "wrong",
            PayloadType = RoadGraphV3Codec.PayloadType,
            SchemaVersion = RoadGraphV3Codec.SchemaVersion,
            NextID = 1,
            Nodes = [],
            Edges = [],
        };

        RoadGraphV3CodecResult result = RoadGraphV3Codec.Deserialize(JsonSerializer.Serialize(data));

        Assert.False(result.Success);
        Assert.Equal("InvalidFormatFamily", result.Error);
    }

    [Fact]
    public void Deserialize_RejectsWrongSchemaVersion()
    {
        var data = new RoadGraphV3Data
        {
            FormatFamily = RoadGraphV3Codec.FormatFamily,
            PayloadType = RoadGraphV3Codec.PayloadType,
            SchemaVersion = 2,
            NextID = 1,
            Nodes = [],
            Edges = [],
        };

        RoadGraphV3CodecResult result = RoadGraphV3Codec.Deserialize(JsonSerializer.Serialize(data));

        Assert.False(result.Success);
        Assert.Equal("UnsupportedSchemaVersion", result.Error);
    }

    [Fact]
    public void Deserialize_RejectsNextIDNotAboveMax()
    {
        var data = new RoadGraphV3Data
        {
            FormatFamily = RoadGraphV3Codec.FormatFamily,
            PayloadType = RoadGraphV3Codec.PayloadType,
            SchemaVersion = RoadGraphV3Codec.SchemaVersion,
            NextID = 1,
            Nodes =
            [
                new RoadGraphV3NodeData { ID = 1, X = 0f, Y = 0f },
            ],
            Edges = [],
        };

        RoadGraphV3CodecResult result = RoadGraphV3Codec.Deserialize(JsonSerializer.Serialize(data));

        Assert.False(result.Success);
        Assert.Equal("NextIDNotAboveMax", result.Error);
    }

    [Fact]
    public void Deserialize_RejectsMissingEndpoint()
    {
        var data = new RoadGraphV3Data
        {
            FormatFamily = RoadGraphV3Codec.FormatFamily,
            PayloadType = RoadGraphV3Codec.PayloadType,
            SchemaVersion = RoadGraphV3Codec.SchemaVersion,
            NextID = 2,
            Nodes =
            [
                new RoadGraphV3NodeData { ID = 1, X = 0f, Y = 0f },
            ],
            Edges =
            [
                new RoadGraphV3EdgeData
                {
                    ID = 1,
                    NodeAID = 1,
                    NodeBID = 99,
                    RoadType = "street",
                    Geometry = [RoadGeometrySerializer.ToData(new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)))],
                },
            ],
        };

        RoadGraphV3CodecResult result = RoadGraphV3Codec.Deserialize(JsonSerializer.Serialize(data));

        Assert.False(result.Success);
        Assert.Equal("MissingEndpoint", result.Error);
    }

    [Fact]
    public void Deserialize_RejectsInvalidRoadType()
    {
        var data = new RoadGraphV3Data
        {
            FormatFamily = RoadGraphV3Codec.FormatFamily,
            PayloadType = RoadGraphV3Codec.PayloadType,
            SchemaVersion = RoadGraphV3Codec.SchemaVersion,
            NextID = 2,
            Nodes =
            [
                new RoadGraphV3NodeData { ID = 1, X = 0f, Y = 0f },
            ],
            Edges =
            [
                new RoadGraphV3EdgeData
                {
                    ID = 1,
                    NodeAID = 1,
                    NodeBID = 1,
                    RoadType = "boulevard",
                    Geometry = [RoadGeometrySerializer.ToData(new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)))],
                },
            ],
        };

        RoadGraphV3CodecResult result = RoadGraphV3Codec.Deserialize(JsonSerializer.Serialize(data));

        Assert.False(result.Success);
        Assert.Equal("InvalidRoadType", result.Error);
    }

    [Fact]
    public void Deserialize_RejectsEmptyGeometry()
    {
        var data = new RoadGraphV3Data
        {
            FormatFamily = RoadGraphV3Codec.FormatFamily,
            PayloadType = RoadGraphV3Codec.PayloadType,
            SchemaVersion = RoadGraphV3Codec.SchemaVersion,
            NextID = 2,
            Nodes =
            [
                new RoadGraphV3NodeData { ID = 1, X = 0f, Y = 0f },
            ],
            Edges =
            [
                new RoadGraphV3EdgeData
                {
                    ID = 1,
                    NodeAID = 1,
                    NodeBID = 1,
                    RoadType = "street",
                    Geometry = [],
                },
            ],
        };

        RoadGraphV3CodecResult result = RoadGraphV3Codec.Deserialize(JsonSerializer.Serialize(data));

        Assert.False(result.Success);
        Assert.Equal("EmptyGeometry", result.Error);
    }
}
