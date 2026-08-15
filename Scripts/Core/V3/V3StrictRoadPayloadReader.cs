using System;
using System.Linq;
using System.Text;
using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

public sealed record V3StrictRoadPayloadResult(
    bool Success,
    RoadCanonicalGraph? Graph,
    int? NextID,
    string? Error)
{
    public static V3StrictRoadPayloadResult Failure(string error) => new(false, null, null, error);
}

/// <summary>
/// 结合 format v1 codec 与分层预算的严格道路 payload 读取。
/// </summary>
public static class V3StrictRoadPayloadReader
{
    public static V3StrictRoadPayloadResult Read(string json, V3PayloadBudget budget)
    {
        ArgumentNullException.ThrowIfNull(json);

        long payloadBytes = Encoding.UTF8.GetByteCount(json);
        if (payloadBytes > budget.MaxPayloadBytes)
            return V3StrictRoadPayloadResult.Failure("PayloadBytesExceeded");

        RoadGraphV3CodecResult codec = RoadGraphV3Codec.Deserialize(json);
        if (!codec.Success || codec.Graph is null || codec.NextID is not int nextID)
            return V3StrictRoadPayloadResult.Failure(codec.Error ?? "InvalidPayload");

        int geometryCount = codec.Graph.Edges.Sum(edge => edge.Geometry.Count);
        if (!budget.AllowsCounts(codec.Graph.Nodes.Count, codec.Graph.Edges.Count, geometryCount))
            return V3StrictRoadPayloadResult.Failure("EntityCountsExceeded");

        return new V3StrictRoadPayloadResult(true, codec.Graph, nextID, null);
    }
}
