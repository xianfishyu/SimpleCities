using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadSurfaceHitTests
{
    [Fact]
    public void IsValid_ValidHit_ReturnsTrue()
    {
        RoadSurfaceHit hit = CreateHit(distanceSquared: 1f, parameter: 0.5f);

        Assert.True(hit.IsValid);
    }

    [Fact]
    public void IsValid_NegativeDistance_ReturnsFalse()
    {
        RoadSurfaceHit hit = CreateHit(distanceSquared: -1f, parameter: 0.5f);

        Assert.False(hit.IsValid);
    }

    [Fact]
    public void IsValid_OutOfRangeParameter_ReturnsFalse()
    {
        RoadSurfaceHit hit = CreateHit(distanceSquared: 1f, parameter: 1.1f);

        Assert.False(hit.IsValid);
    }

    [Fact]
    public void IsValid_NegativeToken_ReturnsFalse()
    {
        RoadSurfaceHit hit = CreateHit(distanceSquared: 1f, parameter: 0.5f) with
        {
            Token = new GraphStateToken(-1, 0, 0),
        };

        Assert.False(hit.IsValid);
    }

    private static RoadSurfaceHit CreateHit(float distanceSquared, float parameter) =>
        new(
            new GraphStateToken(1, 2, 3),
            RoadSurfaceOwnerKind.Ribbon,
            NodeID: 10,
            EdgeID: 20,
            Endpoint: EdgeEndpoint.A,
            new RoadLocation(20, 0, parameter),
            distanceSquared);
}
