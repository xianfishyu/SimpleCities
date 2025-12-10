using System.Collections.Generic;
using Godot;

/// <summary>
/// 列车运行状态
/// </summary>
public enum TrainState
{
    /// <summary>等待发车</summary>
    WaitingToDepart,
    /// <summary>运行中</summary>
    Running,
    /// <summary>停站中</summary>
    Stopped,
    /// <summary>已到达终点</summary>
    Arrived
}

/// <summary>
/// 列车实例 - 表示一列正在运行的列车
/// </summary>
public class Train
{
    /// <summary>列车车次</summary>
    public string TrainId { get; set; }

    /// <summary>关联的时刻表</summary>
    public TrainSchedule Schedule { get; set; }

    /// <summary>当前状态</summary>
    public TrainState State { get; set; } = TrainState.WaitingToDepart;

    /// <summary>当前时刻表索引（正在执行或刚完成的条目）</summary>
    public int CurrentEntryIndex { get; set; } = 0;

    /// <summary>当前X位置（世界坐标）</summary>
    public float PositionX { get; set; }

    /// <summary>当前Y位置（轨道Y坐标）</summary>
    public float PositionY { get; set; }

    /// <summary>当前速度（单位/秒）</summary>
    public float Speed { get; set; }

    /// <summary>当前所在节点ID</summary>
    public string CurrentNodeId { get; set; }

    /// <summary>当前路径（边ID列表）</summary>
    public List<string> CurrentPath { get; set; } = new();

    /// <summary>当前路径中的边索引</summary>
    public int CurrentPathEdgeIndex { get; set; } = 0;

    /// <summary>当前边上的进度（0-1）</summary>
    public float CurrentEdgeProgress { get; set; } = 0f;

    /// <summary>是否下行（北京到天津方向）</summary>
    public bool IsDownbound { get; set; } = true;

    /// <summary>列车颜色</summary>
    public Color TrainColor { get; set; } = Colors.Blue;

    /// <summary>列车长度（渲染用）</summary>
    public float TrainLength { get; set; } = 8f;

    /// <summary>
    /// 轨道初始偏移（用于防止列车重叠）
    /// </summary>
    public float InitialPathOffset { get; set; } = 0f;

    /// <summary>
    /// 停车位置偏移（用于防止停车时重叠）
    /// </summary>
    public float ParkingOffset { get; set; } = 0f;

    public Train(string trainId)
    {
        TrainId = trainId;
    }

    public Train(TrainSchedule schedule)
    {
        TrainId = schedule.TrainId;
        Schedule = schedule;
        IsDownbound = schedule.IsDownbound;
    }

    /// <summary>
    /// 获取当前时刻表条目
    /// </summary>
    public ScheduleEntry GetCurrentEntry()
    {
        if (Schedule == null || CurrentEntryIndex >= Schedule.Entries.Count)
            return null;
        return Schedule.Entries[CurrentEntryIndex];
    }

    /// <summary>
    /// 获取下一个时刻表条目
    /// </summary>
    public ScheduleEntry GetNextEntry()
    {
        if (Schedule == null || CurrentEntryIndex + 1 >= Schedule.Entries.Count)
            return null;
        return Schedule.Entries[CurrentEntryIndex + 1];
    }

    /// <summary>
    /// 移动到下一个时刻表条目
    /// </summary>
    public void MoveToNextEntry()
    {
        CurrentEntryIndex++;
        if (CurrentEntryIndex >= Schedule.Entries.Count)
        {
            State = TrainState.Arrived;
        }
    }

    /// <summary>
    /// 设置新路径
    /// </summary>
    /// <param name="path">边ID列表</param>
    /// <param name="startNodeId">起始节点ID</param>
    public void SetPath(List<string> path, string startNodeId)
    {
        CurrentPath = path ?? new List<string>();
        CurrentPathEdgeIndex = 0;
        CurrentEdgeProgress = 0f;
        CurrentNodeId = startNodeId;
    }

    /// <summary>
    /// 获取当前所在的边
    /// </summary>
    public string GetCurrentEdgeId()
    {
        if (CurrentPath == null || CurrentPathEdgeIndex >= CurrentPath.Count)
            return null;
        return CurrentPath[CurrentPathEdgeIndex];
    }

    /// <summary>
    /// 移动到路径中的下一条边
    /// </summary>
    /// <param name="nextNodeId">下一个节点ID</param>
    /// <returns>是否成功移动到下一条边</returns>
    public bool MoveToNextEdge(string nextNodeId)
    {
        CurrentPathEdgeIndex++;
        CurrentEdgeProgress = 0f;
        CurrentNodeId = nextNodeId;

        return CurrentPathEdgeIndex < CurrentPath.Count;
    }

    /// <summary>
    /// 检查是否到达路径终点
    /// </summary>
    public bool HasReachedPathEnd()
    {
        return CurrentPath == null ||
               CurrentPath.Count == 0 ||
               CurrentPathEdgeIndex >= CurrentPath.Count;
    }

    /// <summary>
    /// 获取路径剩余边数
    /// </summary>
    public int GetRemainingEdges()
    {
        if (CurrentPath == null) return 0;
        return CurrentPath.Count - CurrentPathEdgeIndex;
    }
}
