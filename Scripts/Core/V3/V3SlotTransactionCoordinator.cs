using System;
using System.Collections.Generic;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽事务协调器：在进程内 gate 持有期间执行 Publish/Delete/Recover；
/// 文件槽存储各自使用根锁保证跨进程排他。
/// </summary>
public sealed class V3SlotTransactionCoordinator
{
    private readonly V3CoordinatorGate _gate;

    public V3SlotTransactionCoordinator() : this(new V3CoordinatorGate())
    {
    }

    public V3SlotTransactionCoordinator(V3CoordinatorGate gate)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public V3PublishResult Publish(
        string slotId,
        string root,
        V3Manifest manifest,
        IReadOnlyDictionary<string, byte[]> payloads)
    {
        if (!_gate.TryAcquire(out Guid operationId))
            return V3PublishResult.Failure("Busy");

        try
        {
            return V3SlotTransactionService.Publish(slotId, root, manifest, payloads);
        }
        finally
        {
            _gate.Release(operationId);
        }
    }

    public V3DeleteResult Delete(string slotId, string root)
    {
        if (!_gate.TryAcquire(out Guid operationId))
            return V3DeleteResult.Failure("Busy");

        try
        {
            return V3SlotTransactionService.Delete(slotId, root);
        }
        finally
        {
            _gate.Release(operationId);
        }
    }

    public bool Recover(string slotId, string root, string backupRoot)
    {
        if (!_gate.TryAcquire(out Guid operationId))
            return false;

        try
        {
            return V3SlotTransactionService.Recover(slotId, root, backupRoot);
        }
        finally
        {
            _gate.Release(operationId);
        }
    }
}
