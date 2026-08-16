using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadCapBuilderTests
{
    [Fact]
    public void TryBuild_SingleEdgeEndpoint_ReturnsCap()
    {
        RoadGraphV3Revision revision = CreateSingleEdgeRevision(out int endpointID, out int edgeID);
        RoadStyleProvider styles = CreateProvider();

        Assert.True(RoadCapBuilder.TryBuild(revision, styles, endpointID, out RoadCapMeshData cap));
        Assert.Equal(endpointID, cap.NodeID);
        Assert.Equal(edgeID, cap.EdgeID);
        Assert.True(cap.IsValid);
        Assert.True(cap.Outline.Count >= 3);
    }

    [Fact]
    public void TryBuild_DegreeTwo_Fails()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int center);
        revision.TryAddNode(new Vector2(-1f, 0f), out revision, out int left);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int right);
        revision.TryAddEdge(center, left, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(-1f, 0f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(center, right, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        RoadStyleProvider styles = CreateProvider();

        Assert.False(RoadCapBuilder.TryBuild(revision, styles, center, out _));
    }

    [Fact]
    public void TryBuild_MissingNode_Fails()
    {
        RoadGraphV3Revision revision = CreateSingleEdgeRevision(out _, out _);
        RoadStyleProvider styles = CreateProvider();

        Assert.False(RoadCapBuilder.TryBuild(revision, styles, 999, out _));
    }

    private static RoadGraphV3Revision CreateSingleEdgeRevision(out int endpointID, out int edgeID)
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out endpointID);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int other);
        revision.TryAddEdge(endpointID, other, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out edgeID);
        return revision;
    }

    private static RoadStyleProvider CreateProvider()
    {
        RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
        return new RoadStyleProvider(catalog);
    }
}
