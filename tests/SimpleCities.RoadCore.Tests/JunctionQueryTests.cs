namespace SimpleCities.RoadCore.Tests;

public sealed class JunctionQueryTests
{
    [Fact]
    public void CrossReadsStableEndRolesDirectionsAndGeometricTurns()
    {
        var network = new RoadNetwork();
        Build(network, new(-300, 0), new(300, 0));
        Build(network, new(0, -300), new(0, 300));
        RoadSnapshot snapshot = network.Snapshot;
        RoadNode center = Assert.Single(snapshot.Nodes, node => node.Position == new RoadPoint(0, 0));
        RoadJunctionReadModel junction = RoadJunctionQuery.Read(snapshot, center.Id)!;
        Assert.Equal(snapshot.Token, junction.Source);
        Assert.Equal(4, junction.Incidences.Count);
        Assert.Equal(16, junction.Turns.Count);
        Assert.Equal(new[] { new RoadVector(1, 0), new RoadVector(0, 1), new RoadVector(-1, 0), new RoadVector(0, -1) },
            junction.Incidences.Select(incidence => incidence.Outward));
        Assert.Equal(4, junction.Incidences.Select(incidence => incidence.Key).Distinct().Count());
        Assert.Equal(4, junction.Turns.Count(turn => turn.From == turn.To && turn.SignedAngleRadians == Math.PI));
        Assert.Equal(4, junction.Turns.Count(turn => turn.SignedAngleRadians == 0));
        Assert.Equal(junction.Incidences, RoadJunctionQuery.Read(snapshot, center.Id)!.Incidences);
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }
}
