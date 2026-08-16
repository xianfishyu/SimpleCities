using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 junction patch 构建器：对 degree >= 3 的节点，按入射 Edge 的 outgoing 方向生成
/// 以节点为中心的轮廓多边形；self-loop 只贡献一次 seam 方向。不修改图数据。
/// </summary>
public static class RoadJunctionPatchBuilder
{
    public const float DefaultRadius = 8f;

    public static bool TryBuild(
        RoadGraphV3Revision revision,
        RoadStyleProvider styles,
        int nodeID,
        float radius,
        out RoadJunctionPatchData patch)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(styles);
        patch = null!;

        if (!revision.Nodes.TryGetValue(nodeID, out RoadGraphV3Node? node))
            return false;
        if (!float.IsFinite(radius) || radius <= 0f)
            return false;

        var directions = new List<Vector2>();
        Color? color = null;
        foreach (RoadGraphV3Edge edge in revision.Edges.Values)
        {
            if (edge.NodeAID != nodeID && edge.NodeBID != nodeID)
                continue;
            if (edge.Geometry.Count == 0)
                continue;

            Vector2 direction = GetOutgoingDirection(edge, nodeID);
            if (!direction.IsFinite() || direction.LengthSquared() <= 0f)
                continue;

            directions.Add(direction.Normalized());
            if (color is null && styles.TryGet(edge.RoadType, out RoadTypeStyle? style))
                color = style.Color;
        }

        if (directions.Count < 3)
            return false;

        directions.Sort((left, right) =>
            Mathf.Atan2(left.Y, left.X).CompareTo(Mathf.Atan2(right.Y, right.X)));

        var outline = new List<Vector2>(directions.Count);
        foreach (Vector2 direction in directions)
            outline.Add(node.Position + direction * radius);

        patch = new RoadJunctionPatchData(nodeID, outline, color ?? Colors.White);
        return patch.IsValid;
    }

    private static Vector2 GetOutgoingDirection(RoadGraphV3Edge edge, int nodeID)
    {
        if (edge.NodeAID == nodeID)
        {
            Vector2 start = edge.Geometry[0].Start;
            Vector2 end = edge.Geometry[0].End;
            return end - start;
        }

        Vector2 endPoint = edge.Geometry[^1].End;
        Vector2 startPoint = edge.Geometry[^1].Start;
        return startPoint - endPoint;
    }
}
