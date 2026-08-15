using System;
using System.Collections.Generic;
using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

/// <summary>
/// V3 道路应用门面：持有当前 controller、保存根与保存/加载协调器，
/// 为真实 Godot RoadSystem/RoadBuilder 装配提供纯 C# 同步入口。
/// </summary>
public sealed class RoadGraphV3Application
{
    private readonly string _root;
    private readonly RoadGraphCapacity _capacity;
    private readonly V3PayloadBudget _budget;
    private readonly V3CoordinatorGate _gate;
    private readonly V3RoadSaveLoadCoordinator _coordinator;
    private readonly V3SlotAutosaveCoordinator _autosave;

    public RoadGraphV3Controller Controller { get; private set; }
    public string CurrentSlotID { get; private set; } = string.Empty;

    public RoadGraphV3Application(
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget)
    {
        ArgumentNullException.ThrowIfNull(root);

        _root = root;
        _capacity = capacity;
        _budget = budget;
        _gate = new V3CoordinatorGate();
        _coordinator = new V3RoadSaveLoadCoordinator(_gate);
        _autosave = new V3SlotAutosaveCoordinator(_gate, new V3SlotAutosaveService());
        Controller = new RoadGraphV3Controller(
            new RoadGraphV3Facade(RoadGraphV3Revision.Empty(capacity), 1),
            new RoadEditHistoryV3(100, 100000));
    }

    public bool Save(
        string slotId,
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile)
    {
        bool success = _coordinator.Save(
            slotId,
            _root,
            Controller.Facade.Revision,
            displayName,
            cityName,
            timestamp,
            population,
            funds,
            thumbnailFile);
        if (success)
            CurrentSlotID = slotId;
        return success;
    }

    public bool Load(string slotId, long lineageID = 1)
    {
        RoadGraphV3Controller? loaded = _coordinator.Load(slotId, _root, _capacity, _budget, lineageID);
        if (loaded is null)
            return false;

        Controller = loaded;
        CurrentSlotID = slotId;
        return true;
    }

    public bool TryUndo(GraphStateToken expectedToken, out RoadGraphV3ChangeSummary summary) =>
        Controller.TryUndo(expectedToken, out summary);

    public bool TryRedo(GraphStateToken expectedToken, out RoadGraphV3ChangeSummary summary) =>
        Controller.TryRedo(expectedToken, out summary);

    public bool TryBuild(RoadPlacementSessionV3 session, out RoadGraphV3ChangeSummary summary) =>
        new RoadToolCommandExecutor(Controller).TryBuild(session, out summary);

    public bool TryUpgrade(RoadUpgradeSessionV3 session, out IReadOnlyList<int> changedEdgeIDs) =>
        new RoadToolCommandExecutor(Controller).TryUpgrade(session, out changedEdgeIDs);

    public bool TryRemove(RoadRemovalSessionV3 session, out IReadOnlyList<int> removedEdgeIDs) =>
        new RoadToolCommandExecutor(Controller).TryRemove(session, out removedEdgeIDs);

    public V3AutosaveDecision TryAutosave(
        string slotId,
        RoadGraphV3Revision revision,
        bool hasNewerSuccess,
        out bool saved) =>
        _autosave.TryAutosave(slotId, _root, revision, hasNewerSuccess, out saved);

    public bool Delete(string slotId) => V3SlotDeleteService.Delete(slotId, _root);

    public IReadOnlyList<V3SlotSummary> List() => new V3FileSlotStore(_root).List();

    public void NewCity(long lineageID = 1)
    {
        Controller = new RoadGraphV3Controller(
            new RoadGraphV3Facade(RoadGraphV3Revision.Empty(_capacity), lineageID),
            new RoadEditHistoryV3(100, 100000));
        CurrentSlotID = string.Empty;
    }
}
