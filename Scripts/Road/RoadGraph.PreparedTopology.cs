using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

internal sealed record PreparedRoadNode(int ID, Vector2 Position);

internal sealed record PreparedRoadEdge(
    RoadType RoadType,
    int ID,
    int NodeAID,
    int NodeBID,
    IReadOnlyList<RoadGeometrySegment> GeometrySegments);

internal sealed record PreparedRoadGraphTopology(
    int NextID,
    IReadOnlyList<PreparedRoadNode> Nodes,
    IReadOnlyList<PreparedRoadEdge> Edges);

public partial class RoadGraph
{
    internal static RoadGraph FromPreparedTopology(
        PreparedRoadGraphTopology topology,
        RoadGraphCapacity? capacity = null,
        float bucketSize = IndexBucketSize)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(topology.Nodes);
        ArgumentNullException.ThrowIfNull(topology.Edges);
        capacity ??= RoadGraphCapacity.Default;

        RoadGraphResourceCounts coarseCounts = new(
            topology.Nodes.Count,
            topology.Edges.Count,
            topology.Edges.Sum(edge => (long)(edge.GeometrySegments?.Count ?? 0)),
            0,
            0,
            0);
        if (capacity.Validate(coarseCounts) != RoadGraphCapacityError.None)
            throw new ArgumentException("Prepared topology exceeds RoadGraph capacity.", nameof(topology));

        var graph = new RoadGraph(bucketSize, capacity, topology.NextID);
        var allIDs = new HashSet<int>();
        foreach (PreparedRoadNode node in topology.Nodes.OrderBy(node => node.ID))
        {
            ArgumentNullException.ThrowIfNull(node);
            graph.ValidatePreparedEntityID(node.ID, allIDs);
            if (!RoadNumericPolicy.IsWithinCoordinateRange(node.Position))
                throw new ArgumentException(
                    $"Prepared node {node.ID} is outside the numeric policy.",
                    nameof(topology));

            Vector2 position = RoadNumericPolicy.Canonicalize(node.Position);
            graph._nodes.Add(node.ID, new GraphNode(node.ID, position));
        }

        double graphLength = 0d;
        foreach (PreparedRoadEdge prepared in topology.Edges.OrderBy(edge => edge.ID))
        {
            ArgumentNullException.ThrowIfNull(prepared);
            graph.ValidatePreparedEntityID(prepared.ID, allIDs);
            if (!RoadTypeContract.IsDefined(prepared.RoadType))
                throw new ArgumentException(
                    $"Prepared edge {prepared.ID} has an invalid road type.",
                    nameof(topology));
            ArgumentNullException.ThrowIfNull(prepared.GeometrySegments);
            if (!graph._nodes.ContainsKey(prepared.NodeAID) ||
                !graph._nodes.ContainsKey(prepared.NodeBID))
            {
                throw new ArgumentException(
                    $"Prepared edge {prepared.ID} references a missing endpoint.",
                    nameof(topology));
            }
            if (RoadNumericPolicy.ValidateGeometryChain(
                    prepared.GeometrySegments,
                    graphLength,
                    out _,
                    out graphLength) != RoadNumericError.None)
            {
                throw new ArgumentException(
                    $"Prepared edge {prepared.ID} violates the numeric policy.",
                    nameof(topology));
            }

            var edge = new GraphEdge(
                prepared.RoadType,
                prepared.ID,
                prepared.NodeAID,
                prepared.NodeBID,
                prepared.GeometrySegments);
            GraphNode nodeA = graph._nodes[edge.NodeA];
            GraphNode nodeB = graph._nodes[edge.NodeB];
            if (!RoadExactPredicates.SameBits(edge.GeometrySegments[0].Start, nodeA.Position) ||
                !RoadExactPredicates.SameBits(edge.GeometrySegments[^1].End, nodeB.Position))
            {
                throw new ArgumentException(
                    $"Prepared edge {prepared.ID} geometry does not match its canonical endpoints.",
                    nameof(topology));
            }

            graph._edges.Add(edge.ID, edge);
        }

        if (allIDs.Count > 0 && topology.NextID <= allIDs.Max())
            throw new ArgumentException(
                "Prepared topology nextID must exceed every entity ID.",
                nameof(topology));

