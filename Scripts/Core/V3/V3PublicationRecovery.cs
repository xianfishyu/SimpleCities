namespace SimpleCities.Core.V3;

public enum V3PublicationRecoveryDecision
{
    PublishComplete,
    PreserveOldIsolateStaging,
    CompleteStagingToSlot,
    RestoreOldFromBackup,
    Blocked,
}

/// <summary>
/// 按指南 10.5 的 publish descriptor 恢复矩阵做纯决策。
/// </summary>
public static class V3PublicationRecovery
{
    public static V3PublicationRecoveryDecision Decide(
        bool slotExists,
        bool slotMatchesNew,
        bool slotMatchesOld,
        bool backupMatchesOld,
        bool stagingMatchesNew)
    {
        if (slotExists && slotMatchesNew)
            return V3PublicationRecoveryDecision.PublishComplete;
        if (slotExists && slotMatchesOld)
            return V3PublicationRecoveryDecision.PreserveOldIsolateStaging;
        if (!slotExists && backupMatchesOld && stagingMatchesNew)
            return V3PublicationRecoveryDecision.CompleteStagingToSlot;
        if (!slotExists && backupMatchesOld && !stagingMatchesNew)
            return V3PublicationRecoveryDecision.RestoreOldFromBackup;
        return V3PublicationRecoveryDecision.Blocked;
    }
}
