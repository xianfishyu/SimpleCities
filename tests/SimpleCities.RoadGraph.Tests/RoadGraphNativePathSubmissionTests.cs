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
        var events = new List<RoadGraphChangedEvent>();
        graph.GraphChanged += events.Add;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([geometry]), RoadType.Street));

        Assert.True(result.Success, $"Submission failed with {result.Error}.");
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.IsType(expectedType, Assert.Single(edge.GeometrySegments));
        Assert.Equal([edge.ID], result.Changes.CreatedEdgeIDs);
        Assert.Equal(2, result.Changes.CreatedNodeIDs.Count);
        RoadGraphChangedEvent change = Assert.Single(events);
        Assert.Same(edge, Assert.Single(change.Delta.Edges).After);
    }

    [Fact]
    public void SubmitPath_ContinuousSegmentsFormOneMaximalEdgeAndReportFinalEntities()
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, new Vector2(5f, 0f));
        var cubic = new CubicBezierRoadGeometrySegment(
            line.End, new Vector2(7f, 2f), new Vector2(9f, -1f), new Vector2(11f, 1f));
        var graph = new RoadGraph();
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([line, cubic]), RoadType.Street));

        Assert.True(result.Success);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Collection(
            edge.GeometrySegments,
            segment => Assert.IsType<LineRoadGeometrySegment>(segment),
            segment => Assert.IsType<CubicBezierRoadGeometrySegment>(segment));
        Assert.DoesNotContain(graph.GetAllNodes(), node => node.Position == line.End);
        Assert.Equal(2, graph.GetAllNodes().Count());
        Assert.Equal([edge.ID], result.Changes.CreatedEdgeIDs);
        Assert.Equal(2, result.Changes.CreatedNodeIDs.Count);
        Assert.Equal(1, changedEvents);
    }

    [Fact]
    public void SubmitPath_StartNearExistingTerminalReusesNode()
    {
        var graph = new RoadGraph();
        RoadPathSubmissionResult existing = graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(10f, 0f),
        ]);
        Assert.True(existing.Success);
        int originalEdgeID = Assert.Single(existing.Changes.CreatedEdgeIDs);
        GraphNode terminal = Assert.Single(graph.GetAllNodes(), node => node.Position == new Vector2(10f, 0f));
        var cubic = new CubicBezierRoadGeometrySegment(
            new Vector2(10.25f, 0f), new Vector2(12f, 3f),
            new Vector2(16f, 3f), new Vector2(20f, 1f));

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([cubic]), RoadType.Street));

        Assert.True(result.Success, $"Submission failed with {result.Error}.");
        GraphEdge created = Assert.Single(graph.GetAllEdges());
        Assert.Equal(originalEdgeID, created.ID);
        Assert.DoesNotContain(graph.GetAllNodes(), node => node.ID == terminal.ID);
        Assert.Single(result.Changes.CreatedNodeIDs);
        Assert.Single(result.Changes.RemovedNodeIDs);
        Assert.Empty(result.Changes.CreatedEdgeIDs);
        Assert.Empty(result.Changes.RemovedEdgeIDs);
        Assert.IsType<LineRoadGeometrySegment>(created.GeometrySegments[0]);
        var snapped = Assert.IsType<CubicBezierRoadGeometrySegment>(created.GeometrySegments[1]);
        Assert.Equal(terminal.Position, snapped.Start);
        Assert.Equal(new Vector2(11.75f, 3f), snapped.Control1);

        var restored = new RoadGraph();
        RoadGraphTestCodec.LoadJson(restored, RoadGraphTestCodec.CaptureJson(graph));
        Assert.Equal(CaptureState(graph), CaptureState(restored));
    }

    [Fact]
    public void SubmitPath_DuplicateNativeCurveIsRejectedWithoutSideEffects()
    {
        var cubic = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 4f), new Vector2(7f, -2f), new Vector2(10f, 1f));
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([cubic]), RoadType.Street)).Success);
        string stateBefore = CaptureState(graph);
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([cubic]), RoadType.Street));

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.FullyCovered, result.Error);
        Assert.False(result.Changes.HasChanges);
        Assert.Equal(stateBefore, CaptureState(graph));
        Assert.Equal(0, changedEvents);
    }

    [Fact]
    public void SubmitPath_CurveAndItsEndpointChordFormOneRootedSelfLoop()
    {
        var cubic = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 6f), new Vector2(8f, 6f), new Vector2(10f, 0f));
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([cubic]), RoadType.Street)).Success);

        RoadPathSubmissionResult chordResult = graph.SubmitPath(new RoadBuildRequest(new RoadPath([
            new LineRoadGeometrySegment(cubic.Start, cubic.End),
        ]), RoadType.Street));

        Assert.True(chordResult.Success);
        GraphEdge loop = Assert.Single(graph.GetAllEdges());
        Assert.Equal(loop.NodeA, loop.NodeB);
        Assert.Equal(2, loop.GeometrySegments.Count);
        Assert.Contains(loop.GeometrySegments, segment => segment is CubicBezierRoadGeometrySegment);
        Assert.Contains(loop.GeometrySegments, segment => segment is LineRoadGeometrySegment);
        GraphNode seam = Assert.Single(graph.GetAllNodes());
        Assert.Equal(2, seam.IncidenceCount);
        graph.AssertInvariants();
    }

    [Fact]
    public void SubmitPath_NativeCurveSurvivesRoadGraphPersistence()
    {
        var rational = new RationalQuadraticRoadGeometrySegment(
            Vector2.Zero, 1f, new Vector2(3f, 5f), 0.6f, new Vector2(8f, 1f), 1.2f);
        var source = new RoadGraph();
        Assert.True(source.SubmitPath(new RoadBuildRequest(new RoadPath([rational]), RoadType.Street)).Success);
        var restored = new RoadGraph();

        RoadGraphTestCodec.LoadJson(restored, RoadGraphTestCodec.CaptureJson(source));

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
        Assert.True(graph.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(10f, 0f)]).Success);

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
    }

    private static void AssertRejected(
        RoadGraph graph,
        RoadPath? path,
        RoadPathSubmissionError expectedError)
    {
        string stateBefore = CaptureState(graph);
        int changedEvents = 0;
        graph.GraphChanged += OnChanged;

        RoadBuildRequest? request = path is null
            ? null
            : new RoadBuildRequest(path, RoadType.Street);
        RoadPathSubmissionResult result = graph.SubmitPath(request);

        graph.GraphChanged -= OnChanged;
        Assert.False(result.Success);
        Assert.Equal(expectedError, result.Error);
        Assert.False(result.Changes.HasChanges);
        Assert.Equal(stateBefore, CaptureState(graph));
        Assert.Equal(0, changedEvents);
        return;

        void OnChanged(RoadGraphChangedEvent _) => changedEvents++;
    }

    private static string CaptureState(RoadGraph graph) => RoadGraphTestCodec.CaptureJson(graph);

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

        public override RoadGeometrySegment Reverse() =>
            throw new NotSupportedException();
    }
}
