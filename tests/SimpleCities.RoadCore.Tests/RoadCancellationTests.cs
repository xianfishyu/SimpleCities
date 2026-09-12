namespace SimpleCities.RoadCore.Tests;

public sealed class RoadCancellationTests
{
    [Fact]
    public void CancelledPlanningDoesNotChangeContentAndDoesNotPrepareAPresentation()
    {
        var network = new RoadNetwork();
        RoadSnapshot before = network.Snapshot;
        var request = new RoadBuildRequest(before.Token, new(0, 0), new(300, 0), RoadProfileId.Street);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => network.PlanBuild(request, cancellation.Token));
        Assert.Equal(before.Token, network.Snapshot.Token);
        Assert.Equal(1, network.Snapshot.NextNodeId);
        RoadPlan plan = network.PlanBuild(request).Plan!;
        Assert.Throws<OperationCanceledException>(() => RoadPresentation.Prepare(plan.Target.Nodes, plan.Target.Edges, cancellation.Token));
        Assert.Equal(0, network.Snapshot.EdgeCount);
    }
}
