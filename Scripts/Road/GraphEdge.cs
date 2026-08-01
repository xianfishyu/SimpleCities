using Godot;
using System;

public class GraphEdge
{
    public int ID { get; }
    public int NodeA { get; internal set; }
    public int NodeB { get; internal set; }

    /// <summary>中间途经点（不含两端节点坐标）。</summary>
    private readonly Vector2[] _points;
    public Vector2[] Points => (Vector2[])_points.Clone();
    internal Vector2[] InternalPoints => _points;

    public int GroupID { get; internal set; }
    public RoadType Type { get; internal set; }
    public float Length { get; }

    public GraphEdge(int id, int nodeA, int nodeB, Vector2[] points, int groupID, RoadType type, float length)
    {
        ID = id;
        NodeA = nodeA;
        NodeB = nodeB;
        _points = (Vector2[])points.Clone();
        GroupID = groupID;
        Type = type;
        Length = length;
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
