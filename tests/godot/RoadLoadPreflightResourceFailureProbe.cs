using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class RoadLoadPreflightResourceFailureProbe : RefCounted
{
    private RoadRenderer? _renderer;
    private SaveManager? _saveManager;

    public void FlushPendingManagedFinalizers()
    {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

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

    public void ArmAggregateLoadRendererAdmissionConstructionFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextAggregateLoadRendererAdmissionConstructionFailure();
        _renderer = renderer;
    }

    public bool IsAggregateLoadRendererAdmissionConstructionFailureArmed() =>
        _renderer?.IsAggregateLoadRendererAdmissionConstructionFailureArmed() ?? false;

    public int GetAggregateLoadRendererAdmissionConstructionFailureCount() =>
        _renderer?.GetAggregateLoadRendererAdmissionConstructionFailureCount() ?? 0;

    public string GetAggregateLoadRendererAdmissionConstructionFailureMessage() =>
        RoadRenderer.AggregateLoadRendererAdmissionConstructionFailureMessage;

    public void ArmAggregateLoadRendererAdmissionPublicationFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextAggregateLoadRendererAdmissionPublicationFailure();
        _renderer = renderer;
    }

    public bool IsAggregateLoadRendererAdmissionPublicationFailureArmed() =>
        _renderer?.IsAggregateLoadRendererAdmissionPublicationFailureArmed() ?? false;

    public int GetAggregateLoadRendererAdmissionPublicationFailureCount() =>
        _renderer?.GetAggregateLoadRendererAdmissionPublicationFailureCount() ?? 0;

    public string GetAggregateLoadRendererAdmissionPublicationFailureMessage() =>
        RoadRenderer.AggregateLoadRendererAdmissionPublicationFailureMessage;

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

    public void ArmAggregateLoadReservedRenderTokenFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextAggregateLoadReservedRenderTokenFailure();
        _renderer = renderer;
    }

    public bool IsAggregateLoadReservedRenderTokenFailureArmed() =>
        _renderer?.IsAggregateLoadReservedRenderTokenFailureArmed() ?? false;

    public int GetAggregateLoadReservedRenderTokenFailureCount() =>
        _renderer?.GetAggregateLoadReservedRenderTokenFailureCount() ?? 0;

    public string GetAggregateLoadReservedRenderTokenFailureMessage() =>
        RoadRenderer.AggregateLoadReservedRenderTokenFailureMessage;

    public void ArmAggregateLoadRoadSurfaceSnapshotFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextAggregateLoadRoadSurfaceSnapshotFailure();
        _renderer = renderer;
    }

    public bool IsAggregateLoadRoadSurfaceSnapshotFailureArmed() =>
        _renderer?.IsAggregateLoadRoadSurfaceSnapshotFailureArmed() ?? false;

    public int GetAggregateLoadRoadSurfaceSnapshotFailureCount() =>
        _renderer?.GetAggregateLoadRoadSurfaceSnapshotFailureCount() ?? 0;

    public string GetAggregateLoadRoadSurfaceSnapshotFailureMessage() =>
        new ArgumentNullException("prepared").Message;

    public void ArmAggregateLoadCommitPlanConstructionFailure(RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.ArmNextAggregateLoadCommitPlanConstructionFailure();
        _renderer = renderer;
    }

    public bool IsAggregateLoadCommitPlanConstructionFailureArmed() =>
        _renderer?.IsAggregateLoadCommitPlanConstructionFailureArmed() ?? false;

    public int GetAggregateLoadCommitPlanConstructionFailureCount() =>
        _renderer?.GetAggregateLoadCommitPlanConstructionFailureCount() ?? 0;

    public string GetAggregateLoadCommitPlanConstructionFailureMessage() =>
        RoadRenderer.AggregateLoadCommitPlanConstructionFailureMessage;

    public bool IsAggregateLoadResourcePreflightFailureArmed() =>
        _renderer?.IsAggregateLoadResourcePreflightFailureArmed() ?? false;

    public int GetAggregateLoadResourcePreflightFailureCount() =>
        _renderer?.GetAggregateLoadResourcePreflightFailureCount() ?? 0;

    public string GetAggregateLoadResourcePreflightFailureMessage() =>
        RoadRenderer.AggregateLoadResourcePreflightFailureMessage;

    public void ArmAggregateLoadPostRendererAdmissionFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadPostRendererAdmissionFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadPostRendererAdmissionFailureArmed() =>
        _saveManager?.IsAggregateLoadPostRendererAdmissionFailureArmed() ?? false;

    public int GetAggregateLoadPostRendererAdmissionFailureCount() =>
        _saveManager?.GetAggregateLoadPostRendererAdmissionFailureCount() ?? 0;

    public string GetAggregateLoadPostRendererAdmissionFailureMessage() =>
        SaveManager.AggregateLoadPostRendererAdmissionFailureMessage;

    public void ArmAggregateLoadPostParticipantCaptureFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadPostParticipantCaptureFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadPostParticipantCaptureFailureArmed() =>
        _saveManager?.IsAggregateLoadPostParticipantCaptureFailureArmed() ?? false;

    public int GetAggregateLoadPostParticipantCaptureFailureCount() =>
        _saveManager?.GetAggregateLoadPostParticipantCaptureFailureCount() ?? 0;

    public int GetAggregateLoadPostParticipantCaptureParticipantCount() =>
        _saveManager?.GetAggregateLoadPostParticipantCaptureParticipantCount() ?? 0;

    public string GetAggregateLoadPostParticipantCaptureFailureMessage() =>
        SaveManager.AggregateLoadPostParticipantCaptureFailureMessage;

    public void ArmAggregateLoadPostPreparePhaseFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadPostPreparePhaseFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadPostPreparePhaseFailureArmed() =>
        _saveManager?.IsAggregateLoadPostPreparePhaseFailureArmed() ?? false;

    public int GetAggregateLoadPostPreparePhaseFailureCount() =>
        _saveManager?.GetAggregateLoadPostPreparePhaseFailureCount() ?? 0;

    public int GetAggregateLoadPostPreparePhaseObservedPhase() =>
        _saveManager?.GetAggregateLoadPostPreparePhaseObservedPhase() ?? -1;

    public string GetAggregateLoadPostPreparePhaseFailureMessage() =>
        SaveManager.AggregateLoadPostPreparePhaseFailureMessage;

    public void ArmAggregateLoadWorkerEntryFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadWorkerEntryFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadWorkerEntryFailureArmed() =>
        _saveManager?.IsAggregateLoadWorkerEntryFailureArmed() ?? false;

    public int GetAggregateLoadWorkerEntryFailureCount() =>
        _saveManager?.GetAggregateLoadWorkerEntryFailureCount() ?? 0;

    public bool DidAggregateLoadWorkerEntryFailureRunOffMainThread() =>
        _saveManager?.DidAggregateLoadWorkerEntryFailureRunOffMainThread() ?? false;

    public string GetAggregateLoadWorkerEntryFailureMessage() =>
        SaveManager.AggregateLoadWorkerEntryFailureMessage;

    public void ArmAggregateLoadPostSlotPreparationFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadPostSlotPreparationFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadPostSlotPreparationFailureArmed() =>
        _saveManager?.IsAggregateLoadPostSlotPreparationFailureArmed() ?? false;

    public int GetAggregateLoadPostSlotPreparationFailureCount() =>
        _saveManager?.GetAggregateLoadPostSlotPreparationFailureCount() ?? 0;

    public bool DidAggregateLoadPostSlotPreparationFailureRunOffMainThread() =>
        _saveManager?.DidAggregateLoadPostSlotPreparationFailureRunOffMainThread() ?? false;

    public string GetAggregateLoadPostSlotPreparationObservedSlotID() =>
        _saveManager?.GetAggregateLoadPostSlotPreparationObservedSlotID() ?? string.Empty;

    public int GetAggregateLoadPostSlotPreparationParticipantCount() =>
        _saveManager?.GetAggregateLoadPostSlotPreparationParticipantCount() ?? -1;

    public string GetAggregateLoadPostSlotPreparationFailureMessage() =>
        SaveManager.AggregateLoadPostSlotPreparationFailureMessage;

    public void ArmAggregateLoadPostGraphStateLookupFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadPostGraphStateLookupFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadPostGraphStateLookupFailureArmed() =>
        _saveManager?.IsAggregateLoadPostGraphStateLookupFailureArmed() ?? false;

    public int GetAggregateLoadPostGraphStateLookupFailureCount() =>
        _saveManager?.GetAggregateLoadPostGraphStateLookupFailureCount() ?? 0;

    public bool DidAggregateLoadPostGraphStateLookupFailureRunOffMainThread() =>
        _saveManager?.DidAggregateLoadPostGraphStateLookupFailureRunOffMainThread() ?? false;

    public string GetAggregateLoadPostGraphStateLookupObservedStateTypeName() =>
        _saveManager?.GetAggregateLoadPostGraphStateLookupObservedStateTypeName() ?? string.Empty;

    public string GetAggregateLoadPostGraphStateLookupFailureMessage() =>
        SaveManager.AggregateLoadPostGraphStateLookupFailureMessage;

    public void ArmAggregateLoadRendererWorkerPrepareFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadRendererWorkerPrepareFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadRendererWorkerPrepareFailureArmed() =>
        _saveManager?.IsAggregateLoadRendererWorkerPrepareFailureArmed() ?? false;

    public int GetAggregateLoadRendererWorkerPrepareFailureCount() =>
        _saveManager?.GetAggregateLoadRendererWorkerPrepareFailureCount() ?? 0;

    public string GetAggregateLoadRendererWorkerPrepareFailureMessage() =>
        SaveManager.AggregateLoadRendererWorkerPrepareFailureMessage;

    public void ArmAggregateLoadPostRendererWorkerPreparationFailure(
        SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadPostRendererWorkerPreparationFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadPostRendererWorkerPreparationFailureArmed() =>
        _saveManager?.IsAggregateLoadPostRendererWorkerPreparationFailureArmed() ?? false;

    public int GetAggregateLoadPostRendererWorkerPreparationFailureCount() =>
        _saveManager?.GetAggregateLoadPostRendererWorkerPreparationFailureCount() ?? 0;

    public bool DidAggregateLoadPostRendererWorkerPreparationFailureRunOffMainThread() =>
        _saveManager?.DidAggregateLoadPostRendererWorkerPreparationFailureRunOffMainThread() ??
        false;

    public int GetAggregateLoadPostRendererWorkerPreparationRoadVertexCount() =>
        _saveManager?.GetAggregateLoadPostRendererWorkerPreparationRoadVertexCount() ?? -1;

    public int GetAggregateLoadPostRendererWorkerPreparationSurfacePrimitiveCount() =>
        _saveManager?.GetAggregateLoadPostRendererWorkerPreparationSurfacePrimitiveCount() ?? -1;

    public int GetAggregateLoadPostRendererWorkerPreparationNodeMarkerCount() =>
        _saveManager?.GetAggregateLoadPostRendererWorkerPreparationNodeMarkerCount() ?? -1;

    public string GetAggregateLoadPostRendererWorkerPreparationFailureMessage() =>
        SaveManager.AggregateLoadPostRendererWorkerPreparationFailureMessage;

    public void ArmAggregateLoadPostPreparedWorkReturnFailure(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadPostPreparedWorkReturnFailure();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadPostPreparedWorkReturnFailureArmed() =>
        _saveManager?.IsAggregateLoadPostPreparedWorkReturnFailureArmed() ?? false;

    public int GetAggregateLoadPostPreparedWorkReturnFailureCount() =>
        _saveManager?.GetAggregateLoadPostPreparedWorkReturnFailureCount() ?? 0;

    public bool DidAggregateLoadPostPreparedWorkReturnFailureRunOnMainThread() =>
        _saveManager?.DidAggregateLoadPostPreparedWorkReturnFailureRunOnMainThread() ?? false;

    public string GetAggregateLoadPostPreparedWorkReturnObservedSlotID() =>
        _saveManager?.GetAggregateLoadPostPreparedWorkReturnObservedSlotID() ?? string.Empty;

    public int GetAggregateLoadPostPreparedWorkReturnParticipantCount() =>
        _saveManager?.GetAggregateLoadPostPreparedWorkReturnParticipantCount() ?? -1;

    public int GetAggregateLoadPostPreparedWorkReturnRoadVertexCount() =>
        _saveManager?.GetAggregateLoadPostPreparedWorkReturnRoadVertexCount() ?? -1;

    public int GetAggregateLoadPostPreparedWorkReturnSurfacePrimitiveCount() =>
        _saveManager?.GetAggregateLoadPostPreparedWorkReturnSurfacePrimitiveCount() ?? -1;

    public int GetAggregateLoadPostPreparedWorkReturnNodeMarkerCount() =>
        _saveManager?.GetAggregateLoadPostPreparedWorkReturnNodeMarkerCount() ?? -1;

    public string GetAggregateLoadPostPreparedWorkReturnFailureMessage() =>
        SaveManager.AggregateLoadPostPreparedWorkReturnFailureMessage;

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

    public void ArmAggregateLoadGraphCommitBoundaryGenerationMismatch(
        SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadGraphCommitBoundaryGenerationMismatch();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadGraphCommitBoundaryGenerationMismatchArmed() =>
        _saveManager?.IsAggregateLoadGraphCommitBoundaryGenerationMismatchArmed() ?? false;

    public int GetAggregateLoadGraphCommitBoundaryGenerationMismatchCount() =>
        _saveManager?.GetAggregateLoadGraphCommitBoundaryGenerationMismatchCount() ?? 0;

    public int GetAggregateLoadGraphCommitBoundaryCount() =>
        _saveManager?.GetAggregateLoadGraphCommitBoundaryCount() ?? 0;

    public int GetAggregateLoadGraphMarkCommittedCount() =>
        _saveManager?.GetAggregateLoadGraphMarkCommittedCount() ?? 0;

    public string GetAggregateLoadGraphCommitBoundaryGenerationMismatchMessage() =>
        SaveManager.AggregateLoadGraphCommitBoundaryGenerationMismatchMessage;

    public void ArmAggregateLoadRendererCommitBoundaryGenerationMismatch(
        SaveManager saveManager,
        RoadRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        ArgumentNullException.ThrowIfNull(renderer);
        saveManager.ArmNextAggregateLoadRendererCommitBoundaryGenerationMismatch(renderer);
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadRendererCommitBoundaryGenerationMismatchArmed() =>
        _saveManager?.IsAggregateLoadRendererCommitBoundaryGenerationMismatchArmed() ?? false;

    public int GetAggregateLoadRendererCommitBoundaryGenerationMismatchCount() =>
        _saveManager?.GetAggregateLoadRendererCommitBoundaryGenerationMismatchCount() ?? 0;

    public int GetAggregateLoadRendererCommitBoundaryCount() =>
        _saveManager?.GetAggregateLoadRendererCommitBoundaryCount() ?? 0;

    public int GetAggregateLoadRendererMarkCommittedCount() =>
        _saveManager?.GetAggregateLoadRendererMarkCommittedCount() ?? 0;

    public string GetAggregateLoadRendererCommitBoundaryGenerationMismatchMessage() =>
        SaveManager.AggregateLoadRendererCommitBoundaryGenerationMismatchMessage;

    public void ArmAggregateLoadToolCommitBoundaryGenerationMismatch(
        SaveManager saveManager,
        ToolManager toolManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        ArgumentNullException.ThrowIfNull(toolManager);
        saveManager.ArmNextAggregateLoadToolCommitBoundaryGenerationMismatch(toolManager);
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadToolCommitBoundaryGenerationMismatchArmed() =>
        _saveManager?.IsAggregateLoadToolCommitBoundaryGenerationMismatchArmed() ?? false;

    public int GetAggregateLoadToolCommitBoundaryGenerationMismatchCount() =>
        _saveManager?.GetAggregateLoadToolCommitBoundaryGenerationMismatchCount() ?? 0;

    public int GetAggregateLoadToolCommitBoundaryCount() =>
        _saveManager?.GetAggregateLoadToolCommitBoundaryCount() ?? 0;

    public int GetAggregateLoadToolMarkCommittedCount() =>
        _saveManager?.GetAggregateLoadToolMarkCommittedCount() ?? 0;

    public string GetAggregateLoadToolCommitBoundaryGenerationMismatchMessage() =>
        SaveManager.AggregateLoadToolCommitBoundaryGenerationMismatchMessage;

    public void ArmAggregateLoadSlotTargetCommitBoundaryGenerationMismatch(
        SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);
        saveManager.ArmNextAggregateLoadSlotTargetCommitBoundaryGenerationMismatch();
        _saveManager = saveManager;
    }

    public bool IsAggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed() =>
        _saveManager?.IsAggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed() ?? false;

    public int GetAggregateLoadSlotTargetCommitBoundaryGenerationMismatchCount() =>
        _saveManager?.GetAggregateLoadSlotTargetCommitBoundaryGenerationMismatchCount() ?? 0;

    public int GetAggregateLoadSlotTargetCommitBoundaryCount() =>
        _saveManager?.GetAggregateLoadSlotTargetCommitBoundaryCount() ?? 0;

    public int GetAggregateLoadSlotTargetMarkCommittedCount() =>
        _saveManager?.GetAggregateLoadSlotTargetMarkCommittedCount() ?? 0;

    public string GetAggregateLoadSlotTargetCommitBoundaryGenerationMismatchMessage() =>
        SaveManager.AggregateLoadSlotTargetCommitBoundaryGenerationMismatchMessage;

    public long GetObjectResourceCount() => Convert.ToInt64(
        Performance.GetMonitor(Performance.Monitor.ObjectResourceCount));
}

public partial class SaveManager
{
    internal const string AggregateLoadPostRendererAdmissionFailureMessage =
        "Injected aggregate Load failure after renderer admission publication.";
    internal const string AggregateLoadPostParticipantCaptureFailureMessage =
        "Injected aggregate Load failure after participant capture.";
    internal const string AggregateLoadPostPreparePhaseFailureMessage =
        "Injected aggregate Load failure after entering the Prepare phase.";
    internal const string AggregateLoadWorkerEntryFailureMessage =
        "Injected aggregate Load failure after entering the preparation worker.";
    internal const string AggregateLoadPostSlotPreparationFailureMessage =
        "Injected aggregate Load failure after slot preparation.";
    internal const string AggregateLoadPostGraphStateLookupFailureMessage =
        "Injected aggregate Load failure after graph-state lookup.";
    internal const string AggregateLoadRendererWorkerPrepareFailureMessage =
        "Injected aggregate Load renderer worker-prepare failure.";
    internal const string AggregateLoadPostRendererWorkerPreparationFailureMessage =
        "Injected aggregate Load failure after renderer worker preparation.";
    internal const string AggregateLoadPostPreparedWorkReturnFailureMessage =
        "Injected aggregate Load failure after the preparation worker returned.";
    internal const string AggregateLoadPostRendererPreflightFailureMessage =
        "Injected aggregate Load failure after road presentation preflight.";
    internal const string AggregateLoadPostSlotPreflightFailureMessage =
        "Injected aggregate Load failure after slot-target preflight.";
    internal const string AggregateLoadPostOwnershipPreCommitFailureMessage =
        "Injected aggregate Load failure after ownership transfer and before commit.";
    internal const string AggregateLoadGraphCommitBoundaryGenerationMismatchMessage =
        "A load participant generation changed while entering commit.";
    internal const string AggregateLoadRendererCommitBoundaryGenerationMismatchMessage =
        "A load participant generation changed while entering commit.";
    internal const string AggregateLoadToolCommitBoundaryGenerationMismatchMessage =
        "A load participant generation changed while entering commit.";
    internal const string AggregateLoadSlotTargetCommitBoundaryGenerationMismatchMessage =
        "A load participant generation changed while entering commit.";

    private bool _aggregateLoadPostRendererAdmissionFailureArmed;
    private int _aggregateLoadPostRendererAdmissionFailureCount;
    private bool _aggregateLoadPostParticipantCaptureFailureArmed;
    private int _aggregateLoadPostParticipantCaptureFailureCount;
    private int _aggregateLoadPostParticipantCaptureParticipantCount;
    private bool _aggregateLoadPostPreparePhaseFailureArmed;
    private int _aggregateLoadPostPreparePhaseFailureCount;
    private int _aggregateLoadPostPreparePhaseObservedPhase = -1;
    private bool _aggregateLoadWorkerEntryFailureArmed;
    private int _aggregateLoadWorkerEntryFailureCount;
    private bool _aggregateLoadWorkerEntryFailureRanOffMainThread;
    private bool _aggregateLoadPostSlotPreparationFailureArmed;
    private int _aggregateLoadPostSlotPreparationFailureCount;
    private bool _aggregateLoadPostSlotPreparationFailureRanOffMainThread;
    private string _aggregateLoadPostSlotPreparationObservedSlotID = string.Empty;
    private int _aggregateLoadPostSlotPreparationParticipantCount = -1;
    private bool _aggregateLoadPostGraphStateLookupFailureArmed;
    private int _aggregateLoadPostGraphStateLookupFailureCount;
    private bool _aggregateLoadPostGraphStateLookupFailureRanOffMainThread;
    private string _aggregateLoadPostGraphStateLookupObservedStateTypeName = string.Empty;
    private bool _aggregateLoadRendererWorkerPrepareFailureArmed;
    private int _aggregateLoadRendererWorkerPrepareFailureCount;
    private bool _aggregateLoadPostRendererWorkerPreparationFailureArmed;
    private int _aggregateLoadPostRendererWorkerPreparationFailureCount;
    private bool _aggregateLoadPostRendererWorkerPreparationFailureRanOffMainThread;
    private int _aggregateLoadPostRendererWorkerPreparationRoadVertexCount = -1;
    private int _aggregateLoadPostRendererWorkerPreparationSurfacePrimitiveCount = -1;
    private int _aggregateLoadPostRendererWorkerPreparationNodeMarkerCount = -1;
    private bool _aggregateLoadPostPreparedWorkReturnFailureArmed;
    private int _aggregateLoadPostPreparedWorkReturnFailureCount;
    private bool _aggregateLoadPostPreparedWorkReturnFailureRanOnMainThread;
    private string _aggregateLoadPostPreparedWorkReturnObservedSlotID = string.Empty;
    private int _aggregateLoadPostPreparedWorkReturnParticipantCount = -1;
    private int _aggregateLoadPostPreparedWorkReturnRoadVertexCount = -1;
    private int _aggregateLoadPostPreparedWorkReturnSurfacePrimitiveCount = -1;
    private int _aggregateLoadPostPreparedWorkReturnNodeMarkerCount = -1;
    private bool _aggregateLoadPostRendererPreflightFailureArmed;
    private int _aggregateLoadPostRendererPreflightFailureCount;
    private bool _aggregateLoadPostSlotPreflightFailureArmed;
    private int _aggregateLoadPostSlotPreflightFailureCount;
    private bool _aggregateLoadPostOwnershipPreCommitFailureArmed;
    private int _aggregateLoadPostOwnershipPreCommitFailureCount;
    private bool _aggregateLoadGraphCommitBoundaryGenerationMismatchArmed;
    private int _aggregateLoadGraphCommitBoundaryGenerationMismatchCount;
    private RoadGraph? _aggregateLoadGraphCommitBoundaryOwner;
    private AggregateLoadGraphBoundaryInvalidatingLease? _aggregateLoadGraphBoundaryLease;
    private bool _aggregateLoadRendererCommitBoundaryGenerationMismatchArmed;
    private int _aggregateLoadRendererCommitBoundaryGenerationMismatchCount;
    private RoadRenderer? _aggregateLoadRendererCommitBoundaryOwner;
    private AggregateLoadRendererBoundaryInvalidatingLease? _aggregateLoadRendererBoundaryLease;
    private bool _aggregateLoadToolCommitBoundaryGenerationMismatchArmed;
    private int _aggregateLoadToolCommitBoundaryGenerationMismatchCount;
    private ToolManager? _aggregateLoadToolCommitBoundaryOwner;
    private AggregateLoadToolBoundaryInvalidatingLease? _aggregateLoadToolBoundaryLease;
    private bool _aggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed;
    private int _aggregateLoadSlotTargetCommitBoundaryGenerationMismatchCount;
    private AggregateLoadSlotTargetBoundaryInvalidatingLease? _aggregateLoadSlotTargetBoundaryLease;

    partial void ProbeAggregateLoadPostRendererAdmissionFailure()
    {
        if (!_aggregateLoadPostRendererAdmissionFailureArmed)
            return;

        _aggregateLoadPostRendererAdmissionFailureArmed = false;
        _aggregateLoadPostRendererAdmissionFailureCount++;
        throw new InvalidOperationException(
            AggregateLoadPostRendererAdmissionFailureMessage);
    }

    internal void ArmNextAggregateLoadPostRendererAdmissionFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadPostRendererAdmissionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load post-renderer admission failure probe is already armed.");
        }

        _aggregateLoadPostRendererAdmissionFailureArmed = true;
    }

    internal bool IsAggregateLoadPostRendererAdmissionFailureArmed() =>
        _aggregateLoadPostRendererAdmissionFailureArmed;

    internal int GetAggregateLoadPostRendererAdmissionFailureCount() =>
        _aggregateLoadPostRendererAdmissionFailureCount;

    partial void ProbeAggregateLoadPostParticipantCaptureFailure(
        IReadOnlyList<CapturedLoadParticipant> loadParticipants)
    {
        if (!_aggregateLoadPostParticipantCaptureFailureArmed)
            return;

        ArgumentNullException.ThrowIfNull(loadParticipants);
        _aggregateLoadPostParticipantCaptureFailureArmed = false;
        _aggregateLoadPostParticipantCaptureFailureCount++;
        _aggregateLoadPostParticipantCaptureParticipantCount = loadParticipants.Count;
        throw new InvalidOperationException(
            AggregateLoadPostParticipantCaptureFailureMessage);
    }

    internal void ArmNextAggregateLoadPostParticipantCaptureFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadPostParticipantCaptureFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load post-participant capture failure probe is already armed.");
        }

        _aggregateLoadPostParticipantCaptureFailureArmed = true;
    }

    internal bool IsAggregateLoadPostParticipantCaptureFailureArmed() =>
        _aggregateLoadPostParticipantCaptureFailureArmed;

    internal int GetAggregateLoadPostParticipantCaptureFailureCount() =>
        _aggregateLoadPostParticipantCaptureFailureCount;

    internal int GetAggregateLoadPostParticipantCaptureParticipantCount() =>
        _aggregateLoadPostParticipantCaptureParticipantCount;

    partial void ProbeAggregateLoadPostPreparePhaseFailure(SaveOperationPhase phase)
    {
        if (!_aggregateLoadPostPreparePhaseFailureArmed)
            return;

        _aggregateLoadPostPreparePhaseFailureArmed = false;
        _aggregateLoadPostPreparePhaseFailureCount++;
        _aggregateLoadPostPreparePhaseObservedPhase = (int)phase;
        throw new InvalidOperationException(
            AggregateLoadPostPreparePhaseFailureMessage);
    }

    internal void ArmNextAggregateLoadPostPreparePhaseFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadPostPreparePhaseFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load post-Prepare phase failure probe is already armed.");
        }

        _aggregateLoadPostPreparePhaseObservedPhase = -1;
        _aggregateLoadPostPreparePhaseFailureArmed = true;
    }

    internal bool IsAggregateLoadPostPreparePhaseFailureArmed() =>
        _aggregateLoadPostPreparePhaseFailureArmed;

    internal int GetAggregateLoadPostPreparePhaseFailureCount() =>
        _aggregateLoadPostPreparePhaseFailureCount;

    internal int GetAggregateLoadPostPreparePhaseObservedPhase() =>
        _aggregateLoadPostPreparePhaseObservedPhase;

    partial void ProbeAggregateLoadWorkerEntryFailure()
    {
        if (!_aggregateLoadWorkerEntryFailureArmed)
            return;

        _aggregateLoadWorkerEntryFailureArmed = false;
        _aggregateLoadWorkerEntryFailureCount++;
        _aggregateLoadWorkerEntryFailureRanOffMainThread =
            _mainThreadID != 0 && System.Environment.CurrentManagedThreadId != _mainThreadID;
        throw new InvalidOperationException(
            AggregateLoadWorkerEntryFailureMessage);
    }

    internal void ArmNextAggregateLoadWorkerEntryFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadWorkerEntryFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load worker-entry failure probe is already armed.");
        }

        _aggregateLoadWorkerEntryFailureRanOffMainThread = false;
        _aggregateLoadWorkerEntryFailureArmed = true;
    }

    internal bool IsAggregateLoadWorkerEntryFailureArmed() =>
        _aggregateLoadWorkerEntryFailureArmed;

    internal int GetAggregateLoadWorkerEntryFailureCount() =>
        _aggregateLoadWorkerEntryFailureCount;

    internal bool DidAggregateLoadWorkerEntryFailureRunOffMainThread() =>
        _aggregateLoadWorkerEntryFailureRanOffMainThread;

    partial void ProbeAggregateLoadPostSlotPreparationFailure(PreparedSaveSlot slot)
    {
        if (!_aggregateLoadPostSlotPreparationFailureArmed)
            return;

        ArgumentNullException.ThrowIfNull(slot);
        _aggregateLoadPostSlotPreparationFailureArmed = false;
        _aggregateLoadPostSlotPreparationFailureCount++;
        _aggregateLoadPostSlotPreparationFailureRanOffMainThread =
            _mainThreadID != 0 && System.Environment.CurrentManagedThreadId != _mainThreadID;
        _aggregateLoadPostSlotPreparationObservedSlotID = slot.SlotID;
        _aggregateLoadPostSlotPreparationParticipantCount = slot.Participants.Count;
        throw new InvalidOperationException(
            AggregateLoadPostSlotPreparationFailureMessage);
    }

    internal void ArmNextAggregateLoadPostSlotPreparationFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadPostSlotPreparationFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load post-slot preparation failure probe is already armed.");
        }

        _aggregateLoadPostSlotPreparationFailureRanOffMainThread = false;
        _aggregateLoadPostSlotPreparationObservedSlotID = string.Empty;
        _aggregateLoadPostSlotPreparationParticipantCount = -1;
        _aggregateLoadPostSlotPreparationFailureArmed = true;
    }

    internal bool IsAggregateLoadPostSlotPreparationFailureArmed() =>
        _aggregateLoadPostSlotPreparationFailureArmed;

    internal int GetAggregateLoadPostSlotPreparationFailureCount() =>
        _aggregateLoadPostSlotPreparationFailureCount;

    internal bool DidAggregateLoadPostSlotPreparationFailureRunOffMainThread() =>
        _aggregateLoadPostSlotPreparationFailureRanOffMainThread;

    internal string GetAggregateLoadPostSlotPreparationObservedSlotID() =>
        _aggregateLoadPostSlotPreparationObservedSlotID;

    internal int GetAggregateLoadPostSlotPreparationParticipantCount() =>
        _aggregateLoadPostSlotPreparationParticipantCount;

    partial void ProbeAggregateLoadPostGraphStateLookupFailure(
        IPreparedSaveState graphState)
    {
        if (!_aggregateLoadPostGraphStateLookupFailureArmed)
            return;

        ArgumentNullException.ThrowIfNull(graphState);
        _aggregateLoadPostGraphStateLookupFailureArmed = false;
        _aggregateLoadPostGraphStateLookupFailureCount++;
        _aggregateLoadPostGraphStateLookupFailureRanOffMainThread =
            _mainThreadID != 0 && System.Environment.CurrentManagedThreadId != _mainThreadID;
        _aggregateLoadPostGraphStateLookupObservedStateTypeName = graphState.GetType().Name;
        throw new InvalidOperationException(
            AggregateLoadPostGraphStateLookupFailureMessage);
    }

    internal void ArmNextAggregateLoadPostGraphStateLookupFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadPostGraphStateLookupFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load post-graph-state lookup failure probe is already armed.");
        }

        _aggregateLoadPostGraphStateLookupFailureRanOffMainThread = false;
        _aggregateLoadPostGraphStateLookupObservedStateTypeName = string.Empty;
        _aggregateLoadPostGraphStateLookupFailureArmed = true;
    }

    internal bool IsAggregateLoadPostGraphStateLookupFailureArmed() =>
        _aggregateLoadPostGraphStateLookupFailureArmed;

    internal int GetAggregateLoadPostGraphStateLookupFailureCount() =>
        _aggregateLoadPostGraphStateLookupFailureCount;

    internal bool DidAggregateLoadPostGraphStateLookupFailureRunOffMainThread() =>
        _aggregateLoadPostGraphStateLookupFailureRanOffMainThread;

    internal string GetAggregateLoadPostGraphStateLookupObservedStateTypeName() =>
        _aggregateLoadPostGraphStateLookupObservedStateTypeName;

    partial void ProbeAggregateLoadRendererWorkerPrepareFailure()
    {
        if (!_aggregateLoadRendererWorkerPrepareFailureArmed)
            return;

        _aggregateLoadRendererWorkerPrepareFailureArmed = false;
        _aggregateLoadRendererWorkerPrepareFailureCount++;
        throw new InvalidOperationException(
            AggregateLoadRendererWorkerPrepareFailureMessage);
    }

    internal void ArmNextAggregateLoadRendererWorkerPrepareFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadRendererWorkerPrepareFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load renderer worker-prepare failure probe is already armed.");
        }

        _aggregateLoadRendererWorkerPrepareFailureArmed = true;
    }

    internal bool IsAggregateLoadRendererWorkerPrepareFailureArmed() =>
        _aggregateLoadRendererWorkerPrepareFailureArmed;

    internal int GetAggregateLoadRendererWorkerPrepareFailureCount() =>
        _aggregateLoadRendererWorkerPrepareFailureCount;

    partial void ProbeAggregateLoadPostRendererWorkerPreparationFailure(
        RoadRendererPreparedLoad presentation)
    {
        if (!_aggregateLoadPostRendererWorkerPreparationFailureArmed)
            return;

        ArgumentNullException.ThrowIfNull(presentation);
        _aggregateLoadPostRendererWorkerPreparationFailureArmed = false;
        _aggregateLoadPostRendererWorkerPreparationFailureCount++;
        _aggregateLoadPostRendererWorkerPreparationFailureRanOffMainThread =
            _mainThreadID != 0 && System.Environment.CurrentManagedThreadId != _mainThreadID;
        _aggregateLoadPostRendererWorkerPreparationRoadVertexCount =
            presentation.RoadVertices.Length;
        _aggregateLoadPostRendererWorkerPreparationSurfacePrimitiveCount =
            presentation.RoadSurface.PrimitiveCount;
        _aggregateLoadPostRendererWorkerPreparationNodeMarkerCount =
            presentation.NodeMarkers.Length;
        throw new InvalidOperationException(
            AggregateLoadPostRendererWorkerPreparationFailureMessage);
    }

    internal void ArmNextAggregateLoadPostRendererWorkerPreparationFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadPostRendererWorkerPreparationFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load post-renderer worker preparation failure probe is already armed.");
        }

        _aggregateLoadPostRendererWorkerPreparationFailureRanOffMainThread = false;
        _aggregateLoadPostRendererWorkerPreparationRoadVertexCount = -1;
        _aggregateLoadPostRendererWorkerPreparationSurfacePrimitiveCount = -1;
        _aggregateLoadPostRendererWorkerPreparationNodeMarkerCount = -1;
        _aggregateLoadPostRendererWorkerPreparationFailureArmed = true;
    }

    internal bool IsAggregateLoadPostRendererWorkerPreparationFailureArmed() =>
        _aggregateLoadPostRendererWorkerPreparationFailureArmed;

    internal int GetAggregateLoadPostRendererWorkerPreparationFailureCount() =>
        _aggregateLoadPostRendererWorkerPreparationFailureCount;

    internal bool DidAggregateLoadPostRendererWorkerPreparationFailureRunOffMainThread() =>
        _aggregateLoadPostRendererWorkerPreparationFailureRanOffMainThread;

    internal int GetAggregateLoadPostRendererWorkerPreparationRoadVertexCount() =>
        _aggregateLoadPostRendererWorkerPreparationRoadVertexCount;

    internal int GetAggregateLoadPostRendererWorkerPreparationSurfacePrimitiveCount() =>
        _aggregateLoadPostRendererWorkerPreparationSurfacePrimitiveCount;

    internal int GetAggregateLoadPostRendererWorkerPreparationNodeMarkerCount() =>
        _aggregateLoadPostRendererWorkerPreparationNodeMarkerCount;

    partial void ProbeAggregateLoadPostPreparedWorkReturnFailure(
        PreparedLoadWork prepared)
    {
        if (!_aggregateLoadPostPreparedWorkReturnFailureArmed)
            return;

        ArgumentNullException.ThrowIfNull(prepared);
        _aggregateLoadPostPreparedWorkReturnFailureArmed = false;
        _aggregateLoadPostPreparedWorkReturnFailureCount++;
        _aggregateLoadPostPreparedWorkReturnFailureRanOnMainThread =
            _mainThreadID != 0 && System.Environment.CurrentManagedThreadId == _mainThreadID;
        _aggregateLoadPostPreparedWorkReturnObservedSlotID = prepared.Slot.SlotID;
        _aggregateLoadPostPreparedWorkReturnParticipantCount =
            prepared.Slot.Participants.Count;
        _aggregateLoadPostPreparedWorkReturnRoadVertexCount =
            prepared.Presentation.RoadVertices.Length;
        _aggregateLoadPostPreparedWorkReturnSurfacePrimitiveCount =
            prepared.Presentation.RoadSurface.PrimitiveCount;
        _aggregateLoadPostPreparedWorkReturnNodeMarkerCount =
            prepared.Presentation.NodeMarkers.Length;
        throw new InvalidOperationException(
            AggregateLoadPostPreparedWorkReturnFailureMessage);
    }

    internal void ArmNextAggregateLoadPostPreparedWorkReturnFailure()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadPostPreparedWorkReturnFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load post-prepared-work return failure probe is already armed.");
        }

        _aggregateLoadPostPreparedWorkReturnFailureRanOnMainThread = false;
        _aggregateLoadPostPreparedWorkReturnObservedSlotID = string.Empty;
        _aggregateLoadPostPreparedWorkReturnParticipantCount = -1;
        _aggregateLoadPostPreparedWorkReturnRoadVertexCount = -1;
        _aggregateLoadPostPreparedWorkReturnSurfacePrimitiveCount = -1;
        _aggregateLoadPostPreparedWorkReturnNodeMarkerCount = -1;
        _aggregateLoadPostPreparedWorkReturnFailureArmed = true;
    }

    internal bool IsAggregateLoadPostPreparedWorkReturnFailureArmed() =>
        _aggregateLoadPostPreparedWorkReturnFailureArmed;

    internal int GetAggregateLoadPostPreparedWorkReturnFailureCount() =>
        _aggregateLoadPostPreparedWorkReturnFailureCount;

    internal bool DidAggregateLoadPostPreparedWorkReturnFailureRunOnMainThread() =>
        _aggregateLoadPostPreparedWorkReturnFailureRanOnMainThread;

    internal string GetAggregateLoadPostPreparedWorkReturnObservedSlotID() =>
        _aggregateLoadPostPreparedWorkReturnObservedSlotID;

    internal int GetAggregateLoadPostPreparedWorkReturnParticipantCount() =>
        _aggregateLoadPostPreparedWorkReturnParticipantCount;

    internal int GetAggregateLoadPostPreparedWorkReturnRoadVertexCount() =>
        _aggregateLoadPostPreparedWorkReturnRoadVertexCount;

    internal int GetAggregateLoadPostPreparedWorkReturnSurfacePrimitiveCount() =>
        _aggregateLoadPostPreparedWorkReturnSurfacePrimitiveCount;

    internal int GetAggregateLoadPostPreparedWorkReturnNodeMarkerCount() =>
        _aggregateLoadPostPreparedWorkReturnNodeMarkerCount;

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

    partial void ProbeAggregateLoadGraphCommitBoundaryGenerationMismatch(
        RoadGraph graph,
        ref IStorageOperationLease operationLease)
    {
        if (!_aggregateLoadGraphCommitBoundaryGenerationMismatchArmed)
            return;
        if (!ReferenceEquals(graph, _aggregateLoadGraphCommitBoundaryOwner))
        {
            throw new InvalidOperationException(
                "Aggregate Load graph commit-boundary probe targeted a different RoadGraph.");
        }

        _aggregateLoadGraphCommitBoundaryGenerationMismatchArmed = false;
        _aggregateLoadGraphCommitBoundaryGenerationMismatchCount++;
        _aggregateLoadGraphCommitBoundaryOwner = null;
        var wrapper = new AggregateLoadGraphBoundaryInvalidatingLease(
            operationLease,
            graph);
        _aggregateLoadGraphBoundaryLease = wrapper;
        operationLease = wrapper;
    }

    internal void ArmNextAggregateLoadGraphCommitBoundaryGenerationMismatch()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadGraphCommitBoundaryGenerationMismatchArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load graph commit-boundary generation mismatch probe is already armed.");
        }
        RoadGraph graph = _sceneContext?.Graph ?? throw new InvalidOperationException(
            "SaveManager must have a current RoadGraph before arming its aggregate Load graph probe.");

        _aggregateLoadGraphCommitBoundaryGenerationMismatchArmed = true;
        _aggregateLoadGraphCommitBoundaryOwner = graph;
        _aggregateLoadGraphBoundaryLease = null;
    }

    internal bool IsAggregateLoadGraphCommitBoundaryGenerationMismatchArmed() =>
        _aggregateLoadGraphCommitBoundaryGenerationMismatchArmed;

    internal int GetAggregateLoadGraphCommitBoundaryGenerationMismatchCount() =>
        _aggregateLoadGraphCommitBoundaryGenerationMismatchCount;

    internal int GetAggregateLoadGraphCommitBoundaryCount() =>
        _aggregateLoadGraphBoundaryLease?.BoundaryCount ?? 0;

    internal int GetAggregateLoadGraphMarkCommittedCount() =>
        _aggregateLoadGraphBoundaryLease?.MarkCommittedCount ?? 0;

    partial void ProbeAggregateLoadRendererCommitBoundaryGenerationMismatch(
        RoadRenderer renderer,
        ref IStorageOperationLease operationLease)
    {
        if (!_aggregateLoadRendererCommitBoundaryGenerationMismatchArmed)
            return;
        if (!ReferenceEquals(renderer, _aggregateLoadRendererCommitBoundaryOwner))
        {
            throw new InvalidOperationException(
                "Aggregate Load renderer commit-boundary probe targeted a different renderer.");
        }

        _aggregateLoadRendererCommitBoundaryGenerationMismatchArmed = false;
        _aggregateLoadRendererCommitBoundaryGenerationMismatchCount++;
        _aggregateLoadRendererCommitBoundaryOwner = null;
        var wrapper = new AggregateLoadRendererBoundaryInvalidatingLease(
            operationLease,
            renderer);
        _aggregateLoadRendererBoundaryLease = wrapper;
        operationLease = wrapper;
    }

    internal void ArmNextAggregateLoadRendererCommitBoundaryGenerationMismatch(
        RoadRenderer renderer)
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadRendererCommitBoundaryGenerationMismatchArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load renderer commit-boundary generation mismatch probe is already armed.");
        }

        _aggregateLoadRendererCommitBoundaryGenerationMismatchArmed = true;
        _aggregateLoadRendererCommitBoundaryOwner = renderer;
        _aggregateLoadRendererBoundaryLease = null;
    }

    internal bool IsAggregateLoadRendererCommitBoundaryGenerationMismatchArmed() =>
        _aggregateLoadRendererCommitBoundaryGenerationMismatchArmed;

    internal int GetAggregateLoadRendererCommitBoundaryGenerationMismatchCount() =>
        _aggregateLoadRendererCommitBoundaryGenerationMismatchCount;

    internal int GetAggregateLoadRendererCommitBoundaryCount() =>
        _aggregateLoadRendererBoundaryLease?.BoundaryCount ?? 0;

    internal int GetAggregateLoadRendererMarkCommittedCount() =>
        _aggregateLoadRendererBoundaryLease?.MarkCommittedCount ?? 0;

    partial void ProbeAggregateLoadToolCommitBoundaryGenerationMismatch(
        ToolManager toolManager,
        ref IStorageOperationLease operationLease)
    {
        if (!_aggregateLoadToolCommitBoundaryGenerationMismatchArmed)
            return;
        if (!ReferenceEquals(toolManager, _aggregateLoadToolCommitBoundaryOwner))
        {
            throw new InvalidOperationException(
                "Aggregate Load tool commit-boundary probe targeted a different ToolManager.");
        }

        _aggregateLoadToolCommitBoundaryGenerationMismatchArmed = false;
        _aggregateLoadToolCommitBoundaryGenerationMismatchCount++;
        _aggregateLoadToolCommitBoundaryOwner = null;
        var wrapper = new AggregateLoadToolBoundaryInvalidatingLease(
            operationLease,
            toolManager);
        _aggregateLoadToolBoundaryLease = wrapper;
        operationLease = wrapper;
    }

    internal void ArmNextAggregateLoadToolCommitBoundaryGenerationMismatch(
        ToolManager toolManager)
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadToolCommitBoundaryGenerationMismatchArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load tool commit-boundary generation mismatch probe is already armed.");
        }

        _aggregateLoadToolCommitBoundaryGenerationMismatchArmed = true;
        _aggregateLoadToolCommitBoundaryOwner = toolManager;
        _aggregateLoadToolBoundaryLease = null;
    }

    internal bool IsAggregateLoadToolCommitBoundaryGenerationMismatchArmed() =>
        _aggregateLoadToolCommitBoundaryGenerationMismatchArmed;

    internal int GetAggregateLoadToolCommitBoundaryGenerationMismatchCount() =>
        _aggregateLoadToolCommitBoundaryGenerationMismatchCount;

    internal int GetAggregateLoadToolCommitBoundaryCount() =>
        _aggregateLoadToolBoundaryLease?.BoundaryCount ?? 0;

    internal int GetAggregateLoadToolMarkCommittedCount() =>
        _aggregateLoadToolBoundaryLease?.MarkCommittedCount ?? 0;

    partial void ProbeAggregateLoadSlotTargetCommitBoundaryGenerationMismatch(
        ref IStorageOperationLease operationLease)
    {
        if (!_aggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed)
            return;

        _aggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed = false;
        _aggregateLoadSlotTargetCommitBoundaryGenerationMismatchCount++;
        var wrapper = new AggregateLoadSlotTargetBoundaryInvalidatingLease(
            operationLease,
            this);
        _aggregateLoadSlotTargetBoundaryLease = wrapper;
        operationLease = wrapper;
    }

    internal void ArmNextAggregateLoadSlotTargetCommitBoundaryGenerationMismatch()
    {
        if (IsOperationBusy)
        {
            throw new InvalidOperationException(
                "SaveManager must be idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load slot-target commit-boundary generation mismatch probe is already armed.");
        }

        _aggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed = true;
        _aggregateLoadSlotTargetBoundaryLease = null;
    }

    internal bool IsAggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed() =>
        _aggregateLoadSlotTargetCommitBoundaryGenerationMismatchArmed;

    internal int GetAggregateLoadSlotTargetCommitBoundaryGenerationMismatchCount() =>
        _aggregateLoadSlotTargetCommitBoundaryGenerationMismatchCount;

    internal int GetAggregateLoadSlotTargetCommitBoundaryCount() =>
        _aggregateLoadSlotTargetBoundaryLease?.BoundaryCount ?? 0;

    internal int GetAggregateLoadSlotTargetMarkCommittedCount() =>
        _aggregateLoadSlotTargetBoundaryLease?.MarkCommittedCount ?? 0;

    internal void InvalidateCurrentSlotTargetGenerationForAggregateBoundaryProbe() =>
        _currentSlotGeneration = NextGeneration(_currentSlotGeneration);

    private sealed class AggregateLoadGraphBoundaryInvalidatingLease(
        IStorageOperationLease inner,
        RoadGraph graph) : IStorageOperationLease
    {
        public string OperationToken => inner.OperationToken;
        public SaveOperationKind Kind => inner.Kind;
        internal int BoundaryCount { get; private set; }
        internal int MarkCommittedCount { get; private set; }

        public void ThrowIfCancellationRequested() => inner.ThrowIfCancellationRequested();

        public void AcquireCommitLease() => inner.AcquireCommitLease();

        public void CrossCommitBoundary(Action boundaryAction)
        {
            ArgumentNullException.ThrowIfNull(boundaryAction);
            BoundaryCount++;
            inner.CrossCommitBoundary(() =>
            {
                graph.InvalidateCurrentGraphLoadAdmissionForAggregateBoundaryProbe();
                boundaryAction();
            });
        }

        public void MarkCommitted()
        {
            MarkCommittedCount++;
            inner.MarkCommitted();
        }

        public void EnterCommitBoundary()
        {
            AcquireCommitLease();
            CrossCommitBoundary(static () => { });
            MarkCommitted();
        }
    }

    private sealed class AggregateLoadRendererBoundaryInvalidatingLease(
        IStorageOperationLease inner,
        RoadRenderer renderer) : IStorageOperationLease
    {
        public string OperationToken => inner.OperationToken;
        public SaveOperationKind Kind => inner.Kind;
        internal int BoundaryCount { get; private set; }
        internal int MarkCommittedCount { get; private set; }

        public void ThrowIfCancellationRequested() => inner.ThrowIfCancellationRequested();

        public void AcquireCommitLease() => inner.AcquireCommitLease();

        public void CrossCommitBoundary(Action boundaryAction)
        {
            ArgumentNullException.ThrowIfNull(boundaryAction);
            BoundaryCount++;
            inner.CrossCommitBoundary(() =>
            {
                renderer.InvalidateCurrentLoadAdmissionForAggregateBoundaryProbe();
                boundaryAction();
            });
        }

        public void MarkCommitted()
        {
            MarkCommittedCount++;
            inner.MarkCommitted();
        }

        public void EnterCommitBoundary()
        {
            AcquireCommitLease();
            CrossCommitBoundary(static () => { });
            MarkCommitted();
        }
    }

    private sealed class AggregateLoadToolBoundaryInvalidatingLease(
        IStorageOperationLease inner,
        ToolManager toolManager) : IStorageOperationLease
    {
        public string OperationToken => inner.OperationToken;
        public SaveOperationKind Kind => inner.Kind;
        internal int BoundaryCount { get; private set; }
        internal int MarkCommittedCount { get; private set; }

        public void ThrowIfCancellationRequested() => inner.ThrowIfCancellationRequested();

        public void AcquireCommitLease() => inner.AcquireCommitLease();

        public void CrossCommitBoundary(Action boundaryAction)
        {
            ArgumentNullException.ThrowIfNull(boundaryAction);
            BoundaryCount++;
            inner.CrossCommitBoundary(() =>
            {
                toolManager.InvalidateCurrentToolLoadAdmissionForAggregateBoundaryProbe();
                boundaryAction();
            });
        }

        public void MarkCommitted()
        {
            MarkCommittedCount++;
            inner.MarkCommitted();
        }

        public void EnterCommitBoundary()
        {
            AcquireCommitLease();
            CrossCommitBoundary(static () => { });
            MarkCommitted();
        }
    }

    private sealed class AggregateLoadSlotTargetBoundaryInvalidatingLease(
        IStorageOperationLease inner,
        SaveManager saveManager) : IStorageOperationLease
    {
        public string OperationToken => inner.OperationToken;
        public SaveOperationKind Kind => inner.Kind;
        internal int BoundaryCount { get; private set; }
        internal int MarkCommittedCount { get; private set; }

        public void ThrowIfCancellationRequested() => inner.ThrowIfCancellationRequested();

        public void AcquireCommitLease() => inner.AcquireCommitLease();

        public void CrossCommitBoundary(Action boundaryAction)
        {
            ArgumentNullException.ThrowIfNull(boundaryAction);
            BoundaryCount++;
            inner.CrossCommitBoundary(() =>
            {
                saveManager.InvalidateCurrentSlotTargetGenerationForAggregateBoundaryProbe();
                boundaryAction();
            });
        }

        public void MarkCommitted()
        {
            MarkCommittedCount++;
            inner.MarkCommitted();
        }

        public void EnterCommitBoundary()
        {
            AcquireCommitLease();
            CrossCommitBoundary(static () => { });
            MarkCommitted();
        }
    }
}

