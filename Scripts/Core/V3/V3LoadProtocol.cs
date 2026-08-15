namespace SimpleCities.Core.V3;

public enum V3LoadPhase
{
    NotStarted,
    Admission,
    Prepare,
    Preflight,
    Commit,
    Completed,
    Failed,
}

/// <summary>
/// Load 四阶段状态机：Admission -> Prepare -> Preflight -> Commit -> Completed；
/// 任何关键失败在 Preflight 或之前结束为 Failed。
/// </summary>
public sealed class V3LoadProtocol
{
    public V3LoadPhase Phase { get; private set; } = V3LoadPhase.NotStarted;

    public bool TryEnterAdmission()
    {
        if (Phase != V3LoadPhase.NotStarted)
            return false;
        Phase = V3LoadPhase.Admission;
        return true;
    }

    public bool TryEnterPrepare()
    {
        if (Phase != V3LoadPhase.Admission)
            return false;
        Phase = V3LoadPhase.Prepare;
        return true;
    }

    public bool TryEnterPreflight()
    {
        if (Phase != V3LoadPhase.Prepare)
            return false;
        Phase = V3LoadPhase.Preflight;
        return true;
    }

    public bool TryEnterCommit()
    {
        if (Phase != V3LoadPhase.Preflight)
            return false;
        Phase = V3LoadPhase.Commit;
        return true;
    }

    public bool Complete()
    {
        if (Phase != V3LoadPhase.Commit)
            return false;
        Phase = V3LoadPhase.Completed;
        return true;
    }

    public bool Fail()
    {
        Phase = V3LoadPhase.Failed;
        return true;
    }
}
