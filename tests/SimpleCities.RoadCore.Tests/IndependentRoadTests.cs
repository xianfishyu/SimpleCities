namespace SimpleCities.RoadCore.Tests;

public sealed class IndependentRoadTests
{
    [Theory]
    [InlineData("nextNodeId", long.MaxValue - 2)]
    [InlineData("nextEdgeId", long.MaxValue - 1)]
    [InlineData("contentRevision", long.MaxValue - 1)]
    public void BuildAtIdentityLimit_IsRejectedAndCurrentContentStillRoundTrips(string field, long value)
    {
        const string empty = """
            {"formatFamily":"simple-cities-v4","payloadType":"road-network","schemaVersion":6,
            "contentRevision":1,"nextNodeId":1,"nextEdgeId":1,"profileCatalogVersion":1,
            "map":{"widthMetres":8000,"heightMetres":8000,"origin":"center","metresPerUnit":1,"grid":"square-eight","cellSizeMetres":100},
            "nodes":[],"edges":[]}
            """;
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(empty.Replace($"\"{field}\":1", $"\"{field}\":{value}")));
        var network = new RoadNetwork();
        Assert.True(network.TryCommit(network.PlanLoad(RoadCodec.Read(source))));
        RoadSnapshot before = network.Snapshot;
        RoadBuildResult result = network.PlanBuild(new(before.Token, new(0, 0), new(100, 0), RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Rejected, result.Status);
        Assert.Null(result.Plan);
        Assert.Equal(before.Token, network.Snapshot.Token);
        Assert.Equal(before.NextNodeId, network.Snapshot.NextNodeId);
        Assert.Equal(before.NextEdgeId, network.Snapshot.NextEdgeId);
        using var saved = new MemoryStream();
        RoadCodec.Write(saved, network.Snapshot);
        saved.Position = 0;
        var restored = new RoadNetwork();
        Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(saved))));
        Assert.Equal(before.Token.ContentRevision, restored.Snapshot.Token.ContentRevision);
        Assert.Equal(before.NextNodeId, restored.Snapshot.NextNodeId);
        Assert.Equal(before.NextEdgeId, restored.Snapshot.NextEdgeId);
    }

    [Fact]
    public void RejectedAndNoChangeRequests_DoNotConsumeIdsOrPublish()
    {
        var network = new RoadNetwork();
        RoadSnapshot before = network.Snapshot;
        Assert.Equal(RoadBuildStatus.NoChange, network.PlanBuild(new(before.Token, new(0, 0), new(0, 0), RoadProfileId.Dirt)).Status);
        foreach (RoadPoint end in new[] { new RoadPoint(100, 200), new RoadPoint(25, 25), new RoadPoint(4100, 0), new RoadPoint(double.NaN, 0) })
            Assert.Equal(RoadBuildStatus.Rejected, network.PlanBuild(new(before.Token, new(0, 0), end, RoadProfileId.Dirt)).Status);
        Assert.Equal(RoadBuildStatus.Rejected, network.PlanBuild(new(default, new(0, 0), new(100, 0), RoadProfileId.Dirt)).Status);
        Assert.Equal(RoadBuildStatus.Rejected, network.PlanBuild(new(before.Token, new(0, 0), new(100, 0), default)).Status);
        Assert.Equal(before.Token, network.Snapshot.Token);
        Assert.Equal(1, network.Snapshot.NextNodeId);
        Assert.Equal(1, network.Snapshot.NextEdgeId);
        RoadPlan first = network.PlanBuild(new(before.Token, new(0, 0), new(100, 0), RoadProfileId.Dirt)).Plan!;
        RoadPlan concurrent = network.PlanBuild(new(before.Token, new(0, 0), new(200, 0), RoadProfileId.Dirt)).Plan!;
        Assert.True(network.TryCommit(first));
        Assert.False(network.TryCommit(concurrent));
        Assert.Equal(RoadBuildStatus.Ready, network.PlanBuild(new(network.Snapshot.Token, new(0, 200), new(100, 200), RoadProfileId.Dirt)).Status);
        Assert.Equal(1, network.Snapshot.EdgeCount);
        Assert.Equal(1, network.Snapshot.Token.ChangeSequence);
    }

    [Fact]
    public void DirectionalPreview_PreservesMetresAndDirectionAtMapBoundary()
    {
        var map = new MapDefinition(100);
        Assert.Equal(new RoadPoint(4000, 200), map.SnapDragEnd(new(3800, 0), new(8000, 4200)));
        Assert.Equal(new RoadPoint(-4000, -200), map.SnapDragEnd(new(-3800, 0), new(-8000, -4200)));
        Assert.Equal(new RoadPoint(300, 0), map.SnapDragEnd(new(0, 0), new(320, 80)));
        Assert.Equal(new RoadPoint(300, 300), map.SnapDragEnd(new(0, 0), new(320, 280)));
        Assert.Equal(new RoadPoint(0, 0), map.SnapDragEnd(new(0, 0), new(20, 20)));
    }

    [Theory]
    [InlineData("dirt", 8)]
    [InlineData("street", 12)]
    [InlineData("arterial", 24)]
    [InlineData("highway", 32)]
    public void RoadRoundTrip_RetainsEntitiesAndResolvesOnlyCurrentLocations(string profile, double width)
    {
        var network = new RoadNetwork(new MapDefinition(25));
        var result = network.PlanBuild(new(network.Snapshot.Token, new(-100, 25), new(200, 325), new(profile)));
        Assert.True(network.TryCommit(result.Plan!));
        RoadSurfaceData surface = RoadPresentation.Prepare(network.Snapshot.Nodes, network.Snapshot.Edges)!;
        Assert.InRange(surface.Pieces[0].Corners[0].DistanceTo(surface.Pieces[0].Corners[3]), width - 1e-9, width + 1e-9);
        var location = new RoadLocation(network.Snapshot.Token, new EdgeId(1), 0.5);
        Assert.Equal(new RoadPoint(50, 175), network.Snapshot.Resolve(location));
        using var bytes = new MemoryStream();
        RoadCodec.Write(bytes, network.Snapshot);
        bytes.Position = 0;
        var target = new RoadNetwork();
        Assert.True(target.TryCommit(target.PlanLoad(RoadCodec.Read(bytes))));
        Assert.Equal(2, target.Snapshot.NodeCount);
        RoadEdge edge = Assert.Single(target.Snapshot.Edges);
        Assert.Equal(profile, edge.Profile.Value);
        Assert.Equal(new RoadPoint(-100, 25), target.Snapshot.Nodes[0].Position);
        Assert.Equal(new RoadPoint(200, 325), target.Snapshot.Nodes[1].Position);
        Assert.Equal(3, target.Snapshot.NextNodeId);
        Assert.Equal(2, target.Snapshot.NextEdgeId);
        Assert.Null(target.Snapshot.Resolve(location));
        using var savedAgain = new MemoryStream();
        RoadCodec.Write(savedAgain, target.Snapshot);
        Assert.Equal(bytes.ToArray(), savedAgain.ToArray());
    }

    [Theory]
    [InlineData("\"endNodeId\":2", "\"endNodeId\":9")]
    [InlineData("\"profile\":\"street\"", "\"profile\":\"unknown\"")]
    [InlineData("\"id\":2,\"x\":300", "\"id\":1,\"x\":300")]
    [InlineData("\"x\":300", "\"x\":350")]
    [InlineData("\"x\":300", "\"x\":1e999")]
    [InlineData("\"nextNodeId\":3", "\"nextNodeId\":2")]
    [InlineData("\"startNodeId\":1", "\"startNodeId\":2")]
    public void MalformedRoadPayload_IsRejectedBeforePublishing(string original, string replacement)
    {
        const string valid = """
            {"formatFamily":"simple-cities-v4","payloadType":"road-network","schemaVersion":6,
            "contentRevision":2,"nextNodeId":3,"nextEdgeId":2,"profileCatalogVersion":1,
            "map":{"widthMetres":8000,"heightMetres":8000,"origin":"center","metresPerUnit":1,"grid":"square-eight","cellSizeMetres":100},
            "nodes":[{"id":1,"x":0,"y":0},{"id":2,"x":300,"y":0}],
            "edges":[{"id":1,"startNodeId":1,"endNodeId":2,"profile":"street","points":[{"x":0,"y":0},{"x":300,"y":0}]}]}
            """;
        var network = new RoadNetwork();
        RoadStateToken before = network.Snapshot.Token;
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(valid.Replace(original, replacement)));
        Assert.Throws<InvalidDataException>(() => network.PlanLoad(RoadCodec.Read(source)));
        Assert.Equal(before, network.Snapshot.Token);
        Assert.Equal(0, network.Snapshot.EdgeCount);
    }

    [Theory]
    [InlineData(300, 0)]
    [InlineData(300, 300)]
    [InlineData(0, 300)]
    [InlineData(-300, 300)]
    [InlineData(-300, 0)]
    [InlineData(-300, -300)]
    [InlineData(0, -300)]
    [InlineData(300, -300)]
    public void BuildAcrossSeveralCells_PublishesOneRoadOnce(double x, double y)
    {
        var network = new RoadNetwork();
        RoadSnapshot before = network.Snapshot;
        var request = new RoadBuildRequest(before.Token, new RoadPoint(0, 0), new RoadPoint(x, y), RoadProfileId.Street);
        RoadBuildResult result = network.PlanBuild(request);

        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.Equal(before.Token, network.Snapshot.Token);
        Assert.Equal(1, network.Snapshot.NextNodeId);
        Assert.True(network.TryCommit(result.Plan!));
        RoadSnapshot after = network.Snapshot;
        Assert.Equal(2, after.NodeCount);
        RoadEdge edge = Assert.Single(after.Edges);
        Assert.Equal(new RoadPoint(0, 0), after.Nodes[0].Position);
        Assert.Equal(new RoadPoint(x, y), after.Nodes[1].Position);
        Assert.Equal(RoadProfileId.Street, edge.Profile);
        Assert.Equal(new NodeId(1), edge.Start);
        Assert.Equal(new NodeId(2), edge.End);
        Assert.Equal(new EdgeId(1), edge.Id);
        Assert.Equal(3, after.NextNodeId);
        Assert.Equal(2, after.NextEdgeId);
        Assert.Equal(2, after.Token.ContentRevision);
        Assert.Equal(1, after.Token.ChangeSequence);
        Assert.False(network.TryCommit(result.Plan!));
        Assert.Equal(before.Token, result.Plan!.SourceToken);
        Assert.Equal(2, result.Plan.AddedNodeCount);
        Assert.Equal(1, result.Plan.AddedEdgeCount);
    }
}
