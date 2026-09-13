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
    public double WorkerQueueMilliseconds { get; private set; } = -1;
    public double WorkerMilliseconds { get; private set; } = -1;
    public double ResumeMilliseconds { get; private set; } = -1;
    public double PreflightMilliseconds { get; private set; } = -1;
    public double PublicationMilliseconds { get; private set; } = -1;
    public double DomainMilliseconds { get; private set; } = -1;
    public double PresentationPrepareMilliseconds { get; private set; } = -1;
    public double ReferenceCommitMilliseconds { get; private set; } = -1;
    public double PresentationCommitMilliseconds { get; private set; } = -1;

    public void RecordWorker(long queuedAt, long startedAt, long domainFinishedAt, long finishedAt)
    {
        WorkerQueueMilliseconds = Stopwatch.GetElapsedTime(queuedAt, startedAt).TotalMilliseconds;
        WorkerMilliseconds = Stopwatch.GetElapsedTime(startedAt, finishedAt).TotalMilliseconds;
        DomainMilliseconds = Stopwatch.GetElapsedTime(startedAt, domainFinishedAt).TotalMilliseconds;
        PresentationPrepareMilliseconds = Stopwatch.GetElapsedTime(domainFinishedAt, finishedAt).TotalMilliseconds;
        ResumeMilliseconds = Stopwatch.GetElapsedTime(finishedAt).TotalMilliseconds;
    }

    public void RecordPreflight(long startedAt) => PreflightMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
    public void RecordPublication(long startedAt, long referenceFinishedAt, long displayFinishedAt)
    {
        ReferenceCommitMilliseconds = Stopwatch.GetElapsedTime(startedAt, referenceFinishedAt).TotalMilliseconds;
        PresentationCommitMilliseconds = Stopwatch.GetElapsedTime(referenceFinishedAt, displayFinishedAt).TotalMilliseconds;
        PublicationMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
    }

    public bool TryBegin(long? inputTimestamp = null)
    {
        if (IsBusy) return false;
        IsBusy = true;
        Phase = "Preparing";
        DrawnElapsedMilliseconds = -1;
        WorkerQueueMilliseconds = WorkerMilliseconds = ResumeMilliseconds = PreflightMilliseconds = PublicationMilliseconds = -1;
        DomainMilliseconds = PresentationPrepareMilliseconds = ReferenceCommitMilliseconds = PresentationCommitMilliseconds = -1;
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
