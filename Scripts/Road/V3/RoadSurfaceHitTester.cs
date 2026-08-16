using Godot;
using System;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 表面命中测试器：从权威 revision 的中心线几何查找离查询点最近的 Edge，
/// 生成带完整 token 的 Ribbon RoadSurfaceHit；不依赖已呈现 mesh，供工具层在接入
/// RoadSurfaceHitProvider 前先获得候选命中。
/// </summary>
public static class RoadSurfaceHitTester
{
    public static bool TryFindClosest(
        RoadGraphV3Revision revision,
        GraphStateToken token,
        Vector2 point,
        float maxDistance,
        out RoadSurfaceHit hit)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (!point.IsFinite())
            throw new ArgumentException("Query point must be finite.", nameof(point));
        if (!float.IsFinite(maxDistance) || maxDistance < 0f)
            throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "Max distance must be finite and non-negative.");

        hit = null!;
        float bestDistanceSquared = maxDistance * maxDistance;
        int bestEdgeID = -1;
        int bestGeometryIndex = -1;
        float bestParameter = 0f;

        foreach (RoadGraphV3Edge edge in revision.Edges.Values.OrderBy(edge => edge.ID))
        {
            for (int geometryIndex = 0; geometryIndex < edge.Geometry.Count; geometryIndex++)
            {
                Vector2[] samples = RoadGeometryDisplaySampler.SampleSegment(edge.Geometry[geometryIndex]);
                for (int sampleIndex = 0; sampleIndex < samples.Length - 1; sampleIndex++)
                {
                    Vector2 start = samples[sampleIndex];
                    Vector2 end = samples[sampleIndex + 1];
                    if (!TryClosestPointOnSegment(point, start, end, out Vector2 closest, out float fraction))
                        continue;

                    float distanceSquared = point.DistanceSquaredTo(closest);
                    if (distanceSquared > bestDistanceSquared)
                        continue;

                    bestDistanceSquared = distanceSquared;
                    bestEdgeID = edge.ID;
                    bestGeometryIndex = geometryIndex;
                    bestParameter = (sampleIndex + fraction) / (samples.Length - 1);
                }
            }
        }

        if (bestEdgeID < 0)
            return false;

        RoadGraphV3Edge bestEdge = revision.Edges[bestEdgeID];
        hit = new RoadSurfaceHit(
            token,
            RoadSurfaceOwnerKind.Ribbon,
            NodeID: bestEdge.NodeAID,
            EdgeID: bestEdge.ID,
            Endpoint: EdgeEndpoint.A,
            new RoadLocation(bestEdge.ID, bestGeometryIndex, bestParameter),
            bestDistanceSquared);
        return true;
    }

    public static bool TryFindClosestJunction(
        RoadGraphV3Revision revision,
        GraphStateToken token,
        Vector2 point,
        float maxDistance,
        out RoadSurfaceHit hit)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (!point.IsFinite())
            throw new ArgumentException("Query point must be finite.", nameof(point));
        if (!float.IsFinite(maxDistance) || maxDistance < 0f)
            throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "Max distance must be finite and non-negative.");

        hit = null!;
        float bestDistanceSquared = maxDistance * maxDistance;
        int bestNodeID = -1;

        foreach (RoadGraphV3Node node in revision.Nodes.Values.OrderBy(node => node.ID))
        {
            if (GetIncidentEdgeCount(revision, node.ID) < 3)
                continue;

            float distanceSquared = point.DistanceSquaredTo(node.Position);
            if (distanceSquared > bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            bestNodeID = node.ID;
        }

        if (bestNodeID < 0)
            return false;

        hit = new RoadSurfaceHit(
            token,
            RoadSurfaceOwnerKind.JunctionPatch,
            NodeID: bestNodeID,
            EdgeID: null,
            Endpoint: null,
            new RoadLocation(0, 0, 0f),
            bestDistanceSquared);
        return true;
    }

    private static int GetIncidentEdgeCount(RoadGraphV3Revision revision, int nodeID) =>
        revision.Edges.Values.Count(edge => edge.NodeAID == nodeID || edge.NodeBID == nodeID);

    private static bool TryClosestPointOnSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end,
        out Vector2 closest,
        out float fraction)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0f)
        {
            closest = start;
            fraction = 0f;
            return true;
        }

        float t = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        closest = start + segment * t;
        fraction = t;
        return true;
    }
}
