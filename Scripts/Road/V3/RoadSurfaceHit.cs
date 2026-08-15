using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// 道路表面命中的 owner 分类。
/// </summary>
public enum RoadSurfaceOwnerKind
{
    Ribbon,
    Cap,
    SemanticJoin,
    JunctionPatch,
}

/// <summary>
/// V3 道路表面命中：绑定完整 graph token 与 surface owner，供工具命中拒绝过期表现。
/// </summary>
public sealed record RoadSurfaceHit(
    GraphStateToken Token,
    RoadSurfaceOwnerKind OwnerKind,
    int? NodeID,
    int? EdgeID,
    EdgeEndpoint? Endpoint,
    RoadLocation Location,
    float DistanceSquared)
{
    public bool IsValid =>
        Token.LineageID >= 0 &&
        Token.DomainRevisionID >= 0 &&
        Token.ChangeSequence >= 0 &&
        float.IsFinite(DistanceSquared) &&
        DistanceSquared >= 0f &&
        Location.Parameter >= 0f &&
        Location.Parameter <= 1f &&
        Location.GeometryIndex >= 0;
}
