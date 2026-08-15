using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Core.V3;

/// <summary>
/// 内存版 V3 槽存储：用槽文件字典模拟 Save/Load/Delete/List，便于在无文件系统环境中验证协议。
/// </summary>
public sealed class V3SlotStore
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, byte[]>> _slots = new(StringComparer.Ordinal);

    public bool Save(string slotId, V3Manifest manifest, IReadOnlyDictionary<string, byte[]> payloads)
    {
        if (!V3SlotId.IsValid(slotId) || !string.Equals(slotId, manifest.SlotId, StringComparison.Ordinal))
            return false;

        IReadOnlyDictionary<string, byte[]> files = V3SlotWriter.BuildFiles(manifest, payloads);
        _slots[slotId] = files;
        return true;
    }

    public V3SlotReadResult Load(string slotId)
    {
        if (!_slots.TryGetValue(slotId, out IReadOnlyDictionary<string, byte[]>? files))
            return V3SlotReadResult.Failure("MissingSlot");
        return V3SlotReader.Read(files);
    }

    public bool Delete(string slotId) => _slots.Remove(slotId);

    public IReadOnlyList<V3SlotSummary> List()
    {
        var children = _slots.ToDictionary(
            pair => pair.Key,
            pair => V3SlotOccupant.CompleteV3,
            StringComparer.Ordinal);
        return V3SlotLister.List(children);
    }
}
