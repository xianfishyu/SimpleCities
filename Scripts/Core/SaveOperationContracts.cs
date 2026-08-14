using System;
using System.Collections.Generic;
using System.Linq;

public enum SaveOperationKind
{
    Publish,
    Load,
    Delete,
    Autosave,
}

public enum SaveOperationPhase
{
    Admission,
    Capture,
    Recover,
    Prepare,
    Preflight,
    Commit,
    Publish,
    Cleanup,
    Completed,
}

public enum SaveOperationResultKind
{
    Succeeded,
    SucceededWithWarnings,
    Failed,
    Canceled,
    SkippedBusy,
    RejectedShuttingDown,
    RejectedSceneClosing,
}

public sealed record SaveOperationState(
    string OperationToken,
    SaveOperationKind Kind,
    string TargetSlotID,
    SaveOperationPhase Phase,
    bool HasCrossedCommitBoundary,
    bool CancellationRequested);

public sealed record SaveOperationResult
{
    public string OperationToken { get; }
    public SaveOperationKind Kind { get; }
    public string TargetSlotID { get; }
    public SaveOperationResultKind ResultKind { get; }
    public SaveOperationPhase FinalPhase { get; }
    public bool Committed { get; }
    public IReadOnlyList<string> Warnings { get; }
    public string? Error { get; }

    public bool IsSuccess => ResultKind is SaveOperationResultKind.Succeeded or
        SaveOperationResultKind.SucceededWithWarnings;

    public SaveOperationResult(
        string operationToken,
        SaveOperationKind kind,
        string targetSlotID,
        SaveOperationResultKind resultKind,
        SaveOperationPhase finalPhase,
        bool committed,
        IEnumerable<string>? warnings = null,
        string? error = null)
    {
        if (string.IsNullOrWhiteSpace(operationToken))
            throw new ArgumentException("Operation token cannot be empty.", nameof(operationToken));
        if (string.IsNullOrWhiteSpace(targetSlotID))
            throw new ArgumentException("Target slot ID cannot be empty.", nameof(targetSlotID));
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(resultKind))
            throw new ArgumentOutOfRangeException(nameof(resultKind));
        if (!Enum.IsDefined(finalPhase))
            throw new ArgumentOutOfRangeException(nameof(finalPhase));
        if (committed && resultKind is SaveOperationResultKind.Canceled or
            SaveOperationResultKind.SkippedBusy or SaveOperationResultKind.RejectedShuttingDown or
            SaveOperationResultKind.RejectedSceneClosing)
        {
            throw new ArgumentException("A canceled, skipped, or rejected operation cannot be committed.");
        }

        OperationToken = operationToken;
        Kind = kind;
        TargetSlotID = targetSlotID;
        ResultKind = resultKind;
        FinalPhase = finalPhase;
        Committed = committed;
        Warnings = Array.AsReadOnly((warnings ?? []).Where(warning =>
            !string.IsNullOrWhiteSpace(warning)).ToArray());
        Error = error;
    }
}

internal interface IStorageOperationLease
{
    string OperationToken { get; }
    SaveOperationKind Kind { get; }
    void ThrowIfCancellationRequested();
    void AcquireCommitLease();
    void CrossCommitBoundary(Action boundaryAction);
    void MarkCommitted();
    void EnterCommitBoundary();
}

internal sealed class UncoordinatedStorageOperationLease : IStorageOperationLease
{
    internal UncoordinatedStorageOperationLease(
        SaveOperationKind kind,
        string? operationToken = null)
    {
        Kind = kind;
        OperationToken = operationToken ?? Guid.NewGuid().ToString("N");
    }

    public string OperationToken { get; }
    public SaveOperationKind Kind { get; }
    public void ThrowIfCancellationRequested() { }
    public void AcquireCommitLease() { }
    public void CrossCommitBoundary(Action boundaryAction)
    {
        ArgumentNullException.ThrowIfNull(boundaryAction);
        boundaryAction();
    }
    public void MarkCommitted() { }
    public void EnterCommitBoundary()
    {
        CrossCommitBoundary(static () => { });
        MarkCommitted();
    }
}
