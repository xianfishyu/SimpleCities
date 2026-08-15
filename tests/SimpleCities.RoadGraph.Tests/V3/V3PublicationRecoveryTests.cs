using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3PublicationRecoveryTests
{
    [Fact]
    public void Decide_SlotMatchesNew_ReturnsPublishComplete()
    {
        Assert.Equal(
            V3PublicationRecoveryDecision.PublishComplete,
            V3PublicationRecovery.Decide(slotExists: true, slotMatchesNew: true, slotMatchesOld: false, backupMatchesOld: false, stagingMatchesNew: false));
    }

    [Fact]
    public void Decide_SlotMatchesOld_ReturnsPreserveOldIsolateStaging()
    {
        Assert.Equal(
            V3PublicationRecoveryDecision.PreserveOldIsolateStaging,
            V3PublicationRecovery.Decide(slotExists: true, slotMatchesNew: false, slotMatchesOld: true, backupMatchesOld: false, stagingMatchesNew: false));
    }

    [Fact]
    public void Decide_SlotMissingBackupAndStagingMatch_ReturnsCompleteStagingToSlot()
    {
        Assert.Equal(
            V3PublicationRecoveryDecision.CompleteStagingToSlot,
            V3PublicationRecovery.Decide(slotExists: false, slotMatchesNew: false, slotMatchesOld: false, backupMatchesOld: true, stagingMatchesNew: true));
    }

    [Fact]
    public void Decide_SlotMissingOnlyBackupMatches_ReturnsRestoreOldFromBackup()
    {
        Assert.Equal(
            V3PublicationRecoveryDecision.RestoreOldFromBackup,
            V3PublicationRecovery.Decide(slotExists: false, slotMatchesNew: false, slotMatchesOld: false, backupMatchesOld: true, stagingMatchesNew: false));
    }

    [Fact]
    public void Decide_OtherCombinations_ReturnBlocked()
    {
        Assert.Equal(
            V3PublicationRecoveryDecision.Blocked,
            V3PublicationRecovery.Decide(slotExists: false, slotMatchesNew: false, slotMatchesOld: false, backupMatchesOld: false, stagingMatchesNew: false));
        Assert.Equal(
            V3PublicationRecoveryDecision.Blocked,
            V3PublicationRecovery.Decide(slotExists: true, slotMatchesNew: false, slotMatchesOld: false, backupMatchesOld: true, stagingMatchesNew: true));
    }
}
