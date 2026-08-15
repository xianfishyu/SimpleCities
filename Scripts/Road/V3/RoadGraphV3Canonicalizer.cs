using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// 把 V3 不可变 root 转换为 canonical graph，执行 graph canonicalizer，
/// 再重建为新的不可变 root。
/// </summary>
public static class RoadGraphV3Canonicalizer
{
    public static RoadGraphV3Revision Canonicalize(RoadGraphV3Revision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        var canonical = new RoadCanonicalGraph(
            revision.Nodes.Values
                .OrderBy(node => node.ID)
                .Select(node => new RoadCanonicalNode(node.ID, node.Position))
                .ToList(),
            revision.Edges.Values
                .OrderBy(edge => edge.ID)
                .Select(edge => new RoadCanonicalEdge(
                    edge.ID,
                    edge.NodeAID,
                    edge.NodeBID,
                    edge.Geometry,
                    RoadTypeNames.ToWireName(edge.RoadType)))
                .ToList());

        RoadCanonicalGraph result = RoadGraphCanonicalizer.Canonicalize(canonical);

        var nodes = new Dictionary<int, RoadGraphV3Node>();
        int nextNodeID = revision.NextNodeID;
        foreach (RoadCanonicalNode node in result.Nodes)
        {
            nodes[node.ID] = new RoadGraphV3Node(node.ID, node.Position);
            nextNodeID = Math.Max(nextNodeID, node.ID + 1);
        }

        var edges = new Dictionary<int, RoadGraphV3Edge>();
        int nextEdgeID = revision.NextEdgeID;
        foreach (RoadCanonicalEdge edge in result.Edges)
        {
            if (!RoadTypeNames.TryParseWireName(edge.MergeKey, out RoadType roadType))
                throw new InvalidOperationException($"Canonical edge {edge.ID} has invalid road type '{edge.MergeKey}'.");

            edges[edge.ID] = new RoadGraphV3Edge(
                edge.ID,
                edge.NodeAID,
                edge.NodeBID,
                edge.Geometry,
                roadType);
            nextEdgeID = Math.Max(nextEdgeID, edge.ID + 1);
        }

        return new RoadGraphV3Revision(revision.Capacity, nodes, edges, nextNodeID, nextEdgeID);
    }
}
