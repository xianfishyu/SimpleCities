using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3LoadProtocolTests
{
    [Fact]
    public void HappyPath_TransitionsInOrder()
    {
        var protocol = new V3LoadProtocol();

        Assert.True(protocol.TryEnterAdmission());
        Assert.True(protocol.TryEnterPrepare());
        Assert.True(protocol.TryEnterPreflight());
        Assert.True(protocol.TryEnterCommit());
        Assert.True(protocol.Complete());

        Assert.Equal(V3LoadPhase.Completed, protocol.Phase);
    }

    [Fact]
    public void CannotSkipPrepare()
    {
        var protocol = new V3LoadProtocol();
        protocol.TryEnterAdmission();

        Assert.False(protocol.TryEnterPreflight());
    }

    [Fact]
    public void CannotCommitBeforePreflight()
    {
        var protocol = new V3LoadProtocol();
        protocol.TryEnterAdmission();
        protocol.TryEnterPrepare();

        Assert.False(protocol.TryEnterCommit());
    }

    [Fact]
    public void Fail_SetsFailed()
    {
        var protocol = new V3LoadProtocol();
        protocol.TryEnterAdmission();

        Assert.True(protocol.Fail());
        Assert.Equal(V3LoadPhase.Failed, protocol.Phase);
    }
}
