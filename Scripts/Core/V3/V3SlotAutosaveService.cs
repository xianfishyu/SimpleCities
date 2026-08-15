using System;
using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

/// <summary>
/// autosave 服务：结合 autosave 合并策略与槽保存服务，维护单个 pending 标记。
/// </summary>
public sealed class V3SlotAutosaveService
{
    private bool _pending;

    public bool HasPendingAutosave => _pending;

    public V3AutosaveDecision TryAutosave(
        string slotId,
        string root,
        RoadGraphV3Revision revision,
        bool isBusy,
        bool hasNewerSuccess,
        out bool saved)
    {
        saved = false;
        V3AutosaveDecision decision = V3AutosavePolicy.Decide(isBusy, _pending, hasNewerSuccess);

        switch (decision)
        {
            case V3AutosaveDecision.RunNow:
                saved = V3SlotSaveService.Save(
                    slotId,
                    root,
                    revision,
                    slotId,
                    slotId,
                    DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'"),
                    null,
                    null,
                    null);
                _pending = false;
                break;
            case V3AutosaveDecision.QueuePending:
                _pending = true;
                break;
            case V3AutosaveDecision.SkipBusy:
                break;
        }

        return decision;
    }
}
