using System;
using System.IO;

namespace SimpleCities.Core.V3;

/// <summary>
/// 单个槽状态服务：返回指定槽的 occupant 摘要。
/// </summary>
public static class V3SlotStatusService
{
    public static V3SlotSummary GetStatus(string slotId, string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (!V3SlotId.IsValid(slotId))
            return new V3SlotSummary(slotId, slotId, V3SlotOccupant.Unsafe, null);

        string slotDirectory = Path.Combine(root, slotId);
        if (!Directory.Exists(slotDirectory))
            return new V3SlotSummary(slotId, slotId, V3SlotOccupant.Absent, null);

        V3SlotIntegrityResult integrity = V3SlotIntegrity.Verify(slotDirectory);
        var occupant = integrity.Success ? V3SlotOccupant.CompleteV3 : V3SlotOccupant.CorruptV3;
        return new V3SlotSummary(slotId, slotId, occupant, null);
    }
}