public partial class RoadGraph
{
    internal void InvalidateCurrentGraphLoadAdmissionForAggregateBoundaryProbe()
    {
        RoadGraphLoadAdmission admission = _loadAdmission ??
            throw new InvalidOperationException(
                "RoadGraph must have a current Load admission at the aggregate commit boundary.");
        admission.Dispose();
    }
}

public partial class ToolManager
{
    internal void InvalidateCurrentToolLoadAdmissionForAggregateBoundaryProbe()
    {
        ToolLoadAdmission admission = _loadAdmission ??
            throw new InvalidOperationException(
                "ToolManager must have a current Load admission at the aggregate commit boundary.");
        admission.Dispose();
    }

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
    internal const string AggregateLoadRendererAdmissionConstructionFailureMessage =
        "Injected aggregate Load renderer admission construction failure.";
    internal const string AggregateLoadRendererAdmissionPublicationFailureMessage =
        "Injected aggregate Load renderer admission publication failure.";
    internal const string AggregateLoadRoadMeshFactoryFailureMessage =
        "Injected aggregate Load CreateRoadMesh index enumeration failure.";
    internal const string AggregateLoadNodeBatchFactoryFailureMessage =
        "Injected aggregate Load CreateNodeBatch marker read failure.";
    internal const string AggregateLoadResourcePreflightFailureMessage =
        "Injected aggregate Load road presentation resource preflight failure.";
    internal const string AggregateLoadReservedRenderTokenFailureMessage =
        "Road render load reservation is stale.";
    internal const string AggregateLoadCommitPlanConstructionFailureMessage =
        "Injected aggregate Load road presentation commit plan construction failure.";

