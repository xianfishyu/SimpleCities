using Godot;

namespace SimpleCities.Tests;

public sealed class RoadGeometryDisplaySamplerTests
{
    [Fact]
    public void LineUsesExactEndpointsWithoutExtraSamples()
    {
        var line = new LineRoadGeometrySegment(new Vector2(-4f, 2f), new Vector2(11f, 9f));

        Vector2[] points = RoadGeometryDisplaySampler.SampleSegment(line);

        Assert.Equal([line.Start, line.End], points);
    }

    [Fact]
    public void EveryNativeCurveStaysWithinDisplayToleranceAndPreservesSourceState()
    {
        const float tolerance = 0.25f;
        foreach (RoadGeometrySegment geometry in CreateNativeCurves())
        {
            string before = RoadGeometrySerializer.Serialize(geometry);

            Vector2[] points = RoadGeometryDisplaySampler.SampleSegment(geometry, tolerance);

            Assert.True(points.Length > 2, $"{geometry.Kind} was reduced to its endpoint chord.");
            Assert.Equal(geometry.Start, points[0]);
            Assert.Equal(geometry.End, points[^1]);
            Assert.All(points, point => Assert.True(point.IsFinite()));
            AssertDenseCurveError(geometry, points, tolerance);
            Assert.Equal(before, RoadGeometrySerializer.Serialize(geometry));
        }
    }

    [Fact]
    public void TighterToleranceOnlyAddsSamplesAndKeepsJoinPointsUnique()
    {
        var first = new CubicBezierRoadGeometrySegment(
            Vector2.Zero,
            new Vector2(0f, 80f),
            new Vector2(100f, 80f),
            new Vector2(100f, 0f));
        var second = new CircularArcRoadGeometrySegment(
            new Vector2(100f, 50f),
            50f,
            -Mathf.Pi / 2f,
            Mathf.Pi / 2f);

        Vector2[] coarse = RoadGeometryDisplaySampler.SampleSegments([first, second], 1f);
        Vector2[] fine = RoadGeometryDisplaySampler.SampleSegments([first, second], 0.1f);

        Assert.True(fine.Length >= coarse.Length);
        Assert.Equal(first.Start, fine[0]);
        Assert.Equal(second.End, fine[^1]);
        Assert.Equal(1, fine.Count(point => point == first.End));
    }

    [Fact]
    public void SamplePathCarriesSourceGeometryAndParameterForEveryVisibleSpan()
    {
        RoadGeometrySegment[] geometries =
        [
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
            new CubicBezierRoadGeometrySegment(
                new Vector2(10f, 0f),
                new Vector2(15f, 10f),
                new Vector2(25f, 10f),
                new Vector2(30f, 0f)),
        ];

        RoadGeometryDisplayPath path = RoadGeometryDisplaySampler.SamplePath(
            geometries,
            tolerance: 0.1f);

        Assert.Equal(path.Points.Length - 1, path.Spans.Length);
        Assert.Equal([0, 1], path.Spans.Select(span => span.GeometryIndex).Distinct());
        for (int index = 0; index < path.Spans.Length; index++)
        {
            RoadGeometryDisplaySpan span = path.Spans[index];
            Assert.InRange(span.ParameterStart, 0f, 1f);
            Assert.InRange(span.ParameterEnd, 0f, 1f);
            Assert.True(span.ParameterStart < span.ParameterEnd);
            Assert.True(
                path.Points[index].DistanceTo(
                    geometries[span.GeometryIndex].GetPosition(span.ParameterStart)) <= 1e-4f);
            Assert.True(
                path.Points[index + 1].DistanceTo(
                    geometries[span.GeometryIndex].GetPosition(span.ParameterEnd)) <= 1e-4f);
        }

        RoadGeometryDisplaySpan first = path.Spans[0];
        RoadGeometryDisplaySpan beforeJoin = path.Spans.Last(span => span.GeometryIndex == 0);
        RoadGeometryDisplaySpan afterJoin = path.Spans.First(span => span.GeometryIndex == 1);
        RoadGeometryDisplaySpan last = path.Spans[^1];
        Assert.Equal(0f, first.ParameterStart);
        Assert.Equal(1f, beforeJoin.ParameterEnd);
        Assert.Equal(0f, afterJoin.ParameterStart);
        Assert.Equal(1f, last.ParameterEnd);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidToleranceIsRejected(float tolerance)
    {
        var line = new LineRoadGeometrySegment(Vector2.Zero, Vector2.Right);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoadGeometryDisplaySampler.SampleSegment(line, tolerance));
    }

