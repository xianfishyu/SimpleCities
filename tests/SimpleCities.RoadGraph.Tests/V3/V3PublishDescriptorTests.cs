using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3PublishDescriptorTests
{
    [Fact]
    public void IsValid_AcceptsValidDescriptor()
    {
        var descriptor = ValidDescriptor();

        Assert.True(V3PublishDescriptorValidator.IsValid(descriptor));
    }

    [Fact]
    public void IsValid_RejectsInvalidSlotOrOperation()
    {
        Assert.False(V3PublishDescriptorValidator.IsValid(ValidDescriptor() with { SlotId = "bad/slot" }));
        Assert.False(V3PublishDescriptorValidator.IsValid(ValidDescriptor() with { OperationId = "bad/op" }));
    }

    [Fact]
    public void IsValid_RejectsEmptyDigestOrPath()
    {
        Assert.False(V3PublishDescriptorValidator.IsValid(ValidDescriptor() with { OldDigest = "" }));
        Assert.False(V3PublishDescriptorValidator.IsValid(ValidDescriptor() with { NewDigest = "" }));
        Assert.False(V3PublishDescriptorValidator.IsValid(ValidDescriptor() with { StagingPath = "" }));
        Assert.False(V3PublishDescriptorValidator.IsValid(ValidDescriptor() with { BackupPath = "" }));
    }

    private static V3PublishDescriptor ValidDescriptor() =>
        new(
            "op-1",
            "city-001",
            "old-digest",
            "new-digest",
            "user://saves-v3/.save-transactions/city-001/op-1/staging",
            "user://saves-v3/.save-transactions/city-001/op-1/backup");
}