    private bool _aggregateLoadRendererAdmissionConstructionFailureArmed;
    private int _aggregateLoadRendererAdmissionConstructionFailureCount;
    private bool _aggregateLoadRendererAdmissionPublicationFailureArmed;
    private int _aggregateLoadRendererAdmissionPublicationFailureCount;
    private bool _aggregateLoadRoadMeshFactoryFailureArmed;
    private int _aggregateLoadRoadMeshFactoryFailureCount;
    private int _aggregateLoadRoadMeshFactoryIndexEnumerationCount;
    private bool _aggregateLoadNodeBatchFactoryFailureArmed;
    private int _aggregateLoadNodeBatchFactoryFailureCount;
    private int _aggregateLoadNodeBatchFactoryMarkerReadCount;
    private bool _aggregateLoadResourcePreflightFailureArmed;
    private int _aggregateLoadResourcePreflightFailureCount;
    private bool _aggregateLoadReservedRenderTokenFailureArmed;
    private int _aggregateLoadReservedRenderTokenFailureCount;
    private bool _aggregateLoadRoadSurfaceSnapshotFailureArmed;
    private int _aggregateLoadRoadSurfaceSnapshotFailureCount;
    private bool _aggregateLoadCommitPlanConstructionFailureArmed;
    private int _aggregateLoadCommitPlanConstructionFailureCount;

