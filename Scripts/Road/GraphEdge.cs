using Godot;
using System;

public class GraphEdge
{
    public int ID { get; }
    public int NodeA { get; internal set; }
    public int NodeB { get; internal set; }

    /// <summary>中间途经点（不含两端节点坐标）。</summary>
    public Vector2[] Points { get; }

    public int GroupID { get; internal set; }
    public RoadType Type { get; internal set; }
    public float Length { get; }

    public GraphEdge(int id, int nodeA, int nodeB, Vector2[] points, int groupID, RoadType type, float length)
    {
        ID = id;
        NodeA = nodeA;
        NodeB = nodeB;
        Points = points;
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

        var result = new Vector2[Points.Length + 2];
        result[0] = nodeA.Position;
        for (int i = 0; i < Points.Length; i++)
            result[i + 1] = Points[i];
        result[result.Length - 1] = nodeB.Position;
        return result;
    }
}
