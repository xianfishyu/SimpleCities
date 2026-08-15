using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// V3 槽 ID 校验：1～128 个 `[A-Za-z0-9_-]` ASCII 字符，并逐字匹配目录名。
/// </summary>
public static class V3SlotId
{
    public const int MaxLength = 128;

    public static bool IsValid(string? slotId)
    {
        if (string.IsNullOrEmpty(slotId) || slotId.Length > MaxLength)
            return false;

        foreach (char c in slotId)
        {
            bool isValid =
                (c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '-' ||
                c == '_';
            if (!isValid)
                return false;
        }

        return true;
    }
}
