using SimpleCities.RoadCore;

namespace SimpleCities.RoadCore.Tests;

public sealed class EmptyMapTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void SaveAndLoad_PreservesMapContentButCreatesNewLineage(int cellSize)
    {
        var source = new RoadNetwork(new MapDefinition(cellSize));
        using var bytes = new MemoryStream();
        RoadCodec.Write(bytes, source.Snapshot);
        var target = new RoadNetwork();
        RoadStateToken before = target.Snapshot.Token;
        bytes.Position = 0;
        RoadLoadPlan plan = target.PlanLoad(RoadCodec.Read(bytes));

        Assert.Equal(before, target.Snapshot.Token);
        Assert.True(target.TryCommitLoad(plan));
        Assert.Equal(cellSize, target.Snapshot.Map.CellSizeMetres);
        Assert.Equal(before.NetworkInstance, target.Snapshot.Token.NetworkInstance);
        Assert.NotEqual(before.Lineage, target.Snapshot.Token.Lineage);
        Assert.Equal(1, target.Snapshot.Token.ChangeSequence);
        Assert.Equal(1, target.Snapshot.Token.ContentRevision);
        Assert.Equal(1, target.Snapshot.NextNodeId);
        Assert.Equal(1, target.Snapshot.NextEdgeId);
        Assert.False(target.TryCommitLoad(plan));
        using var roundTrip = new MemoryStream();
        RoadCodec.Write(roundTrip, target.Snapshot);
        Assert.Equal(bytes.ToArray(), roundTrip.ToArray());
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void CreateEmptyMap_UsesMetresAndStartsWithValidIdentity(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        RoadSnapshot snapshot = network.Snapshot;

        Assert.Equal(cellSize, snapshot.Map.CellSizeMetres);
        Assert.Equal(-4000, snapshot.Map.MinimumMetres);
        Assert.Equal(4000, snapshot.Map.MaximumMetres);
        Assert.Equal(0, snapshot.NodeCount);
        Assert.Equal(0, snapshot.EdgeCount);
        Assert.Equal(1, snapshot.NextNodeId);
        Assert.Equal(1, snapshot.NextEdgeId);
        Assert.Equal(1, snapshot.Token.ContentRevision);
        Assert.Equal(0, snapshot.Token.ChangeSequence);
        Assert.NotEqual(Guid.Empty, snapshot.Token.NetworkInstance);
        Assert.NotEqual(Guid.Empty, snapshot.Token.Lineage);
        Assert.Equal(100, new RoadNetwork().Snapshot.Map.CellSizeMetres);
    }
}
