namespace SimpleCities.RoadCore.Tests;

public sealed class LoopRoadTests
{
    [Fact]
    public void FourStrokes_CloseIntoOneCanonicalSelfLoop()
    {
        var network = new RoadNetwork();
        Build(network, new(100, 0), new(100, 100));
        Build(network, new(100, 100), new(0, 100));
        Build(network, new(0, 100), new(0, 0));
        RoadSnapshot before = network.Snapshot;
        RoadBuildResult closing = network.PlanBuild(new(before.Token, new(0, 0), new(100, 0), RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Ready, closing.Status);
        Assert.Same(before, network.Snapshot);
        Assert.True(network.TryCommit(closing.Plan!));
        RoadNode seam = Assert.Single(network.Snapshot.Nodes);
        RoadEdge loop = Assert.Single(network.Snapshot.Edges);
        Assert.Equal(new NodeId(1), seam.Id);
        Assert.Equal(new RoadPoint(100, 0), seam.Position);
        Assert.Equal(seam.Id, loop.Start);
        Assert.Equal(seam.Id, loop.End);
        Assert.Equal(new RoadPoint[] { new(100, 0), new(0, 0), new(0, 100), new(100, 100), new(100, 0) }, loop.Points);
        Assert.Equal(400, loop.Length);
    }

    [Fact]
    public void PureLoopSeam_JoinsBothEndRolesWithoutEndpointCaps()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 0));
        Build(network, new(100, 0), new(100, 100));
        Build(network, new(100, 100), new(0, 100));
        Build(network, new(0, 100), new(0, 0));
        RoadSnapshot snapshot = network.Snapshot;
        RoadEdge loop = Assert.Single(snapshot.Edges);
        RoadNode seam = Assert.Single(snapshot.Nodes);
        RoadJunctionReadModel read = RoadJunctionQuery.Read(snapshot, seam.Id)!;
        Assert.Equal(2, read.Incidences.Count);
        Assert.Equal(4, read.Turns.Count);
        Assert.Contains(read.Incidences, item => item.Key == new RoadIncidenceKey(loop.Id, RoadEndRole.Start) && item.Outward == new RoadVector(0, 1));
        Assert.Contains(read.Incidences, item => item.Key == new RoadIncidenceKey(loop.Id, RoadEndRole.End) && item.Outward == new RoadVector(1, 0));
        RoadSurfaceData surface = RoadPresentation.Prepare(snapshot.Nodes, snapshot.Edges)!;
        RoadSurfacePiece[] joins = surface.Pieces.Where(piece => piece.Start == seam.Position && piece.End == seam.Position).ToArray();
        Assert.Contains(joins, piece => piece.StartParameter == 0 && piece.EndParameter == 0);
        Assert.Contains(joins, piece => piece.StartParameter == 1 && piece.EndParameter == 1);
        Assert.All(joins, piece => Assert.Null(piece.JunctionNode));
        RoadSurfacePiece first = Assert.Single(surface.Pieces, piece => piece.StartParameter == 0 && piece.EndParameter > 0);
        Assert.Equal(0, first.Corners.Min(point => point.Y));
        RoadSurfacePiece last = Assert.Single(surface.Pieces, piece => piece.StartParameter < 1 && piece.EndParameter == 1);
        Assert.Equal(0, last.Corners.Min(point => point.X));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void LoopClosure_KeepsTheSmallestExistingNodeAndChoosesAStableDirection(int first, bool reverse)
    {
        RoadPoint[] corners = [new(0, 0), new(0, 100), new(100, 100), new(100, 0)];
        var network = new RoadNetwork();
        RoadPoint Point(int step) => corners[(first + (reverse ? -step : step) + 8) % 4];
        for (int i = 0; i < 4; i++) Build(network, Point(i), Point(i + 1));
        RoadNode seam = Assert.Single(network.Snapshot.Nodes);
        RoadEdge edge = Assert.Single(network.Snapshot.Edges);
        Assert.Equal(new NodeId(1), seam.Id);
        Assert.Equal(corners[first], seam.Position);
        Assert.Equal(seam.Position, edge.Points[0]);
        Assert.Equal(seam.Position, edge.Points[^1]);
        RoadPoint[] expected = first switch
        {
            0 => [new(0, 0), new(0, 100), new(100, 100), new(100, 0), new(0, 0)],
            1 => [new(0, 100), new(0, 0), new(100, 0), new(100, 100), new(0, 100)],
            2 => [new(100, 100), new(0, 100), new(0, 0), new(100, 0), new(100, 100)],
            _ => [new(100, 0), new(0, 0), new(0, 100), new(100, 100), new(100, 0)],
        };
        Assert.Equal(expected, edge.Points);
    }

    [Fact]
    public void BranchOnClosedRoad_MovesTheOldSeamToTheRealJunction()
    {
        var network = Square();
        RoadNode oldSeam = Assert.Single(network.Snapshot.Nodes);
        Build(network, new(100, 100), new(200, 100));
        RoadSnapshot snapshot = network.Snapshot;
        Assert.Equal(2, snapshot.NodeCount);
        Assert.Equal(2, snapshot.EdgeCount);
        Assert.DoesNotContain(snapshot.Nodes, node => node.Id == oldSeam.Id);
        RoadNode junction = Assert.Single(snapshot.Nodes, node => node.Position == new RoadPoint(100, 100));
        RoadEdge loop = Assert.Single(snapshot.Edges, edge => edge.Start == edge.End);
        Assert.Equal(junction.Id, loop.Start);
        RoadJunctionReadModel read = RoadJunctionQuery.Read(snapshot, junction.Id)!;
        Assert.Equal(3, read.Incidences.Count);
        Assert.Equal(9, read.Turns.Count);
        Assert.Equal(2, read.Incidences.Count(item => item.Key.Edge == loop.Id));
        Assert.Contains(read.Incidences, item => item.Key.Role == RoadEndRole.Start && item.Key.Edge == loop.Id && item.Outward == new RoadVector(-1, 0));
        Assert.Contains(read.Incidences, item => item.Key.Role == RoadEndRole.End && item.Key.Edge == loop.Id && item.Outward == new RoadVector(0, -1));
        RoadSurfaceData surface = RoadPresentation.Prepare(snapshot.Nodes, snapshot.Edges)!;
        RoadSurfacePiece[] junctionPieces = surface.Pieces.Where(piece => piece.JunctionNode == junction.Id).ToArray();
        Assert.Contains(junctionPieces, piece => piece.Edge.Id == loop.Id && piece.StartParameter == 0 && piece.EndParameter == 0);
        Assert.Contains(junctionPieces, piece => piece.Edge.Id == loop.Id && piece.StartParameter == 1 && piece.EndParameter == 1);
        Assert.All(junctionPieces, piece => Assert.Equal(junction.Position, snapshot.Resolve(new(snapshot.Token, piece.Edge.Id, piece.StartParameter))));
    }

    [Fact]
    public void ThreeDifferentPathsBetweenJunctions_KeepDistinctEdgeIdentitiesAndLocations()
    {
        var network = Square();
        Build(network, new(0, 0), new(100, 100));
        RoadSnapshot snapshot = network.Snapshot;
        Assert.Equal(2, snapshot.NodeCount);
        Assert.Equal(3, snapshot.EdgeCount);
        Assert.Equal(3, snapshot.Edges.Select(edge => edge.Id).Distinct().Count());
        NodeId a = snapshot.Nodes[0].Id, b = snapshot.Nodes[1].Id;
        Assert.All(snapshot.Edges, edge => { Assert.Equal(a, edge.Start); Assert.Equal(b, edge.End); });
        Assert.All(snapshot.Nodes, node => Assert.Equal(3, RoadJunctionQuery.Read(snapshot, node.Id)!.Incidences.Count));
        RoadPoint?[] midpoints = snapshot.Edges.Select(edge => snapshot.Resolve(new(snapshot.Token, edge.Id, 0.5))).ToArray();
        Assert.Contains(new RoadPoint(0, 100), midpoints);
        Assert.Contains(new RoadPoint(100, 0), midpoints);
        Assert.Contains(new RoadPoint(50, 50), midpoints);
    }

    [Fact]
    public void TwoLoopsSharingOneJunction_KeepFourDistinctEndConnections()
    {
        var network = Square();
        Build(network, new(0, 0), new(-100, 0));
        Build(network, new(-100, 0), new(-100, -100));
        Build(network, new(-100, -100), new(0, -100));
        Build(network, new(0, -100), new(0, 0));
        RoadSnapshot snapshot = network.Snapshot;
        RoadNode junction = Assert.Single(snapshot.Nodes);
        Assert.Equal(2, snapshot.EdgeCount);
        Assert.All(snapshot.Edges, edge => { Assert.Equal(junction.Id, edge.Start); Assert.Equal(junction.Id, edge.End); });
        RoadJunctionReadModel read = RoadJunctionQuery.Read(snapshot, junction.Id)!;
        Assert.Equal(4, read.Incidences.Count);
        Assert.Equal(4, read.Incidences.Select(item => item.Key).Distinct().Count());
        Assert.Equal(16, read.Turns.Count);
        RoadSurfaceData surface = RoadPresentation.Prepare(snapshot.Nodes, snapshot.Edges)!;
        foreach (RoadIncidence incidence in read.Incidences)
            Assert.Contains(surface.Pieces, piece => piece.JunctionNode == junction.Id && piece.Edge.Id == incidence.Key.Edge &&
                piece.StartParameter == (incidence.Key.Role == RoadEndRole.Start ? 0 : 1));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void ClosingAtCellCenter_CreatesALoopWithTwoDiagonalRolesAndOneBranch(int cell)
    {
        var network = new RoadNetwork(new MapDefinition(cell));
        Build(network, new(0, 0), new(cell, cell));
        Build(network, new(0, 0), new(cell, 0));
        Build(network, new(cell, 0), new(cell / 2d, cell / 2d));
        RoadSnapshot snapshot = network.Snapshot;
        RoadNode junction = Assert.Single(snapshot.Nodes, node => node.Position == new RoadPoint(cell / 2d, cell / 2d));
        RoadEdge loop = Assert.Single(snapshot.Edges, edge => edge.Start == edge.End);
        Assert.Equal(junction.Id, loop.Start);
        Assert.Equal(2, snapshot.NodeCount);
        Assert.Equal(2, snapshot.EdgeCount);
        RoadJunctionReadModel read = RoadJunctionQuery.Read(snapshot, junction.Id)!;
        Assert.Equal(3, read.Incidences.Count);
        Assert.Equal(2, read.Incidences.Count(item => item.Key.Edge == loop.Id));
        Assert.All(read.Incidences, item => Assert.InRange(Math.Abs(item.Outward.X), 0.7071067, 0.7071069));
    }

    [Fact]
    public void CancelledClosingAndStaleLoopPlans_DoNotPublishPartialTopology()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 0));
        Build(network, new(100, 0), new(100, 100));
        var before = network.Snapshot;
        var request = new RoadBuildRequest(before.Token, new(100, 100), new(0, 0), RoadProfileId.Street);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => network.PlanBuild(request, cancellation.Token));
        Assert.Same(before, network.Snapshot);
        RoadBuildResult ready = network.PlanBuild(request);
        Assert.Equal(RoadBuildStatus.Ready, ready.Status);
        Assert.Same(before, network.Snapshot);
        Build(network, new(-200, -200), new(-100, -200));
        RoadSnapshot current = network.Snapshot;
        Assert.False(network.TryCommit(ready.Plan!));
        Assert.Same(current, network.Snapshot);
        Assert.DoesNotContain(current.Edges, edge => edge.Start == edge.End);
    }

    [Fact]
    public void OverlapOnLoop_HasPriorityAndLeavesItsIdentityAndContentUntouched()
    {
        var network = Square();
        RoadSnapshot before = network.Snapshot;
        RoadBuildResult overlap = network.PlanBuild(new(before.Token, new(-100, 0), new(100, 0), RoadProfileId.Dirt));
        Assert.Equal(RoadBuildStatus.Rejected, overlap.Status);
        Assert.Equal(new[] { new RoadConflictSpan(0.5, 1) }, overlap.Conflicts);
        Assert.Null(overlap.Plan);
        Assert.Same(before, network.Snapshot);
    }

    [Fact]
    public void LoopWithProfileBoundaries_RetainsBothDifferentPathsAndNecessaryNodes()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 0));
        Build(network, new(100, 0), new(100, 100));
        Build(network, new(100, 100), new(0, 100), RoadProfileId.Dirt);
        Build(network, new(0, 100), new(0, 0), RoadProfileId.Dirt);
        Assert.Equal(2, network.Snapshot.NodeCount);
        Assert.Equal(2, network.Snapshot.EdgeCount);
        Assert.Single(network.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Street);
        Assert.Single(network.Snapshot.Edges, edge => edge.Profile == RoadProfileId.Dirt);
        Assert.All(network.Snapshot.Nodes, node => Assert.Equal(2, RoadJunctionQuery.Read(network.Snapshot, node.Id)!.Incidences.Count));
    }

    [Fact]
    public void SplittingLoopBeyondEdgeBudget_RejectsTheEntireClosingChord()
    {
        RoadPoint[] loop = Assert.Single(Square().Snapshot.Edges).Points.ToArray();
        RoadNetwork network = TopologyCapacityTests.LoadLines(new[] { loop }
            .Concat(TopologyCapacityTests.DisconnectedLines(16383)));
        RoadSnapshot before = network.Snapshot;
        Assert.Equal(16384, before.EdgeCount);
        Assert.Equal(32767, before.NodeCount);
        RoadBuildResult result = network.PlanBuild(new(before.Token, new(0, 0), new(100, 100), RoadProfileId.Street));
        Assert.Equal(RoadBuildStatus.Rejected, result.Status);
        Assert.Contains("资源上限", result.Reason);
        Assert.Null(result.Plan);
        Assert.Same(before, network.Snapshot);
    }

    internal static RoadNetwork Square()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 0));
        Build(network, new(100, 0), new(100, 100));
        Build(network, new(100, 100), new(0, 100));
        Build(network, new(0, 100), new(0, 0));
        return network;
    }

    internal static void Build(RoadNetwork network, RoadPoint start, RoadPoint end, RoadProfileId? profile = null)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, profile ?? RoadProfileId.Street));
        Assert.True(result.Status == RoadBuildStatus.Ready, result.Reason);
        Assert.True(network.TryCommit(result.Plan!));
    }
}
