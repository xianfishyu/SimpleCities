using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class RoadLoadPreflightResourceFailureProbe : RefCounted
{
    private RoadRenderer? _renderer;
    private SaveManager? _saveManager;

    public Godot.Collections.Dictionary Run(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        return renderer.ProbeRoadSurfaceSnapshotPreflightFailure();
    }

    public Godot.Collections.Dictionary RunUncommittedPlanDisposal(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        return renderer.ProbeUncommittedLoadPlanDisposal();
    }

    public Godot.Collections.Dictionary RunRendererCommitBoundaryGenerationMismatch(
        RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        return renderer.ProbeLoadCommitBoundaryGenerationMismatch();
    }

    public Godot.Collections.Dictionary RunToolCommitBoundaryGenerationMismatch(
        ToolManager toolManager)
    {
        ArgumentNullException.ThrowIfNull(toolManager);
        return toolManager.ProbeToolLoadCommitBoundaryGenerationMismatch();
    }

    public Godot.Collections.Dictionary RunNodeBatchFactoryFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        return renderer.ProbeNodeBatchFactoryFailure();
    }

    public Godot.Collections.Dictionary RunRoadMeshOwnershipFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        return renderer.ProbeRoadMeshOwnershipFailure();
    }

    public void ArmAggregateLoadResourcePreflightFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextAggregateLoadResourcePreflightFailure();
        _renderer = renderer;
    }

    public void ArmAggregateLoadRoadMeshFactoryFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextAggregateLoadRoadMeshFactoryFailure();
        _renderer = renderer;
    }

    public bool IsAggregateLoadRoadMeshFactoryFailureArmed() =>
        _renderer?.IsAggregateLoadRoadMeshFactoryFailureArmed() ?? false;

    public int GetAggregateLoadRoadMeshFactoryFailureCount() =>
        _renderer?.GetAggregateLoadRoadMeshFactoryFailureCount() ?? 0;

    public int GetAggregateLoadRoadMeshFactoryIndexEnumerationCount() =>
        _renderer?.GetAggregateLoadRoadMeshFactoryIndexEnumerationCount() ?? 0;

    public string GetAggregateLoadRoadMeshFactoryFailureMessage() =>
        RoadRenderer.AggregateLoadRoadMeshFactoryFailureMessage;

    public void ArmAggregateLoadNodeBatchFactoryFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextAggregateLoadNodeBatchFactoryFailure();
        _renderer = renderer;
    }

    public bool IsAggregateLoadNodeBatchFactoryFailureArmed() =>
        _renderer?.IsAggregateLoadNodeBatchFactoryFailureArmed() ?? false;

    public int GetAggregateLoadNodeBatchFactoryFailureCount() =>
        _renderer?.GetAggregateLoadNodeBatchFactoryFailureCount() ?? 0;

    public int GetAggregateLoadNodeBatchFactoryMarkerReadCount() =>
        _renderer?.GetAggregateLoadNodeBatchFactoryMarkerReadCount() ?? 0;

    public string GetAggregateLoadNodeBatchFactoryFailureMessage() =>
        RoadRenderer.AggregateLoadNodeBatchFactoryFailureMessage;

    public bool IsAggregateLoadResourcePreflightFailureArmed() =>
        _renderer?.IsAggregateLoadResourcePreflightFailureArmed() ?? false;

    public int GetAggregateLoadResourcePreflightFailureCount() =>
        _renderer?.GetAggregateLoadResourcePreflightFailureCount() ?? 0;

    public string GetAggregateLoadResourcePreflightFailureMessage() =>
        RoadRenderer.AggregateLoadResourcePreflightFailureMessage;

    public void ArmAggregateLoadPostRendererPreflightFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadPostRendererPreflightFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadPostRendererPreflightFailureArmed() =>
        _saveManager?.IsAggregateLoadPostRendererPreflightFailureArmed() ?? false;

    public int GetAggregateLoadPostRendererPreflightFailureCount() =>
        _saveManager?.GetAggregateLoadPostRendererPreflightFailureCount() ?? 0;

    public string GetAggregateLoadPostRendererPreflightFailureMessage() =>
        SaveManager.AggregateLoadPostRendererPreflightFailureMessage;

    public void ArmAggregateLoadPostSlotPreflightFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadPostSlotPreflightFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadPostSlotPreflightFailureArmed() =>
        _saveManager?.IsAggregateLoadPostSlotPreflightFailureArmed() ?? false;

    public int GetAggregateLoadPostSlotPreflightFailureCount() =>
        _saveManager?.GetAggregateLoadPostSlotPreflightFailureCount() ?? 0;

    public string GetAggregateLoadPostSlotPreflightFailureMessage() =>
        SaveManager.AggregateLoadPostSlotPreflightFailureMessage;

    public void ArmAggregateLoadPostOwnershipPreCommitFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadPostOwnershipPreCommitFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadPostOwnershipPreCommitFailureArmed() =>
        _saveManager?.IsAggregateLoadPostOwnershipPreCommitFailureArmed() ?? false;

    public int GetAggregateLoadPostOwnershipPreCommitFailureCount() =>
        _saveManager?.GetAggregateLoadPostOwnershipPreCommitFailureCount() ?? 0;

    public string GetAggregateLoadPostOwnershipPreCommitFailureMessage() =>
        SaveManager.AggregateLoadPostOwnershipPreCommitFailureMessage;

    public long GetObjectResourceCount() => Convert.ToInt64(
        Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
}

