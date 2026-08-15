using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// 组合进程内 gate 与跨进程文件锁的保存根协调器：两者都获取后才算持有。
/// </summary>
public sealed class V3FileCoordinator : IDisposable
{
    private readonly V3CoordinatorGate _gate = new();
    private readonly V3FileLock _fileLock = new();
    private Guid _operationId;

    public bool IsHeld => _gate.IsBusy && _fileLock.IsHeld;

    public bool TryAcquire(string root, out Guid operationId)
    {
        operationId = Guid.Empty;
        if (!_gate.TryAcquire(out operationId))
            return false;

        if (!V3SaveRootLock.TryGetLockPath(root, out string lockPath) ||
            !_fileLock.TryAcquire(lockPath))
        {
            _gate.Release(operationId);
            operationId = Guid.Empty;
            return false;
        }

        _operationId = operationId;
        return true;
    }

    public void Release()
    {
        _fileLock.Dispose();
        if (_operationId != Guid.Empty)
            _gate.Release(_operationId);
        _operationId = Guid.Empty;
    }

    public void Dispose() => Release();
}
