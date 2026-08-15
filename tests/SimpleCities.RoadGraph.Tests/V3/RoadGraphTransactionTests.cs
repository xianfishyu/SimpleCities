using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadGraphTransactionTests
{
    [Fact]
    public void Incremental_SortsAndDeduplicatesIds()
    {
        RoadGraphV3ChangeSummary summary = RoadGraphV3ChangeSummaryFactory.Incremental(
            [3, 1, 3],
            [2],
            [],
            [5, 4, 5],
            [],
            [],
            42);

        Assert.Equal(new[] { 1, 3 }, summary.CreatedNodeIDs);
        Assert.Equal(new[] { 2 }, summary.RemovedNodeIDs);
        Assert.Equal(new[] { 4, 5 }, summary.CreatedEdgeIDs);
        Assert.False(summary.IsFullReset);
        Assert.Equal(42, summary.ChangeSequence);
    }

    [Fact]
    public void FullReset_HasExpectedFlagsAndSequence()
    {
        RoadGraphV3ChangeSummary summary = RoadGraphV3ChangeSummary.FullReset(7);

        Assert.True(summary.IsFullReset);
        Assert.Equal(7, summary.ChangeSequence);
        Assert.Empty(summary.CreatedNodeIDs);
        Assert.Empty(summary.CreatedEdgeIDs);
    }

    [Fact]
    public void Token_MatchesExactFields()
    {
        var token = new GraphStateToken(1, 2, 3);

        Assert.True(token.Matches(new GraphStateToken(1, 2, 3)));
        Assert.False(token.Matches(new GraphStateToken(2, 2, 3)));
        Assert.False(token.Matches(new GraphStateToken(1, 3, 3)));
        Assert.False(token.Matches(new GraphStateToken(1, 2, 4)));
    }
}
