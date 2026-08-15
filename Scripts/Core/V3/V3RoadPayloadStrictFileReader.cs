using System;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 从文件读取道路 payload 并执行严格读取（重复键 + codec + 预算）。
/// </summary>
public static class V3RoadPayloadStrictFileReader
{
    public static V3StrictRoadPayloadResult Read(string filePath, V3PayloadBudget budget)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        V3StrictTokenResult token = V3StrictTokenReader.ReadFile(filePath);
        if (!token.Success)
            return V3StrictRoadPayloadResult.Failure(token.Error ?? "TokenReadFailed");
        if (token.Json is null)
            return V3StrictRoadPayloadResult.Failure("EmptyPayload");

        return V3RoadPayloadStrictReader.Read(token.Json, budget);
    }
}
