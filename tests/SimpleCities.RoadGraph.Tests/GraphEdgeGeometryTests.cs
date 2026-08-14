using Godot;

namespace SimpleCities.Tests;

public sealed class GraphEdgeGeometryTests
{
    [Fact]
    public void SubmitPolyline_PolylineIsStoredAsAuthoritativeLineSegments()
    {
        var graph = new RoadGraph();
        Vector2 start = new(-4f, 1f);
        Vector2 waypoint = new(2f, 5f);
        Vector2 end = new(10f, 8f);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [start, waypoint, end]);

        Assert.True(result.Success);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        Assert.Collection(
            edge.GeometrySegments,
            segment => AssertLine(segment, start, waypoint),
            segment => AssertLine(segment, waypoint, end));
        Assert.Equal(start.DistanceTo(waypoint) + waypoint.DistanceTo(end), edge.Length);
        Assert.Equal([waypoint], edge.Points);
    }

    [Fact]
    public void GeometrySegments_CannotBeReplacedThroughThePublicView()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [Vector2.Zero, new Vector2(8f, 0f)]).Success);
        GraphEdge edge = Assert.Single(graph.GetAllEdges());
        var replacement = new LineRoadGeometrySegment(Vector2.Zero, Vector2.One);

        Assert.Throws<NotSupportedException>(() =>
            ((IList<RoadGeometrySegment>)edge.GeometrySegments)[0] = replacement);
        AssertLine(Assert.Single(edge.GeometrySegments), Vector2.Zero, new Vector2(8f, 0f));
    }

    [Fact]
    public void Constructor_PreservesNativeCubicBezierInsteadOfFlatteningControls()
    {
        var cubic = new CubicBezierRoadGeometrySegment(
            Vector2.Zero,
            new Vector2(2f, 6f),
            new Vector2(7f, -3f),
            new Vector2(10f, 2f));
        RoadGeometrySegment[] callerOwnedSegments = [cubic];

        var edge = new GraphEdge(RoadType.Street, 1, 2, 3, callerOwnedSegments);
        callerOwnedSegments[0] = new LineRoadGeometrySegment(Vector2.Zero, Vector2.One);

        var stored = Assert.IsType<CubicBezierRoadGeometrySegment>(Assert.Single(edge.GeometrySegments));
        Assert.Equal(cubic.Control1, stored.Control1);
        Assert.Equal(cubic.Control2, stored.Control2);
        Assert.Equal(cubic.Length, edge.Length);
        Assert.Empty(edge.Points);
    }

    [Fact]
    public void CaptureAndRestore_PreservesPolylineLineSegmentSemantics()
    {
        var source = new RoadGraph();
        Assert.True(source.SubmitPolyline(RoadType.Street, [
            new Vector2(-6f, -2f),
            new Vector2(-2f, 0f),
            new Vector2(8f, 5f),
            new Vector2(14f, 8f),
        ]).Success);
        var restored = new RoadGraph();

        RoadGraphTestCodec.LoadJson(restored, RoadGraphTestCodec.CaptureJson(source));

        GraphEdge sourceEdge = Assert.Single(source.GetAllEdges());
        GraphEdge restoredEdge = Assert.Single(restored.GetAllEdges());
        Assert.Equal(sourceEdge.Points, restoredEdge.Points);
        Assert.Equal(sourceEdge.Length, restoredEdge.Length);
        Assert.Equal(sourceEdge.GeometrySegments.Count, restoredEdge.GeometrySegments.Count);
        for (int i = 0; i < sourceEdge.GeometrySegments.Count; i++)
        {
            var expected = Assert.IsType<LineRoadGeometrySegment>(sourceEdge.GeometrySegments[i]);
            var actual = Assert.IsType<LineRoadGeometrySegment>(restoredEdge.GeometrySegments[i]);
            Assert.Equal(expected.Start, actual.Start);
            Assert.Equal(expected.End, actual.End);
        }
    }

    [Fact]
    public void Constructor_RejectsEmptyOrDiscontinuousGeometry()
    {
        Assert.Throws<ArgumentException>(() =>
            new GraphEdge(RoadType.Street, 1, 2, 3, []));

        RoadGeometrySegment[] discontinuous =
        [
            new LineRoadGeometrySegment(Vector2.Zero, Vector2.One),
            new LineRoadGeometrySegment(Vector2.Right, new Vector2(2f, 0f)),
        ];
        Assert.Throws<ArgumentException>(() =>
            new GraphEdge(RoadType.Street, 1, 2, 3, discontinuous));
    }

    private static void AssertLine(RoadGeometrySegment segment, Vector2 start, Vector2 end)
    {
        var line = Assert.IsType<LineRoadGeometrySegment>(segment);
        Assert.Equal(start, line.Start);
        Assert.Equal(end, line.End);
    }
}
