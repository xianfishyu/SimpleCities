using System;
using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

/// <summary>
/// 将 RoadGraphV3Revision 保存为文件槽。
/// </summary>
public static class V3SlotSaveService
{
    public static bool Save(
        string slotId,
        string root,
        RoadGraphV3Revision revision,
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(revision);

        V3RoadSlotBundle bundle = V3RoadSlotFactory.Create(
            slotId,
            displayName,
            cityName,
            timestamp,
            population,
            funds,
            thumbnailFile,
            revision);

        return new V3FileSlotStore(root).Save(slotId, bundle.Manifest, bundle.Payloads);
    }
}