        if (!graph.TryMeasurePreparedSpatialResources(out RoadGraphResourceCounts prospectiveCounts) ||
            capacity.Validate(prospectiveCounts) != RoadGraphCapacityError.None)
        {
            throw new ArgumentException("Prepared topology exceeds RoadGraph spatial capacity.", nameof(topology));
        }

        graph.RebuildNodeIncidences();
        graph.RebuildSpatialIndex();
        graph.AssertInvariants();
        graph.SealPreparedInitialRevision();
        return graph;
    }

    private bool TryMeasurePreparedSpatialResources(out RoadGraphResourceCounts counts)
    {
        var index = new UniformGrid(_spatialIndex.BucketSize);
        foreach (GraphNode node in _nodes.Values)
        {
            index.Insert(new NodeSpatialRef(node.ID, node.Position));
            if (index.BucketCount > _capacity.MaximumBuckets ||
                index.ReferenceEntryCount > _capacity.MaximumSpatialReferences)
            {
                counts = default;
                return false;
            }
        }

        long geometryCount = 0L;
        long fragmentCount = 0L;
        foreach (GraphEdge edge in _edges.Values.OrderBy(edge => edge.ID))
        {
            geometryCount += edge.GeometrySegments.Count;
            long remaining = _capacity.MaximumQueryFragments - fragmentCount;
            if (remaining < 0L ||
                !RoadQueryFragmentFactory.TryCreateChain(
                    edge.ID,
                    edge.GeometrySegments,
                    index.BucketSize,
                    edge.NodeA != edge.NodeB,
                    (int)Math.Min(remaining, int.MaxValue),
                    out IReadOnlyList<EdgeGeometryRef> fragments))
            {
                counts = default;
                return false;
            }

            fragmentCount += fragments.Count;
            foreach (EdgeGeometryRef fragment in fragments)
            {
                if (!index.TryCountCoveredBuckets(fragment.Bounds, out long coveredBuckets) ||
                    coveredBuckets > _capacity.MaximumSpatialReferences - index.ReferenceEntryCount)
                {
                    counts = default;
                    return false;
                }
                index.InsertGeometry(fragment);
                if (index.BucketCount > _capacity.MaximumBuckets ||
                    index.ReferenceEntryCount > _capacity.MaximumSpatialReferences)
                {
                    counts = default;
                    return false;
                }
            }
        }

        counts = new RoadGraphResourceCounts(
            _nodes.Count,
            _edges.Count,
            geometryCount,
            fragmentCount,
            index.BucketCount,
            index.ReferenceEntryCount);
        return true;
    }

    private void RebuildNodeIncidences()
    {
        foreach (GraphNode node in _nodes.Values.ToArray())
        {
            TrackNodeChange(node.ID);
            _nodes[node.ID] = node.WithoutIncidences();
        }
        foreach (GraphEdge edge in _edges.Values.OrderBy(edge => edge.ID))
            AttachEdgeIncidences(edge);
    }

    private void AttachEdgeIncidences(GraphEdge edge)
    {
        GraphNode nodeA = GetNode(edge.NodeA)
            ?? throw new InvalidOperationException($"Edge {edge.ID} is missing endpoint A.");
        GraphNode nodeB = GetNode(edge.NodeB)
            ?? throw new InvalidOperationException($"Edge {edge.ID} is missing endpoint B.");
        TrackNodeChange(nodeA.ID);
        _nodes[nodeA.ID] = nodeA.WithAddedIncidence(
            edge.ID,
            EdgeEndpoint.A,
            edge.NodeB);
        nodeB = _nodes[nodeB.ID];
        TrackNodeChange(nodeB.ID);
        _nodes[nodeB.ID] = nodeB.WithAddedIncidence(
            edge.ID,
            EdgeEndpoint.B,
            edge.NodeA);
    }

    private void ValidatePreparedEntityID(int id, HashSet<int> allIDs)
    {
        if (id < 0 || !allIDs.Add(id))
            throw new ArgumentException($"Prepared entity ID {id} is invalid or duplicated.");
    }
}
