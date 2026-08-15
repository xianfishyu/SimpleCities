using System;
using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

/// <summary>
/// 道路保存管线：将 RoadGraphV3Revision/Controller 保存为槽，并从槽加载为新的 Controller。
/// </summary>
public static class V3RoadSavePipeline
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
        string? thumbnailFile) =>
        V3SlotSaveService.Save(slotId, root, revision, displayName, cityName, timestamp, population, funds, thumbnailFile);

    public static bool SaveController(
        string slotId,
        string root,
        RoadGraphV3Controller controller,
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile)
    {
        ArgumentNullException.ThrowIfNull(controller);
        return Save(slotId, root, controller.Facade.Revision, displayName, cityName, timestamp, population, funds, thumbnailFile);
    }

    public static RoadGraphV3Controller? LoadController(
        string slotId,
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget,
        long lineageID = 1)
    {
        ArgumentNullException.ThrowIfNull(root);

        V3SlotLoadServiceResult load = V3SlotLoadService.Load(slotId, root, capacity, budget);
        if (!load.Success || load.Revision is null)
            return null;

        var facade = new RoadGraphV3Facade(load.Revision, lineageID);
        return new RoadGraphV3Controller(facade, new RoadEditHistoryV3(100, 100000));
    }
}
