using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// V3 事务路径派生：operation-specific 目录、staging/backup/publish/tombstone 路径。
/// 所有路径都从已验证 root 与合法 slot/operation ID 派生，避免路径逃逸。
/// </summary>
public static class V3TransactionPath
{
    public static bool IsValidOperationId(string? operationId) =>
        V3SlotId.IsValid(operationId);

    public static bool TryGetTransactionDirectory(
        string root,
        string slotId,
        string operationId,
        out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(root) || !V3SlotId.IsValid(slotId) || !IsValidOperationId(operationId))
            return false;

        path = $"{root.TrimEnd('/')}/.save-transactions/{slotId}/{operationId}";
        return true;
    }

    public static bool TryGetStagingPath(
        string root,
        string slotId,
        string operationId,
        out string path)
    {
        if (!TryGetTransactionDirectory(root, slotId, operationId, out string directory))
        {
            path = string.Empty;
            return false;
        }

        path = $"{directory}/staging";
        return true;
    }

    public static bool TryGetBackupPath(
        string root,
        string slotId,
        string operationId,
        out string path)
    {
        if (!TryGetTransactionDirectory(root, slotId, operationId, out string directory))
        {
            path = string.Empty;
            return false;
        }

        path = $"{directory}/backup";
        return true;
    }

    public static bool TryGetPublishDescriptorPath(
        string root,
        string slotId,
        string operationId,
        out string path)
    {
        if (!TryGetTransactionDirectory(root, slotId, operationId, out string directory))
        {
            path = string.Empty;
            return false;
        }

        path = $"{directory}/publish.json";
        return true;
    }
}
