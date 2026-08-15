using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// 一次性 publish lease：绑定 operation、slot 与签发时间。
/// </summary>
public sealed record V3PublishLease(
    string OperationId,
    string SlotId,
    DateTimeOffset IssuedUtc);

public static class V3PublishLeaseValidator
{
    public static bool IsValid(V3PublishLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return V3TransactionPath.IsValidOperationId(lease.OperationId) &&
               V3SlotId.IsValid(lease.SlotId) &&
               lease.IssuedUtc != default;
    }
}
