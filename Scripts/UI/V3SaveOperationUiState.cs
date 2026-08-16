using System;
using SimpleCities.Core.V3;

/// <summary>
/// 将 V3 存档操作结果映射为暂停菜单可呈现的状态，避免 UI 直接解释 token/result。
/// </summary>
public enum V3SaveOperationUiPhase
{
    Idle,
    Busy,
    Cancelling,
    Completed,
    Failed,
}

public sealed record V3SaveOperationUiState(
    V3SaveOperationKind Kind,
    V3SaveOperationUiPhase Phase,
    V3SaveOperationPhase? OperationPhase,
    bool IsBusy,
    bool IsCancellable,
    bool IsComplete,
    bool IsFailed,
    bool HasWarnings,
    string? Error,
    string? WarningSummary)
{
    public static V3SaveOperationUiState Idle() =>
        new(default, V3SaveOperationUiPhase.Idle, null, false, false, false, false, false, null, null);

    public static V3SaveOperationUiState FromResult(V3SaveOperationResult? result)
    {
        if (result == null)
            return Idle();

        bool isComplete = result.Success && result.CommitCompleted;
        bool isFailed = !result.Success && !result.CommitCompleted;
        bool isBusy = !isComplete && !isFailed;
        bool isCancellable = result.Phase is V3SaveOperationPhase.Admission
            or V3SaveOperationPhase.Prepare
            or V3SaveOperationPhase.Preflight;
        string? warningSummary = result.Warnings.Count > 0
            ? string.Join("；", result.Warnings)
            : null;

        V3SaveOperationUiPhase phase = isComplete
            ? V3SaveOperationUiPhase.Completed
            : isFailed
                ? V3SaveOperationUiPhase.Failed
                : V3SaveOperationUiPhase.Busy;

        return new V3SaveOperationUiState(
            result.Token.Kind,
            phase,
            result.Phase,
            isBusy,
            isCancellable,
            isComplete,
            isFailed,
            result.Warnings.Count > 0,
            result.Error,
            warningSummary);
    }

    public static V3SaveOperationUiState Cancelling(V3SaveOperationKind kind) =>
        new(kind, V3SaveOperationUiPhase.Cancelling, null, false, false, false, false, false, null, null);
}
