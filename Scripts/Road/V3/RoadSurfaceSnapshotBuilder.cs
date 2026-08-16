using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

public sealed record RoadSurfaceSnapshotBuildResult(
    bool Success,
    RoadSurfaceSnapshot? Snapshot,
    string? Error)
{
    public static RoadSurfaceSnapshotBuildResult Failure(string error) => new(false, null, error);
}

/// <summary>
/// 从权威 RoadGraph revision 构建道路表面 owner 快照；每个 Edge 生成一个 Ribbon owner，
/// 并校验所有 Edge 的 RoadType 都有已注册样式。
/// </summary>
public static class RoadSurfaceSnapshotBuilder
{
    public static RoadSurfaceSnapshotBuildResult Build(
        RoadGraphV3Revision revision,
        GraphStateToken token,
        RoadStyleProvider styles,
        float junctionRadius = RoadJunctionPatchBuilder.DefaultRadius)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(styles);

        if (token.LineageID < 0 || token.DomainRevisionID < 0 || token.ChangeSequence < 0)
            return RoadSurfaceSnapshotBuildResult.Failure("InvalidToken");

        var owners = new List<RoadSurfaceOwner>();
        foreach (RoadGraphV3Edge edge in revision.Edges.Values.OrderBy(edge => edge.ID))
        {
            if (!styles.TryGet(edge.RoadType, out _))
                return RoadSurfaceSnapshotBuildResult.Failure($"MissingStyle:{edge.RoadType}");

            owners.Add(new RoadSurfaceOwner(
                RoadSurfaceOwnerKind.Ribbon,
                NodeID: edge.NodeAID,
                EdgeID: edge.ID,
                Endpoint: EdgeEndpoint.A,
                new RoadLocation(edge.ID, 0, 0f)));
        }

        foreach (int nodeID in revision.Nodes.Keys.Order())
        {
            var incidences = RoadJunctionPatchBuilder.GetIncidences(revision, nodeID);
            var distinctEdgeIDs = incidences
                .Select(incidence => incidence.EdgeID)
                .Distinct()
                .ToList();
            if (distinctEdgeIDs.Count == 2 &&
                revision.Edges[distinctEdgeIDs[0]].RoadType != revision.Edges[distinctEdgeIDs[1]].RoadType)
            {
                foreach (RoadJunctionIncidence incidence in incidences)
                {
                    owners.Add(new RoadSurfaceOwner(
                        RoadSurfaceOwnerKind.SemanticJoin,
                        NodeID: nodeID,
                        EdgeID: incidence.EdgeID,
                        Endpoint: incidence.Endpoint,
                        new RoadLocation(incidence.EdgeID, 0, 0f)));
                }
            }
        }

        foreach (int nodeID in revision.Nodes.Keys.Order())
        {
            if (!RoadJunctionPatchBuilder.TryBuild(revision, styles, nodeID, junctionRadius, out _))
                continue;

            owners.Add(new RoadSurfaceOwner(
                RoadSurfaceOwnerKind.JunctionPatch,
                NodeID: nodeID,
                EdgeID: null,
                Endpoint: null,
                new RoadLocation(0, 0, 0f)));

            foreach (RoadJunctionIncidence incidence in RoadJunctionPatchBuilder.GetIncidences(revision, nodeID))
            {
                if (!incidence.Direction.IsFinite() || incidence.Direction.LengthSquared() <= 0f)
                    continue;

                owners.Add(new RoadSurfaceOwner(
                    RoadSurfaceOwnerKind.JunctionPatch,
                    NodeID: nodeID,
                    EdgeID: incidence.EdgeID,
                    Endpoint: incidence.Endpoint,
                    new RoadLocation(incidence.EdgeID, 0, 0f)));
            }
        }

        return new RoadSurfaceSnapshotBuildResult(true, new RoadSurfaceSnapshot(token, owners), null);
    }
}
