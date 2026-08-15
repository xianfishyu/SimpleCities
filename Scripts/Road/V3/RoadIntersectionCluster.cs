using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// 一个交点 witness：携带稳定 key、候选位置和可选的既有 Node ID。
/// 稳定 key 用于与遍历顺序无关的排序和代表选择。
/// </summary>
public sealed record IntersectionWitness(
    string StableKey,
    Vector2 Position,
    int? ExistingNodeID);

public sealed record IntersectionCluster(
    Vector2 Position,
    int? ExistingNodeID,
    IReadOnlyList<IntersectionWitness> Witnesses);

public sealed record IntersectionClusterResult(
    bool Success,
    IReadOnlyList<IntersectionCluster> Clusters,
    string? Error)
{
    public static IntersectionClusterResult Failure(string error) =>
        new(false, [], error);
}

/// <summary>
/// 按稳定 key 排序后，用 connected component 对 witness 聚类；与遍历顺序无关。
/// 组件含多个既有 Node 或直径超过上限时整次事务结构化失败。
/// </summary>
public static class RoadIntersectionCluster
{
    public static IntersectionClusterResult Cluster(
        IReadOnlyList<IntersectionWitness> witnesses,
        float clusterEpsilon,
        float maxClusterDiameter)
    {
        ArgumentNullException.ThrowIfNull(witnesses);
        if (!float.IsFinite(clusterEpsilon) || clusterEpsilon <= 0f)
            throw new ArgumentOutOfRangeException(nameof(clusterEpsilon), clusterEpsilon, "Cluster epsilon must be finite and positive.");
        if (!float.IsFinite(maxClusterDiameter) || maxClusterDiameter <= 0f)
            throw new ArgumentOutOfRangeException(nameof(maxClusterDiameter), maxClusterDiameter, "Max cluster diameter must be finite and positive.");

        if (witnesses.Count == 0)
            return new IntersectionClusterResult(true, [], null);

        foreach (IntersectionWitness witness in witnesses)
        {
            ArgumentNullException.ThrowIfNull(witness);
            if (string.IsNullOrEmpty(witness.StableKey))
                throw new ArgumentException("Witness stable key must be non-empty.", nameof(witnesses));
            if (!RoadNumericPolicy.IsWithinCoordinateRange(witness.Position))
                throw new ArgumentOutOfRangeException(nameof(witnesses), "Witness positions must be finite and within the V3 numeric range.");
        }

        IntersectionWitness[] sorted = witnesses
            .OrderBy(witness => witness.StableKey, StringComparer.Ordinal)
            .ThenBy(witness => witness.Position.X)
            .ThenBy(witness => witness.Position.Y)
            .ToArray();

        int count = sorted.Length;
        var parent = new int[count];
        for (int index = 0; index < count; index++)
            parent[index] = index;

        double epsilonSquared = (double)clusterEpsilon * clusterEpsilon;
        for (int left = 0; left < count; left++)
        {
            for (int right = left + 1; right < count; right++)
            {
                double distanceSquared = RoadNumericPolicy.CheckedDistanceSquared(
                    sorted[left].Position,
                    sorted[right].Position);
                if (distanceSquared <= epsilonSquared)
                    Union(parent, left, right);
            }
        }

        var groups = new Dictionary<int, List<IntersectionWitness>>();
        for (int index = 0; index < count; index++)
        {
            int root = Find(parent, index);
            if (!groups.TryGetValue(root, out List<IntersectionWitness>? list))
            {
                list = [];
                groups[root] = list;
            }

            list.Add(sorted[index]);
        }

        var clusters = new List<IntersectionCluster>();
        double maxDiameterSquared = (double)maxClusterDiameter * maxClusterDiameter;
        foreach (List<IntersectionWitness> group in groups.Values)
        {
            int[] existingNodeIDs = group
                .Select(witness => witness.ExistingNodeID)
                .Where(nodeID => nodeID.HasValue)
                .Select(nodeID => nodeID!.Value)
                .Distinct()
                .Order()
                .ToArray();

            if (existingNodeIDs.Length > 1)
                return IntersectionClusterResult.Failure("MultipleExistingNodes");

            for (int left = 0; left < group.Count; left++)
            {
                for (int right = left + 1; right < group.Count; right++)
                {
                    double distanceSquared = RoadNumericPolicy.CheckedDistanceSquared(
                        group[left].Position,
                        group[right].Position);
                    if (distanceSquared > maxDiameterSquared)
                        return IntersectionClusterResult.Failure("ClusterDiameterExceeded");
                }
            }

            Vector2 position;
            int? existingNodeID;
            if (existingNodeIDs.Length == 1)
            {
                existingNodeID = existingNodeIDs[0];
                IntersectionWitness existing = group.First(witness => witness.ExistingNodeID == existingNodeID);
                position = existing.Position;
            }
            else
            {
                existingNodeID = null;
                position = RoadNumericPolicy.NormalizeVector(group[0].Position);
            }

            clusters.Add(new IntersectionCluster(position, existingNodeID, group.ToArray()));
        }

        return new IntersectionClusterResult(true, clusters, null);
    }

    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }

        return index;
    }

    private static void Union(int[] parent, int left, int right)
    {
        int leftRoot = Find(parent, left);
        int rightRoot = Find(parent, right);
        if (leftRoot == rightRoot)
            return;

        parent[rightRoot] = leftRoot;
    }
}
