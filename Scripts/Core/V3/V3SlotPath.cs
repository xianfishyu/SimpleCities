using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// 从已验证的 V3 root capability 派生槽路径；槽 ID 不合法时拒绝，避免路径逃逸。
/// </summary>
public static class V3SlotPath
{
    public static bool TryGetSlotPath(string root, string slotId, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
            return false;
        if (!V3SlotId.IsValid(slotId))
            return false;

        path = $"{root.TrimEnd('/')}/{slotId}";
        return true;
    }
}
