using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// 按 RoadType 查询稳定道路样式的只读提供器。
/// </summary>
public sealed class RoadStyleProvider
{
    private readonly IReadOnlyDictionary<RoadType, RoadTypeStyle> _styles;

    public RoadStyleProvider(RoadTypeStyleCatalogResult catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!catalog.Success || catalog.Styles is null)
            throw new ArgumentException("Catalog must be successful.", nameof(catalog));

        _styles = catalog.Styles;
    }

    public RoadStyleProvider(IReadOnlyDictionary<RoadType, RoadTypeStyle> styles)
    {
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
    }

    public RoadTypeStyle Get(RoadType roadType) =>
        _styles.TryGetValue(roadType, out RoadTypeStyle? style)
            ? style
            : throw new KeyNotFoundException($"No style registered for road type '{roadType}'.");

    public bool TryGet(RoadType roadType, out RoadTypeStyle style) =>
        _styles.TryGetValue(roadType, out style!);
}
