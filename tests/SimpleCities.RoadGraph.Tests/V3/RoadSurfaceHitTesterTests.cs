using Godot;
using SimpleCities.Road.V3;
using System.Linq;

namespace SimpleCities.Tests.V3;

public sealed class RoadSurfaceHitTesterTests
{
    [Fact]
    public void TryFindClosest_Line_ReturnsEdgeHit()
    {
        RoadGraphV3Revision revision = CreateRevisionWithSingleLine();
        var token = new GraphStateToken(1, 1, 1);

        Assert.True(RoadSurfaceHitTester.TryFindClosest(
            revision,
            token,
            new Vector2(5f, 1f),
            maxDistance: 2f,
            out RoadSurfaceHit hit));

        int edgeID = revision.Edges.Keys.Single();
        Assert.Equal(edgeID, hit.EdgeID);
        Assert.Equal(RoadSurfaceOwnerKind.Ribbon, hit.OwnerKind);
        Assert.Equal(token, hit.Token);
        Assert.InRange(hit.DistanceSquared, 0.99f, 1.01f);
        Assert.Equal(0, hit.Location.GeometryIndex);
        Assert.InRange(hit.Location.Parameter, 0.4f, 0.6f);
    }

    [Fact]
    public void TryFindClosest_TooFar_Fails()
    {
        RoadGraphV3Revision revision = CreateRevisionWithSingleLine();
        var token = new GraphStateToken(1, 1, 1);

        Assert.False(RoadSurfaceHitTester.TryFindClosest(
            revision,
            token,
            new Vector2(5f, 3f),
            maxDistance: 2f,
            out _));
    }

    [Fact]
    public void TryFindClosest_EmptyGraph_Fails()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);

        Assert.False(RoadSurfaceHitTester.TryFindClosest(
            revision,
            new GraphStateToken(1, 1, 1),
            Vector2.Zero,
            maxDistance: 2f,
            out _));
    }

    [Fact]
    public void TryFindClosest_ParallelEdges_ReturnsCloserEdge()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(10f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f))], RoadType.Street, out revision, out int lowerEdgeID);
        revision.TryAddNode(new Vector2(0f, 5f), out revision, out int c);
        revision.TryAddNode(new Vector2(10f, 5f), out revision, out int d);
        revision.TryAddEdge(c, d, [new LineRoadGeometrySegment(new Vector2(0f, 5f), new Vector2(10f, 5f))], RoadType.Street, out revision, out _);

        Assert.True(RoadSurfaceHitTester.TryFindClosest(
            revision,
            new GraphStateToken(1, 1, 1),
            new Vector2(0f, 2f),
            maxDistance: 3f,
            out RoadSurfaceHit hit));

        Assert.Equal(lowerEdgeID, hit.EdgeID);
    }

    [Fact]
    public void TryFindClosestJunction_CrossNode_ReturnsHit()
    {
        RoadGraphV3Revision revision = CreateCrossRevision(out int centerID);

        Assert.True(RoadSurfaceHitTester.TryFindClosestJunction(
            revision,
            new GraphStateToken(1, 1, 1),
            new Vector2(0.1f, 0.1f),
            maxDistance: 1f,
            out RoadSurfaceHit hit));

        Assert.Equal(RoadSurfaceOwnerKind.JunctionPatch, hit.OwnerKind);
        Assert.Equal(centerID, hit.NodeID);
        Assert.Null(hit.EdgeID);
    }

    [Fact]
    public void TryFindClosestJunction_DegreeTwo_Fails()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int center);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int east);
        revision.TryAddNode(new Vector2(-1f, 0f), out revision, out int west);
        revision.TryAddEdge(center, east, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        revision.TryAddEdge(center, west, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(-1f, 0f))], RoadType.Street, out revision, out _);

        Assert.False(RoadSurfaceHitTester.TryFindClosestJunction(
            revision,
            new GraphStateToken(1, 1, 1),
            Vector2.Zero,
            maxDistance: 1f,
            out _));
    }

    [Fact]
    public void TryFindClosestJunction_TooFar_Fails()
    {
        RoadGraphV3Revision revision = CreateCrossRevision(out _);

        Assert.False(RoadSurfaceHitTester.TryFindClosestJunction(
            revision,
            new GraphStateToken(1, 1, 1),
            new Vector2(10f, 10f),
            maxDistance: 1f,
            out _));
    }

    [Fact]
    public void TryFindClosest_InvalidPoint_Throws()
    {
        RoadGraphV3Revision revision = CreateRevisionWithSingleLine();

        Assert.Throws<System.ArgumentException>(() => RoadSurfaceHitTester.TryFindClosest(
            revision,
            new GraphStateToken(1, 1, 1),
            new Vector2(float.NaN, 0f),
            maxDistance: 2f,
            out _));
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

    private static RoadGraphV3Revision CreateRevisionWithSingleLine()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(10f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(10f, 0f))], RoadType.Street, out revision, out _);
        return revision;
    }
}