    internal void InvalidateCurrentLoadAdmissionForAggregateBoundaryProbe()
    {
        RoadRendererLoadAdmission admission = _loadAdmission ??
            throw new InvalidOperationException(
                "RoadRenderer must have a current Load admission at the aggregate commit boundary.");
        admission.Dispose();
    }

    partial void ProbeAggregateLoadRendererAdmissionConstructionFailure()
    {
        if (!_aggregateLoadRendererAdmissionConstructionFailureArmed)
            return;

        _aggregateLoadRendererAdmissionConstructionFailureArmed = false;
        _aggregateLoadRendererAdmissionConstructionFailureCount++;
        throw new InvalidOperationException(
            AggregateLoadRendererAdmissionConstructionFailureMessage);
    }

    internal void ArmNextAggregateLoadRendererAdmissionConstructionFailure()
    {
        if (!IsPresentationReady() || _loadAdmission is not null)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready and idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadRendererAdmissionConstructionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load renderer admission construction failure probe is already armed.");
        }
        if (_aggregateLoadRendererAdmissionPublicationFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load renderer admission publication failure probe is already armed.");
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
        if (_aggregateLoadRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load road-surface snapshot failure probe is already armed.");
        }
        if (_aggregateLoadReservedRenderTokenFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load reserved render-token failure probe is already armed.");
        }
        if (_aggregateLoadCommitPlanConstructionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load commit-plan construction failure probe is already armed.");
        }

        _aggregateLoadRendererAdmissionConstructionFailureArmed = true;
    }

