namespace SimpleCities.RoadCore.Tests;

public sealed class PolylinePresentationTests
{
    [Fact]
    public void TurnSurface_CoversTheOutsideCornerWithBoundedPieces()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(300, 0), RoadProfileId.Street);
        Build(network, new(300, 0), new(300, 400), RoadProfileId.Street);
        RoadSurfaceData surface = RoadPresentation.Prepare(network.Snapshot.Nodes, network.Snapshot.Edges)!;

        Assert.Contains(surface.Pieces, piece => Contains(piece.Corners, new(302, -2)));
        Assert.DoesNotContain(surface.Pieces, piece => Contains(piece.Corners, new(307, -7)));
        Assert.All(surface.Pieces, piece => Assert.Equal(network.Snapshot.Edges[0].Id, piece.Edge.Id));
    }

    [Fact]
    public void SurfacePieces_RetainWholeEdgeArcLengthSourcesAcrossTheTurn()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(300, 0), RoadProfileId.Street);
        Build(network, new(300, 0), new(300, 400), RoadProfileId.Street);
        RoadSurfaceData surface = RoadPresentation.Prepare(network.Snapshot.Nodes, network.Snapshot.Edges)!;
        RoadSurfacePiece horizontal = Assert.Single(surface.Pieces, piece => piece.Start == new RoadPoint(0, 0));
        RoadSurfacePiece vertical = Assert.Single(surface.Pieces, piece => piece.End == new RoadPoint(300, 400));

        Assert.Equal(0, horizontal.StartParameter);
        Assert.Equal(3.0 / 7, horizontal.EndParameter);
        Assert.Equal(horizontal.EndParameter, vertical.StartParameter);
        Assert.Equal(1, vertical.EndParameter);
        RoadSurfacePiece[] joins = surface.Pieces.Where(piece => piece.Start == piece.End).ToArray();
        Assert.NotEmpty(joins);
        Assert.All(joins, piece =>
        {
            Assert.Equal(new RoadPoint(300, 0), piece.Start);
            Assert.Equal(3.0 / 7, piece.StartParameter);
            Assert.Equal(piece.StartParameter, piece.EndParameter);
        });
    }

    [Theory]
    [InlineData("dirt", 8)]
    [InlineData("street", 12)]
    [InlineData("arterial", 24)]
    [InlineData("highway", 32)]
    public void OpenRoadSurface_PreservesWidthAndLetsItsCapExtendBeyondTheMap(string profile, double width)
    {
        var network = new RoadNetwork();
        Build(network, new(3800, 0), new(4000, 0), new RoadProfileId(profile));
        RoadSurfacePiece piece = Assert.Single(RoadPresentation.Prepare(network.Snapshot.Nodes, network.Snapshot.Edges)!.Pieces);

        Assert.Equal(width, piece.Corners[0].DistanceTo(piece.Corners[3]));
        Assert.True(Contains(piece.Corners, new(4000 + width / 2 - 1, 0)));
        Assert.False(Contains(piece.Corners, new(4000 + width / 2 + 1, 0)));
        Assert.Equal(new RoadPoint(4000, 0), piece.End);
        Assert.Equal(1, piece.EndParameter);
    }

    [Fact]
    public void DifferentWidthConnection_HasNoGapAndRetainsBothProfileSources()
    {
        var network = new RoadNetwork();
        Build(network, new(0, 0), new(300, 0), RoadProfileId.Street);
        Build(network, new(300, 0), new(300, 400), RoadProfileId.Arterial);
        RoadSurfaceData surface = RoadPresentation.Prepare(network.Snapshot.Nodes, network.Snapshot.Edges)!;

        Assert.Contains(surface.Pieces, piece => Contains(piece.Corners, new(304, -2)));
        Assert.Contains(surface.Pieces, piece => piece.Edge.Profile == RoadProfileId.Street && Contains(piece.Corners, new(150, 0)));
        Assert.Contains(surface.Pieces, piece => piece.Edge.Profile == RoadProfileId.Arterial && Contains(piece.Corners, new(300, 200)));
        RoadSurfacePiece[] joins = surface.Pieces.Where(piece => piece.Start == piece.End).ToArray();
        Assert.Contains(joins, piece => piece.Edge.Profile == RoadProfileId.Street && piece.StartParameter == 1);
        Assert.Contains(joins, piece => piece.Edge.Profile == RoadProfileId.Arterial && piece.StartParameter == 0);
        Assert.All(joins, piece => Assert.All(piece.Corners,
            corner => Assert.InRange(corner.DistanceTo(new(300, 0)), 0, 12)));
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
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }
}
