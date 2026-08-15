using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// 进程内保存根 gate：同一时间只允许一个目录事务；autosave pending 单独标记。
/// </summary>
public sealed class V3CoordinatorGate
{
    private bool _busy;
    private bool _pendingAutosave;

    public bool IsBusy => _busy;
    public bool HasPendingAutosave => _pendingAutosave;

    public bool TryAcquire(out Guid operationId)
    {
        if (_busy)
        {
            operationId = Guid.Empty;
            return false;
        }

        _busy = true;
        operationId = Guid.NewGuid();
        return true;
    }

    public void Release(Guid operationId)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Operation id must not be empty.", nameof(operationId));
        _busy = false;
    }

    public void SetPendingAutosave(bool pending) => _pendingAutosave = pending;
}
