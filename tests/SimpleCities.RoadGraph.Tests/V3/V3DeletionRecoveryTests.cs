using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3DeletionRecoveryTests
{
    [Fact]
    public void Decide_SlotExists_ReturnsNotDeleted()
    {
        Assert.Equal(V3DeletionRecoveryDecision.NotDeleted, V3DeletionRecovery.Decide(slotExists: true, tombstoneMatchesDescriptor: false));
    }

    [Fact]
    public void Decide_SlotMissingAndTombstoneMatches_ReturnsContinueCleanup()
    {
        Assert.Equal(V3DeletionRecoveryDecision.ContinueCleanup, V3DeletionRecovery.Decide(slotExists: false, tombstoneMatchesDescriptor: true));
    }

    [Fact]
    public void Decide_SlotMissingAndTombstoneMismatch_ReturnsBlocked()
    {
        Assert.Equal(V3DeletionRecoveryDecision.Blocked, V3DeletionRecovery.Decide(slotExists: false, tombstoneMatchesDescriptor: false));
    }
}
