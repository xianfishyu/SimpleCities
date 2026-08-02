using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphNativePathSubmissionTests
{
    public static TheoryData<RoadGeometrySegment, Type> NativeGeometryCases => new()
    {
        {
            new LineRoadGeometrySegment(new Vector2(0f, 0f), new Vector2(4f, 0f)),
            typeof(LineRoadGeometrySegment)
        },
        {
            new CubicBezierRoadGeometrySegment(
                new Vector2(10f, 0f), new Vector2(11f, 3f),
                new Vector2(13f, -2f), new Vector2(14f, 1f)),
            typeof(CubicBezierRoadGeometrySegment)
        },
        {
            new CubicHermiteRoadGeometrySegment(
                new Vector2(20f, 0f), new Vector2(3f, 2f),
                new Vector2(24f, 1f), new Vector2(2f, -1f)),
            typeof(CubicHermiteRoadGeometrySegment)
        },
        {
            new CircularArcRoadGeometrySegment(new Vector2(31f, 0f), 2f, Mathf.Pi, Mathf.Pi / 2f),
            typeof(CircularArcRoadGeometrySegment)
        },
        {
            new ClothoidRoadGeometrySegment(new Vector2(40f, 0f), 0.2f, 0f, 0.05f, 5f),
            typeof(ClothoidRoadGeometrySegment)
        },
        {
            new RationalQuadraticRoadGeometrySegment(
                new Vector2(50f, 0f), 1f, new Vector2(52f, 3f), 0.8f,
                new Vector2(55f, 1f), 1.1f),
            typeof(RationalQuadraticRoadGeometrySegment)
        },
    };

    [Theory]
    [MemberData(nameof(NativeGeometryCases))]
    public void SubmitPath_SupportedNativeGeometryCreatesAuthoritativeEdge(
        RoadGeometrySegment geometry,
        Type expectedType)
    {
        var graph = new RoadGraph();
        var added = new List<GraphEdge>();
        graph.EdgeAdded += added.Add;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([geometry]));

        Assert.True(result.Success);
        int groupID = Assert.IsType<int>(result.GroupID);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.IsType(expectedType, Assert.Single(edge.GeometrySegments));
        Assert.Equal(groupID, edge.GroupID);
        Assert.Equal([edge.ID], result.Changes.CreatedEdgeIDs);
        Assert.Equal([groupID], result.Changes.CreatedGroupIDs);
        Assert.Equal(2, result.Changes.CreatedNodeIDs.Count);
        Assert.Equal([edge], added);
    }

    [Fact]
    public void SubmitPath_ContinuousSegmentsShareNodeAndReportCompleteChanges()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(5f, 0f));
        var cubic = new CubicBezierRoadGeometrySegment(
            line.End, new Vector2(7f, 2f), new Vector2(9f, -1f), new Vector2(11f, 1f));
        var graph = new RoadGraph();
        int addedEvents = 0;
        graph.EdgeAdded += _ => addedEvents++;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([line, cubic]));

        Assert.True(result.Success);
        Assert.Equal(2, graph.GetAllEdges().Count());
        Assert.Equal(3, graph.GetAllNodes().Count());
        Assert.Equal(2, graph.GetNode(FindNodeID(graph, line.End))!.EdgeCount);
        Assert.Equal(2, result.Changes.CreatedEdgeIDs.Count);
        Assert.Equal(3, result.Changes.CreatedNodeIDs.Count);
        Assert.Equal(2, addedEvents);
    }

    [Fact]
    public void SubmitPath_StartNearExistingTerminalReusesNode()
    {
        var graph = new RoadGraph();
        Assert.True(graph.AddRoad(Vector2.Zero, new Vector2(10f, 0f), []) >= 0);
        GraphNode terminal = Assert.Single(graph.GetAllNodes(), node => node.Position == new Vector2(10f, 0f));
        var cubic = new CubicBezierRoadGeometrySegment(
            new Vector2(10.25f, 0f), new Vector2(12f, 3f),
            new Vector2(16f, 3f), new Vector2(20f, 1f));

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([cubic]));

        Assert.True(result.Success);
        GraphEdge created = Assert.Single(
            result.Changes.CreatedEdgeIDs.Select(id => Assert.IsType<GraphEdge>(graph.GetEdge(id))));
        Assert.True(created.NodeA == terminal.ID || created.NodeB == terminal.ID);
        Assert.Single(result.Changes.CreatedNodeIDs);
        var snapped = Assert.IsType<CubicBezierRoadGeometrySegment>(Assert.Single(created.GeometrySegments));
        Assert.Equal(terminal.Position, snapped.Start);
        Assert.Equal(new Vector2(11.75f, 3f), snapped.Control1);

        var restored = new RoadGraph();
        restored.RestoreState(SaveJson.Serialize(graph.CaptureState()));
        Assert.Equal(CaptureState(graph), CaptureState(restored));
    }

    [Fact]
    public void SubmitPath_DuplicateNativeCurveIsRejectedWithoutSideEffects()
    {
        var cubic = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 4f), new Vector2(7f, -2f), new Vector2(10f, 1f));
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadPath([cubic])).Success);
        string stateBefore = CaptureState(graph);
        int addedEvents = 0;
        graph.EdgeAdded += _ => addedEvents++;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadPath([cubic]));

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.FullyCovered, result.Error);
        Assert.False(result.Changes.HasChanges);
        Assert.Equal(stateBefore, CaptureState(graph));
        Assert.Equal(0, addedEvents);
    }

    [Fact]
    public void SubmitPath_CurveAndItsEndpointChordRemainDistinctGeometry()
    {
        var cubic = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 6f), new Vector2(8f, 6f), new Vector2(10f, 0f));
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadPath([cubic])).Success);

        RoadPathSubmissionResult chordResult = graph.SubmitPath(new RoadPath([
            new LineRoadGeometrySegment(cubic.Start, cubic.End),
        ]));

        Assert.True(chordResult.Success);
        Assert.Equal(2, graph.GetAllEdges().Count());
        Assert.Contains(graph.GetAllEdges(), edge =>
            Assert.Single(edge.GeometrySegments) is CubicBezierRoadGeometrySegment);
        Assert.Contains(graph.GetAllEdges(), edge =>
            Assert.Single(edge.GeometrySegments) is LineRoadGeometrySegment);
    }

    [Fact]
    public void SubmitPath_NativeCurveSurvivesRoadGraphPersistence()
    {
        var rational = new RationalQuadraticRoadGeometrySegment(
            Vector2.Zero, 1f, new Vector2(3f, 5f), 0.6f, new Vector2(8f, 1f), 1.2f);
        var source = new RoadGraph();
        Assert.True(source.SubmitPath(new RoadPath([rational])).Success);
        var restored = new RoadGraph();

        restored.RestoreState(SaveJson.Serialize(source.CaptureState()));

        var actual = Assert.IsType<RationalQuadraticRoadGeometrySegment>(
            Assert.Single(Assert.Single(restored.GetAllEdges()).GeometrySegments));
        Assert.Equal(rational.Start, actual.Start);
        Assert.Equal(rational.StartWeight, actual.StartWeight);
        Assert.Equal(rational.Control, actual.Control);
        Assert.Equal(rational.ControlWeight, actual.ControlWeight);
        Assert.Equal(rational.End, actual.End);
        Assert.Equal(rational.EndWeight, actual.EndWeight);
    }

    [Fact]
    public void SubmitPath_InvalidRequestsReturnStructuredReasonsWithoutSideEffects()
    {
        var graph = new RoadGraph();
        Assert.True(graph.AddRoad(Vector2.Zero, new Vector2(10f, 0f), []) >= 0);

        AssertRejected(graph, null, RoadPathSubmissionError.MissingPath);
        AssertRejected(graph, new RoadPath([]), RoadPathSubmissionError.NoSegments);
        AssertRejected(graph, new RoadPath([null]), RoadPathSubmissionError.NullGeometrySegment);
        AssertRejected(
            graph,
            new RoadPath([new UnknownRoadGeometrySegment(Vector2.Zero, new Vector2(2f, 0f))]),
            RoadPathSubmissionError.UnknownGeometryType);
        AssertRejected(
            graph,
            new RoadPath([
                new LineRoadGeometrySegment(new Vector2(20f, 0f), new Vector2(22f, 0f)),
                new LineRoadGeometrySegment(new Vector2(23f, 0f), new Vector2(25f, 0f)),
            ]),
            RoadPathSubmissionError.DiscontinuousGeometry);
        AssertRejected(
            graph,
            new RoadPath([new LineRoadGeometrySegment(new Vector2(-0.25f, 0f), new Vector2(0.25f, 0f))]),
            RoadPathSubmissionError.CollapsedByNodeIdentity);
        AssertRejected(
            graph,
            new RoadPath([
                new CircularArcRoadGeometrySegment(
                    new Vector2(12.25f, 0f), 2f, Mathf.Pi, Mathf.Pi / 2f),
            ]),
            RoadPathSubmissionError.UnsupportedEndpointSnap);
        AssertRejected(
            graph,
            new RoadPath([
                new LineRoadGeometrySegment(new Vector2(-0.25f, 0f), new Vector2(20f, 5f)),
                new LineRoadGeometrySegment(new Vector2(20f, 5f), new Vector2(0.25f, 0f)),
            ]),
            RoadPathSubmissionError.RepeatedPoint);
    }

    private static void AssertRejected(
        RoadGraph graph,
        RoadPath? path,
        RoadPathSubmissionError expectedError)
    {
        string stateBefore = CaptureState(graph);
        int addedEvents = 0;
        int removedEvents = 0;
        graph.EdgeAdded += OnAdded;
        graph.EdgeRemoved += OnRemoved;

        RoadPathSubmissionResult result = graph.SubmitPath(path);

        graph.EdgeAdded -= OnAdded;
        graph.EdgeRemoved -= OnRemoved;
        Assert.False(result.Success);
        Assert.Equal(expectedError, result.Error);
        Assert.False(result.Changes.HasChanges);
        Assert.Equal(stateBefore, CaptureState(graph));
        Assert.Equal(0, addedEvents);
        Assert.Equal(0, removedEvents);
        return;

        void OnAdded(GraphEdge _) => addedEvents++;
        void OnRemoved(GraphEdge _) => removedEvents++;
    }

    private static int FindNodeID(RoadGraph graph, Vector2 position) =>
        Assert.Single(graph.GetAllNodes(), node => node.Position == position).ID;

    private static string CaptureState(RoadGraph graph) => SaveJson.Serialize(graph.CaptureState());

    private sealed class UnknownRoadGeometrySegment : RoadGeometrySegment
    {
        public override RoadGeometryKind Kind => RoadGeometryKind.Line;
        public override Vector2 Start { get; }
        public override Vector2 End { get; }
        public override float Length => Start.DistanceTo(End);
        public override Rect2 Bounds => new(Start, End - Start);

        public UnknownRoadGeometrySegment(Vector2 start, Vector2 end)
        {
            Start = start;
            End = end;
        }

        public override Vector2 GetPosition(float parameter) => Start.Lerp(End, parameter);
        public override Vector2 GetUnitTangent(float parameter) => Start.DirectionTo(End);
        public override RoadGeometrySplit Split(float parameter) =>
            new(
                new UnknownRoadGeometrySegment(Start, GetPosition(parameter)),
                new UnknownRoadGeometrySegment(GetPosition(parameter), End));
    }
}
