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

        if (!File.Exists(filePath))
            return V3StrictRoadPayloadResult.Failure("FileMissing");

        string json = File.ReadAllText(filePath);
        return V3RoadPayloadStrictReader.Read(json, budget);
    }
}
