using Godot;
using System;

public partial class RoadLoadPreflightResourceFailureProbe : RefCounted
{
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
}

public partial class RoadRenderer
{
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
}
