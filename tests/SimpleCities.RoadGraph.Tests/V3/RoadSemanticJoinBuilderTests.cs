using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadSemanticJoinBuilderTests
{
    [Fact]
    public void TryBuild_DifferentTypes_ReturnsMesh()
    {
        RoadGraphV3Revision revision = CreateDifferentTypesRevision(out int centerID);
        RoadStyleProvider styles = CreateProvider();

        Assert.True(RoadSemanticJoinBuilder.TryBuild(revision, styles, centerID, out RoadSemanticJoinMeshData mesh));
        Assert.True(mesh.IsValid);
        Assert.Equal(4, mesh.Vertices.Count);
        Assert.Equal(4, mesh.Colors.Count);
        Assert.Equal(6, mesh.Indices.Count);
    }

    [Fact]
    public void TryBuild_SameTypes_Fails()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int center);
        revision.TryAddNode(new Vector2(-1f, 0f), out revision, out int left);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int right);
        revision.TryAddEdge(center, left, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(-1f, 0f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(center, right, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        RoadStyleProvider styles = CreateProvider();

        Assert.False(RoadSemanticJoinBuilder.TryBuild(revision, styles, center, out _));
    }

    [Fact]
    public void TryBuild_DegreeOne_Fails()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        RoadStyleProvider styles = CreateProvider();

        Assert.False(RoadSemanticJoinBuilder.TryBuild(revision, styles, a, out _));
    }

    private static RoadGraphV3Revision CreateDifferentTypesRevision(out int centerID)
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out centerID);
        revision.TryAddNode(new Vector2(-1f, 0f), out revision, out int left);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int right);
        revision.TryAddEdge(centerID, left, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(-1f, 0f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(centerID, right, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Highway, out revision, out _);
        return revision;
    }

    private static RoadStyleProvider CreateProvider()
    {
        RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
        return new RoadStyleProvider(catalog);
    }
}