public partial class SaveManager
{
    internal const string AggregateLoadPostRendererPreflightFailureMessage =
        "Injected aggregate Load failure after road presentation preflight.";
    internal const string AggregateLoadPostSlotPreflightFailureMessage =
        "Injected aggregate Load failure after slot-target preflight.";
    internal const string AggregateLoadPostOwnershipPreCommitFailureMessage =
        "Injected aggregate Load failure after ownership transfer and before commit.";

    private bool _aggregateLoadPostRendererPreflightFailureArmed;
    private int _aggregateLoadPostRendererPreflightFailureCount;
    private bool _aggregateLoadPostSlotPreflightFailureArmed;
    private int _aggregateLoadPostSlotPreflightFailureCount;
    private bool _aggregateLoadPostOwnershipPreCommitFailureArmed;
    private int _aggregateLoadPostOwnershipPreCommitFailureCount;

    partial void ProbeAggregateLoadPostRendererPreflightFailure()
    {
        if (!_aggregateLoadPostRendererPreflightFailureArmed)
            return;

        _aggregateLoadPostRendererPreflightFailureArmed = false;
        _aggregateLoadPostRendererPreflightFailureCount++;
        throw new InvalidOperationException(
            AggregateLoadPostRendererPreflightFailureMessage);
    }

    internal void ArmNextAggregateLoadPostRendererPreflightFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadPostRendererPreflightFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load post-renderer preflight failure probe is already armed.");
        }

        _aggregateLoadPostRendererPreflightFailureArmed = true;
    }

    internal bool IsAggregateLoadPostRendererPreflightFailureArmed() =>
        _aggregateLoadPostRendererPreflightFailureArmed;

    internal int GetAggregateLoadPostRendererPreflightFailureCount() =>
        _aggregateLoadPostRendererPreflightFailureCount;

    partial void ProbeAggregateLoadPostSlotPreflightFailure()
    {
        if (!_aggregateLoadPostSlotPreflightFailureArmed)
            return;

        _aggregateLoadPostSlotPreflightFailureArmed = false;
        _aggregateLoadPostSlotPreflightFailureCount++;
        throw new InvalidOperationException(
            AggregateLoadPostSlotPreflightFailureMessage);
    }

    internal void ArmNextAggregateLoadPostSlotPreflightFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadPostSlotPreflightFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load post-slot preflight failure probe is already armed.");
        }

        _aggregateLoadPostSlotPreflightFailureArmed = true;
    }

    internal bool IsAggregateLoadPostSlotPreflightFailureArmed() =>
        _aggregateLoadPostSlotPreflightFailureArmed;

    internal int GetAggregateLoadPostSlotPreflightFailureCount() =>
        _aggregateLoadPostSlotPreflightFailureCount;

    partial void ProbeAggregateLoadPostOwnershipPreCommitFailure()
    {
        if (!_aggregateLoadPostOwnershipPreCommitFailureArmed)
            return;

        _aggregateLoadPostOwnershipPreCommitFailureArmed = false;
        _aggregateLoadPostOwnershipPreCommitFailureCount++;
        throw new InvalidOperationException(
            AggregateLoadPostOwnershipPreCommitFailureMessage);
    }

    internal void ArmNextAggregateLoadPostOwnershipPreCommitFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadPostOwnershipPreCommitFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load post-ownership pre-commit failure probe is already armed.");
        }

        _aggregateLoadPostOwnershipPreCommitFailureArmed = true;
    }

    internal bool IsAggregateLoadPostOwnershipPreCommitFailureArmed() =>
        _aggregateLoadPostOwnershipPreCommitFailureArmed;

    internal int GetAggregateLoadPostOwnershipPreCommitFailureCount() =>
        _aggregateLoadPostOwnershipPreCommitFailureCount;
}

