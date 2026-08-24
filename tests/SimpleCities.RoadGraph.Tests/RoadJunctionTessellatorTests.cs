using Godot;

namespace SimpleCities.Tests;

public sealed class RoadJunctionTessellatorTests
{
    private const int NodeID = 7;

    public static TheoryData<Vector2[]> OrthogonalLayouts => new()
    {
        { [
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(-1f, 0f),
        ] },
        { [
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(-1f, 0f),
            new Vector2(0f, -1f),
        ] },
    };

    public static TheoryData<float> InvalidWidths => new()
    {
        0f,
        RoadNumericPolicy.MinimumDisplayRoadWidth * 0.5f,
        float.NaN,
        RoadNumericPolicy.MaximumDisplayRoadWidth + 1f,
    };

    [Theory]
    [MemberData(nameof(OrthogonalLayouts))]
    public void OrthogonalJunctionsProduceFiniteCounterClockwiseSectors(
        Vector2[] directions)
    {
        RoadJunctionIncidence[] incidences = directions
            .Select((direction, index) => Incidence(
                edgeID: 10 + index,
                endpoint: EdgeEndpoint.A,
                direction,
                (RoadType)index,
                width: 4f))
            .ToArray();

        RoadJunctionTriangle[] triangles = RoadJunctionTessellator.Tessellate(
            Vector2.Zero,
            incidences.Reverse().ToArray());

        Assert.NotEmpty(triangles);
        Assert.Equal(
            Enumerable.Range(0, directions.Length),
            triangles.Select(triangle => triangle.SectorOrder).Distinct().Order());
        Assert.Equal(
            directions.Length,
            triangles.Select(triangle => triangle.Incidence.EdgeID).Distinct().Count());
        Assert.Equal(
            triangles.Length,
            triangles.Select(TriangleGeometry).Distinct().Count());
        Assert.All(triangles, AssertValidTriangle);
    }

    [Fact]
    public void MixedWidthsAndColorsRemainOwnedByTheirIncidenceSectors()
    {
        RoadJunctionIncidence[] incidences =
        [
            Incidence(10, EdgeEndpoint.A, new Vector2(1f, 0f), RoadType.Dirt, 4f),
            Incidence(11, EdgeEndpoint.B, new Vector2(0f, 1f), RoadType.Highway, 10f),
            Incidence(12, EdgeEndpoint.A, new Vector2(-1f, 0f), RoadType.Street, 6f),
        ];

        RoadJunctionTriangle[] triangles = RoadJunctionTessellator.Tessellate(
            Vector2.Zero,
            incidences);

        Assert.Equal(
            incidences.Select(incidence => incidence.EdgeID).Order(),
            triangles.Select(triangle => triangle.Incidence.EdgeID).Distinct().Order());
        Assert.All(triangles, triangle =>
        {
            RoadJunctionIncidence expected = Assert.Single(
                incidences,
                incidence => incidence.EdgeID == triangle.Incidence.EdgeID);
            Assert.Equal(expected.Style.Color, triangle.Incidence.Style.Color);
            Assert.Equal(expected.Location, triangle.Incidence.Location);
            AssertValidTriangle(triangle);
        });
    }

