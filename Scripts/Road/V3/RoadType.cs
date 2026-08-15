using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 稳定道路等级。类型是 Edge 级 merge key 的一部分，不进入 Node 或 geometry。
/// </summary>
public enum RoadType
{
    Dirt = 0,
    Street = 1,
    Arterial = 2,
    Highway = 3,
}

/// <summary>
/// V3 format v1 使用的稳定 wire name。禁止保存展示名或枚举整数。
/// </summary>
public static class RoadTypeNames
{
    public const string Dirt = "dirt";
    public const string Street = "street";
    public const string Arterial = "arterial";
    public const string Highway = "highway";

    public static string ToWireName(RoadType roadType) => roadType switch
    {
        RoadType.Dirt => Dirt,
        RoadType.Street => Street,
        RoadType.Arterial => Arterial,
        RoadType.Highway => Highway,
        _ => throw new ArgumentOutOfRangeException(nameof(roadType), roadType, "Unknown road type."),
    };

    public static bool TryParseWireName(string? name, out RoadType roadType)
    {
        switch (name)
        {
            case Dirt:
                roadType = RoadType.Dirt;
                return true;
            case Street:
                roadType = RoadType.Street;
                return true;
            case Arterial:
                roadType = RoadType.Arterial;
                return true;
            case Highway:
                roadType = RoadType.Highway;
                return true;
            default:
                roadType = default;
                return false;
        }
    }
}
