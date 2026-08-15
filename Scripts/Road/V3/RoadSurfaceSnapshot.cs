using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// 道路表面 owner：标识某块已呈现表面的稳定归属，供命中与失效清理使用。
/// </summary>
public sealed record RoadSurfaceOwner(
    RoadSurfaceOwnerKind Kind,
    int? NodeID,
    int? EdgeID,
    EdgeEndpoint? Endpoint,
    RoadLocation Location);

/// <summary>
/// 一次道路表面构建的不可变快照，绑定完整 graph token 与 owner 列表。
/// </summary>
public sealed record RoadSurfaceSnapshot(
    GraphStateToken Token,
    IReadOnlyList<RoadSurfaceOwner> Owners)
{
    public bool IsValid =>
        Token.LineageID >= 0 &&
        Token.DomainRevisionID >= 0 &&
        Token.ChangeSequence >= 0 &&
        Owners is not null;

    public IReadOnlyList<RoadSurfaceOwner> FindByEdge(int edgeID) =>
        Owners
            .Where(owner => owner.EdgeID == edgeID)
            .ToList();

    public IReadOnlyList<RoadSurfaceOwner> FindByNode(int nodeID) =>
        Owners
            .Where(owner => owner.NodeID == nodeID)
            .ToList();
}