    internal bool IsAggregateLoadRendererAdmissionConstructionFailureArmed() =>
        _aggregateLoadRendererAdmissionConstructionFailureArmed;

    internal int GetAggregateLoadRendererAdmissionConstructionFailureCount() =>
        _aggregateLoadRendererAdmissionConstructionFailureCount;

    partial void ProbeAggregateLoadRendererAdmissionPublicationFailure()
    {
        if (!_aggregateLoadRendererAdmissionPublicationFailureArmed)
            return;

        _aggregateLoadRendererAdmissionPublicationFailureArmed = false;
        _aggregateLoadRendererAdmissionPublicationFailureCount++;
        throw new InvalidOperationException(
            AggregateLoadRendererAdmissionPublicationFailureMessage);
    }

    internal void ArmNextAggregateLoadRendererAdmissionPublicationFailure()
    {
        if (!IsPresentationReady() || _loadAdmission is not null)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready and idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadRendererAdmissionPublicationFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load renderer admission publication failure probe is already armed.");
        }
        if (_aggregateLoadRendererAdmissionConstructionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load renderer admission construction failure probe is already armed.");
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
        if (_aggregateLoadRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load road-surface snapshot failure probe is already armed.");
        }
        if (_aggregateLoadReservedRenderTokenFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load reserved render-token failure probe is already armed.");
        }
        if (_aggregateLoadCommitPlanConstructionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load commit-plan construction failure probe is already armed.");
        }

        _aggregateLoadRendererAdmissionPublicationFailureArmed = true;
    }

