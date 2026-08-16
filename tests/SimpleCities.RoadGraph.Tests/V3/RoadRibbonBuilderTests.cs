using Godot;
using SimpleCities.Road.V3;
using System.Collections.Generic;

namespace SimpleCities.Tests.V3;

public sealed class RoadRibbonBuilderTests
{
    [Fact]
    public void TryBuild_StraightLine_ReturnsValidRibbon()
    {
        var edge = CreateEdge([new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f))]);
        RoadTypeStyle style = CreateStyle(width: 2f);

        Assert.True(RoadRibbonBuilder.TryBuild(edge, style, 0.25f, out RoadRibbonMeshData mesh));
        Assert.True(mesh.IsValid);
        Assert.Equal(4, mesh.Vertices.Count);
        Assert.Equal(6, mesh.Indices.Count);
        Assert.Equal(4, mesh.Colors.Count);
        Assert.Equal(new Vector2(0f, 1f), mesh.Vertices[0]);
        Assert.Equal(new Vector2(0f, -1f), mesh.Vertices[1]);
        Assert.Equal(new Vector2(10f, 1f), mesh.Vertices[2]);
        Assert.Equal(new Vector2(10f, -1f), mesh.Vertices[3]);
    }

    [Fact]
    public void TryBuild_Corner_ReturnsValidRibbon()
    {
        var edge = CreateEdge(
        [
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f)),
            new LineRoadGeometrySegment(new Vector2(10f, 0f), new Vector2(10f, 10f)),
        ]);
        RoadTypeStyle style = CreateStyle(width: 2f);

        Assert.True(RoadRibbonBuilder.TryBuild(edge, style, 0.25f, out RoadRibbonMeshData mesh));
        Assert.True(mesh.IsValid);
        Assert.Equal(6, mesh.Vertices.Count);
        Assert.Equal(12, mesh.Indices.Count);
        Assert.Equal(6, mesh.Colors.Count);
    }

    [Fact]
    public void TryBuild_InvalidWidth_Fails()
    {
        var edge = CreateEdge([new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f))]);
        RoadTypeStyle style = CreateStyle(width: 0f);

        Assert.False(RoadRibbonBuilder.TryBuild(edge, style, 0.25f, out _));
    }

    [Fact]
    public void TryBuild_NullEdge_Throws()
    {
        RoadTypeStyle style = CreateStyle();

        Assert.Throws<System.ArgumentNullException>(
            () => RoadRibbonBuilder.TryBuild(null!, style, 0.25f, out _));
    }

    [Fact]
    public void TryBuild_NullStyle_Throws()
    {
        var edge = CreateEdge([new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f))]);

        Assert.Throws<System.ArgumentNullException>(
            () => RoadRibbonBuilder.TryBuild(edge, null!, 0.25f, out _));
    }

    private static RoadGraphV3Edge CreateEdge(IReadOnlyList<RoadGeometrySegment> geometry) =>
        new(1, 10, 11, geometry, RoadType.Street);

    private static RoadTypeStyle CreateStyle(float width = 1f) =>
        new()
        {
            RoadType = RoadType.Street,
            DisplayName = "Street",
            Color = Colors.Red,
            Width = width,
        };
}
