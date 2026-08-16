using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 语义过渡构建器：对 degree-2 且两侧 RoadType 不同的节点，用两条入射道路在节点处的
/// road-side 顶点构造四边形，并为两侧顶点赋予各自 RoadType 颜色。
/// </summary>
public static class RoadSemanticJoinBuilder
{
    public static bool TryBuild(
        RoadGraphV3Revision revision,
        RoadStyleProvider styles,
        int nodeID,
        out RoadSemanticJoinMeshData mesh)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(styles);
        mesh = null!;

        if (!revision.Nodes.TryGetValue(nodeID, out RoadGraphV3Node? node))
            return false;

        var incidences = RoadJunctionPatchBuilder.GetIncidences(revision, nodeID);
        var distinctEdgeIDs = incidences
            .Select(incidence => incidence.EdgeID)
            .Distinct()
            .ToList();
        if (distinctEdgeIDs.Count != 2)
            return false;

        RoadGraphV3Edge edgeA = revision.Edges[distinctEdgeIDs[0]];
        RoadGraphV3Edge edgeB = revision.Edges[distinctEdgeIDs[1]];
        if (edgeA.RoadType == edgeB.RoadType)
            return false;
        if (!styles.TryGet(edgeA.RoadType, out RoadTypeStyle? styleA) ||
            !styles.TryGet(edgeB.RoadType, out RoadTypeStyle? styleB))
        {
            return false;
        }

        RoadJunctionIncidence incidenceA = incidences.First(i => i.EdgeID == edgeA.ID);
        RoadJunctionIncidence incidenceB = incidences.First(i => i.EdgeID == edgeB.ID);
        Vector2 dirA = incidenceA.Direction.Normalized();
        Vector2 dirB = incidenceB.Direction.Normalized();
        if (!dirA.IsFinite() || dirA.LengthSquared() <= 0f ||
            !dirB.IsFinite() || dirB.LengthSquared() <= 0f)
        {
            return false;
        }

        Vector2 normalA = new Vector2(-dirA.Y, dirA.X) * (styleA.Width * 0.5f);
        Vector2 normalB = new Vector2(-dirB.Y, dirB.X) * (styleB.Width * 0.5f);
        Vector2 center = node.Position;

        mesh = new RoadSemanticJoinMeshData(
            nodeID,
            [center + normalA, center - normalA, center - normalB, center + normalB],
            [0, 1, 2, 0, 2, 3],
            [styleA.Color, styleA.Color, styleB.Color, styleB.Color]);
        return mesh.IsValid;
    }
}