    internal bool IsAggregateLoadRendererAdmissionPublicationFailureArmed() =>
        _aggregateLoadRendererAdmissionPublicationFailureArmed;

    internal int GetAggregateLoadRendererAdmissionPublicationFailureCount() =>
        _aggregateLoadRendererAdmissionPublicationFailureCount;

    private void EnsureRendererAdmissionFailureProbeIsNotArmed()
    {
        if (_aggregateLoadRendererAdmissionConstructionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load renderer admission construction failure probe is already armed.");
        }
        if (_aggregateLoadRendererAdmissionPublicationFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load renderer admission publication failure probe is already armed.");
        }
    }

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
        if (_aggregateLoadRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load road-surface snapshot failure probe is already armed.");
        }
        if (_aggregateLoadReservedRenderTokenFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load reserved render-token failure probe is already armed.");
        }
        if (_aggregateLoadCommitPlanConstructionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load commit-plan construction failure probe is already armed.");
        }

        EnsureRendererAdmissionFailureProbeIsNotArmed();
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
        if (_aggregateLoadRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load road-surface snapshot failure probe is already armed.");
        }
        if (_aggregateLoadReservedRenderTokenFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load reserved render-token failure probe is already armed.");
        }
        if (_aggregateLoadCommitPlanConstructionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load commit-plan construction failure probe is already armed.");
        }

