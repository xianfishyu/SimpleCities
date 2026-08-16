using Godot;
using SimpleCities.Road.V3;
using System.Collections.Generic;

namespace SimpleCities.Tests.V3;

public sealed class RoadSurfaceSnapshotBuilderTests
{
    [Fact]
    public void Build_ReturnsOwnerPerEdge()
    {
        RoadGraphV3Revision revision = CreateRevision();
        RoadStyleProvider styles = CreateProvider();

        RoadSurfaceSnapshotBuildResult result = RoadSurfaceSnapshotBuilder.Build(
            revision,
            new GraphStateToken(1, 2, 3),
            styles);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(revision.Edges.Count, result.Snapshot!.Owners.Count);
    }

    [Fact]
    public void Build_CrossNode_AddsJunctionOwner()
    {
        RoadGraphV3Revision revision = CreateCrossRevision(out int centerID);
        RoadStyleProvider styles = CreateProvider();

        RoadSurfaceSnapshotBuildResult result = RoadSurfaceSnapshotBuilder.Build(
            revision,
            new GraphStateToken(1, 2, 3),
            styles);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(revision.Edges.Count + 1, result.Snapshot!.Owners.Count);
        Assert.Contains(result.Snapshot.Owners, owner =>
            owner.Kind == RoadSurfaceOwnerKind.JunctionPatch &&
            owner.NodeID == centerID);
    }

    [Fact]
    public void Build_MissingStyle_Fails()
    {
        RoadGraphV3Revision revision = CreateHighwayRevision();
        var partialStyles = new Dictionary<RoadType, RoadTypeStyle>
        {
            [RoadType.Dirt] = CreateStyle(RoadType.Dirt, "土路", Colors.Brown),
            [RoadType.Street] = CreateStyle(RoadType.Street, "街道", Colors.White),
            [RoadType.Arterial] = CreateStyle(RoadType.Arterial, "主干道", Colors.Yellow),
        };
        var provider = new RoadStyleProvider(partialStyles);

        RoadSurfaceSnapshotBuildResult result = RoadSurfaceSnapshotBuilder.Build(
            revision,
            new GraphStateToken(1, 2, 3),
            provider);

        Assert.False(result.Success);
        Assert.Equal("MissingStyle:Highway", result.Error);
    }

    [Fact]
    public void Build_InvalidToken_Fails()
    {
        RoadGraphV3Revision revision = CreateRevision();
        RoadStyleProvider styles = CreateProvider();

        RoadSurfaceSnapshotBuildResult result = RoadSurfaceSnapshotBuilder.Build(
            revision,
            new GraphStateToken(-1, 0, 0),
            styles);

        Assert.False(result.Success);
        Assert.Equal("InvalidToken", result.Error);
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

    private static RoadGraphV3Revision CreateRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        return revision;
    }

    private static RoadGraphV3Revision CreateHighwayRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Highway, out revision, out _);
        return revision;
    }

    private static RoadTypeStyle CreateStyle(RoadType roadType, string displayName, Color color) =>
        new()
        {
            RoadType = roadType,
            DisplayName = displayName,
            Color = color,
            Width = 1f,
        };

    private static RoadStyleProvider CreateProvider()
    {
        RoadTypeStyleCatalogResult catalog = RoadTypeStyleCatalog.CreateDefault();
        return new RoadStyleProvider(catalog);
    }
}