public partial class ToolManager
{
    internal Godot.Collections.Dictionary ProbeToolLoadCommitBoundaryGenerationMismatch()
    {
        const string ExpectedFailureMessage =
            "A load participant generation changed while entering commit.";
        RoadBuilder builder = _roadBuilder ?? throw new InvalidOperationException(
            "ToolManager must have a RoadBuilder before probing the load commit boundary.");
        ToolType retainedCurrentTool = _currentTool;
        RoadType retainedSelectedRoadType = builder.SelectedRoadType;
        bool retainedIsPlacing = builder.IsPlacing;
        int retainedFixedCornerCount = builder.FixedCornerCount;
        RoadPathDraft? retainedDraft = builder.CurrentDraft;
        int retainedUndoCount = builder.GetUndoEditCount();
        int retainedRedoCount = builder.GetRedoEditCount();
        bool retainedCanUndo = builder.CanUndo;
        bool retainedCanRedo = builder.CanRedo;
        var graphPlan = new ToolTrackingLoadCommitPlan("road-graph");
        var rendererPlan = new ToolTrackingLoadCommitPlan("road-presentation");
        var slotPlan = new ToolTrackingLoadCommitPlan("slot-target");
        bool planWasCurrent = false;
        bool planBecameStale = false;
        bool failedWhileEnteringCommit = false;
        bool toolAdmissionReacquired = false;
        bool builderAdmissionReacquired = false;
        string exceptionType = string.Empty;
        string exceptionMessage = string.Empty;
        int commitLeaseCount = 0;
        int boundaryCount = 0;
        int markCommittedCount = 0;

        using (ToolLoadAdmission admission = BeginLoadAdmission())
        {
            INonThrowingLoadCommitPlan toolPlan = PreflightFullReset(admission);
            planWasCurrent = toolPlan.IsGenerationCurrent;
            var operation = new ToolBoundaryInvalidatingLease(admission.Dispose);
            using (var aggregate = new PreparedAggregateLoad([
                graphPlan,
                toolPlan,
                rendererPlan,
                slotPlan]))
            {
                try
                {
                    aggregate.Commit(operation);
                }
                catch (Exception exception)
                {
                    exceptionType = exception.GetType().Name;
                    exceptionMessage = exception.Message;
                    failedWhileEnteringCommit = exception is LoadPreflightInvalidException &&
                        string.Equals(
                            exception.Message,
                            ExpectedFailureMessage,
                            StringComparison.Ordinal);
                }

                planBecameStale = !toolPlan.IsGenerationCurrent;
                commitLeaseCount = operation.CommitLeaseCount;
                boundaryCount = operation.BoundaryCount;
                markCommittedCount = operation.MarkCommittedCount;
            }
        }

        using (ToolLoadAdmission reacquired = BeginLoadAdmission())
            toolAdmissionReacquired = true;
        using (RoadBuilder.RoadBuilderLoadAdmission reacquired =
            builder.BeginFullResetAdmission())
        {
            builderAdmissionReacquired = true;
        }

        return new Godot.Collections.Dictionary
        {
            ["failedWhileEnteringCommit"] = failedWhileEnteringCommit,
            ["exceptionType"] = exceptionType,
            ["exceptionMessage"] = exceptionMessage,
            ["planWasCurrent"] = planWasCurrent,
            ["planBecameStale"] = planBecameStale,
            ["commitLeaseCount"] = commitLeaseCount,
            ["boundaryCount"] = boundaryCount,
            ["markCommittedCount"] = markCommittedCount,
            ["graphCommitCount"] = graphPlan.CommitCount,
            ["rendererCommitCount"] = rendererPlan.CommitCount,
            ["slotCommitCount"] = slotPlan.CommitCount,
            ["graphDisposeCount"] = graphPlan.DisposeCount,
            ["rendererDisposeCount"] = rendererPlan.DisposeCount,
            ["slotDisposeCount"] = slotPlan.DisposeCount,
            ["toolAdmissionReacquired"] = toolAdmissionReacquired,
            ["builderAdmissionReacquired"] = builderAdmissionReacquired,
            ["currentToolPreserved"] = retainedCurrentTool == _currentTool,
            ["selectedRoadTypePreserved"] =
                retainedSelectedRoadType == builder.SelectedRoadType,
            ["placementPreserved"] =
                retainedIsPlacing == builder.IsPlacing &&
                ReferenceEquals(retainedDraft, builder.CurrentDraft),
            ["fixedCornersPreserved"] =
                retainedFixedCornerCount == builder.FixedCornerCount,
            ["historyPreserved"] =
                retainedUndoCount == builder.GetUndoEditCount() &&
                retainedRedoCount == builder.GetRedoEditCount() &&
                retainedCanUndo == builder.CanUndo &&
                retainedCanRedo == builder.CanRedo,
        };
    }