        EnsureRendererAdmissionFailureProbeIsNotArmed();
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
        if (_aggregateLoadRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load road-surface snapshot failure probe is already armed.");
        }
        if (_aggregateLoadReservedRenderTokenFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load reserved render-token failure probe is already armed.");
        }
        if (_aggregateLoadCommitPlanConstructionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load commit-plan construction failure probe is already armed.");
        }

        EnsureRendererAdmissionFailureProbeIsNotArmed();
        _aggregateLoadResourcePreflightFailureArmed = true;
    }

    internal bool IsAggregateLoadResourcePreflightFailureArmed() =>
        _aggregateLoadResourcePreflightFailureArmed;

    internal int GetAggregateLoadResourcePreflightFailureCount() =>
        _aggregateLoadResourcePreflightFailureCount;

    partial void ProbeAggregateLoadReservedRenderTokenFailure(
        ref RoadRenderLoadReservation renderReservation)
    {
        if (!_aggregateLoadReservedRenderTokenFailureArmed)
            return;

        _aggregateLoadReservedRenderTokenFailureArmed = false;
        _aggregateLoadReservedRenderTokenFailureCount++;
        renderReservation = default;
    }

    internal void ArmNextAggregateLoadReservedRenderTokenFailure()
    {
        if (!IsPresentationReady() || _loadAdmission is not null)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready and idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadReservedRenderTokenFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load reserved render-token failure probe is already armed.");
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
        if (_aggregateLoadRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load road-surface snapshot failure probe is already armed.");
        }
        if (_aggregateLoadCommitPlanConstructionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load commit-plan construction failure probe is already armed.");
        }

        EnsureRendererAdmissionFailureProbeIsNotArmed();
        _aggregateLoadReservedRenderTokenFailureArmed = true;
    }

    internal bool IsAggregateLoadReservedRenderTokenFailureArmed() =>
        _aggregateLoadReservedRenderTokenFailureArmed;

    internal int GetAggregateLoadReservedRenderTokenFailureCount() =>
        _aggregateLoadReservedRenderTokenFailureCount;

    partial void ProbeAggregateLoadRoadSurfaceSnapshotFailure(
        ref RoadSurfaceSnapshot.PreparedData roadSurface)
    {
        if (!_aggregateLoadRoadSurfaceSnapshotFailureArmed)
            return;

        _aggregateLoadRoadSurfaceSnapshotFailureArmed = false;
        _aggregateLoadRoadSurfaceSnapshotFailureCount++;
        roadSurface = null!;
    }

    internal void ArmNextAggregateLoadRoadSurfaceSnapshotFailure()
    {
        if (!IsPresentationReady() || _loadAdmission is not null)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready and idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load road-surface snapshot failure probe is already armed.");
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
        if (_aggregateLoadReservedRenderTokenFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load reserved render-token failure probe is already armed.");
        }
        if (_aggregateLoadCommitPlanConstructionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load commit-plan construction failure probe is already armed.");
        }

        EnsureRendererAdmissionFailureProbeIsNotArmed();
        _aggregateLoadRoadSurfaceSnapshotFailureArmed = true;
    }

    internal bool IsAggregateLoadRoadSurfaceSnapshotFailureArmed() =>
        _aggregateLoadRoadSurfaceSnapshotFailureArmed;

    internal int GetAggregateLoadRoadSurfaceSnapshotFailureCount() =>
        _aggregateLoadRoadSurfaceSnapshotFailureCount;

    partial void ProbeAggregateLoadCommitPlanConstructionFailure()
    {
        if (!_aggregateLoadCommitPlanConstructionFailureArmed)
            return;

        _aggregateLoadCommitPlanConstructionFailureArmed = false;
        _aggregateLoadCommitPlanConstructionFailureCount++;
        throw new InvalidOperationException(
            AggregateLoadCommitPlanConstructionFailureMessage);
    }

    internal void ArmNextAggregateLoadCommitPlanConstructionFailure()
    {
        if (!IsPresentationReady() || _loadAdmission is not null)
        {
            throw new InvalidOperationException(
                "Road presentation must be ready and idle before arming its aggregate Load failure probe.");
        }
        if (_aggregateLoadCommitPlanConstructionFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load commit-plan construction failure probe is already armed.");
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
        if (_aggregateLoadRoadSurfaceSnapshotFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load road-surface snapshot failure probe is already armed.");
        }
        if (_aggregateLoadReservedRenderTokenFailureArmed)
        {
            throw new InvalidOperationException(
                "Aggregate Load reserved render-token failure probe is already armed.");
        }

        EnsureRendererAdmissionFailureProbeIsNotArmed();
        _aggregateLoadCommitPlanConstructionFailureArmed = true;
    }

    internal bool IsAggregateLoadCommitPlanConstructionFailureArmed() =>
        _aggregateLoadCommitPlanConstructionFailureArmed;

    internal int GetAggregateLoadCommitPlanConstructionFailureCount() =>
        _aggregateLoadCommitPlanConstructionFailureCount;

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
