using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class EdgeIncidenceTests
{
    [Fact]
    public void SelfLoop_UsesSameNeighborWithDistinctEndpoints()
    {
        var incidenceA = new EdgeIncidence(7, EdgeEndpoint.A, 1);
        var incidenceB = new EdgeIncidence(7, EdgeEndpoint.B, 1);

        Assert.Equal(7, incidenceA.EdgeID);
        Assert.Equal(1, incidenceA.NeighborNodeID);
        Assert.NotEqual(incidenceA.Endpoint, incidenceB.Endpoint);
        Assert.Equal(incidenceA.NeighborNodeID, incidenceB.NeighborNodeID);
    }

    [Fact]
    public void EdgeIncidence_IsValueEqual()
    {
        var left = new EdgeIncidence(3, EdgeEndpoint.B, 9);
        var right = new EdgeIncidence(3, EdgeEndpoint.B, 9);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }
}
