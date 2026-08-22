using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RoadRenderer
{
    partial void ProbeAggregateLoadNodeBatchFactoryFailure(
        ref IReadOnlyList<RoadRendererNodeMarker> nodeMarkers);
    partial void ProbeAggregateLoadResourcePreflightFailure();
    partial void ProbeLoadCompleteCommitFailure();

    internal RoadRendererLoadAdmission BeginLoadAdmission()
    {
        if (_loadAdmission is not null || _network is null || Config is null)
            throw new InvalidOperationException("RoadRenderer cannot admit a load in its current state.");
        if (!IsInsideTree() ||
            !GodotObject.IsInstanceValid(_roadBatchLayer) ||
            !GodotObject.IsInstanceValid(_nodeBatchLayer))
        {
            throw new InvalidOperationException("RoadRenderer presentation resources are not ready.");
        }
        if (_staticBatchRebuildScheduled)
            FlushScheduledStaticBatchRebuild();
        if (!IsPresentationReady())
            throw new InvalidOperationException("RoadRenderer presentation is not current.");

        _loadAdmissionGeneration = NextLoadGeneration(_loadAdmissionGeneration);
        RoadTypeStyleSnapshot roadTypeStyles = Config.CaptureRoadTypeStyleSnapshot();
        var settings = new RoadRendererLoadSettings(
            Config.CurveDisplayTolerance,
            roadTypeStyles);
        settings.Validate();
        RoadRenderLoadReservation renderReservation = _presentationTokens.ReserveLoad();
        var admission = new RoadRendererLoadAdmission(
            this,
            _loadAdmissionGeneration,
            _network,
            renderReservation,
            new RoadRendererLoadPreparer(settings));
        _loadAdmission = admission;
        return admission;
    }

    internal INonThrowingLoadCommitPlan PreflightPreparedLoad(
        RoadRendererLoadAdmission admission,
        RoadRendererPreparedLoad prepared,
        GraphStateToken targetToken)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(prepared);
        if (!IsLoadAdmissionCurrent(admission))
            throw new LoadPreflightInvalidException("RoadRenderer load admission is stale.");
        if (!IsInsideTree() ||
            !GodotObject.IsInstanceValid(_roadBatchLayer) ||
            !GodotObject.IsInstanceValid(_nodeBatchLayer))
        {
            throw new LoadPreflightInvalidException("RoadRenderer resources left the scene before preflight.");
        }

        ArrayMesh? roadMesh = null;
        MultiMesh? nodeBatch = null;
        try
        {
            roadMesh = CreateRoadMesh(
                prepared.RoadVertices,
                prepared.RoadUvs,
                prepared.RoadColors,
                prepared.RoadIndices);
            IReadOnlyList<RoadRendererNodeMarker> nodeMarkers = prepared.NodeMarkers;
            ProbeAggregateLoadNodeBatchFactoryFailure(ref nodeMarkers);
            nodeBatch = CreateNodeBatch(nodeMarkers);
            ProbeAggregateLoadResourcePreflightFailure();
            RoadRenderToken renderToken = _presentationTokens.CreateReservedLoadToken(
                admission.RenderReservation,
                targetToken.ChangeSequence);
            var surfaceSnapshot = new RoadSurfaceSnapshot(
                renderToken,
                prepared.RoadSurface);
            return new RoadRendererLoadCommitPlan(
                this,
                admission,
                prepared,
                roadMesh,
                nodeBatch,
                targetToken,
                renderToken,
                surfaceSnapshot);
        }
        catch
        {
            DisposePreparedPresentationResources(roadMesh, nodeBatch);
            throw;
        }
    }

    private bool IsLoadAdmissionCurrent(RoadRendererLoadAdmission admission) =>
        ReferenceEquals(_loadAdmission, admission) &&
        admission.Generation == _loadAdmissionGeneration &&
        ReferenceEquals(_network, admission.Graph) &&
        _presentationTokens.IsReservationCurrent(admission.RenderReservation);

    private void AbandonLoadAdmission(RoadRendererLoadAdmission admission)
    {
        if (ReferenceEquals(_loadAdmission, admission))
            _loadAdmission = null;
    }

    private static long NextLoadGeneration(long generation) =>
        generation == long.MaxValue ? 1 : generation + 1;

    private static MultiMesh CreateNodeBatch(IReadOnlyList<RoadRendererNodeMarker> markers)
    {
        return InitializeOwnedResource(
            new MultiMesh(),
            markers,
            static (batch, nodeMarkers) =>
            {
                batch.TransformFormat = MultiMesh.TransformFormatEnum.Transform2D;
                batch.UseColors = true;
                using (var markerMesh = new QuadMesh { Size = Vector2.One })
                    batch.Mesh = markerMesh;
                batch.InstanceCount = nodeMarkers.Count;
                for (int index = 0; index < nodeMarkers.Count; index++)
                {
                    RoadRendererNodeMarker marker = nodeMarkers[index];
                    var transform = new Transform2D(0f, marker.Position)
                        .ScaledLocal(new Vector2(marker.Diameter, marker.Diameter));
                    batch.SetInstanceTransform2D(index, transform);
                    batch.SetInstanceColor(index, marker.Color);
                }
            });
    }

    internal sealed class RoadRendererLoadAdmission : IDisposable
    {
        private RoadRenderer? _owner;

        internal RoadRendererLoadAdmission(
            RoadRenderer owner,
            long generation,
            RoadGraph graph,
            RoadRenderLoadReservation renderReservation,
            RoadRendererLoadPreparer preparer)
        {
            _owner = owner;
            Generation = generation;
            Graph = graph;
            RenderReservation = renderReservation;
            Preparer = preparer;
        }

        internal long Generation { get; }
        internal RoadGraph Graph { get; }
        internal RoadRenderLoadReservation RenderReservation { get; }
        internal RoadRendererLoadPreparer Preparer { get; }

        public void Dispose()
        {
            RoadRenderer? owner = _owner;
            _owner = null;
            owner?.AbandonLoadAdmission(this);
        }
    }

    internal sealed class RoadRendererLoadPreparer
    {
        private readonly RoadRendererLoadSettings _settings;

        internal RoadRendererLoadPreparer(RoadRendererLoadSettings settings)
        {
            _settings = settings;
        }

        internal RoadRendererPreparedLoad Prepare(
            RoadGraphRevision revision,
            IReadOnlyDictionary<int, Vector2[]>? reusableEdgePoints = null,
            IReadOnlyDictionary<int, RoadGeometryDisplaySpan[]>? reusableEdgeDisplaySpans = null,
            IReadOnlySet<int>? invalidatedEdgeIDs = null)
        {
            ArgumentNullException.ThrowIfNull(revision);
            if ((reusableEdgePoints is null) != (reusableEdgeDisplaySpans is null))
            {
                throw new ArgumentException(
                    "Reusable road display points and spans must be supplied together.");
            }
            var edgePoints = new Dictionary<int, Vector2[]>(revision.Edges.Count);
            var edgeDisplaySpans = new Dictionary<int, RoadGeometryDisplaySpan[]>(revision.Edges.Count);
            var roadVertices = new List<Vector2>();
            var roadUvs = new List<Vector2>();
            var roadColors = new List<Color>();
            var roadIndices = new List<int>();
            var surfaceTriangles = new List<RoadSurfaceTriangle>();
            var surfaceDiscs = new List<RoadSurfaceDisc>();
            foreach (GraphEdge edge in revision.Edges.Values.OrderBy(edge => edge.ID))
            {
                Vector2[] points;
                RoadGeometryDisplaySpan[] displaySpans;
                if (reusableEdgePoints is not null &&
                    reusableEdgeDisplaySpans is not null &&
                    (invalidatedEdgeIDs is null || !invalidatedEdgeIDs.Contains(edge.ID)) &&
                    reusableEdgePoints.TryGetValue(edge.ID, out Vector2[]? reusablePoints) &&
                    reusableEdgeDisplaySpans.TryGetValue(
                        edge.ID,
                        out RoadGeometryDisplaySpan[]? reusableSpans))
                {
                    points = reusablePoints;
                    displaySpans = reusableSpans;
                }
                else
                {
                    RoadGeometryDisplayPath displayPath = RoadGeometryDisplaySampler.SamplePath(
                        edge.GeometrySegments,
                        _settings.CurveDisplayTolerance);
                    points = displayPath.Points;
                    displaySpans = displayPath.Spans;
                }
                edgePoints.Add(edge.ID, points);
                edgeDisplaySpans.Add(edge.ID, displaySpans);
                RoadTypeStyleDefinition style = _settings.RoadTypeStyles.Resolve(edge.RoadType);
                AppendRoadRibbon(
                    edge.ID,
                    points,
                    displaySpans,
                    edge.NodeA == edge.NodeB,
                    style.Width * 0.5f,
                    style.Color,
                    roadVertices,
                    roadUvs,
                    roadColors,
                    roadIndices,
                    surfaceTriangles);
            }

            GraphEdge? GetEdge(int edgeID) =>
                revision.Edges.TryGetValue(edgeID, out GraphEdge? edge) ? edge : null;
            var nodeMarkers = new List<RoadRendererNodeMarker>();
            foreach (GraphNode node in revision.Nodes.Values.OrderBy(node => node.ID))
            {
                AppendSemanticJoin(
                    node,
                    GetEdge,
                    edgePoints,
                    _settings.RoadTypeStyles,
                    roadVertices,
                    roadUvs,
                    roadColors,
                    roadIndices,
                    surfaceTriangles);
                AppendJunctionPatch(
                    node,
                    GetEdge,
                    edgePoints,
                    _settings.RoadTypeStyles,
                    roadVertices,
                    roadUvs,
                    roadColors,
                    roadIndices,
                    surfaceTriangles);
                RoadRendererNodeSurface? nullableSurface = CreateNodeSurface(
                    revision,
                    node,
                    _settings.RoadTypeStyles);
                if (nullableSurface is not RoadRendererNodeSurface nodeSurface)
                    continue;

                nodeMarkers.Add(nodeSurface.Marker);
                if (nodeSurface.Surface is RoadSurfaceDisc surfaceDisc)
                    surfaceDiscs.Add(surfaceDisc);
            }
            RoadSurfaceSnapshot.PreparedData roadSurface =
                RoadSurfaceSnapshot.Prepare(surfaceTriangles, surfaceDiscs);
            return new RoadRendererPreparedLoad(
                edgePoints,
                edgeDisplaySpans,
                roadVertices.ToArray(),
                roadUvs.ToArray(),
                roadColors.ToArray(),
                roadIndices.ToArray(),
                roadSurface,
                nodeMarkers.ToArray());
        }
    }

    private sealed class RoadRendererLoadCommitPlan : INonThrowingLoadCommitPlan
    {
        private readonly RoadRenderer _owner;
        private readonly RoadRendererLoadAdmission _admission;
        private readonly RoadRendererPreparedLoad _prepared;
        private readonly ArrayMesh? _roadMesh;
        private readonly MultiMesh _nodeBatch;
        private readonly GraphStateToken _targetGraphToken;
        private readonly RoadRenderToken _targetRenderToken;
        private readonly RoadSurfaceSnapshot _targetSurfaceSnapshot;
        private bool _committed;
        private bool _completed;
        private bool _disposed;

        internal RoadRendererLoadCommitPlan(
            RoadRenderer owner,
            RoadRendererLoadAdmission admission,
            RoadRendererPreparedLoad prepared,
            ArrayMesh? roadMesh,
            MultiMesh nodeBatch,
            GraphStateToken targetGraphToken,
            RoadRenderToken targetRenderToken,
            RoadSurfaceSnapshot targetSurfaceSnapshot)
        {
            _owner = owner;
            _admission = admission;
            _prepared = prepared;
            _roadMesh = roadMesh;
            _nodeBatch = nodeBatch;
            _targetGraphToken = targetGraphToken;
            _targetRenderToken = targetRenderToken;
            _targetSurfaceSnapshot = targetSurfaceSnapshot;
        }

        public string ParticipantID => "road-presentation";
        public bool IsGenerationCurrent =>
            !_disposed && !_committed && _owner.IsLoadAdmissionCurrent(_admission);

        public void CommitReferences()
        {
            _owner.InvalidateScheduledStaticBatchRebuildContinuation();
            _owner._edgePoints = _prepared.EdgePoints;
            _owner._edgeDisplaySpans = _prepared.EdgeDisplaySpans;
            _owner._invalidatedDisplayEdgeIDs.Clear();
            _owner._rebuildAllDisplayPaths = false;
            _owner._roadMeshVertexCount = _prepared.RoadVertices.Length;
            _owner._roadBatchLayer.Mesh = _roadMesh;
            _owner._nodeBatchLayer.Multimesh = _nodeBatch;
            _owner._previewPoints = [];
            _owner._removalPreviewEdgeIDs = [];
            _owner.RemovalSelectionBounds = null;
            _owner._upgradePreviewEdgeIDs = [];
            _owner.UpgradeSelectionBounds = null;
            _owner.HoveredEdgeID = null;
            _owner._presentedSurface = _targetSurfaceSnapshot;
            _owner._presentationTokens.CommitReservedLoad(
                _admission.RenderReservation,
                _targetRenderToken);
            _owner._committedLoadGraphToken = _targetGraphToken;
            _committed = true;
        }

        public IReadOnlyList<string> PublishNotifications()
        {
            var warnings = new List<string>();
            Action<RoadRenderToken>? handlers = _owner.PresentationReady;
            if (handlers is null)
                return warnings;
            foreach (Action<RoadRenderToken> handler in handlers
                         .GetInvocationList()
                         .Cast<Action<RoadRenderToken>>())
            {
                try
                {
                    handler(_targetRenderToken);
                }
                catch (Exception exception)
                {
                    warnings.Add($"Road presentation observer failed: {exception.Message}");
                }
            }
            return warnings;
        }

        public void CompleteCommit()
        {
            _owner.AbandonLoadAdmission(_admission);
            _completed = true;
            _owner.QueueRedraw();
            _owner.ProbeLoadCompleteCommitFailure();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_completed || _committed)
                return;
            try
            {
                DisposePreparedPresentationResources(_roadMesh, _nodeBatch);
            }
            finally
            {
                _admission.Dispose();
            }
        }
    }

    private static RoadRendererNodeSurface? CreateNodeSurface(
        RoadGraphRevision revision,
        GraphNode node,
        RoadTypeStyleSnapshot roadTypeStyles)
    {
        if (node.IncidenceCount == 1)
        {
            EdgeIncidence incidence = node.Incidences[0];
            if (!revision.Edges.TryGetValue(incidence.EdgeID, out GraphEdge? edge))
            {
                throw new InvalidOperationException(
                    $"RoadRenderer terminal Node {node.ID} references missing Edge {incidence.EdgeID}.");
            }
            if (!TryGetOutgoingDirection(revision, node, incidence, out Vector2 inwardDirection))
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

    private static bool TryGetOutgoingDirection(
        RoadGraphRevision revision,
        GraphNode node,
        EdgeIncidence incidence,
        out Vector2 direction)
    {
        direction = Vector2.Zero;
        if (!revision.Edges.TryGetValue(incidence.EdgeID, out GraphEdge? edge))
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
}

internal sealed record RoadRendererPreparedLoad(
    Dictionary<int, Vector2[]> EdgePoints,
    Dictionary<int, RoadGeometryDisplaySpan[]> EdgeDisplaySpans,
    Vector2[] RoadVertices,
    Vector2[] RoadUvs,
    Color[] RoadColors,
    int[] RoadIndices,
    RoadSurfaceSnapshot.PreparedData RoadSurface,
    RoadRendererNodeMarker[] NodeMarkers);

internal readonly record struct RoadRendererNodeMarker(
    Vector2 Position,
    float Diameter,
    Color Color);

internal readonly record struct RoadRendererNodeSurface(
    RoadRendererNodeMarker Marker,
    RoadSurfaceDisc? Surface);

internal readonly record struct RoadRendererLoadSettings(
    float CurveDisplayTolerance,
    RoadTypeStyleSnapshot RoadTypeStyles)
{
    internal void Validate()
    {
        if (!float.IsFinite(CurveDisplayTolerance) || CurveDisplayTolerance <= 0f)
            throw new InvalidOperationException("RoadRenderer curve tolerance is invalid.");
        RoadTypeStyles.Validate();
    }
}
