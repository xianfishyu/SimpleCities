using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public partial class RoadRenderer : Node2D, IRoadSurfaceSelectionProvider
{
    [Export] public RoadConfig Config { get; set; } = null!;

    private RoadGraph? _network;

    // Edge.ID → 确定显示点列；静态道路和动态高亮共用。
    private Dictionary<int, Vector2[]> _edgePoints = new();
    private Dictionary<int, RoadGeometryDisplaySpan[]> _edgeDisplaySpans = new();
    private readonly HashSet<int> _invalidatedDisplayEdgeIDs = [];
    private bool _rebuildAllDisplayPaths = true;

    private MeshInstance2D _roadBatchLayer = null!;
    private MultiMeshInstance2D _nodeBatchLayer = null!;
    private int _roadMeshVertexCount;
    private bool _staticBatchRebuildScheduled;
    private long _staticBatchRebuildContinuationGeneration;
    private bool _graphEventsSubscribed;
    private RoadRendererLoadAdmission? _loadAdmission;
    private long _loadAdmissionGeneration;
    private GraphStateToken? _committedLoadGraphToken;
    private readonly RoadPresentationTokenTracker _presentationTokens = new();
    private RoadSurfaceSnapshot? _presentedSurface;
    private RoadPresentationPerformanceRequest? _pendingPresentationPerformanceRequest;
    private RoadPresentationPerformanceMetrics? _lastPresentationPerformanceMetrics;

    internal event Action<RoadRenderToken>? PresentationReady;
    internal event Action<RoadPresentationFailure>? PresentationStalled;

    partial void ProbeOrdinaryPrepareFailure(
        RoadRenderToken targetToken);
    partial void ProbeOrdinaryPresentationResourcePreflightFailure(
        RoadRenderToken targetToken);
    partial void ProbeOrdinaryRoadMeshFactoryFailure(
        RoadRenderToken targetToken,
        ref IReadOnlyCollection<int> roadIndices);
    partial void ProbeOrdinaryNodeBatchFactoryFailure(
        RoadRenderToken targetToken,
        ref IReadOnlyList<RoadRendererNodeMarker> nodeMarkers);
    partial void ProbeOrdinaryRoadSurfaceSnapshotFailure(
        RoadRenderToken targetToken,
        ref RoadSurfaceSnapshot.PreparedData roadSurface);
    partial void ProbeOrdinaryPreCommitTokenSupersession(
        RoadRenderToken targetToken);

    // 施工预览
    private Vector2[] _previewPoints = [];
    public Vector2[] PreviewPoints
    {
        get => (Vector2[])_previewPoints.Clone();
        set => _previewPoints = value == null ? [] : (Vector2[])value.Clone();
    }

    public int GetPreviewPointCount() => _previewPoints.Length;

    public Vector2 GetPreviewPoint(int index) => _previewPoints[index];

    private int[] _removalPreviewEdgeIDs = [];
    public int[] RemovalPreviewEdgeIDs
    {
        get => (int[])_removalPreviewEdgeIDs.Clone();
        set => _removalPreviewEdgeIDs = value == null ? [] : value.Distinct().Order().ToArray();
    }

    public Rect2? RemovalSelectionBounds { get; set; }

    public bool HasRemovalSelectionBounds() => RemovalSelectionBounds.HasValue;

    public Rect2 GetRemovalSelectionBounds() => RemovalSelectionBounds ?? default;

    public int GetRemovalPreviewEdgeCount() => _removalPreviewEdgeIDs.Length;

    private int[] _upgradePreviewEdgeIDs = [];
    public int[] UpgradePreviewEdgeIDs
    {
        get => (int[])_upgradePreviewEdgeIDs.Clone();
        set => _upgradePreviewEdgeIDs = value == null ? [] : value.Distinct().Order().ToArray();
    }

    public Rect2? UpgradeSelectionBounds { get; set; }

    public bool HasUpgradeSelectionBounds() => UpgradeSelectionBounds.HasValue;

    public Rect2 GetUpgradeSelectionBounds() => UpgradeSelectionBounds ?? default;

    public int GetUpgradePreviewEdgeCount() => _upgradePreviewEdgeIDs.Length;

    public int GetRenderedEdgeCount() => _edgePoints.Count;

    public int GetRenderedPointCount(int edgeID) => _edgePoints[edgeID].Length;

    public Vector2 GetRenderedPoint(int edgeID, int pointIndex) => _edgePoints[edgeID][pointIndex];

    public int GetStaticRenderNodeCount() => 2;

    public int GetRoadMeshVertexCount() => _roadMeshVertexCount;

    public int GetNodeMarkerCount() => _nodeBatchLayer.Multimesh.InstanceCount;

    public Godot.Collections.Dictionary GetPresentationState()
    {
        bool isReady = IsPresentationReady();
        RoadPresentationFailure? failure = _presentationTokens.CurrentFailure;
        return new Godot.Collections.Dictionary
        {
            ["phase"] = GetPresentationPhase(isReady),
            ["isReady"] = isReady,
            ["isStalled"] = _presentationTokens.IsPresentationStalled,
            ["attemptCount"] = _presentationTokens.AttemptCount,
            ["desired"] = ToTokenDictionary(_presentationTokens.DesiredToken),
            ["presented"] = ToTokenDictionary(_presentationTokens.PresentedToken),
            ["stalledToken"] = ToTokenDictionary(failure?.RenderToken),
            ["failureType"] = failure?.ExceptionType ?? string.Empty,
            ["failureMessage"] = failure?.Message ?? string.Empty,
            ["surfacePrimitiveCount"] = isReady
                ? _presentedSurface!.PrimitiveCount
                : 0,
            ["retainedSurfacePrimitiveCount"] = _presentedSurface?.PrimitiveCount ?? 0,
        };
    }

    public Godot.Collections.Dictionary GetLastPresentationPerformanceMetrics()
    {
        if (_lastPresentationPerformanceMetrics is not RoadPresentationPerformanceMetrics metrics)
            return new Godot.Collections.Dictionary();

        return new Godot.Collections.Dictionary
        {
            ["snapshotCaptureMs"] = metrics.SnapshotCaptureDuration.TotalMilliseconds,
            ["prepareMs"] = metrics.PrepareDuration.TotalMilliseconds,
            ["resourcePreflightMs"] = metrics.ResourcePreflightDuration.TotalMilliseconds,
            ["presentationCommitMs"] = metrics.PresentationCommitDuration.TotalMilliseconds,
            ["rebuildTotalMs"] = metrics.RebuildTotalDuration.TotalMilliseconds,
            ["requestToReadyMs"] = metrics.RequestToReadyDuration.TotalMilliseconds,
            ["attemptNumber"] = metrics.AttemptNumber,
            ["isFullReset"] = metrics.IsFullReset,
            ["renderToken"] = ToTokenDictionary(metrics.RenderToken),
        };
    }

    public Godot.Collections.Dictionary FindRoadSurfaceHit(
        Vector2 position,
        float maxSurfaceDistance)
    {
        RoadSurfaceHit? nullableHit = FindPresentedRoadSurfaceHit(
            position,
            maxSurfaceDistance);
        if (nullableHit is not RoadSurfaceHit hit)
            return new Godot.Collections.Dictionary();

        var result = new Godot.Collections.Dictionary
        {
            ["renderToken"] = ToTokenDictionary(hit.RenderToken),
            ["ownerKind"] = hit.OwnerKind.ToString(),
            ["edgeID"] = hit.EdgeID ?? -1,
            ["nodeID"] = hit.NodeID ?? -1,
            ["endpoint"] = hit.Endpoint?.ToString() ?? string.Empty,
            ["surfaceDistance"] = hit.SurfaceDistance,
            ["centerlineDistance"] = hit.CenterlineDistance,
        };
        if (hit.Location is RoadLocation location)
        {
            result["location"] = new Godot.Collections.Dictionary
            {
                ["edgeID"] = location.EdgeID,
                ["geometryIndex"] = location.GeometryIndex,
                ["parameter"] = location.Parameter,
            };
        }
        return result;
    }

    public int[] FindRoadSurfaceEdgeIDs(Rect2 bounds) =>
        GetPresentedRoadSurface()?.FindEdgeIDsIntersecting(bounds) ?? [];

    internal RoadSurfaceHit? FindPresentedRoadSurfaceHit(
        Vector2 position,
        float maxSurfaceDistance) =>
        GetPresentedRoadSurface()?.FindClosest(position, maxSurfaceDistance);

    bool IRoadSurfaceSelectionProvider.TryCaptureCurrentToken(
        out RoadRenderToken renderToken)
    {
        if (IsPresentationReady() &&
            _presentationTokens.PresentedToken is RoadRenderToken presented)
        {
            renderToken = presented;
            return true;
        }

        renderToken = default;
        return false;
    }

    bool IRoadSurfaceSelectionProvider.IsCurrent(RoadRenderToken expectedToken) =>
        GetPresentedRoadSurface(expectedToken) is not null;

    bool IRoadSurfaceSelectionProvider.TryFindClosest(
        RoadRenderToken expectedToken,
        Vector2 position,
        float maxSurfaceDistance,
        out RoadSurfaceHit? hit)
    {
        RoadSurfaceSnapshot? surface = GetPresentedRoadSurface(expectedToken);
        if (surface is null)
        {
            hit = null;
            return false;
        }

        hit = surface.FindClosest(position, maxSurfaceDistance);
        return true;
    }

    bool IRoadSurfaceSelectionProvider.TryFindEdgeIDsIntersecting(
        RoadRenderToken expectedToken,
        Rect2 bounds,
        out int[] edgeIDs)
    {
        RoadSurfaceSnapshot? surface = GetPresentedRoadSurface(expectedToken);
        if (surface is null)
        {
            edgeIDs = [];
            return false;
        }

        edgeIDs = surface.FindEdgeIDsIntersecting(bounds);
        return true;
    }

    public bool RetryRoadPresentation()
    {
        if (_loadAdmission is not null ||
            !PresentationResourcesAreReady() ||
            !_presentationTokens.IsPresentationStalled ||
            _presentationTokens.DesiredToken is not RoadRenderToken targetToken)
        {
            return false;
        }

        return TryRebuildStaticBatches() &&
               _presentationTokens.PresentedToken == targetToken;
    }

    public bool RefreshRoadStyles()
    {
        if (_loadAdmission is not null || _network is null || Config is null)
            return false;

        _ = Config.CaptureRoadTypeStyleSnapshot();
        RoadRenderToken requested = _presentationTokens.RequestStyleRefresh(
            _network.CurrentStateToken.ChangeSequence);
        QueueRedraw();
        if (PresentationResourcesAreReady())
            _ = TryRebuildStaticBatches();
        return IsPresentationReady() && _presentationTokens.PresentedToken == requested;
    }

    internal void ConfigureSceneGeneration(long sceneGeneration)
    {
        if (_loadAdmission is not null)
            throw new InvalidOperationException(
                "RoadRenderer scene generation cannot change during load admission.");

        long changeSequence = _network?.CurrentStateToken.ChangeSequence ?? 0;
        if (!_presentationTokens.SetSceneGeneration(
                sceneGeneration,
                changeSequence,
                out _))
        {
            return;
        }

        QueueRedraw();
        if (PresentationResourcesAreReady())
            RebuildStaticBatches();
    }

    /// <summary>道路拆除或改造工具悬停的 Edge ID（null = 未命中）。</summary>
    public int? HoveredEdgeID { get; set; }

    public void SetHoveredEdgeID(int edgeID)
    {
        HoveredEdgeID = edgeID;
        QueueRedraw();
    }

    public void ClearHoveredEdgeID()
    {
        HoveredEdgeID = null;
        QueueRedraw();
    }

    public override void _Ready()
    {
        if (Config == null)
        {
            GD.PushError("RoadRenderer: Config (RoadConfig resource) is not assigned in the scene.");
            Config = new RoadConfig();
        }
        Config.NormalizeRuntimeValues(message => GD.PushWarning($"RoadRenderer: {message}"));
        if (!Config.TryValidateRoadTypeStyles(out string roadTypeStyleError))
            GD.PushError($"RoadRenderer: RoadTypeStyles resource is invalid: {roadTypeStyleError}");
        if (!float.IsFinite(Config.CurveDisplayTolerance) || Config.CurveDisplayTolerance <= 0f)
        {
            GD.PushError("RoadRenderer: CurveDisplayTolerance must be positive and finite; using the default.");
            Config.CurveDisplayTolerance = RoadGeometryDisplaySampler.DefaultTolerance;
        }

        _roadBatchLayer = new MeshInstance2D
        {
            // Parent _Draw() owns previews and highlights; the static mesh must stay beneath it.
            ZIndex = -1,
            Modulate = Colors.White,
            Material = CreateRoadMaterial(),
        };
        AddChild(_roadBatchLayer);

        _nodeBatchLayer = CreateBatchLayer(useColors: true, zIndex: 1);
        _nodeBatchLayer.Material = CreateCircleMaterial();
        AddChild(_nodeBatchLayer);

        if (_network is not null && _presentationTokens.DesiredToken.HasValue)
            RebuildStaticBatches();
    }

    public override void _EnterTree()
    {
        SubscribeGraphEvents();
    }

    public override void _ExitTree()
    {
        _loadAdmission?.Dispose();
        UnsubscribeGraphEvents();
        InvalidateScheduledStaticBatchRebuildContinuation();
    }

    public void SetGraph(RoadGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (_loadAdmission is not null)
            throw new InvalidOperationException("RoadRenderer graph cannot change during load admission.");
        bool facadeChanged = !ReferenceEquals(_network, graph);
        UnsubscribeGraphEvents();
        _network = graph;
        _committedLoadGraphToken = null;
        InvalidateScheduledStaticBatchRebuildContinuation();
        _invalidatedDisplayEdgeIDs.Clear();
        _rebuildAllDisplayPaths = true;
        if (facadeChanged)
        {
            _presentationTokens.BindGraph(
                graph.FacadeID,
                graph.CurrentStateToken.ChangeSequence);
        }
        else
        {
            _presentationTokens.RequestRebuild(graph.CurrentStateToken.ChangeSequence);
        }
        QueueRedraw();
        SubscribeGraphEvents();

        if (PresentationResourcesAreReady())
            RebuildStaticBatches();
    }

    private void SubscribeGraphEvents()
    {
        if (_network == null || _graphEventsSubscribed || !IsInsideTree())
            return;

        _network.GraphChanged += OnGraphChanged;
        _graphEventsSubscribed = true;
    }

    private void UnsubscribeGraphEvents()
    {
        if (_network == null || !_graphEventsSubscribed)
            return;

        _network.GraphChanged -= OnGraphChanged;
        _graphEventsSubscribed = false;
    }

    // ── 整网重载（存档加载后） ──

    private void OnGraphChanged(RoadGraphChangedEvent change)
    {
        if (_network == null)
            return;
        long requestStarted = Stopwatch.GetTimestamp();
        if (change.Changes.IsFullReset)
        {
            if (_committedLoadGraphToken is GraphStateToken committed &&
                committed == change.StateToken)
            {
                _committedLoadGraphToken = null;
                QueueRedraw();
                return;
            }
            InvalidateScheduledStaticBatchRebuildContinuation();
            _invalidatedDisplayEdgeIDs.Clear();
            _rebuildAllDisplayPaths = true;
            RoadRenderToken resetRequest = _presentationTokens.RequestGraphChange(
                change.StateToken.ChangeSequence,
                isFullReset: true);
            _pendingPresentationPerformanceRequest = new(
                resetRequest,
                requestStarted,
                IsFullReset: true);
            QueueRedraw();
            RebuildStaticBatches();
            return;
        }

        foreach (int edgeID in change.Changes.CreatedEdgeIDs
                     .Concat(change.Changes.UpdatedEdgeIDs))
        {
            _invalidatedDisplayEdgeIDs.Add(edgeID);
        }
        RoadRenderToken mutationRequest = _presentationTokens.RequestGraphChange(
            change.StateToken.ChangeSequence,
            isFullReset: false);
        _pendingPresentationPerformanceRequest = new(
            mutationRequest,
            requestStarted,
            IsFullReset: false);
        QueueRedraw();
        ScheduleStaticBatchRebuild();
    }

    // ── 静态道路和节点批处理 ──

    private void ScheduleStaticBatchRebuild()
    {
        if (_staticBatchRebuildScheduled)
            return;

        _staticBatchRebuildScheduled = true;
        long continuationGeneration = _staticBatchRebuildContinuationGeneration;
        Callable.From(() => FlushScheduledStaticBatchRebuild(continuationGeneration)).CallDeferred();
    }

    private void FlushScheduledStaticBatchRebuild() =>
        FlushScheduledStaticBatchRebuild(_staticBatchRebuildContinuationGeneration);

    private void FlushScheduledStaticBatchRebuild(long continuationGeneration)
    {
        if (!_staticBatchRebuildScheduled ||
            continuationGeneration != _staticBatchRebuildContinuationGeneration)
        {
            return;
        }

        InvalidateScheduledStaticBatchRebuildContinuation();
        if (IsInsideTree())
            RebuildStaticBatches();
    }

    private void InvalidateScheduledStaticBatchRebuildContinuation()
    {
        _staticBatchRebuildScheduled = false;
        _staticBatchRebuildContinuationGeneration =
            _staticBatchRebuildContinuationGeneration == long.MaxValue
                ? 1
                : _staticBatchRebuildContinuationGeneration + 1;
    }

    private void RebuildStaticBatches() => _ = TryRebuildStaticBatches();

    private bool TryRebuildStaticBatches()
    {
        if (_network is not RoadGraph graph ||
            _presentationTokens.DesiredToken is not RoadRenderToken targetToken ||
            !PresentationResourcesAreReady())
        {
            return false;
        }

        long rebuildStarted = Stopwatch.GetTimestamp();
        int attemptNumber = _presentationTokens.BeginBuildAttempt(targetToken);
        ArrayMesh? roadMesh = null;
        MultiMesh? nodeBatch = null;
        bool presentationResourcesTransferred = false;
        try
        {
            long snapshotCaptureStarted = Stopwatch.GetTimestamp();
            var settings = new RoadRendererLoadSettings(
                Config.CurveDisplayTolerance,
                Config.CaptureRoadTypeStyleSnapshot());
            settings.Validate();
            RoadGraphRevision revision = graph.CaptureRevision();
            TimeSpan snapshotCaptureDuration = Stopwatch.GetElapsedTime(snapshotCaptureStarted);

            long prepareStarted = Stopwatch.GetTimestamp();
            var preparer = new RoadRendererLoadPreparer(settings);
            ProbeOrdinaryPrepareFailure(targetToken);
            RoadRendererPreparedLoad prepared = _rebuildAllDisplayPaths
                ? preparer.Prepare(revision)
                : preparer.Prepare(
                    revision,
                    _edgePoints,
                    _edgeDisplaySpans,
                    _invalidatedDisplayEdgeIDs);
            TimeSpan prepareDuration = Stopwatch.GetElapsedTime(prepareStarted);

            long resourcePreflightStarted = Stopwatch.GetTimestamp();
            IReadOnlyCollection<int> roadIndices = prepared.RoadIndices;
            ProbeOrdinaryRoadMeshFactoryFailure(targetToken, ref roadIndices);
            roadMesh = CreateRoadMesh(
                prepared.RoadVertices,
                prepared.RoadUvs,
                prepared.RoadColors,
                roadIndices);
            IReadOnlyList<RoadRendererNodeMarker> nodeMarkers = prepared.NodeMarkers;
            ProbeOrdinaryNodeBatchFactoryFailure(targetToken, ref nodeMarkers);
            nodeBatch = CreateNodeBatch(nodeMarkers);
            ProbeOrdinaryPresentationResourcePreflightFailure(targetToken);
            RoadSurfaceSnapshot.PreparedData roadSurface = prepared.RoadSurface;
            ProbeOrdinaryRoadSurfaceSnapshotFailure(targetToken, ref roadSurface);
            var surfaceSnapshot = new RoadSurfaceSnapshot(
                targetToken,
                roadSurface);
            ProbeOrdinaryPreCommitTokenSupersession(targetToken);
            TimeSpan resourcePreflightDuration = Stopwatch.GetElapsedTime(
                resourcePreflightStarted);

            if (!ReferenceEquals(_network, graph) ||
                _presentationTokens.DesiredToken != targetToken ||
                graph.CurrentStateToken != revision.StateToken)
            {
                return false;
            }

            long presentationCommitStarted = Stopwatch.GetTimestamp();
            _edgePoints = prepared.EdgePoints;
            _edgeDisplaySpans = prepared.EdgeDisplaySpans;
            _invalidatedDisplayEdgeIDs.Clear();
            _rebuildAllDisplayPaths = false;
            _roadMeshVertexCount = prepared.RoadVertices.Length;
            _roadBatchLayer.Mesh = roadMesh;
            _nodeBatchLayer.Multimesh = nodeBatch;
            presentationResourcesTransferred = true;
            _presentedSurface = surfaceSnapshot;
            _presentationTokens.CommitDesired(targetToken);
            TimeSpan presentationCommitDuration = Stopwatch.GetElapsedTime(
                presentationCommitStarted);
            if (_pendingPresentationPerformanceRequest is
                    RoadPresentationPerformanceRequest request &&
                request.RenderToken == targetToken)
            {
                _lastPresentationPerformanceMetrics = new(
                    targetToken,
                    snapshotCaptureDuration,
                    prepareDuration,
                    resourcePreflightDuration,
                    presentationCommitDuration,
                    Stopwatch.GetElapsedTime(rebuildStarted),
                    Stopwatch.GetElapsedTime(request.StartTimestamp),
                    attemptNumber,
                    request.IsFullReset);
            }
            QueueRedraw();
            return true;
        }
        catch (Exception exception)
        {
            RoadPresentationFailure? failure = _presentationTokens.ReportBuildFailure(
                targetToken,
                attemptNumber,
                exception);
            if (failure is RoadPresentationFailure currentFailure)
                PublishPresentationStalled(currentFailure);
            return false;
        }
        finally
        {
            if (!presentationResourcesTransferred)
                DisposePreparedPresentationResources(roadMesh, nodeBatch);
        }
    }

    private void PublishPresentationStalled(RoadPresentationFailure failure)
    {
        Action<RoadPresentationFailure>? handlers = PresentationStalled;
        if (handlers is null)
            return;

        foreach (Action<RoadPresentationFailure> handler in handlers
                     .GetInvocationList()
                     .Cast<Action<RoadPresentationFailure>>())
        {
            try
            {
                handler(failure);
            }
            catch (Exception exception)
            {
                GD.PushWarning(
                    $"Road presentation stalled observer failed: {exception.Message}");
            }
        }
    }

    private bool PresentationResourcesAreReady() =>
        IsInsideTree() &&
        GodotObject.IsInstanceValid(_roadBatchLayer) &&
        GodotObject.IsInstanceValid(_nodeBatchLayer);

    private bool IsPresentationReady() =>
        PresentationResourcesAreReady() &&
        _presentationTokens.IsPresentationCurrent &&
        _presentationTokens.PresentedToken is RoadRenderToken presented &&
        _presentedSurface is RoadSurfaceSnapshot surface &&
        surface.RenderToken == presented;

    private RoadSurfaceSnapshot? GetPresentedRoadSurface() =>
        IsPresentationReady() ? _presentedSurface : null;

    private RoadSurfaceSnapshot? GetPresentedRoadSurface(
        RoadRenderToken expectedToken) =>
        IsPresentationReady() &&
        _presentationTokens.DesiredToken == expectedToken &&
        _presentationTokens.PresentedToken == expectedToken &&
        _presentedSurface?.RenderToken == expectedToken
            ? _presentedSurface
            : null;

    private string GetPresentationPhase(bool isReady)
    {
        if (isReady)
            return "ready";
        if (_presentationTokens.IsPresentationStalled)
            return "stalled";
        return _presentationTokens.DesiredToken.HasValue ? "pending" : "unbound";
    }

    private static Godot.Collections.Dictionary ToTokenDictionary(
        RoadRenderToken? nullableToken)
    {
        if (nullableToken is not RoadRenderToken token)
            return new Godot.Collections.Dictionary();

        return new Godot.Collections.Dictionary
        {
            ["sceneGeneration"] = token.SceneGeneration,
            ["graphFacadeID"] = token.GraphFacadeID,
            ["graphFacadeGeneration"] = token.GraphFacadeGeneration,
            ["changeSequence"] = token.ChangeSequence,
            ["roadStyleRevision"] = token.RoadStyleRevision,
            ["renderRequestID"] = token.RenderRequestID,
        };
    }

    private readonly record struct RoadPresentationPerformanceRequest(
        RoadRenderToken RenderToken,
        long StartTimestamp,
        bool IsFullReset);

    private sealed record RoadPresentationPerformanceMetrics(
        RoadRenderToken RenderToken,
        TimeSpan SnapshotCaptureDuration,
        TimeSpan PrepareDuration,
        TimeSpan ResourcePreflightDuration,
        TimeSpan PresentationCommitDuration,
        TimeSpan RebuildTotalDuration,
        TimeSpan RequestToReadyDuration,
        int AttemptNumber,
        bool IsFullReset);

    private static void AppendRoadRibbon(
        int edgeID,
        IReadOnlyList<Vector2> points,
        IReadOnlyList<RoadGeometryDisplaySpan> displaySpans,
        bool isClosed,
        float halfWidth,
        Color color,
        List<Vector2> vertices,
        List<Vector2> uvs,
        List<Color> colors,
        List<int> indices,
        List<RoadSurfaceTriangle> surfaceTriangles)
    {
        if (edgeID < 0)
            throw new ArgumentOutOfRangeException(nameof(edgeID));
        ArgumentNullException.ThrowIfNull(displaySpans);
        ArgumentNullException.ThrowIfNull(surfaceTriangles);
        if (displaySpans.Count != Math.Max(0, points.Count - 1))
        {
            throw new ArgumentException(
                "Road ribbon display spans must match every visible point interval.",
                nameof(displaySpans));
        }
        int pointCount = points.Count;
        if (isClosed)
        {
            if (pointCount < 2 || !RoadExactPredicates.SameBits(points[0], points[^1]))
                throw new InvalidOperationException("A closed road ribbon must repeat its seam point exactly.");
            pointCount--;
        }
        if (pointCount < 2)
            return;

        int vertexOffset = vertices.Count;
        for (int index = 0; index < pointCount; index++)
        {
            Vector2 offset = CalculateRoadOffset(points, pointCount, index, isClosed, halfWidth);
            vertices.Add(points[index] - offset);
            uvs.Add(Vector2.Zero);
            colors.Add(color);
            vertices.Add(points[index] + offset);
            uvs.Add(Vector2.Down);
            colors.Add(color);
        }

        int segmentCount = isClosed ? pointCount : pointCount - 1;
        for (int index = 0; index < segmentCount; index++)
        {
            int previous = vertexOffset + index * 2;
            int current = vertexOffset + ((index + 1) % pointCount) * 2;
            indices.Add(previous);
            indices.Add(previous + 1);
            indices.Add(current);
            indices.Add(current);
            indices.Add(previous + 1);
            indices.Add(current + 1);

            RoadSurfaceOwner owner = RoadSurfaceOwner.EdgeRibbon(edgeID);
            RoadGeometryDisplaySpan displaySpan = displaySpans[index];
            Vector2 centerlineStart = points[index];
            Vector2 centerlineEnd = points[(index + 1) % pointCount];
            var locationStart = new RoadLocation(
                edgeID,
                displaySpan.GeometryIndex,
                displaySpan.ParameterStart);
            var locationEnd = new RoadLocation(
                edgeID,
                displaySpan.GeometryIndex,
                displaySpan.ParameterEnd);
            bool ownsLocationEnd = !isClosed && index == segmentCount - 1;
            surfaceTriangles.Add(new RoadSurfaceTriangle(
                owner,
                vertices[previous],
                vertices[previous + 1],
                vertices[current],
                centerlineStart,
                centerlineEnd,
                locationStart,
                locationEnd,
                ownsLocationEnd));
            surfaceTriangles.Add(new RoadSurfaceTriangle(
                owner,
                vertices[current],
                vertices[previous + 1],
                vertices[current + 1],
                centerlineStart,
                centerlineEnd,
                locationStart,
                locationEnd,
                ownsLocationEnd));
        }
    }

    private static void AppendSemanticJoin(
        GraphNode node,
        Func<int, GraphEdge?> getEdge,
        IReadOnlyDictionary<int, Vector2[]> edgePoints,
        RoadTypeStyleSnapshot roadTypeStyles,
        List<Vector2> vertices,
        List<Vector2> uvs,
        List<Color> colors,
        List<int> indices,
        List<RoadSurfaceTriangle> surfaceTriangles)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(getEdge);
        ArgumentNullException.ThrowIfNull(edgePoints);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(uvs);
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(surfaceTriangles);
        if (node.IncidenceCount != 2 ||
            node.Incidences[0].EdgeID == node.Incidences[1].EdgeID)
            return;

        RoadJunctionIncidence first = CreateNodeIncidence(
            node,
            node.Incidences[0],
            getEdge,
            edgePoints,
            roadTypeStyles);
        RoadJunctionIncidence second = CreateNodeIncidence(
            node,
            node.Incidences[1],
            getEdge,
            edgePoints,
            roadTypeStyles);
        if (first.Style.RoadType == second.Style.RoadType)
        {
            throw new InvalidOperationException(
                $"RoadRenderer semantic Node {node.ID} has the same RoadType on both incidences.");
        }

        int orientation = RoadExactPredicates.Orient2DSign(
            Vector2.Zero,
            first.OutwardDirection,
            second.OutwardDirection);
        if (orientation < 0)
            (first, second) = (second, first);
        else if (orientation == 0)
        {
            int directionDot = RoadExactPredicates.DotSign(
                first.OutwardDirection,
                second.OutwardDirection);
            if (directionDot < 0)
                return;
            if (directionDot == 0)
            {
                throw new InvalidOperationException(
                    $"RoadRenderer semantic Node {node.ID} has an invalid incidence direction pair.");
            }
            if (first.Style.RoadType > second.Style.RoadType)
                (first, second) = (second, first);

            AppendSameDirectionSemanticJoin(
                node.Position,
                first,
                second,
                vertices,
                uvs,
                colors,
                indices,
                surfaceTriangles);
            return;
        }

        AppendBevelSemanticJoin(
            node.Position,
            first,
            second,
            vertices,
            uvs,
            colors,
            indices,
            surfaceTriangles);
    }

    private static RoadJunctionIncidence CreateNodeIncidence(
        GraphNode node,
        EdgeIncidence incidence,
        Func<int, GraphEdge?> getEdge,
        IReadOnlyDictionary<int, Vector2[]> edgePoints,
        RoadTypeStyleSnapshot roadTypeStyles)
    {
        GraphEdge edge = getEdge(incidence.EdgeID) ??
            throw new InvalidOperationException(
                $"RoadRenderer Node {node.ID} references missing Edge {incidence.EdgeID}.");
        if (!edgePoints.TryGetValue(edge.ID, out Vector2[]? points) || points.Length < 2)
        {
            throw new InvalidOperationException(
                $"RoadRenderer Edge {edge.ID} has no valid display path.");
        }

        Vector2 outwardDirection;
        RoadLocation location;
        switch (incidence.Endpoint)
        {
            case EdgeEndpoint.A when edge.NodeA == node.ID &&
                                     RoadExactPredicates.SameBits(points[0], node.Position):
                outwardDirection = points[1] - points[0];
                location = new RoadLocation(
                    edge.ID,
                    0,
                    RoadGeometrySegment.ParameterStart);
                break;
            case EdgeEndpoint.B when edge.NodeB == node.ID &&
                                     RoadExactPredicates.SameBits(points[^1], node.Position):
                outwardDirection = points[^2] - points[^1];
                location = new RoadLocation(
                    edge.ID,
                    edge.GeometrySegments.Count - 1,
                    RoadGeometrySegment.ParameterEnd);
                break;
            default:
                throw new InvalidOperationException(
                    $"RoadRenderer Node {node.ID} is not Edge {edge.ID} endpoint {incidence.Endpoint}.");
        }
        if (!outwardDirection.IsFinite() || outwardDirection.IsZeroApprox())
        {
            throw new InvalidOperationException(
                $"RoadRenderer Edge {edge.ID} has no valid visible endpoint direction.");
        }

        return new RoadJunctionIncidence(
            node.ID,
            edge.ID,
            incidence.Endpoint,
            outwardDirection.Normalized(),
            roadTypeStyles.Resolve(edge.RoadType),
            location);
    }

    private static void AppendBevelSemanticJoin(
        Vector2 nodePosition,
        RoadJunctionIncidence first,
        RoadJunctionIncidence second,
        List<Vector2> vertices,
        List<Vector2> uvs,
        List<Color> colors,
        List<int> indices,
        List<RoadSurfaceTriangle> surfaceTriangles)
    {
        Vector2 firstRight = nodePosition -
            LeftNormal(first.OutwardDirection) * first.HalfWidth;
        Vector2 secondLeft = nodePosition +
            LeftNormal(second.OutwardDirection) * second.HalfWidth;
        Vector2 splitPoint = (firstRight + secondLeft) * 0.5f;
        AppendSemanticJoinTriangle(
            nodePosition,
            splitPoint,
            firstRight,
            first,
            sectorOrder: 0,
            vertices,
            uvs,
            colors,
            indices,
            surfaceTriangles);
        AppendSemanticJoinTriangle(
            nodePosition,
            secondLeft,
            splitPoint,
            second,
            sectorOrder: 1,
            vertices,
            uvs,
            colors,
            indices,
            surfaceTriangles);
    }

    private static void AppendSameDirectionSemanticJoin(
        Vector2 nodePosition,
        RoadJunctionIncidence first,
        RoadJunctionIncidence second,
        List<Vector2> vertices,
        List<Vector2> uvs,
        List<Color> colors,
        List<int> indices,
        List<RoadSurfaceTriangle> surfaceTriangles)
    {
        float halfWidth = MathF.Max(first.HalfWidth, second.HalfWidth);
        Vector2 normal = LeftNormal(first.OutwardDirection);
        Vector2 left = nodePosition + normal * halfWidth;
        Vector2 tip = nodePosition - first.OutwardDirection * halfWidth;
        Vector2 right = nodePosition - normal * halfWidth;
        AppendSemanticJoinTriangle(
            nodePosition,
            left,
            tip,
            first,
            sectorOrder: 0,
            vertices,
            uvs,
            colors,
            indices,
            surfaceTriangles);
        AppendSemanticJoinTriangle(
            nodePosition,
            tip,
            right,
            second,
            sectorOrder: 1,
            vertices,
            uvs,
            colors,
            indices,
            surfaceTriangles);
    }

    private static void AppendSemanticJoinTriangle(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        RoadJunctionIncidence incidence,
        int sectorOrder,
        List<Vector2> vertices,
        List<Vector2> uvs,
        List<Color> colors,
        List<int> indices,
        List<RoadSurfaceTriangle> surfaceTriangles)
    {
        var owner = RoadSurfaceOwner.SemanticJoin(
            incidence.EdgeID,
            incidence.NodeID,
            incidence.Endpoint,
            sectorOrder);
        var surface = new RoadSurfaceTriangle(
            owner,
            a,
            b,
            c,
            centerlineStart: a,
            centerlineEnd: a + incidence.OutwardDirection * incidence.HalfWidth,
            locationStart: null,
            locationEnd: null,
            fixedLocation: incidence.Location);
        int vertexOffset = vertices.Count;
        vertices.Add(a);
        uvs.Add(new Vector2(0f, 0.5f));
        colors.Add(incidence.Style.Color);
        vertices.Add(b);
        uvs.Add(Vector2.Zero);
        colors.Add(incidence.Style.Color);
        vertices.Add(c);
        uvs.Add(Vector2.Zero);
        colors.Add(incidence.Style.Color);
        indices.Add(vertexOffset);
        indices.Add(vertexOffset + 1);
        indices.Add(vertexOffset + 2);
        surfaceTriangles.Add(surface);
    }

    private static void AppendJunctionPatch(
        GraphNode node,
        Func<int, GraphEdge?> getEdge,
        IReadOnlyDictionary<int, Vector2[]> edgePoints,
        RoadTypeStyleSnapshot roadTypeStyles,
        List<Vector2> vertices,
        List<Vector2> uvs,
        List<Color> colors,
        List<int> indices,
        List<RoadSurfaceTriangle> surfaceTriangles)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(getEdge);
        ArgumentNullException.ThrowIfNull(edgePoints);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(uvs);
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(surfaceTriangles);
        if (node.IncidenceCount < 3)
            return;

        var incidences = new RoadJunctionIncidence[node.IncidenceCount];
        for (int index = 0; index < node.IncidenceCount; index++)
        {
            incidences[index] = CreateNodeIncidence(
                node,
                node.Incidences[index],
                getEdge,
                edgePoints,
                roadTypeStyles);
        }

        foreach (RoadJunctionTriangle triangle in
                 RoadJunctionTessellator.Tessellate(node.Position, incidences))
        {
            AppendJunctionPatchTriangle(
                triangle,
                vertices,
                uvs,
                colors,
                indices,
                surfaceTriangles);
        }
    }

    private static void AppendJunctionPatchTriangle(
        RoadJunctionTriangle triangle,
        List<Vector2> vertices,
        List<Vector2> uvs,
        List<Color> colors,
        List<int> indices,
        List<RoadSurfaceTriangle> surfaceTriangles)
    {
        RoadJunctionIncidence incidence = triangle.Incidence;
        var owner = RoadSurfaceOwner.JunctionPatch(
            incidence.EdgeID,
            incidence.NodeID,
            incidence.Endpoint,
            triangle.SectorOrder);
        var surface = new RoadSurfaceTriangle(
            owner,
            triangle.A,
            triangle.B,
            triangle.C,
            centerlineStart: triangle.NodePosition,
            centerlineEnd:
                triangle.NodePosition +
                incidence.OutwardDirection * triangle.CenterlineLength,
            locationStart: null,
            locationEnd: null,
            fixedLocation: incidence.Location);
        int vertexOffset = vertices.Count;
        vertices.Add(triangle.A);
        uvs.Add(new Vector2(0f, 0.5f));
        colors.Add(incidence.Style.Color);
        vertices.Add(triangle.B);
        uvs.Add(Vector2.Zero);
        colors.Add(incidence.Style.Color);
        vertices.Add(triangle.C);
        uvs.Add(Vector2.Zero);
        colors.Add(incidence.Style.Color);
        indices.Add(vertexOffset);
        indices.Add(vertexOffset + 1);
        indices.Add(vertexOffset + 2);
        surfaceTriangles.Add(surface);
    }

    private static Vector2 LeftNormal(Vector2 direction) =>
        new(-direction.Y, direction.X);

    private static Vector2 CalculateRoadOffset(
        IReadOnlyList<Vector2> points,
        int pointCount,
        int index,
        bool isClosed,
        float halfWidth)
    {
        int previousIndex = isClosed
            ? (index + pointCount - 1) % pointCount
            : index - 1;
        int nextIndex = isClosed
            ? (index + 1) % pointCount
            : index + 1;
        Vector2 previousDirection = previousIndex < 0
            ? Vector2.Zero
            : (points[index] - points[previousIndex]).Normalized();
        Vector2 nextDirection = nextIndex >= pointCount
            ? Vector2.Zero
            : (points[nextIndex] - points[index]).Normalized();
        if (previousDirection.IsZeroApprox())
            previousDirection = nextDirection;
        if (nextDirection.IsZeroApprox())
            nextDirection = previousDirection;
        if (previousDirection.IsZeroApprox())
            return Vector2.Zero;

        var previousNormal = new Vector2(-previousDirection.Y, previousDirection.X);
        var nextNormal = new Vector2(-nextDirection.Y, nextDirection.X);
        Vector2 miter = (previousNormal + nextNormal).Normalized();
        float denominator = miter.Dot(nextNormal);
        if (miter.IsZeroApprox() || Mathf.Abs(denominator) < 0.25f)
            return nextNormal * halfWidth;

        float miterLength = Mathf.Clamp(halfWidth / denominator, -halfWidth * 4f, halfWidth * 4f);
        return miter * miterLength;
    }

    private static ArrayMesh? CreateRoadMesh(
        IReadOnlyCollection<Vector2> vertices,
        IReadOnlyCollection<Vector2> uvs,
        IReadOnlyCollection<Color> colors,
        IReadOnlyCollection<int> indices)
    {
        if (vertices.Count == 0)
            return null;
        if (uvs.Count != vertices.Count || colors.Count != vertices.Count)
        {
            throw new InvalidOperationException(
                "Road mesh vertex, UV, and color arrays must have the same length.");
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        return InitializeOwnedResource(
            new ArrayMesh(),
            (Arrays: arrays, Indices: indices),
            static (mesh, state) =>
            {
                state.Arrays[(int)Mesh.ArrayType.Index] = state.Indices.ToArray();
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, state.Arrays);
            });
    }

    private static TResource InitializeOwnedResource<TResource, TState>(
        TResource resource,
        TState state,
        Action<TResource, TState> initialize)
        where TResource : IDisposable
    {
        try
        {
            initialize(resource, state);
            return resource;
        }
        catch
        {
            resource.Dispose();
            throw;
        }
    }

    private static void DisposePreparedPresentationResources(
        ArrayMesh? roadMesh,
        MultiMesh? nodeBatch)
    {
        try
        {
            nodeBatch?.Dispose();
        }
        finally
        {
            roadMesh?.Dispose();
        }
    }

    private static MultiMeshInstance2D CreateBatchLayer(bool useColors, int zIndex)
    {
        var batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = useColors,
            Mesh = new QuadMesh { Size = Vector2.One },
        };
        return new MultiMeshInstance2D
        {
            Multimesh = batch,
            ZIndex = zIndex,
        };
    }

    private static ShaderMaterial CreateCircleMaterial()
    {
        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;

                void fragment() {
                    vec2 centered = UV * 2.0 - 1.0;
                    float distance_to_center = length(centered);
                    float antialias_width = fwidth(distance_to_center);
                    float coverage = 1.0 - smoothstep(1.0 - antialias_width, 1.0, distance_to_center);
                    COLOR.a *= coverage;
                }
                """,
        };
        return new ShaderMaterial { Shader = shader };
    }

    private static ShaderMaterial CreateRoadMaterial()
    {
        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;

                void fragment() {
                    float edge_distance = abs(UV.y - 0.5);
                    float antialias_width = fwidth(edge_distance);
                    float coverage = 1.0 - smoothstep(0.5 - antialias_width, 0.5, edge_distance);
                    COLOR.a *= coverage;
                }
                """,
        };
        return new ShaderMaterial { Shader = shader };
    }

    // ── RoadRenderer._Draw() 只画施工预览 ──

    public override void _Draw()
    {
        bool canDrawRoadInteraction = IsPresentationReady();
        bool hasEdgeHighlight =
            canDrawRoadInteraction &&
            (_removalPreviewEdgeIDs.Length > 0 ||
             _upgradePreviewEdgeIDs.Length > 0 ||
             HoveredEdgeID.HasValue);
        RoadTypeStyleSnapshot highlightStyles = hasEdgeHighlight
                ? Config.CaptureRoadTypeStyleSnapshot()
                : default;
        if (canDrawRoadInteraction)
        {
            foreach (int edgeID in _removalPreviewEdgeIDs)
                DrawEdgeHighlight(edgeID, highlightStyles);
            foreach (int edgeID in _upgradePreviewEdgeIDs)
                DrawEdgeHighlight(edgeID, highlightStyles);

            if (RemovalSelectionBounds is { } bounds &&
                bounds.Size.X > 0f &&
                bounds.Size.Y > 0f)
            {
                DrawRect(bounds, Config.HoverHighlightColor, false, 2f);
            }
            if (UpgradeSelectionBounds is { } upgradeBounds &&
                upgradeBounds.Size.X > 0f &&
                upgradeBounds.Size.Y > 0f)
            {
                DrawRect(upgradeBounds, Config.HoverHighlightColor, false, 2f);
            }

            if (HoveredEdgeID.HasValue && _network != null)
                DrawEdgeHighlight(HoveredEdgeID.Value, highlightStyles);
        }

        for (int index = 1; index < _previewPoints.Length; index++)
        {
            Vector2 from = _previewPoints[index - 1];
            Vector2 to = _previewPoints[index];
            if (from != to)
                DrawDashedLine(from, to, new Color(1, 1, 1, 0.5f));
        }
    }

    private void DrawEdgeHighlight(
        int edgeID,
        RoadTypeStyleSnapshot roadTypeStyles)
    {
        GraphEdge? edge = _network?.GetEdge(edgeID);
        if (edge == null || _network == null)
            return;

        GraphNode? nodeA = _network.GetNode(edge.NodeA);
        GraphNode? nodeB = _network.GetNode(edge.NodeB);
        if (nodeA == null || nodeB == null)
            return;

        if (!_edgePoints.TryGetValue(edgeID, out Vector2[]? points))
            return;

        DrawPolyline(points, Config.HoverHighlightColor, Config.HoverHighlightWidth);
        DrawNodeHighlight(nodeA, roadTypeStyles);
        DrawNodeHighlight(nodeB, roadTypeStyles);
    }

    private void DrawNodeHighlight(
        GraphNode node,
        RoadTypeStyleSnapshot roadTypeStyles)
    {
        if (_network == null)
            return;

        float radius = GetNodeMarkerRadius(
            _network,
            node,
            roadTypeStyles,
            Config.JunctionRadius);
        if (radius > 0f)
            DrawCircle(node.Position, radius * 1.3f, Config.HoverHighlightColor);
    }

    private static RoadRendererNodeSurface? CreateNodeSurface(
        RoadGraph graph,
        GraphNode node,
        RoadTypeStyleSnapshot roadTypeStyles)
    {
        if (node.IncidenceCount == 1)
        {
            EdgeIncidence incidence = node.Incidences[0];
            GraphEdge edge = graph.GetEdge(incidence.EdgeID) ??
                throw new InvalidOperationException(
                    $"RoadRenderer terminal Node {node.ID} references missing Edge {incidence.EdgeID}.");
            if (!TryGetOutgoingDirection(graph, node, incidence, out Vector2 inwardDirection))
            {
                throw new InvalidOperationException(
                    $"RoadRenderer terminal Node {node.ID} has no valid incidence direction.");
            }

            return CreateTerminalCapSurface(
                edge,
                node.ID,
                node.Position,
                incidence.Endpoint,
                inwardDirection,
                roadTypeStyles.Resolve(edge.RoadType));
        }

        return null;
    }

    private static RoadRendererNodeSurface CreateTerminalCapSurface(
        GraphEdge edge,
        int nodeID,
        Vector2 position,
        EdgeEndpoint endpoint,
        Vector2 inwardDirection,
        RoadTypeStyleDefinition style)
    {
        if (!inwardDirection.IsFinite() || inwardDirection.IsZeroApprox())
            throw new ArgumentException("A terminal cap requires a finite incidence direction.");

        float radius = style.Width * 0.5f;
        Vector2 unitInward = inwardDirection.Normalized();
        RoadLocation location = endpoint switch
        {
            EdgeEndpoint.A when edge.NodeA == nodeID =>
                new RoadLocation(edge.ID, 0, RoadGeometrySegment.ParameterStart),
            EdgeEndpoint.B when edge.NodeB == nodeID =>
                new RoadLocation(
                    edge.ID,
                    edge.GeometrySegments.Count - 1,
                    RoadGeometrySegment.ParameterEnd),
            _ => throw new InvalidOperationException(
                $"RoadRenderer terminal Node {nodeID} is not Edge {edge.ID} endpoint {endpoint}."),
        };
        var owner = RoadSurfaceOwner.TerminalCap(edge.ID, nodeID, endpoint);
        var surface = new RoadSurfaceDisc(
            owner,
            position,
            radius,
            position,
            position - unitInward * radius,
            location);
        return new RoadRendererNodeSurface(
            new RoadRendererNodeMarker(position, style.Width, style.Color),
            surface);
    }

    internal static float GetNodeMarkerRadius(
        RoadGraph graph,
        GraphNode node,
        RoadTypeStyleSnapshot roadTypeStyles,
        float junctionRadius)
    {
        if (node.IncidenceCount == 1)
        {
            GraphEdge edge = graph.GetEdge(node.Incidences[0].EdgeID) ??
                throw new InvalidOperationException(
                    $"RoadRenderer terminal Node {node.ID} references a missing Edge.");
            return roadTypeStyles.Resolve(edge.RoadType).Width * 0.5f;
        }
        return IsJunctionNode(graph, node) ? junctionRadius : 0f;
    }

    internal static bool IsJunctionNode(RoadGraph graph, GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(node);
        return node.IncidenceCount >= 3;
    }

    private static bool TryGetOutgoingDirection(
        RoadGraph graph,
        GraphNode node,
        EdgeIncidence incidence,
        out Vector2 direction)
    {
        direction = Vector2.Zero;
        GraphEdge? edge = graph.GetEdge(incidence.EdgeID);
        if (edge == null)
            return false;

        direction = incidence.Endpoint switch
        {
            EdgeEndpoint.A when edge.NodeA == node.ID =>
                edge.GeometrySegments[0].GetUnitTangent(0f),
            EdgeEndpoint.B when edge.NodeB == node.ID =>
                -edge.GeometrySegments[^1].GetUnitTangent(1f),
            _ => Vector2.Zero,
        };

        return direction.IsFinite() && !direction.IsZeroApprox();
    }

    // ── 虚线工具 ──

    private void DrawDashedLine(Vector2 from, Vector2 to, Color color, float width = 2f, float dashLength = 6f)
    {
        Vector2 delta = to - from;
        float total = delta.Length();
        if (total < 0.01f) return;

        Vector2 dir = delta / total;
        float drawn = 0f;
        bool draw = true;

        while (drawn < total)
        {
            float seg = Mathf.Min(dashLength, total - drawn);
            if (draw)
            {
                Vector2 start = from + dir * drawn;
                Vector2 end = start + dir * seg;
                DrawLine(start, end, color, width);
            }
            drawn += seg;
            draw = !draw;
        }
    }
}
