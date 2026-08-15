using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphDeltaTests
{
    [Fact]
    public void EntityChange_ClassifiesCreatedUpdatedRemoved()
    {
        var node = new RoadGraphV3Node(1, Vector2.Zero);

        var created = new RoadGraphV3EntityChange<RoadGraphV3Node>(null, node);
        var updated = new RoadGraphV3EntityChange<RoadGraphV3Node>(node, node);
        var removed = new RoadGraphV3EntityChange<RoadGraphV3Node>(node, null);

        Assert.True(created.IsCreated);
        Assert.False(created.IsUpdated);
        Assert.True(updated.IsUpdated);
        Assert.True(removed.IsRemoved);
    }

    [Fact]
    public void Invert_SwapsCreatedAndRemovedAndRevisions()
    {
        var node = new RoadGraphV3Node(1, Vector2.Zero);
        var edge = new RoadGraphV3Edge(
            10,
            1,
            1,
            [
                new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f)),
                new LineRoadGeometrySegment(new Vector2(1f, 0f), Vector2.Zero),
            ],
            RoadType.Street);

        var delta = new RoadGraphV3Delta(
            5,
            6,
            [new RoadGraphV3EntityChange<RoadGraphV3Node>(null, node)],
            [new RoadGraphV3EntityChange<RoadGraphV3Edge>(edge, null)]);

        RoadGraphV3Delta inverted = delta.Invert();

        Assert.Equal(6, inverted.BeforeRevisionID);
        Assert.Equal(5, inverted.AfterRevisionID);
        Assert.True(inverted.NodeChanges[0].IsRemoved);
        Assert.True(inverted.EdgeChanges[0].IsCreated);
    }

    [Fact]
    public void Invert_Twice_RestoresOriginalShape()
    {
        var node = new RoadGraphV3Node(1, Vector2.Zero);
        var delta = new RoadGraphV3Delta(
            5,
            6,
            [new RoadGraphV3EntityChange<RoadGraphV3Node>(null, node)],
            []);

        RoadGraphV3Delta twice = delta.Invert().Invert();

        Assert.Equal(delta.BeforeRevisionID, twice.BeforeRevisionID);
        Assert.Equal(delta.AfterRevisionID, twice.AfterRevisionID);
        Assert.Equal(delta.NodeChanges.Count, twice.NodeChanges.Count);
        Assert.True(twice.NodeChanges[0].IsCreated);
        Assert.Equal(delta.NodeChanges[0].After, twice.NodeChanges[0].After);
    }

    [Fact]
    public void Empty_HasNoChanges()
    {
        RoadGraphV3Delta delta = RoadGraphV3Delta.Empty(3);

        Assert.True(delta.IsEmpty);
        Assert.Empty(delta.NodeChanges);
        Assert.Empty(delta.EdgeChanges);
        Assert.Equal(3, delta.BeforeRevisionID);
        Assert.Equal(3, delta.AfterRevisionID);
    }
}
