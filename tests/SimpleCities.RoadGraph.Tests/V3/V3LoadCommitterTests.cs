using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3LoadCommitterTests
{
    [Fact]
    public void TryCommit_WhenPreflightAndAllPrepared_Succeeds()
    {
        var protocol = CreatePreflightProtocol();
        var aggregate = new V3PreparedAggregate(
            new HashSet<string> { "graph" },
            new HashSet<string> { "graph" },
            []);
        var committer = new V3LoadCommitter(protocol, aggregate);

        Assert.True(committer.TryCommit());
        Assert.Equal(V3LoadPhase.Completed, protocol.Phase);
    }

    [Fact]
    public void TryCommit_BeforePreflight_Fails()
    {
        var protocol = new V3LoadProtocol();
        protocol.TryEnterAdmission();
        protocol.TryEnterPrepare();
        var aggregate = new V3PreparedAggregate(
            new HashSet<string> { "graph" },
            new HashSet<string> { "graph" },
            []);
        var committer = new V3LoadCommitter(protocol, aggregate);

        Assert.False(committer.TryCommit());
        Assert.Equal(V3LoadPhase.Prepare, protocol.Phase);
    }

    [Fact]
    public void TryCommit_MissingParticipant_Fails()
    {
        var protocol = CreatePreflightProtocol();
        var aggregate = new V3PreparedAggregate(
            new HashSet<string> { "graph", "tool" },
            new HashSet<string> { "graph" },
            []);
        var committer = new V3LoadCommitter(protocol, aggregate);

        Assert.False(committer.TryCommit());
        Assert.Equal(V3LoadPhase.Preflight, protocol.Phase);
    }

    private static V3LoadProtocol CreatePreflightProtocol()
    {
        var protocol = new V3LoadProtocol();
        protocol.TryEnterAdmission();
        protocol.TryEnterPrepare();
        protocol.TryEnterPreflight();
        return protocol;
    }
}
