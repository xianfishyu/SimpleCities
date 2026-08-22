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

    public long GetObjectResourceCount() => Convert.ToInt64(
        Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
}

public partial class SaveManager
{
    internal const string AggregateLoadPostRendererPreflightFailureMessage =
        "Injected aggregate Load failure after road presentation preflight.";
    internal const string AggregateLoadPostSlotPreflightFailureMessage =
        "Injected aggregate Load failure after slot-target preflight.";

    private bool _aggregateLoadPostRendererPreflightFailureArmed;
    private int _aggregateLoadPostRendererPreflightFailureCount;
    private bool _aggregateLoadPostSlotPreflightFailureArmed;
    private int _aggregateLoadPostSlotPreflightFailureCount;

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
}

public partial class RoadRenderer
{
    internal const string AggregateLoadResourcePreflightFailureMessage =
        "Injected aggregate Load road presentation resource preflight failure.";

    private bool _aggregateLoadResourcePreflightFailureArmed;
    private int _aggregateLoadResourcePreflightFailureCount;

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

        _aggregateLoadResourcePreflightFailureArmed = true;
    }

    internal bool IsAggregateLoadResourcePreflightFailureArmed() =>
        _aggregateLoadResourcePreflightFailureArmed;

    internal int GetAggregateLoadResourcePreflightFailureCount() =>
        _aggregateLoadResourcePreflightFailureCount;

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
