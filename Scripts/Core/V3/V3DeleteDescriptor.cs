using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// 不可变 delete descriptor：绑定槽、待删 digest、tombstone 路径与确认摘要。
/// </summary>
public sealed record V3DeleteDescriptor(
    string OperationId,
    string SlotId,
    string Digest,
    string TombstonePath,
    string ConfirmationSummary);

public static class V3DeleteDescriptorValidator
{
    public static bool IsValid(V3DeleteDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return V3TransactionPath.IsValidOperationId(descriptor.OperationId) &&
               V3SlotId.IsValid(descriptor.SlotId) &&
               !string.IsNullOrWhiteSpace(descriptor.Digest) &&
               !string.IsNullOrWhiteSpace(descriptor.TombstonePath) &&
               !string.IsNullOrWhiteSpace(descriptor.ConfirmationSummary);
    }
}
