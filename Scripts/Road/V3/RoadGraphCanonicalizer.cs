using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

public sealed record RoadCanonicalNode(int ID, Vector2 Position);

public sealed record RoadCanonicalEdge(
    int ID,
    int NodeAID,
    int NodeBID,
    IReadOnlyList<RoadGeometrySegment> Geometry,
    string MergeKey)
{
    public bool IsSelfLoop => NodeAID == NodeBID;
}

public sealed record RoadCanonicalGraph(
    IReadOnlyList<RoadCanonicalNode> Nodes,
    IReadOnlyList<RoadCanonicalEdge> Edges);

/// <summary>
/// graph 层 canonicalizer：按 merge key 合并二 incidence 节点。
/// 同一 self-loop 的 A/B incidence 作为 loop seam 保留；不同 Edge 合并时保留最小 Edge ID，
/// 远端相同则形成 self-loop，非环结果按 NodeAID &lt; NodeBID 定向。
/// </summary>
public static class RoadGraphCanonicalizer
{
    public static RoadCanonicalGraph Canonicalize(RoadCanonicalGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        Dictionary<int, RoadCanonicalNode> nodes = graph.Nodes.ToDictionary(node => node.ID);
        Dictionary<int, RoadCanonicalEdge> edges = graph.Edges.ToDictionary(edge => edge.ID);

        bool changed = true;
        while (changed)
        {
            changed = false;
            Dictionary<int, List<EdgeIncidence>> incidences = BuildIncidences(edges.Values);

            foreach (int nodeID in incidences.Keys.Order())
            {
                List<EdgeIncidence> incident = incidences[nodeID];
                if (incident.Count != 2)
                    continue;

                if (incident[0].EdgeID == incident[1].EdgeID)
                    continue;

                RoadCanonicalEdge first = edges[incident[0].EdgeID];
                RoadCanonicalEdge second = edges[incident[1].EdgeID];
                if (first.MergeKey != second.MergeKey)
                    continue;

                int farA = GetOtherNode(first, nodeID, incident[0].Endpoint);
                int farB = GetOtherNode(second, nodeID, incident[1].Endpoint);

                int keepNode;
                int removeNode;
                List<RoadGeometrySegment> mergedGeometry;
                if (farA == farB)
                {
                    bool keepSmallerSeam =
                        incidences.TryGetValue(farA, out List<EdgeIncidence>? farIncidences) &&
                        farIncidences.Count == 2 &&
                        farA > nodeID;
                    if (keepSmallerSeam)
                    {
                        keepNode = nodeID;
                        removeNode = farA;
                        mergedGeometry = [
                            .. GetGeometry(first, keepNode, removeNode),
                            .. GetGeometry(second, removeNode, keepNode),
                        ];
                    }
                    else
                    {
                        keepNode = farA;
                        removeNode = nodeID;
                        mergedGeometry = [
                            .. GetGeometry(first, keepNode, removeNode),
                            .. GetGeometry(second, removeNode, keepNode),
                        ];
                    }

                    farB = keepNode;

                    mergedGeometry = RoadSelfLoopChain.Canonicalize(mergedGeometry).ToList();
                }
                else
                {
                    keepNode = farA;
                    removeNode = nodeID;
                    mergedGeometry = [
                        .. GetGeometry(first, keepNode, removeNode),
                        .. GetGeometry(second, removeNode, farB),
                    ];
                    mergedGeometry = RoadGeometryCanonicalizer.Canonicalize(mergedGeometry).ToList();
                    if (keepNode > farB)
                    {
                        mergedGeometry = RoadDirectionKey.ReverseChain(mergedGeometry).ToList();
                        (keepNode, farB) = (farB, keepNode);
                    }
                }

                int mergedID = Math.Min(first.ID, second.ID);
                edges.Remove(first.ID);
                edges.Remove(second.ID);
                edges[mergedID] = new RoadCanonicalEdge(
                    mergedID,
                    keepNode,
                    farB,
                    mergedGeometry,
                    first.MergeKey);
                nodes.Remove(removeNode);

                changed = true;
                break;
            }
        }

        return new RoadCanonicalGraph(
            nodes.Values.OrderBy(node => node.ID).ToList(),
            edges.Values.OrderBy(edge => edge.ID).ToList());
    }

    private static Dictionary<int, List<EdgeIncidence>> BuildIncidences(
        IEnumerable<RoadCanonicalEdge> edges)
    {
        var incidences = new Dictionary<int, List<EdgeIncidence>>();
        foreach (RoadCanonicalEdge edge in edges)
        {
            AddIncidence(incidences, edge.NodeAID, edge.ID, EdgeEndpoint.A);
            if (edge.IsSelfLoop)
            {
                AddIncidence(incidences, edge.NodeAID, edge.ID, EdgeEndpoint.B);
            }
            else
            {
                AddIncidence(incidences, edge.NodeBID, edge.ID, EdgeEndpoint.B);
            }
        }

        return incidences;
    }

    private static void AddIncidence(
        Dictionary<int, List<EdgeIncidence>> incidences,
        int nodeID,
        int edgeID,
        EdgeEndpoint endpoint)
    {
        if (!incidences.TryGetValue(nodeID, out List<EdgeIncidence>? list))
        {
            list = [];
            incidences[nodeID] = list;
        }

        list.Add(new EdgeIncidence(edgeID, endpoint, nodeID));
    }

    private static int GetOtherNode(RoadCanonicalEdge edge, int nodeID, EdgeEndpoint endpoint)
    {
        if (endpoint == EdgeEndpoint.A && edge.NodeAID == nodeID)
            return edge.NodeBID;
        if (endpoint == EdgeEndpoint.B && edge.NodeBID == nodeID)
            return edge.NodeAID;
        throw new InvalidOperationException($"Edge {edge.ID} does not connect node {nodeID} at endpoint {endpoint}.");
    }

    private static IReadOnlyList<RoadGeometrySegment> GetGeometry(
        RoadCanonicalEdge edge,
        int fromNodeID,
        int toNodeID)
    {
        if (fromNodeID == edge.NodeAID && toNodeID == edge.NodeBID)
            return edge.Geometry;
        if (fromNodeID == edge.NodeBID && toNodeID == edge.NodeAID)
            return RoadDirectionKey.ReverseChain(edge.Geometry);
        throw new InvalidOperationException($"Edge {edge.ID} does not run from {fromNodeID} to {toNodeID}.");
    }
}