    [Fact]
    public void AcuteGapFallsBackBeforeTheMiterLimit()
    {
        const float width = 8f;
        RoadJunctionIncidence[] incidences =
        [
            Incidence(10, EdgeEndpoint.A, new Vector2(1f, 0f), RoadType.Street, width),
            Incidence(11, EdgeEndpoint.A, new Vector2(1f, 0.01f), RoadType.Arterial, width),
            Incidence(12, EdgeEndpoint.A, new Vector2(-1f, 0f), RoadType.Highway, width),
        ];

        RoadJunctionTriangle[] triangles = RoadJunctionTessellator.Tessellate(
            Vector2.Zero,
            incidences);

        float maximumDistance = triangles
            .SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C })
            .Max(point => point.Length());
        Assert.InRange(
            maximumDistance,
            0f,
            width * 0.5f * RoadJunctionTessellator.MiterLimit +
            RoadJunctionTessellator.MaximumQuantizationError);
        Assert.All(triangles, AssertValidTriangle);
    }

    [Fact]
    public void SameDirectionIncidencesUseRoadTypePriorityAndMaximumWidth()
    {
        RoadJunctionIncidence[] incidences =
        [
            Incidence(10, EdgeEndpoint.A, new Vector2(1f, 0f), RoadType.Dirt, 12f),
            Incidence(11, EdgeEndpoint.A, new Vector2(2f, 0f), RoadType.Highway, 6f),
            Incidence(12, EdgeEndpoint.B, new Vector2(3f, 0f), RoadType.Street, 8f),
        ];

        RoadJunctionTriangle[] triangles = RoadJunctionTessellator.Tessellate(
            Vector2.Zero,
            incidences);

        Assert.NotEmpty(triangles);
        Assert.All(triangles, triangle =>
        {
            Assert.Equal(11, triangle.Incidence.EdgeID);
            Assert.Equal(RoadType.Highway, triangle.Incidence.Style.RoadType);
            Assert.Equal(6f, triangle.CenterlineLength);
            AssertValidTriangle(triangle);
        });
    }

    [Fact]
    public void SelfLoopEndpointRolesParticipateIndependentlyBesideABranch()
    {
        RoadJunctionIncidence[] incidences =
        [
            Incidence(10, EdgeEndpoint.A, new Vector2(1f, 0f), RoadType.Street, 6f),
            Incidence(10, EdgeEndpoint.B, new Vector2(-1f, 0f), RoadType.Street, 6f),
            Incidence(11, EdgeEndpoint.A, new Vector2(0f, 1f), RoadType.Arterial, 8f),
        ];

        RoadJunctionTriangle[] triangles = RoadJunctionTessellator.Tessellate(
            Vector2.Zero,
            incidences);

        Assert.Contains(
            triangles,
            triangle => triangle.Incidence is { EdgeID: 10, Endpoint: EdgeEndpoint.A });
        Assert.Contains(
            triangles,
            triangle => triangle.Incidence is { EdgeID: 10, Endpoint: EdgeEndpoint.B });
        Assert.Contains(
            triangles,
            triangle => triangle.Incidence.EdgeID == 11);
        Assert.All(triangles, AssertValidTriangle);
    }

    [Fact]
    public void ParallelSameDirectionIncidencesCollapseToTheHigherPriorityOwner()
    {
        RoadJunctionIncidence[] incidences =
        [
            Incidence(10, EdgeEndpoint.A, new Vector2(1f, 0f), RoadType.Dirt, 12f),
            Incidence(11, EdgeEndpoint.A, new Vector2(1f, 0f), RoadType.Highway, 6f),
            Incidence(12, EdgeEndpoint.A, new Vector2(0f, 1f), RoadType.Street, 8f),
        ];

        RoadJunctionTriangle[] triangles = RoadJunctionTessellator.Tessellate(
            Vector2.Zero,
            incidences);

        Assert.DoesNotContain(triangles, triangle => triangle.Incidence.EdgeID == 10);
        Assert.Contains(triangles, triangle => triangle.Incidence.EdgeID == 11);
        Assert.Contains(triangles, triangle => triangle.Incidence.EdgeID == 12);
    }

    [Fact]
    public void EdgeIdRenamingAndEnumerationOrderPreserveVisualAndMappedOwnership()
    {
        RoadJunctionIncidence[] first =
        [
            Incidence(10, EdgeEndpoint.A, new Vector2(1f, 0f), RoadType.Dirt, 4f),
            Incidence(11, EdgeEndpoint.B, new Vector2(0f, 1f), RoadType.Highway, 10f),
            Incidence(12, EdgeEndpoint.A, new Vector2(-1f, -0.5f), RoadType.Street, 6f),
        ];
        RoadJunctionIncidence[] permuted = [first[2], first[0], first[1]];
        IReadOnlyDictionary<int, int> edgeIDMap = new Dictionary<int, int>
        {
            [10] = 100,
            [11] = 101,
            [12] = 102,
        };
        RoadJunctionIncidence[] renamed =
        [
            RenameEdge(first[2], edgeIDMap[12]),
            RenameEdge(first[0], edgeIDMap[10]),
            RenameEdge(first[1], edgeIDMap[11]),
        ];

        RoadJunctionTriangle[] firstTriangles = RoadJunctionTessellator.Tessellate(
            new Vector2(0.125f, -0.25f),
            first);
        RoadJunctionTriangle[] permutedTriangles = RoadJunctionTessellator.Tessellate(
            new Vector2(0.125f, -0.25f),
            permuted);
        RoadJunctionTriangle[] renamedTriangles = RoadJunctionTessellator.Tessellate(
            new Vector2(0.125f, -0.25f),
            renamed);

        Assert.Equal(firstTriangles, permutedTriangles);
        Assert.Equal(
            firstTriangles.Select(VisualGeometry),
            renamedTriangles.Select(VisualGeometry));
        Assert.Equal(
            firstTriangles.Select(triangle => RenameEdge(
                triangle.Incidence,
                edgeIDMap[triangle.Incidence.EdgeID])),
            renamedTriangles.Select(triangle => triangle.Incidence));
    }

    [Fact]
    public void CoordinatesUseTheFixedQuantizationGridAtTheLegalMaximumScale()
    {
        Vector2 nodePosition = new(0.0006f, -0.0006f);
        RoadJunctionIncidence[] incidences =
        [
            Incidence(10, EdgeEndpoint.A, new Vector2(1f, 0f), RoadType.Dirt, 4f),
            Incidence(11, EdgeEndpoint.A, new Vector2(0f, 1f), RoadType.Street, 6f),
            Incidence(12, EdgeEndpoint.A, new Vector2(-1f, 0f), RoadType.Highway, 10f),
        ];

        RoadJunctionTriangle[] triangles = RoadJunctionTessellator.Tessellate(
            nodePosition,
            incidences);

        Vector2 quantizedNode = Assert.Single(
            triangles.Select(triangle => triangle.NodePosition).Distinct());
        Assert.InRange(
            quantizedNode.DistanceTo(nodePosition),
            0f,
            MathF.Sqrt(2f) * RoadJunctionTessellator.MaximumQuantizationError);
        Assert.All(
            triangles.SelectMany(triangle => new[]
            {
                triangle.NodePosition,
                triangle.A,
                triangle.B,
                triangle.C,
            }),
            AssertOnQuantizationGrid);

        RoadJunctionIncidence[] maximumScale = incidences
            .Select(incidence => incidence with
            {
                Style = incidence.Style with
                {
                    Width = RoadNumericPolicy.MaximumDisplayRoadWidth,
                },
            })
            .ToArray();
        RoadJunctionTriangle[] maximumTriangles = RoadJunctionTessellator.Tessellate(
            new Vector2(
                RoadNumericPolicy.MaximumCoordinateMagnitude,
                RoadNumericPolicy.MaximumCoordinateMagnitude),
            maximumScale);
        Assert.NotEmpty(maximumTriangles);
        Assert.All(maximumTriangles, AssertValidTriangle);
    }

    [Fact]
    public void MinimumDisplayWidthSurvivesDiagonalQuantization()
    {
        RoadJunctionIncidence[] incidences =
        [
            Incidence(10, EdgeEndpoint.A, new Vector2(1f, 1f), RoadType.Dirt,
                RoadNumericPolicy.MinimumDisplayRoadWidth),
            Incidence(11, EdgeEndpoint.A, new Vector2(-1f, 1f), RoadType.Street,
                RoadNumericPolicy.MinimumDisplayRoadWidth),
            Incidence(12, EdgeEndpoint.A, new Vector2(-1f, -1f), RoadType.Highway,
                RoadNumericPolicy.MinimumDisplayRoadWidth),
        ];

        RoadJunctionTriangle[] triangles = RoadJunctionTessellator.Tessellate(
            Vector2.Zero,
            incidences);

        Assert.NotEmpty(triangles);
        Assert.All(triangles, AssertValidTriangle);
    }

    [Theory]
    [MemberData(nameof(InvalidWidths))]
    public void InvalidDisplayWidthIsRejectedBeforeTessellation(float width)
    {
        RoadJunctionIncidence[] incidences =
        [
            Incidence(10, EdgeEndpoint.A, new Vector2(1f, 0f), RoadType.Dirt, width),
            Incidence(11, EdgeEndpoint.A, new Vector2(0f, 1f), RoadType.Street, 6f),
            Incidence(12, EdgeEndpoint.A, new Vector2(-1f, 0f), RoadType.Highway, 10f),
        ];

        Assert.Throws<ArgumentException>(() =>
            RoadJunctionTessellator.Tessellate(Vector2.Zero, incidences));
    }

    [Fact]
    public void NonCanonicalEndpointLocationIsRejectedBeforeTessellation()
    {
        RoadJunctionIncidence[] incidences =
        [
            Incidence(10, EdgeEndpoint.A, new Vector2(1f, 0f), RoadType.Dirt, 4f)
                with { Location = new RoadLocation(10, 1, 0f) },
            Incidence(11, EdgeEndpoint.A, new Vector2(0f, 1f), RoadType.Street, 6f),
            Incidence(12, EdgeEndpoint.A, new Vector2(-1f, 0f), RoadType.Highway, 10f),
        ];

        Assert.Throws<ArgumentException>(() =>
            RoadJunctionTessellator.Tessellate(Vector2.Zero, incidences));
    }

    private static RoadJunctionIncidence Incidence(
        int edgeID,
        EdgeEndpoint endpoint,
        Vector2 direction,
        RoadType roadType,
        float width) => new(
            NodeID,
            edgeID,
            endpoint,
            direction,
            new RoadTypeStyleDefinition(
                roadType,
                roadType.ToString(),
                ColorFor(roadType),
                width),
            new RoadLocation(
                edgeID,
                0,
                endpoint == EdgeEndpoint.A
                    ? RoadGeometrySegment.ParameterStart
                    : RoadGeometrySegment.ParameterEnd));

    private static Color ColorFor(RoadType roadType) => roadType switch
    {
        RoadType.Dirt => new Color("#8A6652"),
        RoadType.Street => new Color("#60727C"),
        RoadType.Arterial => new Color("#D7A928"),
        RoadType.Highway => new Color("#C84B3A"),
        _ => throw new ArgumentOutOfRangeException(nameof(roadType)),
    };

    private static RoadJunctionIncidence RenameEdge(
        RoadJunctionIncidence incidence,
        int edgeID) => incidence with
        {
            EdgeID = edgeID,
            Location = incidence.Location with { EdgeID = edgeID },
        };

    private static void AssertValidTriangle(RoadJunctionTriangle triangle)
    {
        Assert.True(triangle.NodePosition.IsFinite());
        Assert.True(triangle.A.IsFinite());
        Assert.True(triangle.B.IsFinite());
        Assert.True(triangle.C.IsFinite());
        Assert.True(Cross(triangle.B - triangle.A, triangle.C - triangle.A) > 0d);
        Assert.True(float.IsFinite(triangle.CenterlineLength));
        Assert.True(triangle.CenterlineLength > 0f);
    }

    private static void AssertOnQuantizationGrid(Vector2 point)
    {
        Assert.Equal(
            Math.Round(point.X * RoadJunctionTessellator.QuantizationScale),
            point.X * RoadJunctionTessellator.QuantizationScale,
            precision: 5);
        Assert.Equal(
            Math.Round(point.Y * RoadJunctionTessellator.QuantizationScale),
            point.Y * RoadJunctionTessellator.QuantizationScale,
            precision: 5);
    }

    private static (Vector2 A, Vector2 B, Vector2 C) TriangleGeometry(
        RoadJunctionTriangle triangle) =>
        (triangle.A, triangle.B, triangle.C);

    private static (
        int SectorOrder,
        RoadType RoadType,
        Color Color,
        float CenterlineLength,
        Vector2 NodePosition,
        Vector2 A,
        Vector2 B,
        Vector2 C) VisualGeometry(RoadJunctionTriangle triangle) =>
        (
            triangle.SectorOrder,
            triangle.Incidence.Style.RoadType,
            triangle.Incidence.Style.Color,
            triangle.CenterlineLength,
            triangle.NodePosition,
            triangle.A,
            triangle.B,
            triangle.C);

    private static double Cross(Vector2 first, Vector2 second) =>
        (double)first.X * second.Y - (double)first.Y * second.X;
}
