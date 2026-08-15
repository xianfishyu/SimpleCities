using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// V3 根锁路径：`&lt;root&gt;/.save-root.lock`。锁文件用于跨进程排他目录事务。
/// </summary>
public static class V3SaveRootLock
{
    public static bool TryGetLockPath(string root, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
            return false;

        path = $"{root.TrimEnd('/')}/.save-root.lock";
        return true;
    }
}
