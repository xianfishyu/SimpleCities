using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// 槽移动服务：复制到目标根后删除源槽。
/// </summary>
public static class V3SlotMoveService
{
    public static bool Move(string slotId, string sourceRoot, string destinationRoot)
    {
        ArgumentNullException.ThrowIfNull(sourceRoot);
        ArgumentNullException.ThrowIfNull(destinationRoot);

        if (!V3SlotCopyService.Copy(slotId, sourceRoot, destinationRoot))
            return false;
        return new V3FileSlotStore(sourceRoot).Delete(slotId);
    }
}
