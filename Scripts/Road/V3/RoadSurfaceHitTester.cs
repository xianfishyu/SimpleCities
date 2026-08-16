using Godot;
using System;
using System.Collections.Generic;
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

        int? hitEdgeID = null;
        EdgeEndpoint? hitEndpoint = null;
        RoadLocation hitLocation = new(0, 0, 0f);
        Vector2 offset = point - revision.Nodes[bestNodeID].Position;
        var incidences = RoadJunctionPatchBuilder.GetIncidences(revision, bestNodeID)
            .Where(incidence => incidence.Direction.IsFinite() && incidence.Direction.LengthSquared() > 0f)
            .ToList();
        if (incidences.Count > 0)
        {
            RoadJunctionIncidence best = incidences
                .OrderBy(incidence => AngleBetween(offset, incidence.Direction))
                .ThenBy(incidence => incidence.EdgeID)
                .First();
            hitEdgeID = best.EdgeID;
            hitEndpoint = best.Endpoint;
            hitLocation = new RoadLocation(best.EdgeID, 0, 0f);
        }

        hit = new RoadSurfaceHit(
            token,
            RoadSurfaceOwnerKind.JunctionPatch,
            NodeID: bestNodeID,
            EdgeID: hitEdgeID,
            Endpoint: hitEndpoint,
            hitLocation,
            bestDistanceSquared);
        return true;
    }

    public static bool TryFindClosestSemanticJoin(
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
            var incidences = RoadJunctionPatchBuilder.GetIncidences(revision, node.ID);
            var distinctEdgeIDs = incidences
                .Select(incidence => incidence.EdgeID)
                .Distinct()
                .ToList();
            if (distinctEdgeIDs.Count != 2 ||
                revision.Edges[distinctEdgeIDs[0]].RoadType == revision.Edges[distinctEdgeIDs[1]].RoadType)
            {
                continue;
            }

            float distanceSquared = point.DistanceSquaredTo(node.Position);
            if (distanceSquared > bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            bestNodeID = node.ID;
        }

        if (bestNodeID < 0)
            return false;

        int? hitEdgeID = null;
        EdgeEndpoint? hitEndpoint = null;
        RoadLocation hitLocation = new(0, 0, 0f);
        Vector2 offset = point - revision.Nodes[bestNodeID].Position;
        var semanticIncidences = RoadJunctionPatchBuilder.GetIncidences(revision, bestNodeID)
            .Where(incidence => incidence.Direction.IsFinite() && incidence.Direction.LengthSquared() > 0f)
            .ToList();
        if (semanticIncidences.Count > 0)
        {
            RoadJunctionIncidence best = semanticIncidences
                .OrderBy(incidence => AngleBetween(offset, incidence.Direction))
                .ThenBy(incidence => incidence.EdgeID)
                .First();
            hitEdgeID = best.EdgeID;
            hitEndpoint = best.Endpoint;
            hitLocation = new RoadLocation(best.EdgeID, 0, 0f);
        }

        hit = new RoadSurfaceHit(
            token,
            RoadSurfaceOwnerKind.SemanticJoin,
            NodeID: bestNodeID,
            EdgeID: hitEdgeID,
            Endpoint: hitEndpoint,
            hitLocation,
            bestDistanceSquared);
        return true;
    }

    public static bool TryFindAllInRect(
        RoadGraphV3Revision revision,
        GraphStateToken token,
        Rect2 rect,
        out IReadOnlyList<RoadSurfaceHit> hits)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (!rect.HasArea())
        {
            hits = [];
            return false;
        }
        if (!RoadNumericPolicy.IsWithinCoordinateRange(rect.Position) ||
            !RoadNumericPolicy.IsWithinCoordinateRange(rect.End))
        {
            throw new ArgumentOutOfRangeException(nameof(rect), "Query rect must be within the V3 numeric range.");
        }

        var edgeIDs = new HashSet<int>();
        foreach (RoadGraphV3Edge edge in revision.Edges.Values.OrderBy(edge => edge.ID))
        {
            for (int geometryIndex = 0; geometryIndex < edge.Geometry.Count; geometryIndex++)
            {
                Vector2[] samples = RoadGeometryDisplaySampler.SampleSegment(edge.Geometry[geometryIndex]);
                for (int sampleIndex = 0; sampleIndex < samples.Length - 1; sampleIndex++)
                {
                    if (SegmentIntersectsRect(rect, samples[sampleIndex], samples[sampleIndex + 1]))
                    {
                        edgeIDs.Add(edge.ID);
                        break;
                    }
                }

                if (edgeIDs.Contains(edge.ID))
                    break;
            }
        }

        hits = edgeIDs
            .Order()
            .Select(edgeID => new RoadSurfaceHit(
                token,
                RoadSurfaceOwnerKind.Ribbon,
                NodeID: revision.Edges[edgeID].NodeAID,
                EdgeID: edgeID,
                Endpoint: EdgeEndpoint.A,
                new RoadLocation(edgeID, 0, 0.5f),
                0f))
            .ToList();
        return hits.Count > 0;
    }

    private static bool SegmentIntersectsRect(Rect2 rect, Vector2 a, Vector2 b)
    {
        if (rect.HasPoint(a) || rect.HasPoint(b))
            return true;

        Vector2 topLeft = rect.Position;
        Vector2 topRight = new(rect.End.X, rect.Position.Y);
        Vector2 bottomLeft = new(rect.Position.X, rect.End.Y);
        Vector2 bottomRight = rect.End;
        return SegmentsIntersect(a, b, topLeft, topRight) ||
               SegmentsIntersect(a, b, topRight, bottomRight) ||
               SegmentsIntersect(a, b, bottomRight, bottomLeft) ||
               SegmentsIntersect(a, b, bottomLeft, topLeft);
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float o1 = Cross(b - a, c - a);
        float o2 = Cross(b - a, d - a);
        float o3 = Cross(d - c, a - c);
        float o4 = Cross(d - c, b - c);

        if (Mathf.Abs(o1) < 1e-6f && IsOnSegment(a, b, c))
            return true;
        if (Mathf.Abs(o2) < 1e-6f && IsOnSegment(a, b, d))
            return true;
        if (Mathf.Abs(o3) < 1e-6f && IsOnSegment(c, d, a))
            return true;
        if (Mathf.Abs(o4) < 1e-6f && IsOnSegment(c, d, b))
            return true;

        return (o1 * o2 < 0f) && (o3 * o4 < 0f);
    }

    private static bool IsOnSegment(Vector2 a, Vector2 b, Vector2 point)
    {
        return point.X >= Mathf.Min(a.X, b.X) - 1e-6f &&
               point.X <= Mathf.Max(a.X, b.X) + 1e-6f &&
               point.Y >= Mathf.Min(a.Y, b.Y) - 1e-6f &&
               point.Y <= Mathf.Max(a.Y, b.Y) + 1e-6f;
    }

    private static float Cross(Vector2 left, Vector2 right) =>
        left.X * right.Y - left.Y * right.X;

    private static int GetIncidentEdgeCount(RoadGraphV3Revision revision, int nodeID) =>
        revision.Edges.Values.Count(edge => edge.NodeAID == nodeID || edge.NodeBID == nodeID);

    private static float AngleBetween(Vector2 from, Vector2 to) =>
        Mathf.Abs(Mathf.Atan2(from.Cross(to), from.Dot(to)));

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
