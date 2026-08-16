using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

/// <summary>
/// Load 的渲染参与者：包装 Preflight 阶段生成的 presentation full-reset 计划，
/// 要求同时具备有效 surface snapshot 与 mesh 数据。
/// </summary>
public sealed record V3RendererLoadParticipant(RoadPresentationFullReset? Plan)
{
    public const string ParticipantName = "renderer";

    public bool IsPrepared => Plan is not null;
    public bool CanCommit => Plan is { IsValid: true, HasMeshData: true };

    public static V3RendererLoadParticipant Prepare(RoadPresentationFullReset plan) => new(plan);

    public static V3RendererLoadParticipant Unprepared => new((RoadPresentationFullReset?)null);
}
