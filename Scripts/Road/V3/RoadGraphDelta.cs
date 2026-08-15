using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// 一个实体在 delta 中的前后值。Before/After 都为 null 的条目不允许出现。
/// </summary>
public sealed record RoadGraphV3EntityChange<T>(T? Before, T? After)
{
    public bool IsCreated => Before is null && After is not null;
    public bool IsRemoved => Before is not null && After is null;
    public bool IsUpdated => Before is not null && After is not null;

    public RoadGraphV3EntityChange<T> Invert() => new(After, Before);
}

/// <summary>
/// 可逆 RoadGraph delta：保存完整 Node/Edge 前后实体与 revision 边界，
/// 用于 undo/redo；不保存 JSON 字符串。
/// </summary>
public sealed record RoadGraphV3Delta(
    long BeforeRevisionID,
    long AfterRevisionID,
    IReadOnlyList<RoadGraphV3EntityChange<RoadGraphV3Node>> NodeChanges,
    IReadOnlyList<RoadGraphV3EntityChange<RoadGraphV3Edge>> EdgeChanges)
{
    public bool IsEmpty => NodeChanges.Count == 0 && EdgeChanges.Count == 0;

    public static RoadGraphV3Delta Empty(long revisionID) =>
        new(revisionID, revisionID, [], []);

    public RoadGraphV3Delta Invert() =>
        new(
            AfterRevisionID,
            BeforeRevisionID,
            NodeChanges.Select(change => change.Invert()).ToArray(),
            EdgeChanges.Select(change => change.Invert()).ToArray());
}
