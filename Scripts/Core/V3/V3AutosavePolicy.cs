namespace SimpleCities.Core.V3;

public enum V3AutosaveDecision
{
    RunNow,
    QueuePending,
    SkipBusy,
}

/// <summary>
/// autosave busy 合并策略：忙时至多一个 pending；手动/更晚成功后可丢弃。
/// </summary>
public static class V3AutosavePolicy
{
    public static V3AutosaveDecision Decide(
        bool isBusy,
        bool pendingExists,
        bool hasNewerSuccess)
    {
        if (isBusy)
            return pendingExists ? V3AutosaveDecision.SkipBusy : V3AutosaveDecision.QueuePending;
        return hasNewerSuccess ? V3AutosaveDecision.SkipBusy : V3AutosaveDecision.RunNow;
    }
}
