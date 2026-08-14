using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RoadRenderer
{
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

        ArrayMesh? roadMesh = CreateRoadMesh(
            prepared.RoadVertices,
            prepared.RoadUvs,
            prepared.RoadColors,
            prepared.RoadIndices);
        MultiMesh nodeBatch = CreateNodeBatch(prepared.NodeMarkers);
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
        var batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            Mesh = new QuadMesh { Size = Vector2.One },
            InstanceCount = markers.Count,
        };
        for (int index = 0; index < markers.Count; index++)
        {
            RoadRendererNodeMarker marker = markers[index];
            var transform = new Transform2D(0f, marker.Position)
                .ScaledLocal(new Vector2(marker.Diameter, marker.Diameter));
            batch.SetInstanceTransform2D(index, transform);
            batch.SetInstanceColor(index, marker.Color);
        }
        return batch;
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

        internal RoadRendererPreparedLoad Prepare(RoadGraphRevision revision)
        {
            ArgumentNullException.ThrowIfNull(revision);
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
                RoadGeometryDisplayPath displayPath = RoadGeometryDisplaySampler.SamplePath(
                    edge.GeometrySegments,
                    _settings.CurveDisplayTolerance);
                Vector2[] points = displayPath.Points;
                edgePoints.Add(edge.ID, displayPath.Points);
                edgeDisplaySpans.Add(edge.ID, displayPath.Spans);
                RoadTypeStyleDefinition style = _settings.RoadTypeStyles.Resolve(edge.RoadType);
                AppendRoadRibbon(
                    edge.ID,
                    points,
                    displayPath.Spans,
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
            !_committed && _owner.IsLoadAdmissionCurrent(_admission);

        public void CommitReferences()
        {
            _owner._staticBatchRebuildScheduled = false;
            _owner._edgePoints = _prepared.EdgePoints;
            _owner._edgeDisplaySpans = _prepared.EdgeDisplaySpans;
            _owner._roadMeshVertexCount = _prepared.RoadVertices.Length;
            _owner._roadBatchLayer.Mesh = _roadMesh;
            _owner._nodeBatchLayer.Multimesh = _nodeBatch;
            _owner._previewPoints = [];
            _owner._removalPreviewEdgeIDs = [];
            _owner.RemovalSelectionBounds = null;
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
        }

        public void Dispose()
        {
            if (_completed)
                return;
            if (!_committed)
                _admission.Dispose();
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
