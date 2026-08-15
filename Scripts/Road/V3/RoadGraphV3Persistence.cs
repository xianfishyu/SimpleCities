using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

public sealed record RoadGraphV3PersistenceResult(
    bool Success,
    RoadGraphV3Revision? Revision,
    int? NextID,
    string? Error)
{
    public static RoadGraphV3PersistenceResult Failure(string error) =>
        new(false, null, null, error);
}

/// <summary>
/// 将 V3 不可变 root 与 format v1 codec 桥接：序列化 revision，反序列化后重建 revision。
/// </summary>
public static class RoadGraphV3Persistence
{
    public static string Serialize(RoadGraphV3Revision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        var graph = new RoadCanonicalGraph(
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

        return RoadGraphV3Codec.Serialize(graph);
    }

    public static RoadGraphV3PersistenceResult Deserialize(
        string json,
        RoadGraphCapacity capacity)
    {
        capacity.Validate();
        RoadGraphV3CodecResult codecResult = RoadGraphV3Codec.Deserialize(json);
        if (!codecResult.Success || codecResult.Graph is null || codecResult.NextID is not int nextID)
            return RoadGraphV3PersistenceResult.Failure(codecResult.Error ?? "DeserializeFailed");

        var nodes = new Dictionary<int, RoadGraphV3Node>();
        foreach (RoadCanonicalNode node in codecResult.Graph.Nodes)
            nodes[node.ID] = new RoadGraphV3Node(node.ID, node.Position);

        var edges = new Dictionary<int, RoadGraphV3Edge>();
        foreach (RoadCanonicalEdge edge in codecResult.Graph.Edges)
        {
            if (!RoadTypeNames.TryParseWireName(edge.MergeKey, out RoadType roadType))
                return RoadGraphV3PersistenceResult.Failure("InvalidRoadType");

            edges[edge.ID] = new RoadGraphV3Edge(
                edge.ID,
                edge.NodeAID,
                edge.NodeBID,
                edge.Geometry,
                roadType);
        }

        var revision = new RoadGraphV3Revision(
            capacity,
            nodes,
            edges,
            nextID,
            nextID);
        return new RoadGraphV3PersistenceResult(true, revision, nextID, null);
    }
}
