using System;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 类型化建造请求：几何路径 + 目标 RoadType。
/// 构造时不提供静默默认类型；非法类型或空几何在进入 mutation plan 前失败。
/// </summary>
public sealed record RoadBuildRequest(RoadPath Path, RoadType RoadType)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Path);
        if (!RoadTypeChangeValidator.IsValidRoadType(RoadType))
            throw new ArgumentOutOfRangeException(nameof(RoadType), RoadType, "Unknown road type.");
        if (Path.Segments.Count == 0)
            throw new ArgumentException("Build request must contain at least one geometry segment.", nameof(Path));
        if (Path.Segments.Any(segment => segment is null))
            throw new ArgumentException("Build request geometry cannot contain null.", nameof(Path));
    }
}
