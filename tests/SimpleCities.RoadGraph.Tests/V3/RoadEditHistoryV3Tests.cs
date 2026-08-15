using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadEditHistoryV3Tests
{
    [Fact]
    public void TryPush_EvictsOldestWhenOverByteBudget()
    {
        var history = new RoadEditHistoryV3(10, 150);

        Assert.True(history.TryPush(NodeDelta(1, 2, 1)));
        Assert.True(history.TryPush(NodeDelta(2, 3, 2)));

        Assert.Equal(1, history.UndoCount);
        Assert.True(history.TryUndo(out RoadGraphV3Delta delta));
        Assert.Equal(3, delta.AfterRevisionID);
    }

    [Fact]
    public void TryPush_RejectsSingleEntryOverMaxBytes()
    {
        var history = new RoadEditHistoryV3(10, 100);

        Assert.False(history.TryPush(NodeDelta(1, 2, 1)));

        Assert.Equal(0, history.UndoCount);
    }

    [Fact]
    public void UndoRedo_RestoresDeltasInOrder()
    {
        var history = new RoadEditHistoryV3(10, 10000);
        history.TryPush(NodeDelta(1, 2, 1));
        history.TryPush(NodeDelta(2, 3, 2));

        Assert.True(history.TryUndo(out RoadGraphV3Delta undo1));
        Assert.True(history.TryUndo(out RoadGraphV3Delta undo2));
        Assert.True(history.TryRedo(out RoadGraphV3Delta redo1));
        Assert.True(history.TryRedo(out RoadGraphV3Delta redo2));

        Assert.Equal(3, undo1.AfterRevisionID);
        Assert.Equal(2, undo2.AfterRevisionID);
        Assert.Equal(2, redo1.AfterRevisionID);
        Assert.Equal(3, redo2.AfterRevisionID);
    }

    [Fact]
    public void Push_ClearsRedo()
    {
        var history = new RoadEditHistoryV3(10, 10000);
        history.TryPush(NodeDelta(1, 2, 1));
        history.TryUndo(out _);

        Assert.Equal(1, history.RedoCount);

        history.TryPush(NodeDelta(2, 3, 2));

        Assert.Equal(0, history.RedoCount);
    }

    [Fact]
    public void Clear_ResetsCountsAndBytes()
    {
        var history = new RoadEditHistoryV3(10, 10000);
        history.TryPush(NodeDelta(1, 2, 1));
        history.TryPush(NodeDelta(2, 3, 2));
        history.TryUndo(out _);

        history.Clear();

        Assert.Equal(0, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
        Assert.Equal(0, history.TotalBytes);
    }

    [Fact]
    public void Estimator_CountsGeometrySegments()
    {
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
            1,
            2,
            [],
            [new RoadGraphV3EntityChange<RoadGraphV3Edge>(null, edge)]);

        long estimate = RoadGraphV3DeltaSizeEstimator.Estimate(delta);

        Assert.True(estimate > 64L + 48L);
    }

    private static RoadGraphV3Delta NodeDelta(long before, long after, int nodeID) =>
        new(
            before,
            after,
            [new RoadGraphV3EntityChange<RoadGraphV3Node>(null, new RoadGraphV3Node(nodeID, Vector2.Zero))],
            []);
}