    [Fact]
    public void RendererAndPlacementPreviewUseTheSameDisplaySampler()
    {
        string projectRoot = FindProjectRoot();
        string renderer = File.ReadAllText(Path.Combine(projectRoot, "Scripts", "Road", "RoadRenderer.cs"));
        string loadCommit = File.ReadAllText(
            Path.Combine(projectRoot, "Scripts", "Road", "RoadRenderer.LoadCommit.cs"));
        string builder = File.ReadAllText(Path.Combine(projectRoot, "Scripts", "Road", "RoadBuilder.cs"));

        Assert.Contains("new RoadRendererLoadPreparer(settings)", renderer, StringComparison.Ordinal);
        Assert.Contains("preparer.Prepare(", renderer, StringComparison.Ordinal);
        Assert.Contains("RoadGeometryDisplaySampler.SamplePath(", loadCommit, StringComparison.Ordinal);
        Assert.Contains("edge.GeometrySegments", loadCommit, StringComparison.Ordinal);
        Assert.Contains("RoadGeometryDisplaySampler.SampleSegments(draft.Path.Segments", builder, StringComparison.Ordinal);
        Assert.Contains("_edgePoints,", renderer, StringComparison.Ordinal);
        Assert.Contains("_edgeDisplaySpans,", renderer, StringComparison.Ordinal);
        Assert.Contains("reusableEdgePoints.TryGetValue", loadCommit, StringComparison.Ordinal);
        Assert.Contains("reusableEdgeDisplaySpans.TryGetValue", loadCommit, StringComparison.Ordinal);
        Assert.Contains("AppendRoadRibbon", renderer, StringComparison.Ordinal);
        Assert.Contains("Mesh.PrimitiveType.Triangles", renderer, StringComparison.Ordinal);
        Assert.Contains("ScheduleStaticBatchRebuild()", renderer, StringComparison.Ordinal);
        Assert.Contains("Callable.From(FlushScheduledStaticBatchRebuild).CallDeferred()", renderer, StringComparison.Ordinal);
    }

    private static RoadGeometrySegment[] CreateNativeCurves() =>
    [
        new CubicBezierRoadGeometrySegment(
            Vector2.Zero,
            new Vector2(0f, 80f),
            new Vector2(100f, -80f),
            new Vector2(100f, 0f)),
        new CubicHermiteRoadGeometrySegment(
            Vector2.Zero,
            new Vector2(100f, 180f),
            new Vector2(100f, 0f),
            new Vector2(100f, -180f)),
        new CircularArcRoadGeometrySegment(Vector2.Zero, 50f, 0f, Mathf.Pi * 1.5f),
        new ClothoidRoadGeometrySegment(Vector2.Zero, 0f, 0f, 0.03f, 100f),
        new RationalQuadraticRoadGeometrySegment(
            Vector2.Zero, 1f,
            new Vector2(50f, 100f), 0.6f,
            new Vector2(100f, 0f), 1f),
    ];

    private static void AssertDenseCurveError(
        RoadGeometrySegment geometry,
        IReadOnlyList<Vector2> polyline,
        float tolerance)
    {
        float maximumErrorSquared = 0f;
        const int denseSampleCount = 2048;
        for (int sample = 0; sample <= denseSampleCount; sample++)
        {
            Vector2 position = geometry.GetPosition(sample / (float)denseSampleCount);
            float closestSquared = float.MaxValue;
            for (int index = 1; index < polyline.Count; index++)
            {
                closestSquared = Mathf.Min(
                    closestSquared,
                    DistanceSquaredToSegment(position, polyline[index - 1], polyline[index]));
            }
            maximumErrorSquared = Mathf.Max(maximumErrorSquared, closestSquared);
        }

        Assert.InRange(Mathf.Sqrt(maximumErrorSquared), 0f, tolerance * 1.01f);
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        float lengthSquared = delta.LengthSquared();
        if (lengthSquared == 0f)
            return point.DistanceSquaredTo(start);
        float parameter = Mathf.Clamp((point - start).Dot(delta) / lengthSquared, 0f, 1f);
        return point.DistanceSquaredTo(start + parameter * delta);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "project.godot")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("SimpleCities project root was not found.");
    }
}
