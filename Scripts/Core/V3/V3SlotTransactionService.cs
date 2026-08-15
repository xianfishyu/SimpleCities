using System;
using System.Collections.Generic;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽事务服务：统一 Publish / Delete / Recover 操作入口。
/// </summary>
public static class V3SlotTransactionService
{
    public static V3PublishResult Publish(
        string slotId,
        string root,
        V3Manifest manifest,
        IReadOnlyDictionary<string, byte[]> payloads) =>
        V3PublishService.Publish(slotId, root, manifest, payloads);

    public static V3DeleteResult Delete(string slotId, string root) =>
        V3DeleteService.Delete(slotId, root);

    public static bool Recover(string slotId, string root, string backupRoot) =>
        V3SlotRecoveryService.Recover(slotId, root, backupRoot);
}
