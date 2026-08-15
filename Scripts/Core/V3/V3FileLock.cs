using System;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 跨进程文件锁：以独占打开/创建 `.save-root.lock` 表示持有锁；释放后其他进程可获取。
/// </summary>
public sealed class V3FileLock : IDisposable
{
    private FileStream? _stream;

    public bool IsHeld => _stream is not null;

    public bool TryAcquire(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Lock path must not be empty.", nameof(path));

        if (_stream is not null)
            return true;

        try
        {
            _stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _stream = null;
    }
}
