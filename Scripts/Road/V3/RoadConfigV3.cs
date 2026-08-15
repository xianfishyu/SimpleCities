using Godot;
using System;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 道路配置资源：持有四类 RoadTypeStyleResource，并提供目录校验。
/// </summary>
[GlobalClass]
public partial class RoadConfigV3 : Resource
{
    [Export] public RoadTypeStyleResource[] Styles { get; set; } = [];

    public RoadTypeStyleCatalogResult CreateCatalog() =>
        RoadTypeStyleCatalog.Create(Styles.Select(style => style.ToData()).ToList());
}
