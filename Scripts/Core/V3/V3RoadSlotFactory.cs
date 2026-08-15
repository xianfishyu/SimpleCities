using System;
using System.Collections.Generic;
using System.Text;
using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

public sealed record V3RoadSlotBundle(
    V3Manifest Manifest,
    IReadOnlyDictionary<string, byte[]> Payloads);

/// <summary>
/// 从 RoadGraphV3Revision 构造完整 V3 道路槽（manifest + road_network.json payload）。
/// </summary>
public static class V3RoadSlotFactory
{
    public const string RoadNetworkFileName = "road_network.json";

    public static V3RoadSlotBundle Create(
        string slotId,
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile,
        RoadGraphV3Revision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        string json = RoadGraphV3Persistence.Serialize(revision);
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [RoadNetworkFileName] = Encoding.UTF8.GetBytes(json),
        };

        V3Manifest manifest = V3ManifestBuilder.Create(
            slotId,
            displayName,
            cityName,
            timestamp,
            population,
            funds,
            thumbnailFile,
            [new KeyValuePair<string, byte[]>(RoadNetworkFileName, payloads[RoadNetworkFileName])]);

        return new V3RoadSlotBundle(manifest, payloads);
    }
}
