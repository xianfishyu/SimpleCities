using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly V3SlotTransactionCoordinator _transactionCoordinator;

    public RoadGraphV3Controller Controller { get; private set; }
    public RoadGraphV3Revision Revision => Controller.Facade.Revision;
    public bool CanUndo => Controller.History.UndoCount > 0;
    public bool CanRedo => Controller.History.RedoCount > 0;
    public void ClearHistory() => Controller.History.Clear();
    public RoadToolState ToolState { get; } = new();
    public RoadStyleProvider DefaultStyles { get; }
    public RoadPresentationController Presentation { get; }
    public RoadToolType CurrentTool
    {
        get => ToolState.CurrentTool;
        set => ToolState.SwitchTo(value);
    }

    public RoadType SelectedRoadType
    {
        get => ToolState.SelectedRoadType;
        set
        {
            if (!ToolState.TrySelectRoadType(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown road type.");
        }
    }

    public string CurrentSlotID { get; private set; } = string.Empty;
    public bool HasCurrentSlot => CurrentSlotID.Length > 0;
    public V3SlotSummary? CurrentSlotSummary =>
        string.IsNullOrEmpty(CurrentSlotID) ? null : GetStatus(CurrentSlotID);

    public string? CurrentSlotDisplayName => CurrentSlotSummary?.DisplayName;
    public string? CurrentSlotTimestamp => CurrentSlotSummary?.Timestamp;
    public V3SlotOccupant? CurrentSlotOccupant => CurrentSlotSummary?.Occupant;
    public bool CurrentSlotIsComplete => CurrentSlotOccupant == V3SlotOccupant.CompleteV3;
    public bool CurrentSlotIsCorrupt => CurrentSlotOccupant == V3SlotOccupant.CorruptV3;
    public bool CurrentSlotIsAbsent => CurrentSlotOccupant == V3SlotOccupant.Absent;
    public bool CurrentSlotIsUnsafe => CurrentSlotOccupant == V3SlotOccupant.Unsafe;
    public bool CurrentSlotIsForeign => CurrentSlotOccupant == V3SlotOccupant.Foreign;
    public bool CurrentSlotIsOperable => CurrentSlotIsComplete || CurrentSlotIsCorrupt;

    public RoadGraphV3Application(
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget) : this(root, capacity, budget, RoadTypeStyleCatalog.CreateDefault())
    {
    }

    public RoadGraphV3Application(
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget,
        RoadTypeStyleCatalogResult catalogResult)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(catalogResult);
        if (!catalogResult.Success || catalogResult.Styles is null)
            throw new ArgumentException("Catalog must be successful.", nameof(catalogResult));

        _root = root;
        _capacity = capacity;
        _budget = budget;
        _gate = new V3CoordinatorGate();
        _coordinator = new V3RoadSaveLoadCoordinator(_gate);
        _autosave = new V3SlotAutosaveCoordinator(_gate, new V3SlotAutosaveService());
        _transactionCoordinator = new V3SlotTransactionCoordinator(_gate);
        DefaultStyles = new RoadStyleProvider(catalogResult);
        Presentation = new RoadPresentationController(
            new RoadPresentationState(new RoadRenderToken(0, 0, 0, 0, 0, 0)),
            DefaultStyles);
        Controller = new RoadGraphV3Controller(
            new RoadGraphV3Facade(RoadGraphV3Revision.Empty(capacity), 1),
            new RoadEditHistoryV3(100, 100000));
    }

    public RoadGraphV3Application(
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget,
        RoadConfigV3 config) : this(root, capacity, budget, config.CreateCatalog())
    {
        ArgumentNullException.ThrowIfNull(config);
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

    public bool SaveCurrent(
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile)
    {
        if (string.IsNullOrEmpty(CurrentSlotID))
            return false;

        return Save(CurrentSlotID, displayName, cityName, timestamp, population, funds, thumbnailFile);
    }

    public bool SaveAs(
        string slotId,
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile) =>
        Save(slotId, displayName, cityName, timestamp, population, funds, thumbnailFile);

    public bool Load(string slotId, long lineageID = 1)
    {
        RoadGraphV3Controller? loaded = _coordinator.Load(slotId, _root, _capacity, _budget, lineageID);
        if (loaded is null)
            return false;

        Controller = loaded;
        CurrentSlotID = slotId;
        return true;
    }

    public bool LoadIntoCurrent(string slotId, long newLineageID = 1)
    {
        bool success = V3RoadLoadPipeline.TryLoadIntoController(
            slotId,
            _root,
            _capacity,
            _budget,
            Controller,
            newLineageID);
        if (success)
            CurrentSlotID = slotId;
        return success;
    }

    public bool TryUndo(GraphStateToken expectedToken, out RoadGraphV3ChangeSummary summary) =>
        Controller.TryUndo(expectedToken, out summary);

    public bool TryRedo(GraphStateToken expectedToken, out RoadGraphV3ChangeSummary summary) =>
        Controller.TryRedo(expectedToken, out summary);

    public bool TryUndo(out RoadGraphV3ChangeSummary summary) => Controller.TryUndo(out summary);

    public bool TryRedo(out RoadGraphV3ChangeSummary summary) => Controller.TryRedo(out summary);

    public bool TryAddNode(Vector2 position, out RoadGraphV3ChangeSummary summary) =>
        Controller.TryAddNode(position, out summary);

    public bool TryAddEdge(
        int nodeAID,
        int nodeBID,
        IReadOnlyList<RoadGeometrySegment> geometry,
        RoadType roadType,
        out RoadGraphV3ChangeSummary summary) =>
        Controller.TryAddEdge(nodeAID, nodeBID, geometry, roadType, out summary);

    public bool TryBuild(RoadPlacementSessionV3 session, out RoadGraphV3ChangeSummary summary) =>
        TryBuild(session, 0f, out summary);

    public bool TryBuildFromPolyline(
        IReadOnlyList<Vector2> points,
        RoadType roadType,
        out RoadGraphV3ChangeSummary summary) =>
        TryBuildFromPolyline(points, roadType, 0f, out summary);

    public bool TryBuildFromPolyline(
        IReadOnlyList<Vector2> points,
        RoadType roadType,
        float snapRadius,
        out RoadGraphV3ChangeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            summary = null!;
            return false;
        }

        var session = new RoadPlacementSessionV3(roadType, points[0]);
        for (int index = 1; index < points.Count; index++)
        {
            if (!session.TryAddPoint(points[index]))
            {
                summary = null!;
                return false;
            }
        }

        return TryBuild(session, snapRadius, out summary);
    }

    public bool TryBuild(RoadPlacementSessionV3 session, float snapRadius, out RoadGraphV3ChangeSummary summary) =>
        new RoadToolCommandExecutor(Controller).TryBuild(session, snapRadius, out summary);

    public RoadSurfaceSnapshotBuildResult BuildSurfaceSnapshot(RoadStyleProvider styles) =>
        RoadSurfaceSnapshotBuilder.Build(
            Controller.Facade.Revision,
            Controller.Facade.CurrentToken,
            styles);

    public RoadSurfaceSnapshotBuildResult BuildDefaultSurfaceSnapshot() =>
        BuildSurfaceSnapshot(DefaultStyles);

    public bool TryRequestPresentation(RoadRenderToken desiredToken) =>
        Presentation.TryRequest(
            Controller.Facade.Revision,
            Controller.Facade.CurrentToken,
            desiredToken);

    public bool TryUpgrade(RoadUpgradeSessionV3 session, out IReadOnlyList<int> changedEdgeIDs) =>
        new RoadToolCommandExecutor(Controller).TryUpgrade(session, out changedEdgeIDs);

    public bool TryUpgradeEdges(
        IReadOnlyList<int> edgeIDs,
        RoadType targetType,
        out RoadGraphV3ChangeSummary summary) =>
        Controller.TryUpgradeSelection(edgeIDs, targetType, out summary);

    public bool TryChangeRoadType(
        int edgeID,
        RoadType targetType,
        out RoadGraphV3ChangeSummary summary) =>
        Controller.TryChangeRoadType(edgeID, targetType, out summary);

    public bool TryRemove(RoadRemovalSessionV3 session, out IReadOnlyList<int> removedEdgeIDs) =>
        new RoadToolCommandExecutor(Controller).TryRemove(session, out removedEdgeIDs);

    public bool TryRemoveEdges(
        IReadOnlyList<int> edgeIDs,
        out RoadGraphV3ChangeSummary summary) =>
        Controller.TryRemoveSelection(edgeIDs, out summary);

    public bool TryRemoveEdge(int edgeID, out RoadGraphV3ChangeSummary summary) =>
        Controller.TryRemoveEdge(edgeID, out summary);

    public V3AutosaveDecision TryAutosave(
        string slotId,
        RoadGraphV3Revision revision,
        bool hasNewerSuccess,
        out bool saved) =>
        _autosave.TryAutosave(slotId, _root, revision, hasNewerSuccess, out saved);

    public V3AutosaveDecision TryAutosaveCurrent(
        RoadGraphV3Revision revision,
        bool hasNewerSuccess,
        out bool saved)
    {
        saved = false;
        if (string.IsNullOrEmpty(CurrentSlotID))
            return V3AutosaveDecision.SkipBusy;

        return TryAutosave(CurrentSlotID, revision, hasNewerSuccess, out saved);
    }

    public V3SlotSummary GetStatus(string slotId) => _transactionCoordinator.GetStatus(slotId, _root);

    public V3Manifest? GetManifest(string slotId) => V3SlotManifestService.GetManifest(slotId, _root);

    public V3Manifest? CurrentSlotManifest =>
        string.IsNullOrEmpty(CurrentSlotID) ? null : GetManifest(CurrentSlotID);

    public string? CurrentSlotFormatFamily => CurrentSlotManifest?.FormatFamily;
    public int CurrentSlotSchemaVersion => CurrentSlotManifest?.SchemaVersion ?? 0;

    public string? CurrentSlotCityName => CurrentSlotManifest?.CityName;
    public long? CurrentSlotPopulation => CurrentSlotManifest?.Population;
    public decimal? CurrentSlotFunds => CurrentSlotManifest?.Funds;
    public string? CurrentSlotThumbnailFile => CurrentSlotManifest?.ThumbnailFile;
    public bool CurrentSlotHasThumbnail => CurrentSlotThumbnailFile is not null;
    public int CurrentSlotFileCount => CurrentSlotManifest?.Files.Count ?? 0;
    public bool CurrentSlotHasFiles => CurrentSlotFileCount > 0;

    public IReadOnlyList<V3ManifestFile> CurrentSlotFiles =>
        CurrentSlotManifest?.Files ?? [];

    public IReadOnlyList<string> CurrentSlotFileNames =>
        CurrentSlotManifest?.Files.Select(file => file.Name).ToList() ?? [];

    public long CurrentSlotTotalBytes =>
        CurrentSlotManifest?.Files.Sum(file => file.EncodedLength) ?? 0;

    public byte[]? GetPayload(string slotId, string fileName) =>
        V3SlotPayloadService.GetPayload(slotId, _root, fileName);

    public byte[]? CurrentSlotPayload(string fileName) =>
        string.IsNullOrEmpty(CurrentSlotID) ? null : GetPayload(CurrentSlotID, fileName);

    public bool Delete(string slotId) => _transactionCoordinator.Delete(slotId, _root).Success;

    public bool DeleteCurrentSlot()
    {
        if (string.IsNullOrEmpty(CurrentSlotID))
            return false;

        if (!Delete(CurrentSlotID))
            return false;

        CurrentSlotID = string.Empty;
        return true;
    }

    public IReadOnlyList<V3SlotSummary> List() => _transactionCoordinator.List(_root);

    public IReadOnlyList<V3SlotSummary> ListUsableSlots() =>
        List().Where(summary => summary.IsUsable).ToList();

    public void NewCity(long lineageID = 1)
    {
        Controller = new RoadGraphV3Controller(
            new RoadGraphV3Facade(RoadGraphV3Revision.Empty(_capacity), lineageID),
            new RoadEditHistoryV3(100, 100000));
        CurrentSlotID = string.Empty;
    }
}
