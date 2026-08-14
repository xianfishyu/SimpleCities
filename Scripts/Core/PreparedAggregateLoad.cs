using System;
using System.Collections.Generic;
using System.Linq;

internal interface INonThrowingLoadCommitPlan : IDisposable
{
    string ParticipantID { get; }
    bool IsGenerationCurrent { get; }
    void CommitReferences();
    IReadOnlyList<string> PublishNotifications();
    void CompleteCommit();
}

internal sealed class PreparedAggregateLoad : IDisposable
{
    private readonly INonThrowingLoadCommitPlan[] _plans;
    private bool _committed;
    private bool _disposed;

    internal PreparedAggregateLoad(IEnumerable<INonThrowingLoadCommitPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        _plans = plans.ToArray();
        if (_plans.Length == 0)
            throw new ArgumentException("A prepared load aggregate cannot be empty.", nameof(plans));

        var participantIDs = new HashSet<string>(StringComparer.Ordinal);
        foreach (INonThrowingLoadCommitPlan plan in _plans)
        {
            ArgumentNullException.ThrowIfNull(plan);
            if (string.IsNullOrWhiteSpace(plan.ParticipantID))
                throw new ArgumentException("A load participant ID cannot be empty.", nameof(plans));
            if (!participantIDs.Add(plan.ParticipantID))
                throw new ArgumentException(
                    $"Duplicate load participant ID '{plan.ParticipantID}'.",
                    nameof(plans));
        }
    }

    internal bool IsGenerationCurrent => _plans.All(plan => plan.IsGenerationCurrent);

    internal IReadOnlyList<string> Commit(IStorageOperationLease operationLease)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(operationLease);
        if (operationLease.Kind != SaveOperationKind.Load)
            throw new ArgumentException("An aggregate load requires a load operation lease.", nameof(operationLease));
        if (_committed)
            throw new InvalidOperationException("The prepared aggregate was already committed.");
        if (!IsGenerationCurrent)
            throw new LoadPreflightInvalidException("A load participant generation changed before commit.");

        operationLease.ThrowIfCancellationRequested();
        operationLease.AcquireCommitLease();
        operationLease.CrossCommitBoundary(() =>
        {
            if (!IsGenerationCurrent)
                throw new LoadPreflightInvalidException(
                    "A load participant generation changed while entering commit.");
            foreach (INonThrowingLoadCommitPlan plan in _plans)
                plan.CommitReferences();
        });
        operationLease.MarkCommitted();
        _committed = true;

        var warnings = new List<string>();
        foreach (INonThrowingLoadCommitPlan plan in _plans)
        {
            try
            {
                warnings.AddRange(plan.PublishNotifications().Where(warning =>
                    !string.IsNullOrWhiteSpace(warning)));
            }
            catch (Exception exception)
            {
                warnings.Add($"Load observer '{plan.ParticipantID}' failed: {exception.Message}");
            }
        }
        foreach (INonThrowingLoadCommitPlan plan in _plans)
        {
            try
            {
                plan.CompleteCommit();
            }
            catch (Exception exception)
            {
                warnings.Add($"Load participant '{plan.ParticipantID}' cleanup failed: {exception.Message}");
            }
        }
        return warnings.AsReadOnly();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (INonThrowingLoadCommitPlan plan in _plans)
        {
            try
            {
                plan.Dispose();
            }
            catch
            {
                // Abandoning a preflight plan must continue releasing every participant.
            }
        }
    }
}

internal sealed class LoadPreflightInvalidException(string message) : InvalidOperationException(message);