    private sealed class ToolBoundaryInvalidatingLease(Action invalidate) : IStorageOperationLease
    {
        public string OperationToken => "tool-generation-mismatch";
        public SaveOperationKind Kind => SaveOperationKind.Load;
        internal int CommitLeaseCount { get; private set; }
        internal int BoundaryCount { get; private set; }
        internal int MarkCommittedCount { get; private set; }

        public void ThrowIfCancellationRequested() { }

        public void AcquireCommitLease()
        {
            CommitLeaseCount++;
        }

        public void CrossCommitBoundary(Action boundaryAction)
        {
            BoundaryCount++;
            invalidate();
            boundaryAction();
        }

        public void MarkCommitted()
        {
            MarkCommittedCount++;
        }

        public void EnterCommitBoundary()
        {
            AcquireCommitLease();
            CrossCommitBoundary(static () => { });
            MarkCommitted();
        }
    }

    private sealed class ToolTrackingLoadCommitPlan(string participantID) :
        INonThrowingLoadCommitPlan
    {
        private bool _disposed;

        public string ParticipantID { get; } = participantID;
        public bool IsGenerationCurrent => !_disposed && CommitCount == 0;
        internal int CommitCount { get; private set; }
        internal int DisposeCount { get; private set; }

        public void CommitReferences()
        {
            CommitCount++;
        }

        public IReadOnlyList<string> PublishNotifications() => [];
        public void CompleteCommit() { }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            DisposeCount++;
        }
    }
}

public partial class RoadRenderer
{
    internal const string AggregateLoadRoadMeshFactoryFailureMessage =
        "Injected aggregate Load CreateRoadMesh index enumeration failure.";
    internal const string AggregateLoadNodeBatchFactoryFailureMessage =
        "Injected aggregate Load CreateNodeBatch marker read failure.";
    internal const string AggregateLoadResourcePreflightFailureMessage =
        "Injected aggregate Load road presentation resource preflight failure.";

    private bool _aggregateLoadRoadMeshFactoryFailureArmed;
    private int _aggregateLoadRoadMeshFactoryFailureCount;
    private int _aggregateLoadRoadMeshFactoryIndexEnumerationCount;
    private bool _aggregateLoadNodeBatchFactoryFailureArmed;
    private int _aggregateLoadNodeBatchFactoryFailureCount;
    private int _aggregateLoadNodeBatchFactoryMarkerReadCount;
    private bool _aggregateLoadResourcePreflightFailureArmed;
    private int _aggregateLoadResourcePreflightFailureCount;

    partial void ProbeAggregateLoadRoadMeshFactoryFailure(
        ref IReadOnlyCollection<int> roadIndices)
    {
        if (!_aggregateLoadRoadMeshFactoryFailureArmed)
            return;

        _aggregateLoadRoadMeshFactoryFailureArmed = false;
        _aggregateLoadRoadMeshFactoryFailureCount++;
        roadIndices = new AggregateLoadRoadMeshFactoryFailureIndices(this);
    }

    internal void ArmNextAggregateLoadRoadMeshFactoryFailure()
    {
        if (!IsPresentationReady() || _loadAdmission is not null)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready and idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadRoadMeshFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load road-mesh factory failure probe is already armed.");
        }
        if (_aggregateLoadNodeBatchFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load node-batch factory failure probe is already armed.");
        }
        if (_aggregateLoadResourcePreflightFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load resource preflight failure probe is already armed.");
        }

