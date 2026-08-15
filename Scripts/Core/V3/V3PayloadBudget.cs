namespace SimpleCities.Core.V3;

/// <summary>
/// V3 分层资源预算：manifest/payload/整槽字节、实体/geometry 数量、JSON 深度与字符串长度。
/// 超限在分配大对象前失败。
/// </summary>
public readonly record struct V3PayloadBudget(
    long MaxManifestBytes,
    long MaxPayloadBytes,
    long MaxSlotTotalBytes,
    int MaxNodes,
    int MaxEdges,
    int MaxGeometrySegments,
    int MaxJsonDepth,
    int MaxStringLength)
{
    public static V3PayloadBudget Default { get; } = new(
        MaxManifestBytes: 64 * 1024,
        MaxPayloadBytes: 64 * 1024 * 1024,
        MaxSlotTotalBytes: 128 * 1024 * 1024,
        MaxNodes: 1_000_000,
        MaxEdges: 2_000_000,
        MaxGeometrySegments: 5_000_000,
        MaxJsonDepth: 64,
        MaxStringLength: 512);

    public bool AllowsCounts(int nodes, int edges, int geometrySegments) =>
        nodes >= 0 && nodes <= MaxNodes &&
        edges >= 0 && edges <= MaxEdges &&
        geometrySegments >= 0 && geometrySegments <= MaxGeometrySegments;

    public bool AllowsBytes(long manifestBytes, long payloadBytes, long slotTotalBytes) =>
        manifestBytes >= 0 && manifestBytes <= MaxManifestBytes &&
        payloadBytes >= 0 && payloadBytes <= MaxPayloadBytes &&
        slotTotalBytes >= 0 && slotTotalBytes <= MaxSlotTotalBytes;
}
