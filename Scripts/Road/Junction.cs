using Godot;
using System.Collections.Generic;
using System.Linq;

public enum JunctionType
{
    Endpoint,
    Straight,
    Curve,
    TJunction,
    Cross,
    XCross,
    MultiWay
}

public class Junction
{
    public int ID { get; }
    public Vector2 Position { get; }

    /// <summary>
    /// 每条连接到此 Junction 的 Segment 在这里的入向 + 对端 Junction ID。
    /// 以 SegmentID 为键：SegmentID 是真正唯一的物理边标识，
    /// 同一对 Junction 之间的多重边 (Segment) 各自独立记录；
    /// 同一条 Road 内部的多个 Segment 接到同一 Junction 也不冲突。
    /// </summary>
    private readonly Dictionary<int /*segmentID*/, (int neighborJunctionID, Direction dirAtThis)> _connections = new();

    public JunctionType Type { get; private set; } = JunctionType.Endpoint;

    public Junction(int id, Vector2 position)
    {
        ID = id;
        Position = position;
    }

    public void AddSegmentConnection(int segmentID, int neighborJunctionID, Direction dirAtThisJunction)
    {
        _connections[segmentID] = (neighborJunctionID, dirAtThisJunction);
        RecalculateType();
    }

    public void RemoveSegmentConnection(int segmentID)
    {
        _connections.Remove(segmentID);
        RecalculateType();
    }

    /// <summary>连接到此 Junction 的 Segment 总数。</summary>
    public int ConnectionCount => _connections.Count;

    /// <summary>所有连接到此 Junction 的 Segment ID（不重复，每条 Segment 仅一项）。</summary>
    public IEnumerable<int> ConnectedSegmentIDs => _connections.Keys;

    /// <summary>邻居 Junction ID 列表。多重边场景下同一邻居可能出现多次。</summary>
    public IReadOnlyList<int> NeighborJunctionIDs =>
        _connections.Values.Select(v => v.neighborJunctionID).ToList();

    /// <summary>所有 Segment 在此处的入向（含重复）。</summary>
    public IReadOnlyList<Direction> IncomingDirections =>
        _connections.Values.Select(v => v.dirAtThis).ToList();

    public void RecalculateType()
    {
        var dirs = IncomingDirections;
        int count = dirs.Count;
        Type = count switch
        {
            0 or 1 => JunctionType.Endpoint,
            2 => DetermineTwoWayType(dirs),
            3 => JunctionType.TJunction,
            4 => DetermineFourWayType(dirs),
            _ => JunctionType.MultiWay
        };
    }

    private static JunctionType DetermineTwoWayType(IReadOnlyList<Direction> dirs)
    {
        var d0 = DirectionUtil.GetDisplacement(dirs[0]);
        var d1 = DirectionUtil.GetDisplacement(dirs[1]);
        if (d0.X + d1.X == 0 && d0.Y + d1.Y == 0)
            return JunctionType.Straight;
        return JunctionType.Curve;
    }

    private static JunctionType DetermineFourWayType(IReadOnlyList<Direction> dirs)
    {
        int ortho = 0, diag = 0;
        foreach (var d in dirs)
        {
            if (DirectionUtil.IsOrthogonal(d)) ortho++;
            else diag++;
        }
        if (ortho == 4) return JunctionType.Cross;
        if (diag == 4) return JunctionType.XCross;
        return JunctionType.MultiWay;
    }
}
