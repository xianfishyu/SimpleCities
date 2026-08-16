using Godot;
using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 道路 ribbon 构建器：从权威 Edge 几何采样中心线，按 RoadTypeStyle 的半宽生成左右顶点、
/// 三角形索引与逐顶点颜色；不修改图数据，也不创建 Godot 渲染资源。
/// </summary>
public static class RoadRibbonBuilder
{
    public static bool TryBuild(
        RoadGraphV3Edge edge,
        RoadTypeStyle style,
        float displayTolerance,
        out RoadRibbonMeshData mesh)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(style);
        mesh = null!;

        if (!float.IsFinite(style.Width) || style.Width <= 0f)
            return false;

        Vector2[] points = RoadGeometryDisplaySampler.SampleSegments(edge.Geometry, displayTolerance);
        if (points.Length < 2)
            return false;

        float halfWidth = style.Width * 0.5f;
        bool isClosed = points.Length >= 3 && points[0] == points[^1];
        var vertices = new List<Vector2>(points.Length * 2);
        var colors = new List<Color>(points.Length * 2);

        for (int index = 0; index < points.Length; index++)
        {
            Vector2 direction = isClosed && (index == 0 || index == points.Length - 1)
                ? GetClosedSeamDirection(points)
                : GetDirection(points, index);
            if (!direction.IsFinite() || direction.LengthSquared() <= 0f)
                return false;

            Vector2 normal = new Vector2(-direction.Y, direction.X).Normalized();
            vertices.Add(points[index] + normal * halfWidth);
            vertices.Add(points[index] - normal * halfWidth);
            colors.Add(style.Color);
            colors.Add(style.Color);
        }

        var indices = new List<int>((points.Length - 1) * 6);
        for (int index = 0; index < points.Length - 1; index++)
        {
            int baseIndex = index * 2;
            indices.Add(baseIndex);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 3);
            indices.Add(baseIndex + 2);
        }

        mesh = new RoadRibbonMeshData(vertices, indices, colors);
        return mesh.IsValid;
    }

    private static Vector2 GetDirection(Vector2[] points, int index)
    {
        if (index == 0)
            return points[1] - points[0];
        if (index == points.Length - 1)
            return points[index] - points[index - 1];
        return (points[index + 1] - points[index - 1]).Normalized();
    }

    private static Vector2 GetClosedSeamDirection(Vector2[] points)
    {
        Vector2 incoming = points[0] - points[^2];
        Vector2 outgoing = points[1] - points[0];
        Vector2 sum = incoming + outgoing;
        return sum.LengthSquared() > 0f ? sum.Normalized() : outgoing.Normalized();
    }
}
