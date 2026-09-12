namespace SimpleCities.RoadCore.Tests;

public sealed class PrimaryJunctionPresentationTests
{
    [Fact]
    public void Cross_HasAnExplicitContinuousJunctionSurface()
    {
        var network = new RoadNetwork();
        Build(network, new(-300, 0), new(300, 0), RoadProfileId.Street);
        Build(network, new(0, -300), new(0, 300), RoadProfileId.Arterial);
        RoadSurfaceData surface = RoadPresentation.Prepare(network.Snapshot.Nodes, network.Snapshot.Edges)!;

        RoadSurfacePiece[] junction = surface.Pieces.Where(piece => piece.Start == new RoadPoint(0, 0) && piece.Start == piece.End).ToArray();
        NodeId junctionId = Assert.Single(network.Snapshot.Nodes, node => node.Position == new RoadPoint(0, 0)).Id;
        Assert.NotEmpty(junction);
        Assert.Contains(junction, piece => Contains(piece.Corners, new(2, 3)));
        Assert.All(junction, piece =>
        {
            Assert.Contains(piece.Edge, network.Snapshot.Edges);
            Assert.Equal(junctionId, piece.JunctionNode);
            Assert.Equal(piece.StartParameter, piece.EndParameter);
            Assert.Contains(piece.StartParameter, new[] { 0.0, 1.0 });
            Assert.All(piece.Corners, point => Assert.True(point.IsFinite));
        });
        Assert.All(surface.Pieces.Where(piece => piece.Start != piece.End), piece => Assert.Null(piece.JunctionNode));
    }

    [Theory]
    [InlineData(25, false)]
    [InlineData(100, false)]
    [InlineData(25, true)]
    [InlineData(200, true)]
    public void UnequalWidthTJunction_HasNoCentralGapAndStableOwnedPatches(int cellSize, bool diagonal)
    {
        var network = new RoadNetwork(new MapDefinition(cellSize));
        Build(network, new(-cellSize, 0), new(cellSize, 0), RoadProfileId.Highway);
        Build(network, new(diagonal ? -cellSize : 0, -cellSize), new(0, 0), RoadProfileId.Dirt);
        RoadSnapshot snapshot = network.Snapshot;
        RoadNode node = Assert.Single(snapshot.Nodes, node => node.Position == new RoadPoint(0, 0));
        RoadSurfaceData surface = RoadPresentation.Prepare(snapshot.Nodes, snapshot.Edges)!;
        RoadSurfacePiece[] patches = surface.Pieces.Where(piece => piece.JunctionNode == node.Id).ToArray();
        Assert.NotEmpty(patches);

        // Literal interior points exercise both sides of the join and its center.
        foreach (RoadPoint point in new RoadPoint[] { new(0, 0), new(-2, -1), new(2, -1), new(-2, 1), new(2, 1) })
            Assert.Contains(patches, piece => Contains(piece.Corners, point));
        Assert.All(patches, piece =>
        {
            Assert.Equal(node.Position, piece.Start);
            Assert.Equal(node.Position, piece.End);
            Assert.Equal(piece.Edge.Start == node.Id ? 0 : 1, piece.StartParameter);
            Assert.Equal(piece.StartParameter, piece.EndParameter);
            Assert.All(piece.Corners, point => Assert.InRange(point.DistanceTo(node.Position), 0, 23));
        });

        RoadSurfacePiece[] reordered = RoadPresentation.Prepare(snapshot.Nodes.Reverse().ToArray(), snapshot.Edges.Reverse().ToArray())!
            .Pieces.Where(piece => piece.JunctionNode == node.Id).ToArray();
        Assert.Equal(patches.Length, reordered.Length);
        for (int i = 0; i < patches.Length; i++)
        {
            Assert.Equal(patches[i].Edge.Id, reordered[i].Edge.Id);
            Assert.Equal(patches[i].Corners, reordered[i].Corners);
            Assert.Equal(patches[i].StartParameter, reordered[i].StartParameter);
        }
    }

    [Fact]
    public void BoundaryJunction_SurfaceCanExtendOutsideWithoutCreatingOutsideNodes()
    {
        var network = new RoadNetwork();
        Build(network, new(4000, -100), new(4000, 100), RoadProfileId.Highway);
        Build(network, new(3900, 0), new(4000, 0), RoadProfileId.Dirt);
        RoadSurfaceData surface = RoadPresentation.Prepare(network.Snapshot.Nodes, network.Snapshot.Edges)!;

        Assert.Contains(surface.Pieces, piece => piece.JunctionNode is not null && Contains(piece.Corners, new(4002, 1)));
        Assert.All(network.Snapshot.Nodes, node => Assert.InRange(node.Position.X, -4000, 4000));
        Assert.All(surface.Pieces, piece => Assert.All(piece.Corners, point => Assert.True(point.IsFinite)));
    }

    [Fact]
    public void OneSidedFork_ConnectsAllThreeMouthsWithoutLeavingTheNodeOutsideItsPatch()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(100, 0), RoadProfileId.Street);
        Build(network, new(0, 0), new(100, 100), RoadProfileId.Dirt);
        Build(network, new(0, 0), new(0, 100), RoadProfileId.Arterial);
        RoadSurfaceData surface = RoadPresentation.Prepare(network.Snapshot.Nodes, network.Snapshot.Edges)!;

        Assert.Contains(surface.Pieces, piece => piece.JunctionNode is not null && Contains(piece.Corners, new(0, 0)));
        foreach (RoadPoint point in new RoadPoint[] { new(6, 0), new(3, 3), new(0, 6) })
            Assert.Contains(surface.Pieces, piece => piece.JunctionNode is not null && Contains(piece.Corners, point));
    }

    private static void Build(RoadNetwork network, RoadPoint start, RoadPoint end, RoadProfileId profile)
    {
        RoadBuildResult result = network.PlanBuild(new(network.Snapshot.Token, start, end, profile));
        Assert.Equal(RoadBuildStatus.Ready, result.Status);
        Assert.True(network.TryCommit(result.Plan!));
    }

    private static bool Contains(IReadOnlyList<RoadPoint> polygon, RoadPoint point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            RoadPoint a = polygon[i], b = polygon[j];
            double cross = (point.X - a.X) * (b.Y - a.Y) - (point.Y - a.Y) * (b.X - a.X);
            if (Math.Abs(cross) < 1e-9 && point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) &&
                point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y)) return true;
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }
}
