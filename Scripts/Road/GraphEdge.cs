using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public class GraphEdge
{
    public int ID { get; }
    public int NodeA { get; internal set; }
    public int NodeB { get; internal set; }

    private readonly RoadGeometrySegment[] _geometrySegments;
    private readonly ReadOnlyCollection<RoadGeometrySegment> _readOnlyGeometrySegments;

    /// <summary>保留类型与控制参数的权威原生几何段。</summary>
    public IReadOnlyList<RoadGeometrySegment> GeometrySegments => _readOnlyGeometrySegments;

    /// <summary>几何段之间的中间锚点（不含两端节点坐标）。</summary>
    private readonly Vector2[] _points;
    public Vector2[] Points => (Vector2[])_points.Clone();
    internal Vector2[] InternalPoints => _points;

    public int GroupID { get; internal set; }
    public RoadType Type { get; internal set; }
    public float Length { get; }

    public GraphEdge(
        int id,
        int nodeA,
        int nodeB,
        IReadOnlyList<RoadGeometrySegment> geometrySegments,
        int groupID,
        RoadType type)
    {
        ArgumentNullException.ThrowIfNull(geometrySegments);
        if (geometrySegments.Count == 0)
            throw new ArgumentException("An edge must contain at least one geometry segment.", nameof(geometrySegments));

        _geometrySegments = geometrySegments.ToArray();
        for (int i = 0; i < _geometrySegments.Length; i++)
        {
            if (_geometrySegments[i] is null)
                throw new ArgumentException("Geometry segments cannot contain null.", nameof(geometrySegments));
            if (i > 0 && _geometrySegments[i - 1].End != _geometrySegments[i].Start)
                throw new ArgumentException("Geometry segments must form a continuous path.", nameof(geometrySegments));
        }

        ID = id;
        NodeA = nodeA;
        NodeB = nodeB;
        _readOnlyGeometrySegments = Array.AsReadOnly(_geometrySegments);
        _points = _geometrySegments.Take(_geometrySegments.Length - 1).Select(segment => segment.End).ToArray();
        GroupID = groupID;
        Type = type;
        Length = _geometrySegments.Sum(segment => segment.Length);
    }

    /// <summary>
    /// 返回完整路径：[NodeA.Position, ...Points, NodeB.Position]。
    /// </summary>
    public Vector2[] GetFullPath(Func<int, GraphNode?> getNode)
    {
        var nodeA = getNode(NodeA);
        var nodeB = getNode(NodeB);
        if (nodeA == null || nodeB == null)
            return Points;

        var result = new Vector2[_points.Length + 2];
        result[0] = nodeA.Position;
        for (int i = 0; i < _points.Length; i++)
            result[i + 1] = _points[i];
        result[result.Length - 1] = nodeB.Position;
        return result;
    }
}
