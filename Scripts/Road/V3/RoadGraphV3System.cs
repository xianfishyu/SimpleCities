using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;

/// <summary>
/// V3 道路系统根节点：在真实 Godot 场景中持有 RoadGraphV3Application。
/// </summary>
public partial class RoadGraphV3System : Node2D
{
    [Export] public RoadConfigV3 Config { get; set; } = null!;

    public static RoadGraphV3System Instance { get; private set; } = null!;
    public RoadGraphV3Application Application { get; private set; } = null!;
    public RoadGraphV3Controller Controller => Application.Controller;
    public RoadGraphV3Revision Revision => Application.Revision;
    public bool CanUndo => Application.CanUndo;
    public bool CanRedo => Application.CanRedo;
    public void ClearHistory() => Application.ClearHistory();
    public string CurrentSlotID => Application.CurrentSlotID;
    public SimpleCities.Core.V3.V3SlotSummary? CurrentSlotSummary => Application.CurrentSlotSummary;
    public string? CurrentSlotDisplayName => Application.CurrentSlotDisplayName;
    public string? CurrentSlotTimestamp => Application.CurrentSlotTimestamp;
    public SimpleCities.Core.V3.V3SlotOccupant? CurrentSlotOccupant => Application.CurrentSlotOccupant;
    public bool CurrentSlotIsComplete => Application.CurrentSlotIsComplete;
    public bool CurrentSlotIsCorrupt => Application.CurrentSlotIsCorrupt;
    public bool CurrentSlotIsAbsent => Application.CurrentSlotIsAbsent;
    public bool CurrentSlotIsUnsafe => Application.CurrentSlotIsUnsafe;
    public bool CurrentSlotIsForeign => Application.CurrentSlotIsForeign;
    public bool CurrentSlotIsOperable => Application.CurrentSlotIsOperable;
    public SimpleCities.Core.V3.V3Manifest? CurrentSlotManifest => Application.CurrentSlotManifest;
    public string? CurrentSlotCityName => Application.CurrentSlotCityName;
    public long? CurrentSlotPopulation => Application.CurrentSlotPopulation;
    public decimal? CurrentSlotFunds => Application.CurrentSlotFunds;
    public string? CurrentSlotThumbnailFile => Application.CurrentSlotThumbnailFile;
    public bool CurrentSlotHasThumbnail => Application.CurrentSlotHasThumbnail;
    public int CurrentSlotFileCount => Application.CurrentSlotFileCount;
    public bool CurrentSlotHasFiles => Application.CurrentSlotHasFiles;
    public System.Collections.Generic.IReadOnlyList<string> CurrentSlotFileNames => Application.CurrentSlotFileNames;
    public long CurrentSlotTotalBytes => Application.CurrentSlotTotalBytes;

    public System.Collections.Generic.IReadOnlyList<SimpleCities.Core.V3.V3SlotSummary> ListUsableSlots() =>
        Application.ListUsableSlots();
    public RoadToolState ToolState => Application.ToolState;

    public bool TryBuildFromPolyline(
        System.Collections.Generic.IReadOnlyList<Vector2> points,
        RoadType roadType,
        out RoadGraphV3ChangeSummary summary) =>
        Application.TryBuildFromPolyline(points, roadType, out summary);

    public bool TryBuildFromPolyline(
        System.Collections.Generic.IReadOnlyList<Vector2> points,
        RoadType roadType,
        float snapRadius,
        out RoadGraphV3ChangeSummary summary) =>
        Application.TryBuildFromPolyline(points, roadType, snapRadius, out summary);

    public bool TryBuild(RoadPlacementSessionV3 session, out RoadGraphV3ChangeSummary summary) =>
        Application.TryBuild(session, out summary);

    public bool TryAddNode(Vector2 position, out RoadGraphV3ChangeSummary summary) =>
        Application.TryAddNode(position, out summary);

    public bool TryAddEdge(
        int nodeAID,
        int nodeBID,
        System.Collections.Generic.IReadOnlyList<RoadGeometrySegment> geometry,
        RoadType roadType,
        out RoadGraphV3ChangeSummary summary) =>
        Application.TryAddEdge(nodeAID, nodeBID, geometry, roadType, out summary);

    public bool TryUndo(out RoadGraphV3ChangeSummary summary) =>
        Application.TryUndo(out summary);

    public SimpleCities.Core.V3.V3AutosaveDecision TryAutosaveCurrent(
        RoadGraphV3Revision revision,
        bool hasNewerSuccess,
        out bool saved) =>
        Application.TryAutosaveCurrent(revision, hasNewerSuccess, out saved);

    public bool TryRedo(out RoadGraphV3ChangeSummary summary) =>
        Application.TryRedo(out summary);

    public bool SaveCurrent(
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile) =>
        Application.SaveCurrent(displayName, cityName, timestamp, population, funds, thumbnailFile);

    public bool SaveAs(
        string slotId,
        string displayName,
        string cityName,
        string timestamp,
        long? population,
        decimal? funds,
        string? thumbnailFile) =>
        Application.SaveAs(slotId, displayName, cityName, timestamp, population, funds, thumbnailFile);

    public bool TryUpgradeEdges(
        System.Collections.Generic.IReadOnlyList<int> edgeIDs,
        RoadType targetType,
        out RoadGraphV3ChangeSummary summary) =>
        Application.TryUpgradeEdges(edgeIDs, targetType, out summary);

    public bool TryChangeRoadType(
        int edgeID,
        RoadType targetType,
        out RoadGraphV3ChangeSummary summary) =>
        Application.TryChangeRoadType(edgeID, targetType, out summary);

    public bool TryRemoveEdges(
        System.Collections.Generic.IReadOnlyList<int> edgeIDs,
        out RoadGraphV3ChangeSummary summary) =>
        Application.TryRemoveEdges(edgeIDs, out summary);

    public bool TryRemoveEdge(int edgeID, out RoadGraphV3ChangeSummary summary) =>
        Application.TryRemoveEdge(edgeID, out summary);

    public bool Load(string slotId, long lineageID = 1) =>
        Application.Load(slotId, lineageID);

    public bool DeleteCurrentSlot() =>
        Application.DeleteCurrentSlot();

    public SimpleCities.Core.V3.V3Manifest? GetManifest(string slotId) =>
        Application.GetManifest(slotId);

    public byte[]? GetPayload(string slotId, string fileName) =>
        Application.GetPayload(slotId, fileName);

    public byte[]? CurrentSlotPayload(string fileName) =>
        Application.CurrentSlotPayload(fileName);

    public override void _Ready()
    {
        Instance = this;
        string root = ProjectSettings.GlobalizePath(V3SaveRoot.EditorRoot);
        Application = Config is not null
            ? new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default, Config)
            : new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null!;
    }
}
