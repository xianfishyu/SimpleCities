using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SaveOperationTests
{
    [Fact]
    public void Token_Create_ProducesUniqueOperationIds()
    {
        V3SaveOperationToken first = V3SaveOperationToken.Create(V3SaveOperationKind.Publish, 1);
        V3SaveOperationToken second = V3SaveOperationToken.Create(V3SaveOperationKind.Publish, 1);

        Assert.NotEqual(first.OperationID, second.OperationID);
        Assert.Equal(V3SaveOperationKind.Publish, first.Kind);
    }

    [Fact]
    public void Succeeded_HasSuccessAndCommitCompleted()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Load, 1);
        V3SaveOperationResult result = V3SaveOperationResult.Succeeded(token);

        Assert.True(result.Success);
        Assert.True(result.CommitCompleted);
        Assert.Equal(V3SaveOperationPhase.Completed, result.Phase);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void FailedBeforeCommit_HasNoCommit()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Delete, 2);
        V3SaveOperationResult result = V3SaveOperationResult.FailedBeforeCommit(
            token,
            V3SaveOperationPhase.Preflight,
            "boom");

        Assert.False(result.Success);
        Assert.False(result.CommitCompleted);
        Assert.Equal(V3SaveOperationPhase.Preflight, result.Phase);
        Assert.Equal("boom", result.Error);
    }

    [Fact]
    public void SucceededWithObserverWarnings_ExposesWarnings()
    {
        V3SaveOperationToken token = V3SaveOperationToken.Create(V3SaveOperationKind.Load, 1);
        V3SaveOperationResult result = V3SaveOperationResult.SucceededWithObserverWarnings(
            token,
            ["observer failed"]);

        Assert.True(result.Success);
        Assert.True(result.CommitCompleted);
        Assert.Equal(new[] { "observer failed" }, result.Warnings);
    }
}
