using Godot;
using System.Collections.Immutable;

namespace SimpleCities.Tests;

public sealed class UniformGridInvariantTests
{
    [Fact]
    public void HasExactCoverage_AcceptsSingleAndMultiBucketReferences()
    {
        var grid = new UniformGrid(64f);
        var node = new NodeSpatialRef(1, new Vector2(4f, 8f));
        var geometry = new LineRoadGeometrySegment(
            new Vector2(0f, 96f),
            new Vector2(130f, 96f));
        var geometryRef = new EdgeGeometryRef(
            2,
            0,
            0,
            RoadGeometrySegment.ParameterStart,
            RoadGeometrySegment.ParameterEnd,
            geometry,
            geometry,
            ownsParameterEnd: true);
        grid.Insert(node);
        grid.InsertGeometry(geometryRef);

        var expected = new Dictionary<ISpatialRef, Rect2>(ReferenceEqualityComparer.Instance)
        {
            [node] = new Rect2(node.Position, Vector2.Zero),
            [geometryRef] = geometryRef.Bounds,
        };

        Assert.True(grid.HasExactCoverage(expected));
    }

    [Fact]
    public void HasExactCoverage_RejectsMissingAndUnexpectedReferences()
    {
        var missingGrid = new UniformGrid(64f);
        var expectedNode = new NodeSpatialRef(1, Vector2.Zero);
        var expected = new Dictionary<ISpatialRef, Rect2>(ReferenceEqualityComparer.Instance)
        {
            [expectedNode] = new Rect2(expectedNode.Position, Vector2.Zero),
        };
        Assert.False(missingGrid.HasExactCoverage(expected));

        var unexpectedGrid = new UniformGrid(64f);
        unexpectedGrid.Insert(new NodeSpatialRef(2, Vector2.Zero));
        Assert.False(unexpectedGrid.HasExactCoverage(expected));
    }

    [Fact]
    public void HasExactCoverage_RejectsWrongBucketDuplicateAndCountMismatch()
    {
        var node = new NodeSpatialRef(1, Vector2.Zero);
        var expected = new Dictionary<ISpatialRef, Rect2>(ReferenceEqualityComparer.Instance)
        {
            [node] = new Rect2(node.Position, Vector2.Zero),
        };

        Assert.False(CreateGrid((1, 0), [node], 1).HasExactCoverage(expected));
        Assert.False(CreateGrid((0, 0), [node, node], 2).HasExactCoverage(expected));
        Assert.False(CreateGrid((0, 0), [node], 0).HasExactCoverage(expected));
    }

    private static UniformGrid CreateGrid(
        (int bx, int by) bucket,
        ISpatialRef[] references,
        long referenceEntryCount)
    {
        ImmutableDictionary<(int bx, int by), ImmutableArray<ISpatialRef>> buckets =
            ImmutableDictionary<(int bx, int by), ImmutableArray<ISpatialRef>>.Empty
                .Add(bucket, references.ToImmutableArray());
        return new UniformGrid(new UniformGridSnapshot(64f, buckets, referenceEntryCount));
    }
}
