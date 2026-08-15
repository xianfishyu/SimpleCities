using System;
using System.Collections.Generic;

namespace SimpleCities.Core.V3;

/// <summary>
/// 组合内存槽存储与进程内 gate 的简单保存协调器：Save/Load/Delete 通过 gate 排他执行。
/// </summary>
public sealed class V3SaveCoordinator
{
    private readonly V3SlotStore _store;
    private readonly V3CoordinatorGate _gate;

    public V3SaveCoordinator(V3SlotStore store, V3CoordinatorGate gate)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public V3SaveOperationResult TrySave(
        string slotId,
        V3Manifest manifest,
        IReadOnlyDictionary<string, byte[]> payloads)
    {
        if (!_gate.TryAcquire(out Guid operationId))
            return V3SaveOperationResult.FailedBeforeCommit(
                V3SaveOperationToken.Create(V3SaveOperationKind.Publish, 0),
                V3SaveOperationPhase.Admission,
                "Busy");

        try
        {
            bool success = _store.Save(slotId, manifest, payloads);
            return success
                ? V3SaveOperationResult.Succeeded(V3SaveOperationToken.Create(V3SaveOperationKind.Publish, 0))
                : V3SaveOperationResult.FailedBeforeCommit(
                    V3SaveOperationToken.Create(V3SaveOperationKind.Publish, 0),
                    V3SaveOperationPhase.Prepare,
                    "SaveRejected");
        }
        finally
        {
            _gate.Release(operationId);
        }
    }

    public V3SaveOperationResult TryLoad(string slotId, out V3SlotReadResult readResult)
    {
        if (!_gate.TryAcquire(out Guid operationId))
        {
            readResult = V3SlotReadResult.Failure("Busy");
            return V3SaveOperationResult.FailedBeforeCommit(
                V3SaveOperationToken.Create(V3SaveOperationKind.Load, 0),
                V3SaveOperationPhase.Admission,
                "Busy");
        }

        try
        {
            readResult = _store.Load(slotId);
            return readResult.Success
                ? V3SaveOperationResult.Succeeded(V3SaveOperationToken.Create(V3SaveOperationKind.Load, 0))
                : V3SaveOperationResult.FailedBeforeCommit(
                    V3SaveOperationToken.Create(V3SaveOperationKind.Load, 0),
                    V3SaveOperationPhase.Prepare,
                    readResult.Error ?? "LoadFailed");
        }
        finally
        {
            _gate.Release(operationId);
        }
    }

    public V3SaveOperationResult TryDelete(string slotId)
    {
        if (!_gate.TryAcquire(out Guid operationId))
            return V3SaveOperationResult.FailedBeforeCommit(
                V3SaveOperationToken.Create(V3SaveOperationKind.Delete, 0),
                V3SaveOperationPhase.Admission,
                "Busy");

        try
        {
            bool success = _store.Delete(slotId);
            return success
                ? V3SaveOperationResult.Succeeded(V3SaveOperationToken.Create(V3SaveOperationKind.Delete, 0))
                : V3SaveOperationResult.FailedBeforeCommit(
                    V3SaveOperationToken.Create(V3SaveOperationKind.Delete, 0),
                    V3SaveOperationPhase.Prepare,
                    "DeleteRejected");
        }
        finally
        {
            _gate.Release(operationId);
        }
    }
}
