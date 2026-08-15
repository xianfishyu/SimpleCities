using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadSurfaceSnapshotTests
{
    [Fact]
    public void IsValid_ValidSnapshot_ReturnsTrue()
    {
        RoadSurfaceSnapshot snapshot = CreateSnapshot();

        Assert.True(snapshot.IsValid);
    }

    [Fact]
    public void IsValid_NegativeToken_ReturnsFalse()
    {
        RoadSurfaceSnapshot snapshot = CreateSnapshot() with
        {
            Token = new GraphStateToken(-1, 0, 0),
        };

        Assert.False(snapshot.IsValid);
    }

    [Fact]
    public void FindByEdge_ReturnsMatchingOwners()
    {
        RoadSurfaceSnapshot snapshot = CreateSnapshot();

        IReadOnlyList<RoadSurfaceOwner> owners = snapshot.FindByEdge(20);

        Assert.Single(owners);
        Assert.Equal(20, owners[0].EdgeID);
    }

    [Fact]
    public void FindByNode_ReturnsMatchingOwners()
    {
        RoadSurfaceSnapshot snapshot = CreateSnapshot();

        IReadOnlyList<RoadSurfaceOwner> owners = snapshot.FindByNode(10);

        Assert.Single(owners);
        Assert.Equal(10, owners[0].NodeID);
    }

    private static RoadSurfaceSnapshot CreateSnapshot() =>
        new(
            new GraphStateToken(1, 2, 3),
            [
                new RoadSurfaceOwner(
                    RoadSurfaceOwnerKind.Ribbon,
                    NodeID: 10,
                    EdgeID: 20,
                    Endpoint: EdgeEndpoint.A,
                    new RoadLocation(20, 0, 0.5f)),
            ]);
}
