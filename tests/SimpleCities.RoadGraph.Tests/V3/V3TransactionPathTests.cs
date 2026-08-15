using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3TransactionPathTests
{
    [Fact]
    public void TryGetTransactionDirectory_ReturnsExpectedPath()
    {
        Assert.True(V3TransactionPath.TryGetTransactionDirectory("user://saves-v3", "city-001", "op-1", out string path));
        Assert.Equal("user://saves-v3/.save-transactions/city-001/op-1", path);
    }

    [Fact]
    public void TryGetTransactionDirectory_RejectsInvalidSlotOrOperation()
    {
        Assert.False(V3TransactionPath.TryGetTransactionDirectory("user://saves-v3", "bad/slot", "op-1", out _));
        Assert.False(V3TransactionPath.TryGetTransactionDirectory("user://saves-v3", "city-001", "bad/op", out _));
    }

    [Fact]
    public void TryGetStagingAndBackupPaths_AreUnderTransactionDirectory()
    {
        Assert.True(V3TransactionPath.TryGetStagingPath("user://saves-v3", "city-001", "op-1", out string staging));
        Assert.True(V3TransactionPath.TryGetBackupPath("user://saves-v3", "city-001", "op-1", out string backup));

        Assert.Equal("user://saves-v3/.save-transactions/city-001/op-1/staging", staging);
        Assert.Equal("user://saves-v3/.save-transactions/city-001/op-1/backup", backup);
    }

    [Fact]
    public void TryGetPublishDescriptorPath_ReturnsJsonPath()
    {
        Assert.True(V3TransactionPath.TryGetPublishDescriptorPath("user://saves-v3", "city-001", "op-1", out string path));
        Assert.Equal("user://saves-v3/.save-transactions/city-001/op-1/publish.json", path);
    }
}