        _aggregateLoadRoadMeshFactoryFailureArmed = true;
    }

    internal bool IsAggregateLoadRoadMeshFactoryFailureArmed() =>
        _aggregateLoadRoadMeshFactoryFailureArmed;

    internal int GetAggregateLoadRoadMeshFactoryFailureCount() =>
        _aggregateLoadRoadMeshFactoryFailureCount;

    internal int GetAggregateLoadRoadMeshFactoryIndexEnumerationCount() =>
        _aggregateLoadRoadMeshFactoryIndexEnumerationCount;

    partial void ProbeAggregateLoadNodeBatchFactoryFailure(
        ref IReadOnlyList<RoadRendererNodeMarker> nodeMarkers)
    {
        if (!_aggregateLoadNodeBatchFactoryFailureArmed)
            return;

        _aggregateLoadNodeBatchFactoryFailureArmed = false;
        _aggregateLoadNodeBatchFactoryFailureCount++;
        nodeMarkers = new AggregateLoadNodeBatchFactoryFailureMarkers(this);
    }

    internal void ArmNextAggregateLoadNodeBatchFactoryFailure()
    {
        if (!IsPresentationReady() || _loadAdmission is not null)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready and idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadNodeBatchFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load node-batch factory failure probe is already armed.");
        }
        if (_aggregateLoadResourcePreflightFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load resource preflight failure probe is already armed.");
        }
        if (_aggregateLoadRoadMeshFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load road-mesh factory failure probe is already armed.");
        }

        _aggregateLoadNodeBatchFactoryFailureArmed = true;
    }

    internal bool IsAggregateLoadNodeBatchFactoryFailureArmed() =>
        _aggregateLoadNodeBatchFactoryFailureArmed;

    internal int GetAggregateLoadNodeBatchFactoryFailureCount() =>
        _aggregateLoadNodeBatchFactoryFailureCount;

    internal int GetAggregateLoadNodeBatchFactoryMarkerReadCount() =>
        _aggregateLoadNodeBatchFactoryMarkerReadCount;

    partial void ProbeAggregateLoadResourcePreflightFailure()
    {
        if (!_aggregateLoadResourcePreflightFailureArmed)
            return;

        _aggregateLoadResourcePreflightFailureArmed = false;
        _aggregateLoadResourcePreflightFailureCount++;
        throw new InvalidOperationException(
            AggregateLoadResourcePreflightFailureMessage);
    }

    internal void ArmNextAggregateLoadResourcePreflightFailure()
    {
        if (!IsPresentationReady() || _loadAdmission is not null)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready and idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadResourcePreflightFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load resource preflight failure probe is already armed.");
        }
        if (_aggregateLoadNodeBatchFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load node-batch factory failure probe is already armed.");
        }
        if (_aggregateLoadRoadMeshFactoryFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load road-mesh factory failure probe is already armed.");
        }

        _aggregateLoadResourcePreflightFailureArmed = true;
    }

    internal bool IsAggregateLoadResourcePreflightFailureArmed() =>
        _aggregateLoadResourcePreflightFailureArmed;

    internal int GetAggregateLoadResourcePreflightFailureCount() =>
        _aggregateLoadResourcePreflightFailureCount;

    private sealed class AggregateLoadRoadMeshFactoryFailureIndices(RoadRenderer owner)
        : IReadOnlyCollection<int>
    {
        public int Count => 1;

        public IEnumerator<int> GetEnumerator()
        {
            owner._aggregateLoadRoadMeshFactoryIndexEnumerationCount++;
            throw new InvalidOperationException(
                AggregateLoadRoadMeshFactoryFailureMessage);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class AggregateLoadNodeBatchFactoryFailureMarkers(RoadRenderer owner)
        : IReadOnlyList<RoadRendererNodeMarker>
    {
        public int Count => 1;

        public RoadRendererNodeMarker this[int index]
        {
            get
            {
                owner._aggregateLoadNodeBatchFactoryMarkerReadCount++;
                throw new InvalidOperationException(
                    AggregateLoadNodeBatchFactoryFailureMessage);
            }
        }

        public IEnumerator<RoadRendererNodeMarker> GetEnumerator() =>
            ((IEnumerable<RoadRendererNodeMarker>)Array.Empty<RoadRendererNodeMarker>())
                .GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal Godot.Collections.Dictionary ProbeRoadSurfaceSnapshotPreflightFailure()
    {
        RoadGraph graph = _network ?? throw new InvalidOperationException(
            "RoadRenderer must have a graph before probing load preflight.");
        RoadGraphRevision revision = graph.CaptureRevision();
        Mesh? retainedRoadMesh = _roadBatchLayer.Mesh;
        MultiMesh retainedNodeBatch = _nodeBatchLayer.Multimesh;
        RoadSurfaceSnapshot? retainedSurface = _presentedSurface;
        long resourceCountBefore = Convert.ToInt64(
            Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
        bool failedAtSnapshot = false;
        string exceptionType = string.Empty;
        string exceptionMessage = string.Empty;
        int roadVertexCount;
        int nodeMarkerCount;

        using (RoadRendererLoadAdmission admission = BeginLoadAdmission())
        {
            RoadRendererPreparedLoad prepared = admission.Preparer.Prepare(revision);
            roadVertexCount = prepared.RoadVertices.Length;
            nodeMarkerCount = prepared.NodeMarkers.Length;

            // Deliberately violate the final preflight constructor input after both Godot resources exist.
            RoadRendererPreparedLoad invalid = prepared with { RoadSurface = null! };
            try
            {
                using INonThrowingLoadCommitPlan plan = PreflightPreparedLoad(
                    admission,
                    invalid,
                    revision.StateToken);
            }
            catch (Exception exception)
            {
                exceptionType = exception.GetType().Name;
                exceptionMessage = exception.Message;
                failedAtSnapshot = exception is ArgumentNullException argumentNullException &&
                    string.Equals(argumentNullException.ParamName, "prepared", StringComparison.Ordinal);
            }
        }

        long resourceCountAfter = Convert.ToInt64(
            Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
        return new Godot.Collections.Dictionary
        {
            ["failedAtSnapshot"] = failedAtSnapshot,
            ["exceptionType"] = exceptionType,
            ["exceptionMessage"] = exceptionMessage,
            ["roadVertexCount"] = roadVertexCount,
            ["nodeMarkerCount"] = nodeMarkerCount,
            ["resourceCountBefore"] = resourceCountBefore,
            ["resourceCountAfter"] = resourceCountAfter,
            ["roadMeshPreserved"] = ReferenceEquals(retainedRoadMesh, _roadBatchLayer.Mesh),
            ["nodeBatchPreserved"] = ReferenceEquals(retainedNodeBatch, _nodeBatchLayer.Multimesh),
            ["surfacePreserved"] = ReferenceEquals(retainedSurface, _presentedSurface),
        };
    }

    internal Godot.Collections.Dictionary ProbeUncommittedLoadPlanDisposal()
    {
        RoadGraph graph = _network ?? throw new InvalidOperationException(
            "RoadRenderer must have a graph before probing load plan disposal.");
        RoadGraphRevision revision = graph.CaptureRevision();
        Mesh? retainedRoadMesh = _roadBatchLayer.Mesh;
        MultiMesh retainedNodeBatch = _nodeBatchLayer.Multimesh;
        RoadSurfaceSnapshot? retainedSurface = _presentedSurface;
        long resourceCountBefore = Convert.ToInt64(
            Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
        bool planWasCurrent;
        bool planBecameStale;
        int roadVertexCount;
        int nodeMarkerCount;

        using (RoadRendererLoadAdmission admission = BeginLoadAdmission())
        {
            RoadRendererPreparedLoad prepared = admission.Preparer.Prepare(revision);
            roadVertexCount = prepared.RoadVertices.Length;
            nodeMarkerCount = prepared.NodeMarkers.Length;
            INonThrowingLoadCommitPlan plan = PreflightPreparedLoad(
                admission,
                prepared,
                revision.StateToken);
            planWasCurrent = plan.IsGenerationCurrent;
            plan.Dispose();
            planBecameStale = !plan.IsGenerationCurrent;
            plan.Dispose();
        }

        long resourceCountAfter = Convert.ToInt64(
            Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
        return new Godot.Collections.Dictionary
        {
            ["planWasCurrent"] = planWasCurrent,
            ["planBecameStale"] = planBecameStale,
            ["roadVertexCount"] = roadVertexCount,
            ["nodeMarkerCount"] = nodeMarkerCount,
            ["resourceCountBefore"] = resourceCountBefore,
            ["resourceCountAfter"] = resourceCountAfter,
            ["roadMeshPreserved"] = ReferenceEquals(retainedRoadMesh, _roadBatchLayer.Mesh),
            ["nodeBatchPreserved"] = ReferenceEquals(retainedNodeBatch, _nodeBatchLayer.Multimesh),
            ["surfacePreserved"] = ReferenceEquals(retainedSurface, _presentedSurface),
        };
    }

    internal Godot.Collections.Dictionary ProbeLoadCommitBoundaryGenerationMismatch()
    {
        const string ExpectedFailureMessage =
            "A load participant generation changed while entering commit.";
        RoadGraph graph = _network ?? throw new InvalidOperationException(
            "RoadRenderer must have a graph before probing the load commit boundary.");
        RoadGraphRevision revision = graph.CaptureRevision();
        Mesh? retainedRoadMesh = _roadBatchLayer.Mesh;
        MultiMesh retainedNodeBatch = _nodeBatchLayer.Multimesh;
        RoadSurfaceSnapshot? retainedSurface = _presentedSurface;
        RoadRenderToken? retainedDesiredToken = _presentationTokens.DesiredToken;
        RoadRenderToken? retainedPresentedToken = _presentationTokens.PresentedToken;
        long resourceCountBefore = Convert.ToInt64(
            Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
        var graphPlan = new TrackingLoadCommitPlan("road-graph");
        var toolPlan = new TrackingLoadCommitPlan("road-tools");
        var slotPlan = new TrackingLoadCommitPlan("slot-target");
        bool planWasCurrent = false;
        bool planBecameStale = false;
        bool failedWhileEnteringCommit = false;
        bool admissionReacquired = false;
        string exceptionType = string.Empty;
        string exceptionMessage = string.Empty;
        int roadVertexCount = 0;
        int nodeMarkerCount = 0;
        int commitLeaseCount = 0;
        int boundaryCount = 0;
        int markCommittedCount = 0;

        using (RoadRendererLoadAdmission admission = BeginLoadAdmission())
        {
            RoadRendererPreparedLoad prepared = admission.Preparer.Prepare(revision);
            roadVertexCount = prepared.RoadVertices.Length;
            nodeMarkerCount = prepared.NodeMarkers.Length;
            INonThrowingLoadCommitPlan rendererPlan = PreflightPreparedLoad(
                admission,
                prepared,
                revision.StateToken);
            planWasCurrent = rendererPlan.IsGenerationCurrent;
            var operation = new BoundaryInvalidatingLease(admission.Dispose);
            using (var aggregate = new PreparedAggregateLoad([
                graphPlan,
                toolPlan,
                rendererPlan,
                slotPlan]))
            {
                try
                {
                    aggregate.Commit(operation);
                }
                catch (Exception exception)
                {
                    exceptionType = exception.GetType().Name;
                    exceptionMessage = exception.Message;
                    failedWhileEnteringCommit = exception is LoadPreflightInvalidException &&
                        string.Equals(
                            exception.Message,
                            ExpectedFailureMessage,
                            StringComparison.Ordinal);
                }

                planBecameStale = !rendererPlan.IsGenerationCurrent;
                commitLeaseCount = operation.CommitLeaseCount;
                boundaryCount = operation.BoundaryCount;
                markCommittedCount = operation.MarkCommittedCount;
            }
        }

        using (RoadRendererLoadAdmission reacquired = BeginLoadAdmission())
            admissionReacquired = true;

        long resourceCountAfter = Convert.ToInt64(
            Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
        return new Godot.Collections.Dictionary
        {
            ["failedWhileEnteringCommit"] = failedWhileEnteringCommit,
            ["exceptionType"] = exceptionType,
            ["exceptionMessage"] = exceptionMessage,
            ["planWasCurrent"] = planWasCurrent,
            ["planBecameStale"] = planBecameStale,
            ["roadVertexCount"] = roadVertexCount,
            ["nodeMarkerCount"] = nodeMarkerCount,
            ["commitLeaseCount"] = commitLeaseCount,
            ["boundaryCount"] = boundaryCount,
            ["markCommittedCount"] = markCommittedCount,
            ["graphCommitCount"] = graphPlan.CommitCount,
            ["toolCommitCount"] = toolPlan.CommitCount,
            ["slotCommitCount"] = slotPlan.CommitCount,
            ["graphDisposeCount"] = graphPlan.DisposeCount,
            ["toolDisposeCount"] = toolPlan.DisposeCount,
            ["slotDisposeCount"] = slotPlan.DisposeCount,
            ["admissionReacquired"] = admissionReacquired,
            ["resourceCountBefore"] = resourceCountBefore,
            ["resourceCountAfter"] = resourceCountAfter,
            ["roadMeshPreserved"] = ReferenceEquals(retainedRoadMesh, _roadBatchLayer.Mesh),
            ["nodeBatchPreserved"] = ReferenceEquals(retainedNodeBatch, _nodeBatchLayer.Multimesh),
            ["surfacePreserved"] = ReferenceEquals(retainedSurface, _presentedSurface),
            ["tokensPreserved"] =
                retainedDesiredToken == _presentationTokens.DesiredToken &&
                retainedPresentedToken == _presentationTokens.PresentedToken,
        };
    }

    private sealed class BoundaryInvalidatingLease(Action invalidate) : IStorageOperationLease
    {
        public string OperationToken => "renderer-generation-mismatch";
        public SaveOperationKind Kind => SaveOperationKind.Load;
        internal int CommitLeaseCount { get; private set; }
        internal int BoundaryCount { get; private set; }
        internal int MarkCommittedCount { get; private set; }

        public void ThrowIfCancellationRequested() { }

        public void AcquireCommitLease()
        {
            CommitLeaseCount++;
        }

        public void CrossCommitBoundary(Action boundaryAction)
        {
            BoundaryCount++;
            invalidate();
            boundaryAction();
        }

        public void MarkCommitted()
        {
            MarkCommittedCount++;
        }

        public void EnterCommitBoundary()
        {
            AcquireCommitLease();
            CrossCommitBoundary(static () => { });
            MarkCommitted();
        }
    }

    private sealed class TrackingLoadCommitPlan(string participantID) : INonThrowingLoadCommitPlan
    {
        private bool _disposed;

        public string ParticipantID { get; } = participantID;
        public bool IsGenerationCurrent => !_disposed && CommitCount == 0;
        internal int CommitCount { get; private set; }
        internal int DisposeCount { get; private set; }

        public void CommitReferences()
        {
            CommitCount++;
        }

        public IReadOnlyList<string> PublishNotifications() => [];
        public void CompleteCommit() { }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            DisposeCount++;
        }
    }

    internal Godot.Collections.Dictionary ProbeNodeBatchFactoryFailure()
    {
        Mesh? retainedRoadMesh = _roadBatchLayer.Mesh;
        MultiMesh retainedNodeBatch = _nodeBatchLayer.Multimesh;
        RoadSurfaceSnapshot? retainedSurface = _presentedSurface;
        long resourceCountBefore = Convert.ToInt64(
            Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
        var markers = new ThrowingNodeMarkerList();
        bool failedInsideFactory = false;
        string exceptionType = string.Empty;
        string exceptionMessage = string.Empty;

        try
        {
            using MultiMesh batch = CreateNodeBatch(markers);
        }
        catch (Exception exception)
        {
            exceptionType = exception.GetType().Name;
            exceptionMessage = exception.Message;
            failedInsideFactory = exception is InvalidOperationException &&
                string.Equals(
                    exception.Message,
                    ThrowingNodeMarkerList.InjectedMessage,
                    StringComparison.Ordinal);
        }

        long resourceCountAfter = Convert.ToInt64(
            Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
        return new Godot.Collections.Dictionary
        {
            ["failedInsideFactory"] = failedInsideFactory,
            ["markerIndexerRead"] = markers.IndexerRead,
            ["exceptionType"] = exceptionType,
            ["exceptionMessage"] = exceptionMessage,
            ["resourceCountBefore"] = resourceCountBefore,
            ["resourceCountAfter"] = resourceCountAfter,
            ["roadMeshPreserved"] = ReferenceEquals(retainedRoadMesh, _roadBatchLayer.Mesh),
            ["nodeBatchPreserved"] = ReferenceEquals(retainedNodeBatch, _nodeBatchLayer.Multimesh),
            ["surfacePreserved"] = ReferenceEquals(retainedSurface, _presentedSurface),
        };
    }

    internal Godot.Collections.Dictionary ProbeRoadMeshOwnershipFailure()
    {
        Mesh? retainedRoadMesh = _roadBatchLayer.Mesh;
        MultiMesh retainedNodeBatch = _nodeBatchLayer.Multimesh;
        RoadSurfaceSnapshot? retainedSurface = _presentedSurface;
        long resourceCountBefore = Convert.ToInt64(
            Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
        var initializer = new ThrowingRoadMeshInitializer();
        bool failedInsideOwnershipBoundary = false;
        string exceptionType = string.Empty;
        string exceptionMessage = string.Empty;

        try
        {
            using ArrayMesh mesh = InitializeOwnedResource(
                new ArrayMesh(),
                initializer,
                static (_, state) => state.Throw());
        }
        catch (Exception exception)
        {
            exceptionType = exception.GetType().Name;
            exceptionMessage = exception.Message;
            failedInsideOwnershipBoundary = exception is InvalidOperationException &&
                string.Equals(
                    exception.Message,
                    ThrowingRoadMeshInitializer.InjectedMessage,
                    StringComparison.Ordinal);
        }

        long resourceCountAfter = Convert.ToInt64(
            Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
        return new Godot.Collections.Dictionary
        {
            ["failedInsideOwnershipBoundary"] = failedInsideOwnershipBoundary,
            ["initializerEntered"] = initializer.Entered,
            ["exceptionType"] = exceptionType,
            ["exceptionMessage"] = exceptionMessage,
            ["resourceCountBefore"] = resourceCountBefore,
            ["resourceCountAfter"] = resourceCountAfter,
            ["roadMeshPreserved"] = ReferenceEquals(retainedRoadMesh, _roadBatchLayer.Mesh),
            ["nodeBatchPreserved"] = ReferenceEquals(retainedNodeBatch, _nodeBatchLayer.Multimesh),
            ["surfacePreserved"] = ReferenceEquals(retainedSurface, _presentedSurface),
        };
    }

    private sealed class ThrowingRoadMeshInitializer
    {
        internal const string InjectedMessage = "Injected road mesh initialization failure.";

        internal bool Entered { get; private set; }

        internal void Throw()
        {
            Entered = true;
            throw new InvalidOperationException(InjectedMessage);
        }
    }

    private sealed class ThrowingNodeMarkerList : IReadOnlyList<RoadRendererNodeMarker>
    {
        internal const string InjectedMessage = "Injected CreateNodeBatch marker read failure.";

        internal bool IndexerRead { get; private set; }

        public int Count => 1;

        public RoadRendererNodeMarker this[int index]
        {
            get
            {
                IndexerRead = true;
                throw new InvalidOperationException(InjectedMessage);
            }
        }

        public IEnumerator<RoadRendererNodeMarker> GetEnumerator() =>
            ((IEnumerable<RoadRendererNodeMarker>)Array.Empty<RoadRendererNodeMarker>())
                .GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
