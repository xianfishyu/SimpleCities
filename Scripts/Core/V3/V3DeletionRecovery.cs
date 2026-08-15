namespace SimpleCities.Core.V3;

public enum V3DeletionRecoveryDecision
{
    NotDeleted,
    ContinueCleanup,
    Blocked,
}

/// <summary>
/// 按指南 10.5 的 delete descriptor 恢复决策：槽仍存在表示未越界；槽缺失且 tombstone 匹配则继续清理；否则阻塞。
/// </summary>
public static class V3DeletionRecovery
{
    public static V3DeletionRecoveryDecision Decide(
        bool slotExists,
        bool tombstoneMatchesDescriptor)
    {
        if (slotExists)
            return V3DeletionRecoveryDecision.NotDeleted;
        if (tombstoneMatchesDescriptor)
            return V3DeletionRecoveryDecision.ContinueCleanup;
        return V3DeletionRecoveryDecision.Blocked;
    }
}
