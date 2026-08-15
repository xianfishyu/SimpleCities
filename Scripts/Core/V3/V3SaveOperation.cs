using System;
using System.Collections.Generic;

namespace SimpleCities.Core.V3;

public enum V3SaveOperationKind
{
    Publish,
    Load,
    Delete,
}

public enum V3SaveOperationPhase
{
    Admission,
    Prepare,
    Preflight,
    Commit,
    Completed,
}

public sealed record V3SaveOperationToken(
    Guid OperationID,
    V3SaveOperationKind Kind,
    long SceneGeneration,
    long OperationGeneration)
{
    public static V3SaveOperationToken Create(V3SaveOperationKind kind, long sceneGeneration) =>
        new(Guid.NewGuid(), kind, sceneGeneration, 0);
}

public sealed record V3SaveOperationResult(
    V3SaveOperationToken Token,
    V3SaveOperationPhase Phase,
    bool Success,
    bool CommitCompleted,
    IReadOnlyList<string> Warnings,
    string? Error)
{
    public static V3SaveOperationResult Succeeded(
        V3SaveOperationToken token,
        IReadOnlyList<string>? warnings = null) =>
        new(token, V3SaveOperationPhase.Completed, true, true, warnings ?? [], null);

    public static V3SaveOperationResult FailedBeforeCommit(
        V3SaveOperationToken token,
        V3SaveOperationPhase phase,
        string error) =>
        new(token, phase, false, false, [], error);

    public static V3SaveOperationResult SucceededWithObserverWarnings(
        V3SaveOperationToken token,
        IReadOnlyList<string> warnings) =>
        new(token, V3SaveOperationPhase.Completed, true, true, warnings, null);
}
