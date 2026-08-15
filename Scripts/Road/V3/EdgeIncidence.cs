namespace SimpleCities.Road.V3;

public enum EdgeEndpoint
{
    A,
    B,
}

/// <summary>
/// 一条 Edge 在某个 GraphNode 上的端接角色。
/// self-loop 在同一节点注册 A/B 两条 incidence，因此 degree 按 incidence 计数。
/// </summary>
public readonly record struct EdgeIncidence(
    int EdgeID,
    EdgeEndpoint Endpoint,
    int NeighborNodeID);
