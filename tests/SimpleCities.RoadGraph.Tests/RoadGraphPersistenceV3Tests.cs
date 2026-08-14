using Godot;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SimpleCities.Tests;

public sealed class RoadGraphPersistenceV3Tests
{
    [Fact]
    public void Writer_ProducesDeterministicUnindentedV3Payload()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(4f, 1f),
            new Vector2(8f, 3f),
        ]).Success);

        string first = RoadGraphTestCodec.CaptureJson(graph);
        string second = RoadGraphTestCodec.CaptureJson(graph);
        JsonObject root = ParseRoot(first);

        Assert.Equal(first, second);
        Assert.DoesNotContain('\n', first);
        Assert.Equal("simple-cities-v3", root["formatFamily"]!.GetValue<string>());
        Assert.Equal("road-network", root["payloadType"]!.GetValue<string>());
        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.NotNull(root["nextID"]);
        Assert.NotNull(root["nodes"]);
        Assert.NotNull(root["edges"]);
        Assert.Null(root["groups"]);
        JsonObject edge = FirstObject(root, "edges");
        Assert.Equal("street", edge["roadType"]!.GetValue<string>());
        Assert.Null(edge["groupID"]);
    }

    [Fact]
    public void Codec_RoundTripsEveryNativeGeometryKindAndRoadType()
    {
        RoadGeometrySegment[] geometry =
        [
            new LineRoadGeometrySegment(new Vector2(0f, 0f), new Vector2(2f, 0f)),
            new CubicBezierRoadGeometrySegment(
                new Vector2(10f, 0f), new Vector2(10.5f, 2f),
                new Vector2(11.5f, -1f), new Vector2(12f, 0f)),
            new CubicHermiteRoadGeometrySegment(
                new Vector2(20f, 0f), new Vector2(2f, 1f),
                new Vector2(22f, 1f), new Vector2(1f, -1f)),
            new CircularArcRoadGeometrySegment(new Vector2(31f, 0f), 1f, Mathf.Pi, Mathf.Pi / 2f),
            new ClothoidRoadGeometrySegment(new Vector2(40f, 0f), 0f, 0f, 0f, 2f),
            new RationalQuadraticRoadGeometrySegment(
                new Vector2(50f, 0f), 1f, new Vector2(51f, 1f), 0.75f,
                new Vector2(52f, 0f), 1f),
        ];
        RoadType[] types =
        [
            RoadType.Dirt,
            RoadType.Street,
            RoadType.Arterial,
            RoadType.Highway,
            RoadType.Dirt,
            RoadType.Street,
        ];
        RoadGraph source = CreateSeparatedGeometryGraph(geometry, types, nextID: 1000);

        string payload = RoadGraphTestCodec.CaptureJson(source);
        var restored = new RoadGraph();
        RoadGraphTestCodec.LoadJson(restored, payload);

        Assert.Equal(payload, RoadGraphTestCodec.CaptureJson(restored));
        Assert.Equal(1000, restored.NextIDWatermark);
        Assert.Collection(
            restored.GetAllEdges().OrderBy(edge => edge.ID),
            edge => Assert.IsType<LineRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)),
            edge => Assert.IsType<CubicBezierRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)),
            edge => Assert.IsType<CubicHermiteRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)),
            edge => Assert.IsType<CircularArcRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)),
            edge => Assert.IsType<ClothoidRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)),
            edge => Assert.IsType<RationalQuadraticRoadGeometrySegment>(Assert.Single(edge.GeometrySegments)));
    }

    [Fact]
    public void Codec_RoundTripsRootedFullTurnSelfLoopAndParallelEdges()
    {
        Vector2 seam = new(10f, 0f);
        var loop = CircularArcRoadGeometrySegment.CreateAnchored(
            Vector2.Zero,
            10f,
            0f,
            Mathf.Tau,
            seam,
            seam,
            0f);
        var topology = new PreparedRoadGraphTopology(
            20,
            [
                new PreparedRoadNode(0, seam),
                new PreparedRoadNode(1, new Vector2(20f, 0f)),
            ],
            [
                new PreparedRoadEdge(RoadType.Street, 2, 0, 0, [loop]),
                new PreparedRoadEdge(
                    RoadType.Dirt,
                    3,
                    0,
                    1,
                    [new LineRoadGeometrySegment(seam, new Vector2(20f, 0f))]),
                new PreparedRoadEdge(
                    RoadType.Highway,
                    4,
                    0,
                    1,
                    [new CubicBezierRoadGeometrySegment(
                        seam,
                        new Vector2(12f, 6f),
                        new Vector2(18f, 6f),
                        new Vector2(20f, 0f))]),
            ]);
        RoadGraph source = RoadGraph.FromPreparedTopology(topology);

        string payload = RoadGraphTestCodec.CaptureJson(source);
        RoadGraph restored = RoadGraphTestCodec.Clone(source);

        Assert.Equal(payload, RoadGraphTestCodec.CaptureJson(restored));
        GraphEdge restoredLoop = Assert.Single(restored.GetAllEdges(), edge => edge.NodeA == edge.NodeB);
        Assert.True(Assert.IsType<CircularArcRoadGeometrySegment>(
            Assert.Single(restoredLoop.GeometrySegments)).IsFullTurn);
        Assert.Equal(2, restored.GetAllEdges().Count(edge => edge.NodeA == 0 && edge.NodeB == 1));
    }

    [Fact]
    public void Load_CommitsOneFullResetWithNewLineageAndExactWatermark()
    {
        RoadGraph source = RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(
            int.MaxValue,
            [new PreparedRoadNode(0, Vector2.Zero), new PreparedRoadNode(1, Vector2.Right)],
            [new PreparedRoadEdge(
                RoadType.Street,
                2,
                0,
                1,
                [new LineRoadGeometrySegment(Vector2.Zero, Vector2.Right)])]));
        var target = new RoadGraph();
        Assert.True(target.SubmitPolyline(
            RoadType.Street,
            Enumerable.Range(0, 8).Select(index => new Vector2(index, index % 2)).ToArray()).Success);
        GraphLineageID oldLineage = target.CaptureRevision().LineageID;
        long oldChangeSequence = target.CaptureRevision().ChangeSequence;
        var events = new List<RoadGraphChangedEvent>();
        target.GraphChanged += events.Add;

        RoadGraphTestCodec.LoadJson(target, RoadGraphTestCodec.CaptureJson(source));

        RoadGraphChangedEvent change = Assert.Single(events);
        Assert.True(change.Changes.IsFullReset);
        Assert.NotEqual(oldLineage, target.CaptureRevision().LineageID);
        Assert.Equal(int.MaxValue, target.NextIDWatermark);
        Assert.Equal(oldChangeSequence + 1, target.CaptureRevision().ChangeSequence);
        Assert.Equal(0, target.CaptureRevision().DomainRevisionID);
    }

    [Fact]
    public void PrepareLoad_PreservesTargetSpatialBucketSize()
    {
        var source = new RoadGraph();
        Assert.True(source.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(128f, 0f)]).Success);
        var target = new RoadGraph(4f);

        RoadGraphRevision prepared = RoadGraphTestCodec.PrepareJson(
            target,
            RoadGraphTestCodec.CaptureJson(source));

        Assert.Equal(4f, prepared.SpatialIndex.BucketSize);
        target.CommitPreparedLoad(prepared);
        Assert.Equal(4f, target.CaptureRevision().SpatialIndex.BucketSize);
        target.AssertInvariants();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(31)]
    public void Reader_HandlesChunkBoundariesAndNonSeekableStreams(int chunkSize)
    {
        RoadGraph source = CreatePopulatedGraph();
        byte[] payload = RoadGraphTestCodec.CaptureBytes(source);
        using var stream = new ChunkedNonSeekableStream(payload, chunkSize);

        RoadGraphRevision prepared = RoadGraphTestCodec.PrepareStream(new RoadGraph(), stream);

        var restored = new RoadGraph();
        restored.CommitPreparedLoad(prepared);
        Assert.Equal(payload, RoadGraphTestCodec.CaptureBytes(restored));
        Assert.Equal(payload.Length, stream.BytesRead);
    }

    [Fact]
    public void Reader_AcceptsArbitraryPropertyOrderWithoutObjectMaterialization()
    {
        const string payload = """
            {"edges":[{"geometry":[{"end":{"y":0,"x":1},"start":{"y":0,"x":0},"kind":"line","version":1}],"roadType":"street","nodeBID":1,"nodeAID":0,"id":2}],"nodes":[{"y":0,"x":0,"id":0},{"y":0,"id":1,"x":1}],"nextID":3,"schemaVersion":1,"payloadType":"road-network","formatFamily":"simple-cities-v3"}
            """;

        RoadGraphRevision prepared = RoadGraphTestCodec.PrepareJson(new RoadGraph(), payload);

        Assert.Single(prepared.Edges);
        Assert.Equal(2, prepared.Nodes.Count);
    }

    [Fact]
    public void Reader_RejectsStringNumberAndDepthBudgetsBeforeCommit()
    {
        const string longNumber = "11111111111111111111111111111111111111111111111111111111111111111";
        string numberPayload = $"{{\"formatFamily\":\"simple-cities-v3\",\"payloadType\":\"road-network\",\"schemaVersion\":1,\"nextID\":{longNumber},\"nodes\":[],\"edges\":[]}}";
        string stringPayload = $"{{\"formatFamily\":\"{new string('x', 5000)}\",\"payloadType\":\"road-network\",\"schemaVersion\":1,\"nextID\":0,\"nodes\":[],\"edges\":[]}}";
        string depthPayload = "[[[[[[[[[[[[[[[[[0]]]]]]]]]]]]]]]]]";

        Assert.Throws<JsonException>(() => RoadGraphTestCodec.PrepareJson(new RoadGraph(), numberPayload));
        Assert.Throws<JsonException>(() => RoadGraphTestCodec.PrepareJson(new RoadGraph(), stringPayload));
        Assert.Throws<JsonException>(() => RoadGraphTestCodec.PrepareJson(new RoadGraph(), depthPayload));
    }

    [Fact]
    public void Reader_EnforcesPerEdgeAndTotalGeometryBudgetsBeforeAddingOverflow()
    {
        var perEdgeCapacity = new RoadGraphCapacity
        {
            MaximumNodes = 2,
            MaximumEdges = 1,
            MaximumGeometrySegments = 2,
            MaximumGeometrySegmentsPerEdge = 1,
        };
        const string twoSegmentEdge = """
            {"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":3,
             "nodes":[{"id":0,"x":0,"y":0},{"id":1,"x":2,"y":1}],
             "edges":[{"id":2,"nodeAID":0,"nodeBID":1,"roadType":"street","geometry":[
               {"version":1,"kind":"line","start":{"x":0,"y":0},"end":{"x":1,"y":1}},
               {"version":1,"kind":"line","start":{"x":1,"y":1},"end":{"x":2,"y":1}}]}]}
            """;
        var totalCapacity = perEdgeCapacity with
        {
            MaximumEdges = 2,
            MaximumNodes = 4,
            MaximumGeometrySegments = 1,
            MaximumGeometrySegmentsPerEdge = 1,
        };
        const string twoEdges = """
            {"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":6,
             "nodes":[{"id":0,"x":0,"y":0},{"id":1,"x":1,"y":0},{"id":2,"x":0,"y":2},{"id":3,"x":1,"y":2}],
             "edges":[
               {"id":4,"nodeAID":0,"nodeBID":1,"roadType":"street","geometry":[{"version":1,"kind":"line","start":{"x":0,"y":0},"end":{"x":1,"y":0}}]},
               {"id":5,"nodeAID":2,"nodeBID":3,"roadType":"street","geometry":[{"version":1,"kind":"line","start":{"x":0,"y":2},"end":{"x":1,"y":2}}]}]}
            """;

        Assert.Throws<JsonException>(() => RoadGraphTestCodec.PrepareJson(new RoadGraph(perEdgeCapacity), twoSegmentEdge));
        Assert.Throws<JsonException>(() => RoadGraphTestCodec.PrepareJson(new RoadGraph(totalCapacity), twoEdges));
    }

    [Fact]
    public void Reader_EnforcesPreparedAllocationBudgetBeforeAddingEntity()
    {
        var capacity = new RoadGraphCapacity
        {
            MaximumNodes = 2,
            MaximumEdges = 1,
            MaximumGeometrySegments = 1,
            MaximumGeometrySegmentsPerEdge = 1,
            MaximumPreparedAllocationBytes = 255,
        };
        const string oneNode = """
            {"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":1,
             "nodes":[{"id":0,"x":0,"y":0}],"edges":[]}
            """;

        Assert.Throws<JsonException>(() => RoadGraphTestCodec.PrepareJson(new RoadGraph(capacity), oneNode));
    }

    [Theory]
    [InlineData("formatFamily", null)]
    [InlineData("formatFamily", "Simple-Cities-V3")]
    [InlineData("formatFamily", "simple-cities-v2")]
    [InlineData("payloadType", null)]
    [InlineData("payloadType", "Road-Network")]
    [InlineData("schemaVersion", null)]
    [InlineData("schemaVersion", "0")]
    [InlineData("schemaVersion", "2")]
    public void Reader_RejectsMissingOrWrongAdmissionTokens(string property, string? replacement)
    {
        var graph = CreatePopulatedGraph();
        JsonObject invalid = CaptureRoot(graph);
        if (replacement is null)
            invalid.Remove(property);
        else if (property == "schemaVersion")
            invalid[property] = int.Parse(replacement);
        else
            invalid[property] = replacement;

        AssertLoadRejectedWithoutMutation(graph, invalid.ToJsonString());
    }

    [Theory]
    [InlineData("{\"formatFamily\":\"simple-cities-v3\",\"formatFamily\":\"simple-cities-v3\",\"payloadType\":\"road-network\",\"schemaVersion\":1,\"nextID\":0,\"nodes\":[],\"edges\":[]}")]
    [InlineData("{\"formatFamily\":\"simple-cities-v3\",\"payloadType\":\"road-network\",\"schemaVersion\":1,\"nextID\":00,\"nodes\":[],\"edges\":[]}")]
    [InlineData("{\"formatFamily\":\"simple-cities-v3\",\"payloadType\":\"road-network\",\"schemaVersion\":1,\"nextID\":0.0,\"nodes\":[],\"edges\":[]}")]
    [InlineData("{\"formatFamily\":\"simple-cities-v3\",\"payloadType\":\"road-network\",\"schemaVersion\":1,\"nextID\":0e0,\"nodes\":[],\"edges\":[]}")]
    public void Reader_RejectsDuplicateFieldsAndNonCanonicalIntegerTokens(string json)
    {
        Assert.ThrowsAny<JsonException>(() => RoadGraphTestCodec.PrepareJson(new RoadGraph(), json));
    }

    [Fact]
    public void Reader_RejectsUnknownLegacyAndCaseVariantFields()
    {
        var graph = CreatePopulatedGraph();
        JsonObject unknownRoot = CaptureRoot(graph);
        unknownRoot["unexpected"] = true;
        AssertLoadRejectedWithoutMutation(graph, unknownRoot.ToJsonString());

        JsonObject groups = CaptureRoot(graph);
        groups["groups"] = new JsonArray();
        AssertLoadRejectedWithoutMutation(graph, groups.ToJsonString());

        JsonObject groupID = CaptureRoot(graph);
        FirstObject(groupID, "edges")["groupID"] = 1;
        AssertLoadRejectedWithoutMutation(graph, groupID.ToJsonString());

        string caseVariant = RoadGraphTestCodec.CaptureJson(graph)
            .Replace("\"formatFamily\"", "\"FormatFamily\"", StringComparison.Ordinal);
        AssertLoadRejectedWithoutMutation(graph, caseVariant);
    }

    [Fact]
    public void Reader_RejectsInvalidRoadTypeReferencesAndEndpointBits()
    {
        var graph = CreatePopulatedGraph();
        JsonObject badType = CaptureRoot(graph);
        FirstObject(badType, "edges")["roadType"] = "Street";
        AssertLoadRejectedWithoutMutation(graph, badType.ToJsonString());

        JsonObject missingNode = CaptureRoot(graph);
        FirstObject(missingNode, "edges")["nodeAID"] = 999;
        AssertLoadRejectedWithoutMutation(graph, missingNode.ToJsonString());

        JsonObject endpointMismatch = CaptureRoot(graph);
        FirstGeometry(endpointMismatch)["start"] = new JsonObject { ["x"] = 0.000001f, ["y"] = 0f };
        AssertLoadRejectedWithoutMutation(graph, endpointMismatch.ToJsonString());
    }

    [Fact]
    public void Reader_RejectsNonCanonicalDirectionMergeAndDegreeTwoBoundary()
    {
        const string reversedEndpointOrder = """
            {"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":3,
             "nodes":[{"id":0,"x":0,"y":0},{"id":1,"x":1,"y":0}],
             "edges":[{"id":2,"nodeAID":1,"nodeBID":0,"roadType":"street","geometry":[
               {"version":1,"kind":"line","start":{"x":1,"y":0},"end":{"x":0,"y":0}}]}]}
            """;
        const string mergeableLines = """
            {"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":3,
             "nodes":[{"id":0,"x":0,"y":0},{"id":1,"x":2,"y":0}],
             "edges":[{"id":2,"nodeAID":0,"nodeBID":1,"roadType":"street","geometry":[
               {"version":1,"kind":"line","start":{"x":0,"y":0},"end":{"x":1,"y":0}},
               {"version":1,"kind":"line","start":{"x":1,"y":0},"end":{"x":2,"y":0}}]}]}
            """;
        const string degreeTwoBoundary = """
            {"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":5,
             "nodes":[{"id":0,"x":0,"y":0},{"id":1,"x":1,"y":1},{"id":2,"x":2,"y":0}],
             "edges":[
               {"id":3,"nodeAID":0,"nodeBID":1,"roadType":"street","geometry":[{"version":1,"kind":"line","start":{"x":0,"y":0},"end":{"x":1,"y":1}}]},
               {"id":4,"nodeAID":1,"nodeBID":2,"roadType":"street","geometry":[{"version":1,"kind":"line","start":{"x":1,"y":1},"end":{"x":2,"y":0}}]}]}
            """;

        Assert.Throws<JsonException>(() => RoadGraphTestCodec.PrepareJson(new RoadGraph(), reversedEndpointOrder));
        Assert.Throws<JsonException>(() => RoadGraphTestCodec.PrepareJson(new RoadGraph(), mergeableLines));
        Assert.Throws<JsonException>(() => RoadGraphTestCodec.PrepareJson(new RoadGraph(), degreeTwoBoundary));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reader_RejectsInternalCrossingAndDuplicateOverlap(bool crossing)
    {
        Vector2 secondStart = crossing ? new Vector2(0f, 2f) : Vector2.Zero;
        Vector2 secondEnd = crossing ? new Vector2(2f, 0f) : new Vector2(2f, 2f);
        var root = new JsonObject
        {
            ["formatFamily"] = "simple-cities-v3",
            ["payloadType"] = "road-network",
            ["schemaVersion"] = 1,
            ["nextID"] = 8,
            ["nodes"] = new JsonArray(
                Node(0, Vector2.Zero),
                Node(1, new Vector2(2f, 2f)),
                Node(2, secondStart),
                Node(3, secondEnd)),
            ["edges"] = new JsonArray(
                LineEdge(4, 0, 1, "street", Vector2.Zero, new Vector2(2f, 2f)),
                LineEdge(5, 2, 3, "highway", secondStart, secondEnd)),
        };

        Assert.Throws<JsonException>(() => RoadGraphTestCodec.PrepareJson(
            new RoadGraph(),
            root.ToJsonString()));
    }

    private static RoadGraph CreateSeparatedGeometryGraph(
        IReadOnlyList<RoadGeometrySegment> geometry,
        IReadOnlyList<RoadType> types,
        int nextID)
    {
        var nodes = new List<PreparedRoadNode>();
        var edges = new List<PreparedRoadEdge>();
        int edgeIDBase = geometry.Count * 2;
        for (int index = 0; index < geometry.Count; index++)
        {
            int nodeAID = index * 2;
            int nodeBID = nodeAID + 1;
            nodes.Add(new PreparedRoadNode(nodeAID, geometry[index].Start));
            nodes.Add(new PreparedRoadNode(nodeBID, geometry[index].End));
            edges.Add(new PreparedRoadEdge(
                types[index], edgeIDBase + index, nodeAID, nodeBID, [geometry[index]]));
        }
        return RoadGraph.FromPreparedTopology(new PreparedRoadGraphTopology(nextID, nodes, edges));
    }

    private static RoadGraph CreatePopulatedGraph()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(8f, 3f)]).Success);
        return graph;
    }

    private static void AssertLoadRejectedWithoutMutation(RoadGraph graph, string invalidJson)
    {
        string before = RoadGraphTestCodec.CaptureJson(graph);
        GraphStateToken tokenBefore = graph.CurrentStateToken;
        int changedCount = 0;
        graph.GraphChanged += _ => changedCount++;

        Assert.Throws<JsonException>(() => RoadGraphTestCodec.PrepareJson(graph, invalidJson));

        Assert.Equal(0, changedCount);
        Assert.Equal(tokenBefore, graph.CurrentStateToken);
        Assert.Equal(before, RoadGraphTestCodec.CaptureJson(graph));
    }

    private static JsonObject CaptureRoot(RoadGraph graph) =>
        ParseRoot(RoadGraphTestCodec.CaptureJson(graph));

    private static JsonObject ParseRoot(string json) =>
        Assert.IsType<JsonObject>(JsonNode.Parse(json));

    private static JsonObject FirstObject(JsonObject root, string arrayName) =>
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(root[arrayName])[0]);

    private static JsonObject FirstGeometry(JsonObject root) =>
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(FirstObject(root, "edges")["geometry"])[0]);

    private static JsonObject Node(int id, Vector2 position) => new()
    {
        ["id"] = id,
        ["x"] = position.X,
        ["y"] = position.Y,
    };

    private static JsonObject LineEdge(
        int id,
        int nodeAID,
        int nodeBID,
        string roadType,
        Vector2 start,
        Vector2 end) => new()
    {
        ["id"] = id,
        ["nodeAID"] = nodeAID,
        ["nodeBID"] = nodeBID,
        ["roadType"] = roadType,
        ["geometry"] = new JsonArray(new JsonObject
        {
            ["version"] = 1,
            ["kind"] = "line",
            ["start"] = new JsonObject { ["x"] = start.X, ["y"] = start.Y },
            ["end"] = new JsonObject { ["x"] = end.X, ["y"] = end.Y },
        }),
    };

    private sealed class ChunkedNonSeekableStream(byte[] data, int chunkSize) : Stream
    {
        private int _position;
        internal int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int available = data.Length - _position;
            int read = Math.Min(Math.Min(count, chunkSize), available);
            if (read <= 0)
                return 0;
            Array.Copy(data, _position, buffer, offset, read);
            _position += read;
            BytesRead += read;
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
