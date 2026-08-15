using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Core.V3;

/// <summary>
/// Load 的 prepared aggregate 摘要：所有 required participant 都 prepared 后才能进入 non-yield commit。
/// </summary>
public sealed record V3PreparedAggregate(
    IReadOnlySet<string> RequiredParticipants,
    IReadOnlySet<string> PreparedParticipants,
    IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<string> MissingParticipants =>
        RequiredParticipants.Except(PreparedParticipants).Order().ToList();

    public bool AllPrepared => MissingParticipants.Count == 0;
    public bool CanCommit => AllPrepared;
}
