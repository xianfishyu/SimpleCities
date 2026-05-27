using Godot;
using System.Collections.Generic;

/// <summary>
/// 几何边：相邻两个 Junction 之间的一段路。
/// 含两端 Junction ID + 中间 waypoints + 总长度。
/// 归属于一条 Road（玩家一次画线产生的逻辑集合）；劈分场景下同一 Road 可由多个 Segment 组成。
/// </summary>
public class Segment
{
    public int ID { get; }
    public int FromJunctionID { get; }
    public int ToJunctionID { get; }

    /// <summary>所属 Road 的 ID。劈分时新两段继承原 Segment 的 RoadID；合并时较早 RoadID 吸收较晚。</summary>
    public int RoadID { get; internal set; }

    /// <summary>中间途经格点（不含两端 Junction 坐标），可为空数组</summary>
    public Vector2[] Waypoints { get; }

    /// <summary>总长度 = 所有 waypoint 段 + 首尾段长度之和</summary>
    public float TotalLength { get; }

    public Segment(int id, int fromJunctionID, int toJunctionID, int roadID,
                   Vector2[] waypoints, float totalLength)
    {
        ID = id;
        FromJunctionID = fromJunctionID;
        ToJunctionID = toJunctionID;
        RoadID = roadID;
        Waypoints = waypoints;
        TotalLength = totalLength;
    }

    /// <summary>
    /// 返回 (from, to, dir) 序列，从 FromJunction → Waypoints → ToJunction。
    /// 供渲染器遍历。
    /// </summary>
    public IEnumerable<(Vector2 from, Vector2 to, Direction dir)> GetSubSegments(
        Junction fromJunction, Junction toJunction, float cellSize)
    {
        Vector2 prev = fromJunction.Position;

        foreach (var wp in Waypoints)
        {
            var d = DirectionUtil.FromDisplacement(prev, wp, cellSize);
            if (d.HasValue)
                yield return (prev, wp, d.Value);
            prev = wp;
        }

        var lastDir = DirectionUtil.FromDisplacement(prev, toJunction.Position, cellSize);
        if (lastDir.HasValue)
            yield return (prev, toJunction.Position, lastDir.Value);
    }
}
