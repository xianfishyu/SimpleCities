using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

public sealed record RoadTypeStyleCatalogResult(
    bool Success,
    IReadOnlyDictionary<RoadType, RoadTypeStyle>? Styles,
    string? Error)
{
    public static RoadTypeStyleCatalogResult Failure(string error) => new(false, null, error);
}

/// <summary>
/// 四类道路样式目录：必须恰好覆盖 Dirt/Street/Arterial/Highway 且无重复。
/// </summary>
public static class RoadTypeStyleCatalog
{
    public static RoadTypeStyleCatalogResult CreateDefault() =>
        Create(
            [
                new RoadTypeStyle { RoadType = RoadType.Dirt, DisplayName = "土路", Color = Colors.Brown, Width = 1f },
                new RoadTypeStyle { RoadType = RoadType.Street, DisplayName = "街道", Color = Colors.White, Width = 1f },
                new RoadTypeStyle { RoadType = RoadType.Arterial, DisplayName = "主干道", Color = Colors.Yellow, Width = 1.5f },
                new RoadTypeStyle { RoadType = RoadType.Highway, DisplayName = "高速", Color = Colors.Red, Width = 2f },
            ]);

    public static RoadTypeStyleCatalogResult Create(IReadOnlyList<RoadTypeStyle> styles)
    {
        ArgumentNullException.ThrowIfNull(styles);

        var byType = new Dictionary<RoadType, RoadTypeStyle>();
        foreach (RoadTypeStyle style in styles)
        {
            ArgumentNullException.ThrowIfNull(style);
            if (!style.Validate(out string? error))
                return RoadTypeStyleCatalogResult.Failure(error ?? "InvalidStyle");
            if (!byType.TryAdd(style.RoadType, style))
                return RoadTypeStyleCatalogResult.Failure($"DuplicateRoadType:{style.RoadType}");
        }

        foreach (RoadType roadType in Enum.GetValues<RoadType>())
        {
            if (!byType.ContainsKey(roadType))
                return RoadTypeStyleCatalogResult.Failure($"MissingRoadType:{roadType}");
        }

        return new RoadTypeStyleCatalogResult(true, byType, null);
    }
}
