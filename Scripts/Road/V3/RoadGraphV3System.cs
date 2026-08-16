using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;

/// <summary>
/// V3 道路系统根节点：在真实 Godot 场景中持有 RoadGraphV3Application。
/// </summary>
public partial class RoadGraphV3System : Node2D
{
    [Export] public RoadConfigV3 Config { get; set; } = null!;

    private RoadGraphV3Renderer? _renderer;
    private RoadGraphV3InputHandler? _inputHandler;

    public static RoadGraphV3System Instance { get; private set; } = null!;
    public RoadGraphV3Application Application { get; private set; } = null!;
    public RoadGraphV3Renderer? Renderer => _renderer;
    public RoadGraphV3InputHandler? InputHandler => _inputHandler;
    public RoadSurfaceHitProvider HitProvider => Application.HitProvider;
    public RoadGraphV3Controller Controller => Application.Controller;
    public RoadGraphV3Revision Revision => Application.Revision;
    public int CurrentNodeCount => Application.CurrentNodeCount;
    public int CurrentEdgeCount => Application.CurrentEdgeCount;
    public bool CurrentGraphIsEmpty => Application.CurrentGraphIsEmpty;
    public int CurrentGeometrySegmentCount => Application.CurrentGeometrySegmentCount;
    public int CurrentSelfLoopCount => Application.CurrentSelfLoopCount;
    public System.Collections.Generic.IReadOnlyDictionary<RoadType, int> CurrentRoadTypeCounts => Application.CurrentRoadTypeCounts;
    public bool CanUndo => Application.CanUndo;
    public bool CanRedo => Application.CanRedo;
    public void ClearHistory() => Application.ClearHistory();
    public string CurrentSlotID => Application.CurrentSlotID;
    public bool HasCurrentSlot => Application.HasCurrentSlot;
    public SimpleCities.Core.V3.V3SlotSummary? CurrentSlotSummary => Application.CurrentSlotSummary;
    public string? CurrentSlotDisplayName => Application.CurrentSlotDisplayName;
    public string? CurrentSlotTimestamp => Application.CurrentSlotTimestamp;
    public SimpleCities.Core.V3.V3SlotOccupant? CurrentSlotOccupant => Application.CurrentSlotOccupant;
    public bool CurrentSlotIsComplete => Application.CurrentSlotIsComplete;
    public bool CurrentSlotIsUsable => Application.CurrentSlotIsUsable;
    public bool CurrentSlotIsCorrupt => Application.CurrentSlotIsCorrupt;
    public bool CurrentSlotIsAbsent => Application.CurrentSlotIsAbsent;
    public bool CurrentSlotIsUnsafe => Application.CurrentSlotIsUnsafe;
    public bool CurrentSlotIsForeign => Application.CurrentSlotIsForeign;
    public bool CurrentSlotIsOperable => Application.CurrentSlotIsOperable;
    public SimpleCities.Core.V3.V3Manifest? CurrentSlotManifest => Application.CurrentSlotManifest;
    public bool CurrentSlotManifestIsValid => Application.CurrentSlotManifestIsValid;
    public string? CurrentSlotFormatFamily => Application.CurrentSlotFormatFamily;
    public int CurrentSlotSchemaVersion => Application.CurrentSlotSchemaVersion;
    public string? CurrentSlotCityName => Application.CurrentSlotCityName;
    public long? CurrentSlotPopulation => Application.CurrentSlotPopulation;
    public decimal? CurrentSlotFunds => Application.CurrentSlotFunds;
    public string? CurrentSlotThumbnailFile => Application.CurrentSlotThumbnailFile;
    public bool CurrentSlotHasThumbnail => Application.CurrentSlotHasThumbnail;
    public byte[]? CurrentSlotThumbnailBytes => Application.CurrentSlotThumbnailBytes;
    public string? CurrentSlotThumbnailHash => Application.CurrentSlotThumbnailHash;
    public int CurrentSlotFileCount => Application.CurrentSlotFileCount;
    public bool CurrentSlotHasFiles => Application.CurrentSlotHasFiles;
    public System.Collections.Generic.IReadOnlyList<SimpleCities.Core.V3.V3ManifestFile> CurrentSlotFiles => Application.CurrentSlotFiles;
    public SimpleCities.Core.V3.V3ManifestFile? GetCurrentSlotFile(string fileName) =>
        Application.GetCurrentSlotFile(fileName);

    public long? GetCurrentSlotFileSize(string fileName) =>
        Application.GetCurrentSlotFileSize(fileName);
    public bool CurrentSlotHasRoadNetwork => Application.CurrentSlotHasRoadNetwork;
    public SimpleCities.Core.V3.V3ManifestFile? CurrentSlotRoadNetworkFile => Application.CurrentSlotRoadNetworkFile;
    public string? CurrentSlotRoadNetworkHash => Application.CurrentSlotRoadNetworkHash;
    public string? CurrentSlotRoadNetworkJson => Application.CurrentSlotRoadNetworkJson;
    public int CurrentSlotRoadNetworkJsonLength => Application.CurrentSlotRoadNetworkJsonLength;
    public byte[]? CurrentSlotRoadNetworkPayload => Application.CurrentSlotRoadNetworkPayload;
    public int CurrentSlotRoadNetworkPayloadLength => Application.CurrentSlotRoadNetworkPayloadLength;
    public bool CurrentSlotHasRoadNetworkPayload => Application.CurrentSlotHasRoadNetworkPayload;
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

