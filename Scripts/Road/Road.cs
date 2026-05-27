using System.Collections.Generic;

/// <summary>
/// 逻辑路：玩家"一次画线"操作产生的 Segment 集合。
/// 一条 Road 可由 1..N 个 Segment 组成（被路口劈分仍是同一条 Road）。
/// 合并时较早 RoadID 吸收较晚 RoadID 的所有 Segment。
/// 未来扩展：路名、车道数、限速等属性挂在这层。
/// </summary>
public class Road
{
    public int ID { get; }

    private readonly HashSet<int> _segmentIDs = new();

    /// <summary>该 Road 当前包含的所有 Segment ID。</summary>
    public IReadOnlyCollection<int> SegmentIDs => _segmentIDs;

    public Road(int id)
    {
        ID = id;
    }

    public void AddSegment(int segmentID) => _segmentIDs.Add(segmentID);

    public void RemoveSegment(int segmentID) => _segmentIDs.Remove(segmentID);

    public bool ContainsSegment(int segmentID) => _segmentIDs.Contains(segmentID);

    /// <summary>该 Road 是否已无 Segment，可被 RoadNetwork 清理。</summary>
    public bool IsEmpty => _segmentIDs.Count == 0;

    public int SegmentCount => _segmentIDs.Count;
}
