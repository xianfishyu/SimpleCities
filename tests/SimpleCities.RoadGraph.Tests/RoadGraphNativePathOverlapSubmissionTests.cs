using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGraphNativePathOverlapSubmissionTests
{
    public static TheoryData<RoadGeometrySegment, bool> NativeOverlapCases
    {
        get
        {
            RoadGeometrySegment[] geometry =
            [
                new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 1f)),
                new CubicBezierRoadGeometrySegment(
                    new Vector2(20f, 0f), new Vector2(22f, 5f),
                    new Vector2(27f, -3f), new Vector2(30f, 1f)),
                new CubicHermiteRoadGeometrySegment(
                    new Vector2(40f, 0f), new Vector2(6f, 4f),
                    new Vector2(48f, 2f), new Vector2(4f, -3f)),
                new CircularArcRoadGeometrySegment(new Vector2(60f, 0f), 5f, 0f, Mathf.Pi),
                new ClothoidRoadGeometrySegment(
                    new Vector2(75f, 0f), 0.15f, 0f, 0.08f, 8f),
                new RationalQuadraticRoadGeometrySegment(
                    new Vector2(90f, 0f), 1f, new Vector2(94f, 6f), 0.65f,
                    new Vector2(99f, 1f), 1.2f),
            ];
            var cases = new TheoryData<RoadGeometrySegment, bool>();
            foreach (RoadGeometrySegment segment in geometry)
            {
                cases.Add(segment, false);
                cases.Add(segment, true);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(NativeOverlapCases))]
    public void SubmitPath_FullyCoveredNativeSubcurveIsRejectedWithoutSideEffects(
        RoadGeometrySegment full,
        bool reversed)
    {
        RoadGeometrySegment covered = Partial(full, 0.2f, 0.8f);
        if (reversed)
            covered = Reverse(covered);
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([full]), RoadType.Street)).Success);
        string stateBefore = RoadGraphTestCodec.CaptureJson(graph);
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([covered]), RoadType.Street));

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.FullyCovered, result.Error);
        Assert.False(result.Changes.HasChanges);
        Assert.Equal(stateBefore, RoadGraphTestCodec.CaptureJson(graph));
        Assert.Equal(0, changedEvents);
    }

    [Theory]
    [MemberData(nameof(NativeOverlapCases))]
    public void SubmitPath_PartialNativeOverlapCreatesOnlyUncoveredEnds(
        RoadGeometrySegment full,
        bool reversed)
    {
        RoadGeometrySegment existing = Partial(full, 0.2f, 0.8f);
        if (reversed)
            existing = Reverse(existing);
        var graph = new RoadGraph();
        RoadPathSubmissionResult existingResult = graph.SubmitPath(new RoadBuildRequest(new RoadPath([existing]), RoadType.Street));
        int existingEdgeID = Assert.Single(existingResult.Changes.CreatedEdgeIDs);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([full]), RoadType.Street));

        Assert.True(result.Success);
        Assert.Empty(result.Changes.RemovedEdgeIDs);
        Assert.Empty(result.Changes.CreatedEdgeIDs);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Equal(existingEdgeID, edge.ID);
        Assert.All(edge.GeometrySegments, segment => Assert.Equal(full.GetType(), segment.GetType()));

        Vector2 overlapStart = full.GetPosition(0.2f);
        Vector2 overlapEnd = full.GetPosition(0.8f);
        Assert.DoesNotContain(graph.GetAllNodes(), node =>
            node.Position.DistanceTo(overlapStart) <= 2e-3f);
        Assert.DoesNotContain(graph.GetAllNodes(), node =>
            node.Position.DistanceTo(overlapEnd) <= 2e-3f);
        Assert.Equal(2, graph.GetAllNodes().Count());

        string state = RoadGraphTestCodec.CaptureJson(graph);
        var restored = new RoadGraph();
        RoadGraphTestCodec.LoadJson(restored, state);
        Assert.Equal(state, RoadGraphTestCodec.CaptureJson(restored));
    }

    [Fact]
    public void SubmitPath_OverlapUnionCoversWholeCurveWithoutTopologyChurn()
    {
        var full = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 5f), new Vector2(7f, -3f), new Vector2(10f, 1f));
        RoadGeometrySplit halves = full.Split(0.5f);
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([halves.Before]), RoadType.Street)).Success);
        Assert.True(graph.SubmitPath(new RoadBuildRequest(new RoadPath([halves.After]), RoadType.Street)).Success);
        string stateBefore = RoadGraphTestCodec.CaptureJson(graph);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([full]), RoadType.Street));

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.FullyCovered, result.Error);
        Assert.Equal(stateBefore, RoadGraphTestCodec.CaptureJson(graph));
    }

    [Fact]
    public void SubmitPath_CoveredPieceStillAnchorsFollowingNewGeometryToExistingEdge()
    {
        var full = new CubicBezierRoadGeometrySegment(
            Vector2.Zero, new Vector2(2f, 5f), new Vector2(7f, -3f), new Vector2(10f, 1f));
        RoadGeometrySegment covered = Partial(full, 0.2f, 0.8f);
        Vector2 tangent = full.GetUnitTangent(0.8f);
        Vector2 normal = new(-tangent.Y, tangent.X);
        var extension = new LineRoadGeometrySegment(covered.End, covered.End + normal * 5f);
        var graph = new RoadGraph();
        RoadPathSubmissionResult existingResult = graph.SubmitPath(new RoadBuildRequest(new RoadPath([full]), RoadType.Street));
        int originalEdgeID = Assert.Single(existingResult.Changes.CreatedEdgeIDs);

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([covered, extension]), RoadType.Street));

        Assert.True(result.Success);
        Assert.Empty(result.Changes.RemovedEdgeIDs);
        Assert.NotNull(graph.GetEdge(originalEdgeID));
        Assert.True(
            graph.GetAllEdges().Count() == 3,
            "Nodes=" + string.Join(
                "; ",
                graph.GetAllNodes().OrderBy(node => node.ID).Select(node =>
                    $"{node.ID}@{node.Position}:inc={node.IncidenceCount}")) +
            " Edges=" + string.Join(
                "; ",
                graph.GetAllEdges().OrderBy(edge => edge.ID).Select(edge =>
                    $"{edge.ID}:{edge.NodeA}->{edge.NodeB}:" +
                    $"{edge.GeometrySegments[0].Start}->{edge.GeometrySegments[^1].End}")));
        GraphNode join = Assert.Single(
            graph.GetAllNodes(), node => node.Position.DistanceTo(covered.End) <= 2e-3f);
        Assert.Equal(3, join.IncidenceCount);
        Assert.Single(graph.GetAllEdges(), edge =>
            edge.GeometrySegments.Count == 1 &&
            edge.GeometrySegments[0] is LineRoadGeometrySegment);
    }

    private static RoadGeometrySegment Partial(
        RoadGeometrySegment geometry,
        float start,
        float end)
    {
        RoadGeometrySegment tail = geometry.Split(start).After;
        return tail.Split((end - start) / (1f - start)).Before;
    }

    private static RoadGeometrySegment Reverse(RoadGeometrySegment geometry) => geometry switch
    {
        LineRoadGeometrySegment curve => new LineRoadGeometrySegment(curve.End, curve.Start),
        CubicBezierRoadGeometrySegment curve => new CubicBezierRoadGeometrySegment(
            curve.End, curve.Control2, curve.Control1, curve.Start),
        CubicHermiteRoadGeometrySegment curve => new CubicHermiteRoadGeometrySegment(
            curve.End, -curve.EndTangent, curve.Start, -curve.StartTangent),
        CircularArcRoadGeometrySegment curve => new CircularArcRoadGeometrySegment(
            curve.Center, curve.Radius, curve.StartAngle + curve.SweepAngle, -curve.SweepAngle),
        ClothoidRoadGeometrySegment curve => new ClothoidRoadGeometrySegment(
            curve.End,
            curve.GetUnitTangent(1f).Angle() + Mathf.Pi,
            -curve.EndCurvature,
            -curve.StartCurvature,
            curve.ArcLength),
        RationalQuadraticRoadGeometrySegment curve => new RationalQuadraticRoadGeometrySegment(
            curve.End, curve.EndWeight,
            curve.Control, curve.ControlWeight,
            curve.Start, curve.StartWeight),
        _ => throw new NotSupportedException(geometry.GetType().Name),
    };
}
