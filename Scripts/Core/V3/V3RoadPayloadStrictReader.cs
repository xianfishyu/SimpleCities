using System;
using System.Text.Json;

namespace SimpleCities.Core.V3;

/// <summary>
/// 道路 payload 严格读取：先拒绝重复键，再执行 codec + 分层预算读取。
/// </summary>
public static class V3RoadPayloadStrictReader
{
    public static V3StrictRoadPayloadResult Read(string json, V3PayloadBudget budget)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            if (V3JsonDuplicateDetector.TryDetectDuplicateKey(json, out string? duplicateKey))
                return V3StrictRoadPayloadResult.Failure($"DuplicateKey:{duplicateKey}");
        }
        catch (JsonException)
        {
            return V3StrictRoadPayloadResult.Failure("MalformedJson");
        }

        return V3StrictRoadPayloadReader.Read(json, budget);
    }
}
