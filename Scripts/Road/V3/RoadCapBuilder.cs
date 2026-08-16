using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 端帽构建器：对 degree-1 非 self-loop 端点生成半圆端帽轮廓，
/// 轮廓首点为节点中心，后续为外法线半圆采样点，供渲染器 fan 填充。
/// </summary>
public static class RoadCapBuilder
{
    public const int DefaultSegments = 6;

    public static bool TryBuild(
        RoadGraphV3Revision revision,
        RoadStyleProvider styles,
        int nodeID,
        out RoadCapMeshData cap)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(styles);
        cap = null!;

        if (!revision.Nodes.TryGetValue(nodeID, out RoadGraphV3Node? node))
            return false;

        var incidences = RoadJunctionPatchBuilder.GetIncidences(revision, nodeID);
        var distinctEdgeIDs = incidences
            .Select(incidence => incidence.EdgeID)
            .Distinct()
            .ToList();
        if (distinctEdgeIDs.Count != 1)
            return false;

        int edgeID = distinctEdgeIDs[0];
        RoadGraphV3Edge edge = revision.Edges[edgeID];
        if (edge.IsSelfLoop)
            return false;
        if (!styles.TryGet(edge.RoadType, out RoadTypeStyle? style))
            return false;

        RoadJunctionIncidence incidence = incidences[0];
        Vector2 outward = -incidence.Direction;
        if (!outward.IsFinite() || outward.LengthSquared() <= 0f)
            return false;
        outward = outward.Normalized();

        float radius = style.Width * 0.5f;
        Vector2 normal = new(-outward.Y, outward.X);
        var outline = new List<Vector2>(DefaultSegments + 2) { node.Position };
        for (int index = 0; index <= DefaultSegments; index++)
        {
            float angle = Mathf.Pi * (0.5f - (float)index / DefaultSegments);
            Vector2 direction = outward * Mathf.Cos(angle) + normal * Mathf.Sin(angle);
            outline.Add(node.Position + direction * radius);
        }

        cap = new RoadCapMeshData(nodeID, edgeID, incidence.Endpoint, outline, style.Color);
        return cap.IsValid;
    }
}