    public bool TryUpgrade(RoadUpgradeSessionV3 session, out System.Collections.Generic.IReadOnlyList<int> changedEdgeIDs) =>
        Application.TryUpgrade(session, out changedEdgeIDs);

    public bool TryRemove(RoadRemovalSessionV3 session, out System.Collections.Generic.IReadOnlyList<int> removedEdgeIDs) =>
        Application.TryRemove(session, out removedEdgeIDs);

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

    public bool TryRequestPresentation(RoadRenderToken desiredToken) =>
        Application.TryRequestPresentation(desiredToken);

    public bool TryFindClosestSurfaceHit(
        Vector2 point,
        float maxDistance,
        out RoadSurfaceHit hit) =>
        Application.TryFindClosestSurfaceHit(point, maxDistance, out hit);

    public bool TryFindClosestJunctionSurfaceHit(
        Vector2 point,
        float maxDistance,
        out RoadSurfaceHit hit) =>
        Application.TryFindClosestJunctionSurfaceHit(point, maxDistance, out hit);

    public RoadSurfaceSnapshotBuildResult BuildDefaultSurfaceSnapshot() =>
        Application.BuildDefaultSurfaceSnapshot();

    public bool TryApplyPresentationFullReset(RoadPresentationFullReset plan) =>
        Application.Presentation.TryApplyFullReset(plan);

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

    public bool TryPrepareLoad(
        string slotId,
        long lineageID,
        out SimpleCities.Core.V3.V3RoadLoadPrepareResult? prepare) =>
        Application.TryPrepareLoad(slotId, lineageID, out prepare);

    public bool TryCommitPreparedLoad(
        SimpleCities.Core.V3.V3RoadLoadPrepareResult prepare,
        long lineageID,
        out SimpleCities.Core.V3.V3RoadLoadPipelineResult result)
    {
        if (!Application.TryCommitPreparedLoad(prepare, lineageID, out result))
            return false;
        ApplyCurrentPresentation();
        _inputHandler?.ResetTools();
        return true;
    }

    public bool Load(string slotId, long lineageID = 1)
    {
        if (!Application.Load(slotId, lineageID))
            return false;
        ApplyCurrentPresentation();
        _inputHandler?.ResetTools();
        return true;
    }

    public bool LoadIntoCurrent(string slotId, long newLineageID = 1)
    {
        if (!Application.LoadIntoCurrent(slotId, newLineageID))
            return false;
        ApplyCurrentPresentation();
        _inputHandler?.ResetTools();
        return true;
    }

    private void ApplyCurrentPresentation()
    {
        RoadGraphV3Revision revision = Application.Revision;
        var plan = Application.BuildPresentationFullReset(
            new RoadRenderToken(0, Application.Controller.Facade.LineageID, 0, 0, 0, 0));
        if (plan is not null)
            ApplyPresentationFullReset(plan);
    }

    public void RefreshPresentation() => ApplyCurrentPresentation();

    public void ClearPresentation() => _renderer?.ResetMesh();

    public void NewCity(long lineageID = 1)
    {
        Application.NewCity(lineageID);
        ApplyCurrentPresentation();
        _inputHandler?.ResetTools();
    }

    public System.Collections.Generic.IReadOnlyList<SimpleCities.Core.V3.V3SlotSummary> List() =>
        Application.List();

    public bool DeleteCurrentSlot() =>
        Application.DeleteCurrentSlot();

    public SimpleCities.Core.V3.V3Manifest? GetManifest(string slotId) =>
        Application.GetManifest(slotId);

    public byte[]? GetPayload(string slotId, string fileName) =>
        Application.GetPayload(slotId, fileName);

    public string? GetSlotFileHash(string slotId, string fileName) =>
        Application.GetSlotFileHash(slotId, fileName);

    public long? GetSlotFileSize(string slotId, string fileName) =>
        Application.GetSlotFileSize(slotId, fileName);

    public System.Collections.Generic.IReadOnlyList<string> GetSlotFileNames(string slotId) =>
        Application.GetSlotFileNames(slotId);

    public SimpleCities.Core.V3.V3ManifestFile? GetSlotFile(string slotId, string fileName) =>
        Application.GetSlotFile(slotId, fileName);

    public string? GetSlotPayloadString(string slotId, string fileName) =>
        Application.GetSlotPayloadString(slotId, fileName);

    public int GetSlotFileCount(string slotId) =>
        Application.GetSlotFileCount(slotId);

    public byte[]? CurrentSlotPayload(string fileName) =>
        Application.CurrentSlotPayload(fileName);

    public override void _Ready()
    {
        Instance = this;
        string root = ProjectSettings.GlobalizePath(V3SaveRoot.EditorRoot);
        Application = Config is not null
            ? new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default, Config)
            : new RoadGraphV3Application(root, RoadGraphCapacity.Default, V3PayloadBudget.Default);
        _renderer = GetNodeOrNull<RoadGraphV3Renderer>("RoadGraphV3Renderer");
        _inputHandler = GetNodeOrNull<RoadGraphV3InputHandler>("RoadGraphV3InputHandler");
    }

    public void ApplyPresentationFullReset(RoadPresentationFullReset plan) =>
        _renderer?.ApplyPresentationFullReset(plan);

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null!;
    }
}
