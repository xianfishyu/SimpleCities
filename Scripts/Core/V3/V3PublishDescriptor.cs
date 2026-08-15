using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// 不可变 publish descriptor：绑定槽、新旧 digest 与固定 staging/backup 路径。
/// </summary>
public sealed record V3PublishDescriptor(
    string OperationId,
    string SlotId,
    string OldDigest,
    string NewDigest,
    string StagingPath,
    string BackupPath);

public static class V3PublishDescriptorValidator
{
    public static bool IsValid(V3PublishDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return V3TransactionPath.IsValidOperationId(descriptor.OperationId) &&
               V3SlotId.IsValid(descriptor.SlotId) &&
               !string.IsNullOrWhiteSpace(descriptor.OldDigest) &&
               !string.IsNullOrWhiteSpace(descriptor.NewDigest) &&
               !string.IsNullOrWhiteSpace(descriptor.StagingPath) &&
               !string.IsNullOrWhiteSpace(descriptor.BackupPath);
    }
}
