using Godot;
using SimpleCities.Tests;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class RoadGraphRoadTypeV3Tests
{
    public static TheoryData<RoadGeometrySegment> NativeGeometryCases => new()
    {
        new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
        new CubicBezierRoadGeometrySegment(
            new Vector2(20f, 0f), new Vector2(22f, 6f),
            new Vector2(28f, -4f), new Vector2(30f, 1f)),
        new CubicHermiteRoadGeometrySegment(
            new Vector2(40f, 0f), new Vector2(4f, 5f),
            new Vector2(50f, 1f), new Vector2(3f, -4f)),
        new CircularArcRoadGeometrySegment(new Vector2(60f, 0f), 5f, 0f, Mathf.Pi / 2f),
        new ClothoidRoadGeometrySegment(new Vector2(80f, 0f), 0.2f, 0f, 0.08f, 10f),
        new RationalQuadraticRoadGeometrySegment(
            new Vector2(100f, 0f), 1f, new Vector2(105f, 6f), 0.7f,
            new Vector2(110f, 1f), 1.1f),
    };

    [Fact]
    public void RoadType_HasStableDomainValuesAndRequiresExplicitEdgeConstruction()
    {
        Assert.Equal(0, (int)RoadType.Dirt);
        Assert.Equal(1, (int)RoadType.Street);
        Assert.Equal(2, (int)RoadType.Arterial);
        Assert.Equal(3, (int)RoadType.Highway);
        Assert.Equal(4, Enum.GetValues<RoadType>().Length);

        Assert.All(typeof(GraphEdge).GetConstructors(), constructor =>
            Assert.Equal(typeof(RoadType), constructor.GetParameters()[0].ParameterType));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphEdge(
            (RoadType)99,
            2,
            0,
            1,
            [new LineRoadGeometrySegment(Vector2.Zero, Vector2.Right)]));
        Assert.Null(typeof(RoadPath).GetProperty(nameof(RoadType)));

        System.Reflection.MethodInfo submitPath = Assert.Single(
            typeof(RoadGraph).GetMethods(),
            method => method.Name == nameof(RoadGraph.SubmitPath));
        Assert.Equal(
            [typeof(RoadBuildRequest)],
            submitPath.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(
            RoadPathSubmissionError.MissingPath,
            new RoadGraph().SubmitPath(null).Error);
    }

    [Theory]
    [InlineData(RoadType.Dirt)]
    [InlineData(RoadType.Street)]
    [InlineData(RoadType.Arterial)]
    [InlineData(RoadType.Highway)]
    public void TypedBuild_AssignsEveryRoadTypeToOpenAndClosedEdges(RoadType roadType)
    {
        var openGraph = new RoadGraph();
        RoadPathSubmissionResult open = openGraph.SubmitPath(new RoadBuildRequest(
            new RoadPath([new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f))]),
            roadType));

        Assert.True(open.Success, open.Error.ToString());
        Assert.Equal(roadType, Assert.Single(openGraph.GetAllEdges()).RoadType);
        openGraph.AssertInvariants();

        var closedGraph = new RoadGraph();
        RoadPathSubmissionResult closed = closedGraph.SubmitPolyline(roadType, [
            Vector2.Zero,
            new Vector2(10f, 0f),
            new Vector2(10f, 10f),
            new Vector2(0f, 10f),
            Vector2.Zero,
        ]);

        Assert.True(closed.Success, closed.Error.ToString());
        GraphEdge loop = Assert.Single(closedGraph.GetAllEdges());
        Assert.Equal(roadType, loop.RoadType);
        Assert.Equal(loop.NodeA, loop.NodeB);
        closedGraph.AssertInvariants();
    }

    [Theory]
    [MemberData(nameof(NativeGeometryCases))]
    public void TypedBuild_PreservesTypeAcrossEveryNativeGeometryKind(RoadGeometrySegment geometry)
    {
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(
            new RoadPath([geometry]),
            RoadType.Highway));

        Assert.True(result.Success, result.Error.ToString());
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Equal(RoadType.Highway, edge.RoadType);
        Assert.IsType(geometry.GetType(), Assert.Single(edge.GeometrySegments));
        graph.AssertInvariants();
    }

    [Fact]
    public void Canonicalization_MergesSameTypeContinuationAndPreservesDifferentTypeBoundary()
    {
        var sameType = new RoadGraph();
        Assert.True(sameType.SubmitPolyline(
            RoadType.Dirt,
            [Vector2.Zero, new Vector2(10f, 0f)]).Success);
        int retainedID = Assert.Single(sameType.GetAllEdges()).ID;

        Assert.True(sameType.SubmitPolyline(
            RoadType.Dirt,
            [new Vector2(10f, 0f), new Vector2(20f, 0f)]).Success);

        GraphEdge merged = Assert.Single(sameType.GetAllEdges());
        Assert.Equal(retainedID, merged.ID);
        Assert.Equal(RoadType.Dirt, merged.RoadType);
        Assert.Equal(2, sameType.GetAllNodes().Count());
        sameType.AssertInvariants();

        var differentType = new RoadGraph();
        Assert.True(differentType.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(10f, 0f)]).Success);
        Assert.True(differentType.SubmitPolyline(
            RoadType.Arterial,
            [new Vector2(10f, 0f), new Vector2(20f, 0f)]).Success);

        Assert.Equal(2, differentType.GetAllEdges().Count());
        Assert.Equal(
            [RoadType.Street, RoadType.Arterial],
            differentType.GetAllEdges().OrderBy(edge => edge.ID).Select(edge => edge.RoadType));
        GraphNode boundary = Assert.Single(
            differentType.GetAllNodes(),
            node => node.Position == new Vector2(10f, 0f));
        Assert.Equal(2, boundary.IncidenceCount);
        Assert.Equal(2, boundary.IncidentEdgeCount);
        differentType.AssertInvariants();
    }

    [Fact]
    public void IntersectionSubdivision_InheritsExistingTypeAndAssignsIncomingType()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(
            RoadType.Highway,
            [new Vector2(0f, -10f), new Vector2(0f, 10f)]).Success);

        RoadPathSubmissionResult result = graph.SubmitPolyline(
            RoadType.Dirt,
            [new Vector2(-10f, 0f), new Vector2(10f, 0f)]);

        Assert.True(result.Success, result.Error.ToString());
        Assert.Equal(4, graph.GetAllEdges().Count());
        Assert.Equal(2, graph.GetAllEdges().Count(edge => edge.RoadType == RoadType.Highway));
        Assert.Equal(2, graph.GetAllEdges().Count(edge => edge.RoadType == RoadType.Dirt));
        Assert.Equal(4, Assert.Single(
            graph.GetAllNodes(), node => node.Position == Vector2.Zero).IncidenceCount);
        graph.AssertInvariants();
    }

    [Fact]
    public void FullyCoveredDifferentType_DoesNotImplicitlyUpgradeOrConsumeState()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(10f, 0f)]).Success);
        string stateBefore = RoadGraphTestCodec.CaptureJson(graph);
        int watermarkBefore = graph.NextIDWatermark;
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        RoadPathSubmissionResult result = graph.SubmitPolyline(
            RoadType.Highway,
            [Vector2.Zero, new Vector2(10f, 0f)]);

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.FullyCovered, result.Error);
        Assert.Equal(stateBefore, RoadGraphTestCodec.CaptureJson(graph));
        Assert.Equal(watermarkBefore, graph.NextIDWatermark);
        Assert.Equal(0, changedEvents);
        Assert.Equal(RoadType.Street, Assert.Single(graph.GetAllEdges()).RoadType);
    }

    [Fact]
    public void PartialCoverage_PreservesCoveredTypeAndAssignsOnlyNewGeometry()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(10f, 0f)]).Success);

        RoadPathSubmissionResult result = graph.SubmitPolyline(
            RoadType.Arterial,
            [new Vector2(5f, 0f), new Vector2(15f, 0f)]);

        Assert.True(result.Success, result.Error.ToString());
        GraphEdge[] edges = graph.GetAllEdges().OrderBy(edge => edge.ID).ToArray();
        Assert.Equal(2, edges.Length);
        GraphEdge street = Assert.Single(edges, edge => edge.RoadType == RoadType.Street);
        GraphEdge arterial = Assert.Single(edges, edge => edge.RoadType == RoadType.Arterial);
        Assert.Equal(10f, street.Length, 3);
        Assert.Equal(5f, arterial.Length, 3);
        Assert.DoesNotContain(graph.GetAllNodes(), node => node.Position == new Vector2(5f, 0f));
        Assert.Contains(graph.GetAllNodes(), node => node.Position == new Vector2(10f, 0f));
        graph.AssertInvariants();
    }

    [Fact]
    public void DifferentTypeParallelEdges_PreserveBothSemanticBoundaries()
    {
        Vector2 start = Vector2.Zero;
        Vector2 end = new(20f, 0f);
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [start, end]).Success);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(
            new RoadPath([new CubicBezierRoadGeometrySegment(
                start,
                new Vector2(5f, 8f),
                new Vector2(15f, 8f),
                end)]),
            RoadType.Arterial));

        Assert.True(result.Success, result.Error.ToString());
        Assert.Equal(2, graph.GetAllNodes().Count());
        Assert.Equal(2, graph.GetAllEdges().Count());
        Assert.All(graph.GetAllNodes(), node => Assert.Equal(2, node.IncidenceCount));
        Assert.Equal(
            [RoadType.Street, RoadType.Arterial],
            graph.GetAllEdges().OrderBy(edge => edge.ID).Select(edge => edge.RoadType));
        graph.AssertInvariants();
    }

    [Fact]
    public void InvalidRoadType_IsRejectedBeforeGraphIDOrEventsChange()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(10f, 0f)]).Success);
        string stateBefore = RoadGraphTestCodec.CaptureJson(graph);
        int watermarkBefore = graph.NextIDWatermark;
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(
            new RoadPath([new LineRoadGeometrySegment(
                new Vector2(20f, 0f),
                new Vector2(30f, 0f))]),
            (RoadType)99));

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.InvalidRoadType, result.Error);
        Assert.Equal(stateBefore, RoadGraphTestCodec.CaptureJson(graph));
        Assert.Equal(watermarkBefore, graph.NextIDWatermark);
        Assert.Equal(0, changedEvents);
    }

    [Fact]
    public void InternalSnapshotAndHistory_RoundTripEveryRoadTypeExactly()
    {
        var graph = new RoadGraph();
        RoadType[] expected = Enum.GetValues<RoadType>();
        for (int index = 0; index < expected.Length; index++)
        {
            float y = index * 10f;
            Assert.True(graph.SubmitPolyline(
                expected[index],
                [new Vector2(0f, y), new Vector2(5f, y)]).Success);
        }

        string state = RoadGraphTestCodec.CaptureJson(graph);
        JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(state));
        Assert.Equal("simple-cities-v3", root["formatFamily"]!.GetValue<string>());
        Assert.Equal("road-network", root["payloadType"]!.GetValue<string>());
        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal(
            ["dirt", "street", "arterial", "highway"],
            Assert.IsType<JsonArray>(root["edges"])
                .Select(node => node!["roadType"]!.GetValue<string>()));

        var restored = new RoadGraph();
        RoadGraphTestCodec.LoadJson(restored, state);
        Assert.Equal(
            expected,
            restored.GetAllEdges().OrderBy(edge => edge.ID).Select(edge => edge.RoadType));
        Assert.Equal(state, RoadGraphTestCodec.CaptureJson(restored));

        var historyGraph = new RoadGraph();
        using var history = new RoadEditHistory(historyGraph);
        Assert.True(history.Execute(() => historyGraph.SubmitPolyline(
            RoadType.Highway,
            [Vector2.Zero, new Vector2(10f, 0f)]).Success));
        Assert.Equal(RoadType.Highway, Assert.Single(historyGraph.GetAllEdges()).RoadType);
        Assert.True(history.Undo());
        Assert.Empty(historyGraph.GetAllEdges());
        Assert.True(history.Redo());
        Assert.Equal(RoadType.Highway, Assert.Single(historyGraph.GetAllEdges()).RoadType);
    }

    [Fact]
    public void InternalSnapshot_PreservesSemanticBoundaryAndRejectsSameTypePseudoBoundary()
    {
        var source = new RoadGraph();
        Assert.True(source.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(10f, 0f)]).Success);
        Assert.True(source.SubmitPolyline(
            RoadType.Arterial,
            [new Vector2(10f, 0f), new Vector2(20f, 0f)]).Success);
        string state = RoadGraphTestCodec.CaptureJson(source);

        var restored = new RoadGraph();
        RoadGraphTestCodec.LoadJson(restored, state);

        Assert.Equal(state, RoadGraphTestCodec.CaptureJson(restored));
        Assert.Equal(2, restored.GetAllEdges().Count());
        Assert.Contains(restored.GetAllNodes(), node => node.Position == new Vector2(10f, 0f));
        restored.AssertInvariants();

        JsonObject invalid = Assert.IsType<JsonObject>(JsonNode.Parse(state));
        JsonArray edges = Assert.IsType<JsonArray>(invalid["edges"]);
        Assert.IsType<JsonObject>(edges[1])["roadType"] = "street";
        Assert.Throws<JsonException>(() =>
            RoadGraphTestCodec.LoadJson(new RoadGraph(), invalid.ToJsonString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Street")]
    [InlineData("STREET")]
    [InlineData("future")]
    public void InternalSnapshot_RejectsMissingOrInvalidRoadTypeWithoutMutation(string? token)
    {
        var source = new RoadGraph();
        Assert.True(source.SubmitPolyline(
            RoadType.Street,
            [Vector2.Zero, new Vector2(10f, 0f)]).Success);
        JsonObject invalid = Assert.IsType<JsonObject>(JsonNode.Parse(
            RoadGraphTestCodec.CaptureJson(source)));
        JsonObject edge = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(invalid["edges"])[0]);
        if (token is null)
            edge.Remove("roadType");
        else
            edge["roadType"] = token;

        var target = new RoadGraph();
        Assert.True(target.SubmitPolyline(
            RoadType.Dirt,
            [new Vector2(20f, 0f), new Vector2(30f, 0f)]).Success);
        string stateBefore = RoadGraphTestCodec.CaptureJson(target);
        int changedEvents = 0;
        target.GraphChanged += _ => changedEvents++;

        Assert.Throws<JsonException>(() => RoadGraphTestCodec.LoadJson(target, invalid.ToJsonString()));
        Assert.Equal(stateBefore, RoadGraphTestCodec.CaptureJson(target));
        Assert.Equal(0, changedEvents);
    }
}
