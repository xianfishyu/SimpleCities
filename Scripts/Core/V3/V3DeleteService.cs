using System;
using System.Collections.Generic;

namespace SimpleCities.Core.V3;

public sealed record V3DeleteResult(
    bool Success,
    V3DeleteDescriptor? Descriptor,
    string? Error)
{
    public static V3DeleteResult Failure(string error) => new(false, null, error);
}

/// <summary>
/// 删除服务：删除槽并生成 delete descriptor。
/// </summary>
public static class V3DeleteService
{
    public static V3DeleteResult Delete(string slotId, string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var store = new V3FileSlotStore(root);
        V3SlotReadResult read = store.Load(slotId);
        if (!read.Success || read.Manifest is null || read.Payloads is null)
            return V3DeleteResult.Failure(read.Error ?? "MissingSlot");

        IReadOnlyDictionary<string, byte[]> files = V3SlotWriter.BuildFiles(read.Manifest, read.Payloads);
        string digest = V3SlotDigest.Compute(files);

        if (!store.Delete(slotId))
            return V3DeleteResult.Failure("DeleteRejected");

        string operationId = Guid.NewGuid().ToString("N");
        var descriptor = new V3DeleteDescriptor(
            operationId,
            slotId,
            digest,
            $"{root.TrimEnd('/')}/.save-transactions/{slotId}/{operationId}/tombstone",
            $"delete {slotId}");

        return new V3DeleteResult(true, descriptor, null);
    }
}
