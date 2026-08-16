using Godot;
using SimpleCities.Road.V3;
using System.Linq;

namespace SimpleCities.Tests.V3;

public sealed class RoadJunctionPatchBuilderTests
{
    [Fact]
    public void TryBuild_CrossNode_ReturnsPatch()
    {
        RoadGraphV3Revision revision = CreateCrossRevision(out int centerID);
        RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
        Assert.True(catalog.Success);
        var styles = new RoadStyleProvider(catalog);

        Assert.True(RoadJunctionPatchBuilder.TryBuild(revision, styles, centerID, radius: 2f, out RoadJunctionPatchData patch));
        Assert.True(patch.IsValid);
        Assert.Equal(4, patch.Outline.Count);
        Assert.All(patch.Outline, point => Assert.Equal(2f, point.DistanceTo(Vector2.Zero), 3));
    }

    [Fact]
    public void TryBuild_DegreeTwo_Fails()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int center);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int east);
        revision.TryAddNode(new Vector2(-1f, 0f), out revision, out int west);
        revision.TryAddEdge(center, east, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(center, west, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(-1f, 0f))], RoadType.Street, out revision, out _);
        RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
        Assert.True(catalog.Success);
        var styles = new RoadStyleProvider(catalog);

        Assert.False(RoadJunctionPatchBuilder.TryBuild(revision, styles, center, radius: 2f, out _));
    }

    [Fact]
    public void TryBuild_MissingNode_Fails()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
        Assert.True(catalog.Success);
        var styles = new RoadStyleProvider(catalog);

        Assert.False(RoadJunctionPatchBuilder.TryBuild(revision, styles, 99, radius: 2f, out _));
    }

    [Fact]
    public void TryBuild_InvalidRadius_Fails()
    {
        RoadGraphV3Revision revision = CreateCrossRevision(out int centerID);
        RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
        Assert.True(catalog.Success);
        var styles = new RoadStyleProvider(catalog);

        Assert.False(RoadJunctionPatchBuilder.TryBuild(revision, styles, centerID, radius: 0f, out _));
    }

    [Fact]
    public void TryBuild_SelfLoopPlusTwoEdges_ReturnsPatch()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int center);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int east);
        revision.TryAddNode(new Vector2(-1f, 0f), out revision, out int west);
        revision.TryAddEdge(center, east, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(center, west, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(-1f, 0f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(center, center, [
            new LineRoadGeometrySegment(Vector2.Zero, new Vector2(0f, 1f)),
            new LineRoadGeometrySegment(new Vector2(0f, 1f), Vector2.Zero),
        ], RoadType.Street, out revision, out _);
        RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
        Assert.True(catalog.Success);
        var styles = new RoadStyleProvider(catalog);

        Assert.True(RoadJunctionPatchBuilder.TryBuild(revision, styles, center, radius: 2f, out RoadJunctionPatchData patch));
        Assert.True(patch.IsValid);
        Assert.Equal(3, patch.Outline.Count);
    }

    private static RoadGraphV3Revision CreateCrossRevision(out int centerID)
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out centerID);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int east);
        revision.TryAddNode(new Vector2(-1f, 0f), out revision, out int west);
        revision.TryAddNode(new Vector2(0f, 1f), out revision, out int north);
        revision.TryAddNode(new Vector2(0f, -1f), out revision, out int south);
        revision.TryAddEdge(centerID, east, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(centerID, west, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(-1f, 0f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(centerID, north, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(0f, 1f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(centerID, south, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(0f, -1f))], RoadType.Street, out revision, out _);
        return revision;
    }
}
