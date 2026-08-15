using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3DeleteDescriptorTests
{
    [Fact]
    public void IsValid_AcceptsValidDescriptor()
    {
        Assert.True(V3DeleteDescriptorValidator.IsValid(ValidDescriptor()));
    }

    [Fact]
    public void IsValid_RejectsInvalidSlotOrOperation()
    {
        Assert.False(V3DeleteDescriptorValidator.IsValid(ValidDescriptor() with { SlotId = "bad/slot" }));
        Assert.False(V3DeleteDescriptorValidator.IsValid(ValidDescriptor() with { OperationId = "bad/op" }));
    }

    [Fact]
    public void IsValid_RejectsEmptyDigestOrPathOrSummary()
    {
        Assert.False(V3DeleteDescriptorValidator.IsValid(ValidDescriptor() with { Digest = "" }));
        Assert.False(V3DeleteDescriptorValidator.IsValid(ValidDescriptor() with { TombstonePath = "" }));
        Assert.False(V3DeleteDescriptorValidator.IsValid(ValidDescriptor() with { ConfirmationSummary = "" }));
    }

    private static V3DeleteDescriptor ValidDescriptor() =>
        new(
            "op-1",
            "city-001",
            "digest",
            "user://saves-v3/.save-transactions/city-001/op-1/tombstone",
            "delete city-001");
}
