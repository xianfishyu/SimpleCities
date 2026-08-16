using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

/// <summary>
/// Load 的工具参与者：包装 Preflight 阶段生成的 empty tool root 计划。
/// </summary>
public sealed record V3ToolLoadParticipant(RoadToolFullReset? Plan)
{
    public const string ParticipantName = "tool";

    public bool IsPrepared => Plan is not null;
    public bool CanCommit => Plan is { IsValid: true };

    public static V3ToolLoadParticipant Prepare(RoadToolFullReset plan) => new(plan);

    public static V3ToolLoadParticipant Unprepared => new((RoadToolFullReset?)null);

    public bool TryApplyTo(RoadToolState state) =>
        Plan is not null && Plan.TryApplyTo(state);
}
