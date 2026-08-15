using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3AutosavePolicyTests
{
    [Fact]
    public void Decide_BusyWithoutPending_QueuesPending()
    {
        Assert.Equal(V3AutosaveDecision.QueuePending, V3AutosavePolicy.Decide(isBusy: true, pendingExists: false, hasNewerSuccess: false));
    }

    [Fact]
    public void Decide_BusyWithPending_SkipsDuplicate()
    {
        Assert.Equal(V3AutosaveDecision.SkipBusy, V3AutosavePolicy.Decide(isBusy: true, pendingExists: true, hasNewerSuccess: false));
    }

    [Fact]
    public void Decide_IdleWithoutNewerSuccess_RunsNow()
    {
        Assert.Equal(V3AutosaveDecision.RunNow, V3AutosavePolicy.Decide(isBusy: false, pendingExists: false, hasNewerSuccess: false));
    }

    [Fact]
    public void Decide_IdleWithNewerSuccess_Skips()
    {
        Assert.Equal(V3AutosaveDecision.SkipBusy, V3AutosavePolicy.Decide(isBusy: false, pendingExists: false, hasNewerSuccess: true));
    }
}
