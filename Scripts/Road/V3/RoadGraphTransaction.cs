using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 唯一事务事件摘要：普通 mutation 为增量 created/removed/updated；
/// 外部存档恢复为 full reset，消费者丢弃缓存并从活动图重建。
/// </summary>
public sealed record RoadGraphV3ChangeSummary(
    IReadOnlyList<int> CreatedNodeIDs,
    IReadOnlyList<int> RemovedNodeIDs,
    IReadOnlyList<int> UpdatedNodeIDs,
    IReadOnlyList<int> CreatedEdgeIDs,
    IReadOnlyList<int> RemovedEdgeIDs,
    IReadOnlyList<int> UpdatedEdgeIDs,
    bool IsFullReset,
    long ChangeSequence)
{
    public static RoadGraphV3ChangeSummary FullReset(long changeSequence) =>
        new([], [], [], [], [], [], true, changeSequence);
}

/// <summary>
/// 三层身份：LineageID 标识外部 load/full reset 后的新图世代；
/// DomainRevisionID 标识可逆领域内容状态；ChangeSequence 标识每次成功 commit（含 undo/redo/full reset）。
/// </summary>
public readonly record struct GraphStateToken(
    long LineageID,
    long DomainRevisionID,
    long ChangeSequence)
{
    public bool Matches(GraphStateToken expected) =>
        LineageID == expected.LineageID &&
        DomainRevisionID == expected.DomainRevisionID &&
        ChangeSequence == expected.ChangeSequence;
}

public static class RoadGraphV3ChangeSummaryFactory
{
    public static RoadGraphV3ChangeSummary Incremental(
        IEnumerable<int> createdNodeIDs,
        IEnumerable<int> removedNodeIDs,
        IEnumerable<int> updatedNodeIDs,
        IEnumerable<int> createdEdgeIDs,
        IEnumerable<int> removedEdgeIDs,
        IEnumerable<int> updatedEdgeIDs,
        long changeSequence)
    {
        ArgumentNullException.ThrowIfNull(createdNodeIDs);
        ArgumentNullException.ThrowIfNull(removedNodeIDs);
        ArgumentNullException.ThrowIfNull(updatedNodeIDs);
        ArgumentNullException.ThrowIfNull(createdEdgeIDs);
        ArgumentNullException.ThrowIfNull(removedEdgeIDs);
        ArgumentNullException.ThrowIfNull(updatedEdgeIDs);

        return new RoadGraphV3ChangeSummary(
            createdNodeIDs.Distinct().Order().ToArray(),
            removedNodeIDs.Distinct().Order().ToArray(),
            updatedNodeIDs.Distinct().Order().ToArray(),
            createdEdgeIDs.Distinct().Order().ToArray(),
            removedEdgeIDs.Distinct().Order().ToArray(),
            updatedEdgeIDs.Distinct().Order().ToArray(),
            false,
            changeSequence);
    }
}
