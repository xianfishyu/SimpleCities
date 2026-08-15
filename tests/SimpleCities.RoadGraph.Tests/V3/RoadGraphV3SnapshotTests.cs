using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphV3SnapshotTests
{
    [Fact]
    public void CaptureSnapshot_UnaffectedByLaterMutations()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        RoadGraphV3Snapshot snapshot = facade.CaptureSnapshot();

        facade.TryAddNode(Vector2.Zero, out _, out _);

        Assert.Empty(snapshot.Revision.Nodes);
        Assert.Equal(0, snapshot.Token.ChangeSequence);
        Assert.Equal(1, facade.CurrentToken.ChangeSequence);
    }

    [Fact]
    public void Snapshot_RecordsTokenAtCaptureTime()
    {
        var facade = new RoadGraphV3Facade(RoadGraphV3Revision.Empty(RoadGraphCapacity.Default));
        facade.TryAddNode(Vector2.Zero, out _, out _);

        RoadGraphV3Snapshot snapshot = facade.CaptureSnapshot();

        Assert.Equal(facade.CurrentToken, snapshot.Token);
        Assert.Single(snapshot.Revision.Nodes);
    }
}
