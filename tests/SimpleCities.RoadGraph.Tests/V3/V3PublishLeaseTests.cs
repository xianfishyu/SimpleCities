using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3PublishLeaseTests
{
    [Fact]
    public void IsValid_AcceptsValidLease()
    {
        var lease = new V3PublishLease("op-1", "city-001", DateTimeOffset.UtcNow);

        Assert.True(V3PublishLeaseValidator.IsValid(lease));
    }

    [Fact]
    public void IsValid_RejectsInvalidOperationOrSlot()
    {
        Assert.False(V3PublishLeaseValidator.IsValid(new V3PublishLease("bad/op", "city-001", DateTimeOffset.UtcNow)));
        Assert.False(V3PublishLeaseValidator.IsValid(new V3PublishLease("op-1", "bad/slot", DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void IsValid_RejectsDefaultTimestamp()
    {
        Assert.False(V3PublishLeaseValidator.IsValid(new V3PublishLease("op-1", "city-001", default)));
    }
}
