namespace SimpleCities.Road.V3;

/// <summary>
/// 道路表现接管 token：包含场景、图、样式与请求世代，后台结果必须完全匹配才能发布。
/// </summary>
public readonly record struct RoadRenderToken(
    long SceneGeneration,
    long GraphFacadeID,
    long GraphFacadeGeneration,
    long ChangeSequence,
    long RoadStyleRevision,
    long RenderRequestID)
{
    public bool IsValid =>
        SceneGeneration >= 0 &&
        GraphFacadeID >= 0 &&
        GraphFacadeGeneration >= 0 &&
        ChangeSequence >= 0 &&
        RoadStyleRevision >= 0 &&
        RenderRequestID >= 0;

    public bool Matches(RoadRenderToken other) => this == other;
}
