using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

/// <summary>
/// Load 的 Preflight 计划：持有已解析 revision 与 tool/renderer 参与者计划，
/// 只有全部参与者可提交时才允许进入 non-yield commit。
/// </summary>
public sealed record V3LoadPreflightPlan(
    RoadGraphV3Revision Revision,
    V3ToolLoadParticipant? Tool,
    V3RendererLoadParticipant? Renderer)
{
    public bool CanCommit =>
        (Tool?.CanCommit ?? true) &&
        (Renderer?.CanCommit ?? true);

    public RoadGraphV3Controller CreateController(long lineageID) =>
        new(
            new RoadGraphV3Facade(Revision, lineageID),
            new RoadEditHistoryV3(100, 100000));
}
