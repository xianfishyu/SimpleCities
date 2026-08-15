using Godot;
using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// 一次解析全部 request anchor 的 NodeSnap。只读不写图；等距时选择最小 Node ID。
/// NodeSnapRadius 表示用户意图，不得与 intersection cluster epsilon 复用或取最大值。
/// </summary>
public static class RoadNodeSnap
{
    public static IReadOnlyList<RoadSnappedAnchor> SnapAll(
        IReadOnlyList<Vector2> anchors,
        IReadOnlyDictionary<int, Vector2> nodes,
        float snapRadius)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(nodes);
        if (!float.IsFinite(snapRadius) || snapRadius < 0f)
            throw new ArgumentOutOfRangeException(nameof(snapRadius), snapRadius, "Snap radius must be finite and non-negative.");

        var result = new RoadSnappedAnchor[anchors.Count];
        for (int index = 0; index < anchors.Count; index++)
        {
            Vector2 anchor = anchors[index];
            if (!RoadNumericPolicy.IsWithinCoordinateRange(anchor))
                throw new ArgumentOutOfRangeException(nameof(anchors), "Anchors must be finite and within the V3 numeric range.");

            int? bestNodeID = null;
            double bestDistanceSquared = (double)snapRadius * snapRadius;
            foreach (KeyValuePair<int, Vector2> pair in nodes)
            {
                double distanceSquared = RoadNumericPolicy.CheckedDistanceSquared(anchor, pair.Value);
                if (distanceSquared > bestDistanceSquared)
                    continue;

                if (bestNodeID is null ||
                    distanceSquared < bestDistanceSquared ||
                    (distanceSquared == bestDistanceSquared && pair.Key < bestNodeID.Value))
                {
                    bestDistanceSquared = distanceSquared;
                    bestNodeID = pair.Key;
                }
            }

            result[index] = bestNodeID is int nodeID
                ? new RoadSnappedAnchor(index, nodeID, nodes[nodeID])
                : new RoadSnappedAnchor(index, null, anchor);
        }

        return result;
    }
}

public readonly record struct RoadSnappedAnchor(
    int AnchorIndex,
    int? NodeID,
    Vector2 Position)
{
    public bool IsSnapped => NodeID.HasValue;
}
