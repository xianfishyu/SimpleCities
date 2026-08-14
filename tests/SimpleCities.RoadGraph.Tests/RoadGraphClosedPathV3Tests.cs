using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Tests;

public sealed class RoadGraphClosedPathV3Tests
{
    [Fact]
    public void SubmitPolyline_SimpleLoopCreatesOneRootedSelfLoopAtFirstAnchor()
    {
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(10f, 0f),
            new Vector2(10f, 10f),
            new Vector2(0f, 10f),
            Vector2.Zero,
        ]);

        Assert.True(result.Success, $"Submission failed with {result.Error}.");
        GraphNode seam = Assert.Single(graph.GetAllNodes());
        GraphEdge loop = Assert.Single(graph.GetAllEdges());
        Assert.Equal(Vector2.Zero, seam.Position);
        Assert.Equal(seam.ID, loop.NodeA);
        Assert.Equal(seam.ID, loop.NodeB);
        Assert.Equal(2, seam.IncidenceCount);
        Assert.Equal(1, seam.IncidentEdgeCount);
        Assert.Equal(4, loop.GeometrySegments.Count);
        graph.AssertInvariants();
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void SubmitPath_ExactFullTurnCreatesOneRootedSelfLoop(float direction)
    {
        var arc = new CircularArcRoadGeometrySegment(
            new Vector2(4f, -3f), 8f, 0.37f, direction * Mathf.Tau);
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPath(new RoadBuildRequest(new RoadPath([arc]), RoadType.Street));

        Assert.True(result.Success, $"Submission failed with {result.Error}.");
        GraphNode seam = Assert.Single(graph.GetAllNodes());
        GraphEdge loop = Assert.Single(graph.GetAllEdges());
        Assert.Equal(loop.NodeA, loop.NodeB);
        Assert.Equal(seam.ID, loop.NodeA);
        CircularArcRoadGeometrySegment stored = Assert.IsType<CircularArcRoadGeometrySegment>(
            Assert.Single(loop.GeometrySegments));
        Assert.True(stored.IsFullTurn);
        Assert.True(RoadExactPredicates.SameBits(seam.Position, stored.Start));
        Assert.True(RoadExactPredicates.SameBits(stored.Start, stored.End));
        graph.AssertInvariants();
    }

    [Fact]
    public void SubmitPolyline_LollipopCreatesJunctionSelfLoopAndTail()
    {
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-10f, 0f),
            Vector2.Zero,
            new Vector2(10f, 0f),
            new Vector2(10f, 10f),
            new Vector2(0f, 10f),
            Vector2.Zero,
        ]);

        Assert.True(result.Success, $"Submission failed with {result.Error}.");
        GraphNode junction = Assert.Single(graph.GetAllNodes(), node => node.Position == Vector2.Zero);
        Assert.Equal(3, junction.IncidenceCount);
        Assert.Equal(2, junction.IncidentEdgeCount);
        Assert.Single(graph.GetAllEdges(), edge => edge.NodeA == junction.ID && edge.NodeB == junction.ID);
        Assert.Single(graph.GetAllEdges(), edge => edge.NodeA != edge.NodeB);
        Assert.Equal(2, graph.GetAllNodes().Count());
        Assert.Equal(2, graph.GetAllEdges().Count());
        graph.AssertInvariants();
    }

    [Fact]
    public void SubmitPolyline_FigureEightCreatesTwoSelfLoopsAtOneJunction()
    {
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            Vector2.Zero,
            new Vector2(-10f, 10f),
            new Vector2(-20f, 0f),
            new Vector2(-10f, -10f),
            Vector2.Zero,
            new Vector2(10f, 10f),
            new Vector2(20f, 0f),
            new Vector2(10f, -10f),
            Vector2.Zero,
        ]);

        Assert.True(result.Success, $"Submission failed with {result.Error}.");
        GraphNode junction = Assert.Single(graph.GetAllNodes());
        Assert.Equal(Vector2.Zero, junction.Position);
        Assert.Equal(4, junction.IncidenceCount);
        Assert.Equal(2, junction.IncidentEdgeCount);
        Assert.Equal(2, graph.GetAllEdges().Count());
        Assert.All(graph.GetAllEdges(), edge =>
        {
            Assert.Equal(junction.ID, edge.NodeA);
            Assert.Equal(junction.ID, edge.NodeB);
        });
        graph.AssertInvariants();
    }

    [Fact]
    public void SubmitPolyline_InteriorSelfCrossingCreatesFigureEightJunction()
    {
        var graph = new RoadGraph();

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-10f, -10f),
            new Vector2(10f, 10f),
            new Vector2(-10f, 10f),
            new Vector2(10f, -10f),
            new Vector2(-10f, -10f),
        ]);

        Assert.True(result.Success, $"Submission failed with {result.Error}.");
        GraphNode junction = Assert.Single(graph.GetAllNodes());
        Assert.Equal(Vector2.Zero, junction.Position);
        Assert.Equal(4, junction.IncidenceCount);
        Assert.Equal(2, graph.GetAllEdges().Count());
        Assert.All(graph.GetAllEdges(), edge => Assert.Equal(edge.NodeA, edge.NodeB));
        graph.AssertInvariants();
    }

    [Fact]
    public void SubmitPolyline_LoopCrossingExistingRoadTwiceCreatesTwoParallelLoopArcs()
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-20f, 0f),
            new Vector2(20f, 0f),
        ]).Success);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-10f, -10f),
            new Vector2(10f, -10f),
            new Vector2(10f, 10f),
            new Vector2(-10f, 10f),
            new Vector2(-10f, -10f),
        ]);

        Assert.True(result.Success, $"Submission failed with {result.Error}.");
        GraphNode[] junctions = graph.GetAllNodes()
            .Where(node => node.IncidenceCount == 4)
            .OrderBy(node => node.Position.X)
            .ToArray();
        Assert.True(
            junctions.Length == 2,
            "Expected two junctions, found: " + string.Join(
                "; ",
                graph.GetAllNodes()
                    .OrderBy(node => node.ID)
                    .Select(node =>
                        $"{node.ID}@{node.Position}:inc={node.IncidenceCount},edges={node.IncidentEdgeCount}")));
        Assert.Equal(new Vector2(-10f, 0f), junctions[0].Position);
        Assert.Equal(new Vector2(10f, 0f), junctions[1].Position);
        GraphEdge[] parallel = graph.GetAllEdges().Where(edge =>
            (edge.NodeA == junctions[0].ID && edge.NodeB == junctions[1].ID) ||
            (edge.NodeA == junctions[1].ID && edge.NodeB == junctions[0].ID)).ToArray();
        Assert.Equal(3, parallel.Length);
        Assert.Equal(2, parallel.Count(edge => edge.GeometrySegments.Count > 1));
        Assert.Equal(5, graph.GetAllEdges().Count());
        graph.AssertInvariants();
    }

    [Fact]
    public void RemoveEdge_TwoJunctionLoopRelocatesSeamToRemainingJunction()
    {
        Vector2 originalSeamPosition = Vector2.Zero;
        Vector2 remainingJunctionPosition = new(100f, 100f);
        Vector2 removedBranchEnd = new(-100f, 0f);
        Vector2 remainingBranchEnd = new(200f, 100f);
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            originalSeamPosition,
            new Vector2(100f, 0f),
            remainingJunctionPosition,
            new Vector2(0f, 100f),
            originalSeamPosition,
        ]).Success);
        Assert.True(graph.SubmitPolyline(
            RoadType.Street,
            [originalSeamPosition, removedBranchEnd]).Success);
        Assert.True(graph.SubmitPolyline(
            RoadType.Street,
            [remainingJunctionPosition, remainingBranchEnd]).Success);
        GraphNode originalSeam = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position == originalSeamPosition);
        GraphNode remainingJunction = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position == remainingJunctionPosition);
        GraphNode removedEndpoint = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position == removedBranchEnd);
        GraphEdge removedBranch = Assert.Single(
            graph.GetAllEdges(),
            edge => edge.NodeA == removedEndpoint.ID || edge.NodeB == removedEndpoint.ID);

        Assert.True(graph.RemoveEdge(removedBranch.ID));

        Assert.Null(graph.GetNode(originalSeam.ID));
        Assert.Null(graph.GetNode(removedEndpoint.ID));
        GraphNode relocatedSeam = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position == remainingJunctionPosition);
        GraphNode remainingEndpoint = Assert.Single(
            graph.GetAllNodes(),
            node => node.Position == remainingBranchEnd);
        GraphEdge loop = Assert.Single(
            graph.GetAllEdges(),
            edge => edge.NodeA == edge.NodeB);
        GraphEdge branch = Assert.Single(
            graph.GetAllEdges(),
            edge => edge.NodeA != edge.NodeB);
        Assert.Equal(relocatedSeam.ID, loop.NodeA);
        Assert.Equal(3, relocatedSeam.IncidenceCount);
        Assert.True(branch.NodeA == relocatedSeam.ID || branch.NodeB == relocatedSeam.ID);
        Assert.True(branch.NodeA == remainingEndpoint.ID || branch.NodeB == remainingEndpoint.ID);
        Assert.Equal(2, graph.GetAllNodes().Count());
        Assert.Equal(2, graph.GetAllEdges().Count());
        graph.AssertInvariants();
    }

    [Theory]
    [MemberData(nameof(SelfOverlapPaths))]
    public void SubmitPolyline_ContinuousSelfOverlapIsRejectedWithoutSideEffects(Vector2[] points)
    {
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-30f, -20f),
            new Vector2(30f, -20f),
        ]).Success);
        string stateBefore = RoadGraphTestCodec.CaptureJson(graph);
        int nextIDBefore = graph.NextIDWatermark;
        int changedEvents = 0;
        graph.GraphChanged += _ => changedEvents++;

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, points);

        Assert.False(result.Success);
        Assert.Equal(RoadPathSubmissionError.SelfOverlap, result.Error);
        Assert.False(result.Changes.HasChanges);
        Assert.Equal(stateBefore, RoadGraphTestCodec.CaptureJson(graph));
        Assert.Equal(nextIDBefore, graph.NextIDWatermark);
        Assert.Equal(0, changedEvents);
        graph.AssertInvariants();
    }

    public static TheoryData<Vector2[]> SelfOverlapPaths => new()
    {
        { [Vector2.Zero, new Vector2(10f, 0f), Vector2.Zero] },
        {
            [
                Vector2.Zero,
                new Vector2(10f, 0f),
                new Vector2(10f, 10f),
                new Vector2(5f, 0f),
                new Vector2(15f, 0f),
            ]
        },
    };

    [Fact]
    public void LoopAndCrossingRoadHaveStableCanonicalShapeAcrossSubmissionOrder()
    {
        RoadGraph loopFirst = BuildLoopAndCrossingRoad(loopFirst: true);
        RoadGraph crossingFirst = BuildLoopAndCrossingRoad(loopFirst: false);

        Assert.Equal(CanonicalShape(crossingFirst), CanonicalShape(loopFirst));
        loopFirst.AssertInvariants();
        crossingFirst.AssertInvariants();
    }

    [Fact]
    public void SelfIntersectionClusterDoesNotSnapToNearbyUnrelatedNode()
    {
        var graph = new RoadGraph();
        Vector2 nearby = new(0.25f, 0f);
        Assert.True(graph.SubmitPolyline(RoadType.Street, [
            nearby,
            nearby + new Vector2(0.6f, 0f),
        ]).Success);

        RoadPathSubmissionResult result = graph.SubmitPolyline(RoadType.Street, [
            new Vector2(-10f, -10f),
            new Vector2(10f, 10f),
            new Vector2(-10f, 10f),
            new Vector2(10f, -10f),
            new Vector2(-10f, -10f),
        ]);

        Assert.True(result.Success, $"Submission failed with {result.Error}.");
        Assert.Contains(graph.GetAllNodes(), node => RoadExactPredicates.SameBits(node.Position, nearby));
        GraphNode junction = Assert.Single(
            graph.GetAllNodes(),
            node => RoadExactPredicates.SameBits(node.Position, Vector2.Zero));
        Assert.Equal(4, junction.IncidenceCount);
        graph.AssertInvariants();
    }

    private static RoadGraph BuildLoopAndCrossingRoad(bool loopFirst)
    {
        Vector2[] loop =
        [
            new Vector2(-10f, -10f),
            new Vector2(10f, -10f),
            new Vector2(10f, 10f),
            new Vector2(-10f, 10f),
            new Vector2(-10f, -10f),
        ];
        Vector2[] crossing = [new Vector2(-20f, 0f), new Vector2(20f, 0f)];
        var graph = new RoadGraph();
        Assert.True(graph.SubmitPolyline(RoadType.Street, loopFirst ? loop : crossing).Success);
        Assert.True(graph.SubmitPolyline(RoadType.Street, loopFirst ? crossing : loop).Success);
        return graph;
    }

    private static string CanonicalShape(RoadGraph graph)
    {
        string[] nodes = graph.GetAllNodes()
            .Select(node => $"{PointKey(node.Position)}:{node.IncidenceCount}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] edges = graph.GetAllEdges()
            .Select(EdgeKey)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return string.Join(";", nodes) + "||" + string.Join(";", edges);
    }

    private static string EdgeKey(GraphEdge edge)
    {
        string firstEndpoint = PointKey(edge.GeometrySegments[0].Start);
        string secondEndpoint = PointKey(edge.GeometrySegments[^1].End);
        string endpoints = string.CompareOrdinal(firstEndpoint, secondEndpoint) <= 0
            ? $"{firstEndpoint}>{secondEndpoint}"
            : $"{secondEndpoint}>{firstEndpoint}";
        string forward = GeometryKey(edge.GeometrySegments);
        string reverse = GeometryKey(RoadGeometryDirection.ReverseChain(edge.GeometrySegments));
        string geometry = string.CompareOrdinal(forward, reverse) <= 0 ? forward : reverse;
        return $"{endpoints}:{geometry}";
    }

    private static string GeometryKey(IReadOnlyList<RoadGeometrySegment> segments) =>
        string.Join("|", segments.Select(RoadGeometrySerializer.Serialize));

    private static string PointKey(Vector2 point) =>
        $"{BitConverter.SingleToInt32Bits(point.X):X8},{BitConverter.SingleToInt32Bits(point.Y):X8}";
}
