using System;
using System.Diagnostics;

/// <summary>
/// Main-thread arbitration for one road write. Cancellation revokes publication immediately;
/// ownership remains held until the worker and prepared resources have been cleaned up.
/// </summary>
public sealed class V4RoadOperation
{
    private long _startedAt;
    private double _finishedElapsed;
    public string Phase { get; private set; } = "Idle";
    public bool IsBusy { get; private set; }
    public bool CanPublish => IsBusy && Phase == "Preparing";
    public bool IsWaiting => CanPublish && ElapsedMilliseconds > 300;
    public double ElapsedMilliseconds => IsBusy ? Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds : _finishedElapsed;
    public double DrawnElapsedMilliseconds { get; private set; } = -1;

    public bool TryBegin(long? inputTimestamp = null)
    {
        if (IsBusy) return false;
        IsBusy = true;
        Phase = "Preparing";
        DrawnElapsedMilliseconds = -1;
        _startedAt = inputTimestamp ?? Stopwatch.GetTimestamp();
        return true;
    }

    public bool TryCancel()
    {
        if (!CanPublish) return false;
        Phase = "Cancelling";
        return true;
    }

    public bool TryCommit(Func<bool> publish)
    {
        if (!CanPublish) return false;
        if (!publish()) return false;
        Phase = "Committed";
        return true;
    }

    public bool RecordFirstDraw()
    {
        if (!IsBusy || Phase != "Committed") return false;
        DrawnElapsedMilliseconds = ElapsedMilliseconds;
        Finish("Drawn");
        return true;
    }

    public void Finish(string outcome = "Finished")
    {
        if (!IsBusy) return;
        _finishedElapsed = ElapsedMilliseconds;
        Phase = Phase == "Cancelling" ? "Cancelled" : outcome;
        IsBusy = false;
    }
}
