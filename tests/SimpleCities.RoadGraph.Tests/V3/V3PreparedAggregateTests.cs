using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3PreparedAggregateTests
{
    [Fact]
    public void AllPrepared_TrueWhenAllRequiredArePrepared()
    {
        var aggregate = new V3PreparedAggregate(
            new HashSet<string> { "graph", "tool", "renderer" },
            new HashSet<string> { "graph", "tool", "renderer" },
            []);

        Assert.True(aggregate.AllPrepared);
        Assert.True(aggregate.CanCommit);
        Assert.Empty(aggregate.MissingParticipants);
    }

    [Fact]
    public void AllPrepared_FalseWhenAnyRequiredMissing()
    {
        var aggregate = new V3PreparedAggregate(
            new HashSet<string> { "graph", "tool", "renderer" },
            new HashSet<string> { "graph" },
            []);

        Assert.False(aggregate.AllPrepared);
        Assert.False(aggregate.CanCommit);
        Assert.Equal(new[] { "renderer", "tool" }, aggregate.MissingParticipants);
    }

    [Fact]
    public void Warnings_AreExposed()
    {
        var aggregate = new V3PreparedAggregate(
            new HashSet<string> { "graph" },
            new HashSet<string> { "graph" },
            ["observer failed"]);

        Assert.Equal(new[] { "observer failed" }, aggregate.Warnings);
    }
}
