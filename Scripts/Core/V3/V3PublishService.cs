using System;
using System.Collections.Generic;

namespace SimpleCities.Core.V3;

public sealed record V3PublishResult(
    bool Success,
    V3PublishDescriptor? Descriptor,
    string? Error)
{
    public static V3PublishResult Failure(string error) => new(false, null, error);
}

/// <summary>
/// 发布服务：保存新槽并生成 publish descriptor（旧/新聚合 digest）。
/// </summary>
public static class V3PublishService
{
    public static V3PublishResult Publish(
        string slotId,
        string root,
        V3Manifest manifest,
        IReadOnlyDictionary<string, byte[]> payloads)
    {
        ArgumentNullException.ThrowIfNull(root);

        var store = new V3FileSlotStore(root);
        string oldDigest = string.Empty;
        V3SlotReadResult oldRead = store.Load(slotId);
        if (oldRead.Success && oldRead.Manifest is not null && oldRead.Payloads is not null)
        {
            IReadOnlyDictionary<string, byte[]> oldFiles = V3SlotWriter.BuildFiles(oldRead.Manifest, oldRead.Payloads);
            oldDigest = V3SlotDigest.Compute(oldFiles);
        }

        IReadOnlyDictionary<string, byte[]> newFiles = V3SlotWriter.BuildFiles(manifest, payloads);
        string newDigest = V3SlotDigest.Compute(newFiles);

        if (!store.Save(slotId, manifest, payloads))
            return V3PublishResult.Failure("SaveRejected");

        string operationId = Guid.NewGuid().ToString("N");
        var descriptor = new V3PublishDescriptor(
            operationId,
            slotId,
            oldDigest,
            newDigest,
            $"{root.TrimEnd('/')}/.save-transactions/{slotId}/{operationId}/staging",
            $"{root.TrimEnd('/')}/.save-transactions/{slotId}/{operationId}/backup");

        return new V3PublishResult(true, descriptor, null);
    }
}
