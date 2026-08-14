using System;
using System.Collections.Generic;
using System.Linq;

public partial class RoadGraph
{
    private void FinalizeMutation(IEnumerable<int> affectedNodeIDs)
    {
        var pending = new SortedSet<int>(affectedNodeIDs);
        foreach (int nodeID in pending.ToArray())
            RemoveNodeIfIsolated(GetNode(nodeID));
        HashSet<int> protectedLoopSeams = FindProtectedLoopSeams(pending);

        while (pending.Count > 0)
        {
            int nodeID = pending.Min;
            pending.Remove(nodeID);
            if (protectedLoopSeams.Contains(nodeID) ||
                !TryMergeAtNode(nodeID, out int firstEndpointID, out int secondEndpointID))
                continue;
            pending.Add(firstEndpointID);
            pending.Add(secondEndpointID);
        }

        AssertCommittedInvariants();
    }

    private HashSet<int> FindProtectedLoopSeams(IEnumerable<int> affectedNodeIDs)
    {
        var protectedSeams = new HashSet<int>();
        var visited = new HashSet<int>();
        foreach (int startNodeID in affectedNodeIDs.Distinct().Order())
        {
            if (!visited.Add(startNodeID))
                continue;

            var component = new List<GraphNode>();
            var pending = new Queue<int>();
            pending.Enqueue(startNodeID);
            while (pending.TryDequeue(out int nodeID))
            {
                if (!_nodes.TryGetValue(nodeID, out GraphNode? node))
                    continue;
                component.Add(node);
                foreach (int neighborID in node.Incidences
                             .Select(incidence => incidence.NeighborNodeID)
                             .Distinct()
                             .Order())
                {
                    if (visited.Add(neighborID))
                        pending.Enqueue(neighborID);
                }
            }

            bool isPureDegreeTwoCycle = component.Count > 1 && component.All(node =>
                node.IncidenceCount == 2 && node.IncidentEdgeCount == 2);
            bool hasSingleRoadType = component
                .SelectMany(node => node.Incidences)
                .Select(incidence => _edges[incidence.EdgeID].RoadType)
                .Distinct()
                .Take(2)
                .Count() == 1;
            if (isPureDegreeTwoCycle && hasSingleRoadType)
                protectedSeams.Add(component.Min(node => node.ID));
        }
        return protectedSeams;
    }

    private bool TryMergeAtNode(
        int nodeID,
        out int firstEndpointID,
        out int secondEndpointID)
    {
        firstEndpointID = -1;
        secondEndpointID = -1;
        if (!_nodes.TryGetValue(nodeID, out GraphNode? node) ||
            node.IncidenceCount != 2 ||
            node.IncidentEdgeCount != 2)
        {
            return false;
        }

        EdgeIncidence[] incidences = node.Incidences
            .OrderBy(incidence => incidence.EdgeID)
            .ToArray();
        if (!_edges.TryGetValue(incidences[0].EdgeID, out GraphEdge? first) ||
            !_edges.TryGetValue(incidences[1].EdgeID, out GraphEdge? second))
        {
            return false;
        }
        if (first.RoadType != second.RoadType)
            return false;

        (int firstFarID, IReadOnlyList<RoadGeometrySegment> firstToNode) =
            OrientGeometryTowardsNode(first, nodeID);
        (int secondFarID, IReadOnlyList<RoadGeometrySegment> secondToNode) =
            OrientGeometryTowardsNode(second, nodeID);

        var mergedGeometry = new List<RoadGeometrySegment>(
            firstToNode.Count + secondToNode.Count);
        mergedGeometry.AddRange(firstToNode);
        mergedGeometry.AddRange(RoadGeometryDirection.ReverseChain(secondToNode));
        RoadGeometryCanonicalizationResult canonical =
            RoadGeometryCanonicalizer.Canonicalize(mergedGeometry);
        if (RoadNumericPolicy.ValidateGeometryChain(
                canonical.GeometrySegments,
                0d,
                out _,
                out _) != RoadNumericError.None)
        {
            return false;
        }

        int retainedEdgeID = Math.Min(first.ID, second.ID);
        var merged = new GraphEdge(
            first.RoadType,
            retainedEdgeID,
            firstFarID,
            secondFarID,
            canonical.GeometrySegments);

        DetachEdge(first);
        DetachEdge(second);
        TrackNodeChange(nodeID);
        _nodes.Remove(nodeID);
        RemoveNodeSpatialRef(nodeID);
        AttachEdge(merged);

        firstEndpointID = merged.NodeA;
        secondEndpointID = merged.NodeB;
        return true;
    }

    private static (int FarNodeID, IReadOnlyList<RoadGeometrySegment> Geometry) OrientGeometryTowardsNode(
        GraphEdge edge,
        int nodeID)
    {
        if (edge.NodeB == nodeID)
            return (edge.NodeA, edge.GeometrySegments);
        if (edge.NodeA == nodeID)
            return (edge.NodeB, RoadGeometryDirection.ReverseChain(edge.GeometrySegments));
        throw new InvalidOperationException($"Edge {edge.ID} is not incident to node {nodeID}.");
    }

}
