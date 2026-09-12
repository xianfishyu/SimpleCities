namespace SimpleCities.RoadCore.Tests;

public sealed class EndpointContinuationTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void StraightContinuation_RemovesTheOrdinaryConnectionPoint(int cellSize)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        Build(network, new(0, 0), new(cellSize, 0));
        Build(network, new(cellSize, 0), new(3 * cellSize, 0));

        Assert.Equal(2, network.Snapshot.NodeCount);
        RoadEdge edge = Assert.Single(network.Snapshot.Edges);
        Assert.Equal(new[] { new RoadPoint(0, 0), new RoadPoint(3 * cellSize, 0) }, edge.Points);
        Assert.DoesNotContain(network.Snapshot.Nodes, node => node.Position == new RoadPoint(cellSize, 0));
        Assert.Equal(3, network.Snapshot.Token.ContentRevision);
        Assert.Equal(2, network.Snapshot.Token.ChangeSequence);
    }

    [Fact]
    public void SameProfileTurn_IsOneCanonicalEdgeWithoutAForceCreatedStructuralNode()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(300, 0));
        Build(network, new(300, 0), new(300, 400));

        Assert.Equal(2, network.Snapshot.NodeCount);
        RoadEdge edge = Assert.Single(network.Snapshot.Edges);
        Assert.Equal(new[] { new RoadPoint(0, 0), new RoadPoint(300, 0), new RoadPoint(300, 400) }, edge.Points);
        Assert.Equal(RoadProfileId.Street, edge.Profile);
        Assert.DoesNotContain(network.Snapshot.Nodes, node => node.Position == new RoadPoint(300, 0));
    }

    [Theory]
    [InlineData(false, "street")]
    [InlineData(true, "street")]
    [InlineData(false, "dirt")]
    [InlineData(true, "dirt")]
    public void DrawingTowardTheExistingEndpoint_ProducesTheSameCanonicalContent(bool atInitialStart, string profile)
    {
        var outward = new RoadNetwork();
        var inward = new RoadNetwork();
        Build(outward, new(0, 0), new(300, 0));
        Build(inward, new(0, 0), new(300, 0));
        RoadPoint existing = atInitialStart ? new(0, 0) : new(300, 0);
        RoadPoint free = atInitialStart ? new(-200, 200) : new(300, 200);

        Build(outward, existing, free, new RoadProfileId(profile));
        Build(inward, free, existing, new RoadProfileId(profile));

        Assert.Equal(Save(outward), Save(inward));
        Assert.Contains(inward.Snapshot.Nodes, node => node.Position == free);
        Assert.Equal(profile == "street" ? 1 : 2, inward.Snapshot.EdgeCount);
    }

    [Fact]
    public void DifferentProfileConnection_KeepsTheStructuralBoundary()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(300, 0));
        Build(network, new(300, 0), new(300, 400), RoadProfileId.Arterial);

        Assert.Equal(3, network.Snapshot.NodeCount);
        Assert.Equal(2, network.Snapshot.EdgeCount);
        RoadNode boundary = Assert.Single(network.Snapshot.Nodes, node => node.Position == new RoadPoint(300, 0));
        Assert.All(network.Snapshot.Edges, edge => Assert.True(edge.Start == boundary.Id || edge.End == boundary.Id));
        Assert.Single(network.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Street);
        Assert.Single(network.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Arterial);
    }

    [Fact]
    public void LocationsMeasureTheWholePolylineArcLengthAndExpireAfterContinuation()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(300, 0));
        RoadSnapshot straight = network.Snapshot;
        var previous = new RoadLocation(straight.Token, Assert.Single(straight.Edges).Id, 0.5);
        Assert.Equal(new RoadPoint(150, 0), straight.Resolve(previous));
        Build(network, new(300, 0), new(300, 400));
        RoadSnapshot turn = network.Snapshot;
        EdgeId id = Assert.Single(turn.Edges).Id;

        Assert.Null(turn.Resolve(previous));
        Assert.Equal(new RoadPoint(0, 0), turn.Resolve(new(turn.Token, id, 0)));
        Assert.Equal(new RoadPoint(175, 0), turn.Resolve(new(turn.Token, id, 0.25)));
        Assert.Equal(new RoadPoint(300, 50), turn.Resolve(new(turn.Token, id, 0.5)));
        Assert.Equal(new RoadPoint(300, 225), turn.Resolve(new(turn.Token, id, 0.75)));
        Assert.Equal(new RoadPoint(300, 400), turn.Resolve(new(turn.Token, id, 1)));
        var selected = new RoadLocation(turn.Token, id, 0.5);
        Build(network, new(300, 400), new(500, 600));
        Assert.Null(network.Snapshot.Resolve(selected));
        Assert.Equal(new RoadPoint(300, 50), turn.Resolve(selected));
    }

    [Theory]
    [InlineData(200, 0)] // Touch the interior of the first span.
    [InlineData(200, -100)] // Cross the first span.
    [InlineData(0, 0)] // Close onto the other open endpoint.
    [InlineData(400, 400)] // Touch an existing internal turn.
    [InlineData(200, 500)] // Overlap the last span in reverse.
    [InlineData(4100, 200)] // Leave the map on a legal direction.
    public void UnsupportedContinuation_DoesNotPublishOrConsumeIdentities(double x, double y)
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(400, 0));
        Build(network, new(400, 0), new(400, 400));
        Build(network, new(400, 400), new(200, 400));
        Build(network, new(200, 400), new(200, 200));
        RoadSnapshot before = network.Snapshot;
        byte[] content = Save(network);

        RoadBuildResult result = network.PlanBuild(new(before.Token, new(200, 200), new(x, y), RoadProfileId.Street));

        Assert.Equal(RoadBuildStatus.Rejected, result.Status);
        Assert.Null(result.Plan);
        Assert.Equal(before.Token, network.Snapshot.Token);
        Assert.Equal(before.NextNodeId, network.Snapshot.NextNodeId);
        Assert.Equal(before.NextEdgeId, network.Snapshot.NextEdgeId);
        Assert.Equal(content, Save(network));
    }

    [Fact]
    public void BuildingFromAProfileBoundary_DoesNotCreateAThirdBranch()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(300, 0));
        Build(network, new(300, 0), new(300, 400), RoadProfileId.Dirt);
        RoadStateToken before = network.Snapshot.Token;
        byte[] content = Save(network);

        RoadBuildResult result = network.PlanBuild(new(before, new(300, 0), new(500, 0), RoadProfileId.Street));

        Assert.Equal(RoadBuildStatus.Rejected, result.Status);
        Assert.Null(result.Plan);
        Assert.Equal(before, network.Snapshot.Token);
        Assert.Equal(content, Save(network));
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end, RoadProfileId? profile = null)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, profile ?? RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }

    private static byte[] Save(RoadNetwork network)
    {
        using var stream = new MemoryStream();
        RoadCodec.Write(stream, network.Snapshot);
        return stream.ToArray();
    }
}
