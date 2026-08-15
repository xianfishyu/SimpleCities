using System;
using System.Text;
using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

public sealed record V3SlotLoadServiceResult(
    bool Success,
    RoadGraphV3Revision? Revision,
    string? Error)
{
    public static V3SlotLoadServiceResult Failure(string error) => new(false, null, error);
}

/// <summary>
/// 从文件槽加载道路 revision：读取槽文件、严格解析 road payload、重建不可变 root。
/// </summary>
public static class V3SlotLoadService
{
    public static V3SlotLoadServiceResult Load(
        string slotId,
        string root,
        RoadGraphCapacity capacity,
        V3PayloadBudget budget)
    {
        ArgumentNullException.ThrowIfNull(root);

        V3SlotReadResult read = new V3FileSlotStore(root).Load(slotId);
        if (!read.Success || read.Manifest is null || read.Payloads is null)
            return V3SlotLoadServiceResult.Failure(read.Error ?? "LoadFailed");

        foreach (V3ManifestFile file in read.Manifest.Files)
        {
            if (!read.Payloads.TryGetValue(file.Name, out byte[]? fileBytes))
                return V3SlotLoadServiceResult.Failure($"MissingPayload:{file.Name}");
            if (!V3PayloadDigest.Matches(file, fileBytes))
                return V3SlotLoadServiceResult.Failure($"PayloadDigestMismatch:{file.Name}");
        }

        if (!read.Payloads.TryGetValue(V3RoadSlotFactory.RoadNetworkFileName, out byte[]? payloadBytes))
            return V3SlotLoadServiceResult.Failure("MissingRoadPayload");

        string json = Encoding.UTF8.GetString(payloadBytes);
        V3StrictRoadPayloadResult strict = V3RoadPayloadStrictReader.Read(json, budget);
        if (!strict.Success)
            return V3SlotLoadServiceResult.Failure(strict.Error ?? "InvalidRoadPayload");

        RoadGraphV3PersistenceResult persistence = RoadGraphV3Persistence.Deserialize(json, capacity);
        if (!persistence.Success || persistence.Revision is null)
            return V3SlotLoadServiceResult.Failure(persistence.Error ?? "DeserializeFailed");

        return new V3SlotLoadServiceResult(true, persistence.Revision, null);
    }
}
