using System;
using SimpleCities.Road.V3;

namespace SimpleCities.Core.V3;

/// <summary>
/// autosave 协调器：根据进程内 gate 是否可获取决定 isBusy，
/// 并保持 autosave 合并策略与手动保存/加载排他。
/// </summary>
public sealed class V3SlotAutosaveCoordinator
{
    private readonly V3CoordinatorGate _gate;
    private readonly V3SlotAutosaveService _service;

    public V3SlotAutosaveCoordinator() : this(new V3CoordinatorGate(), new V3SlotAutosaveService())
    {
    }

    public V3SlotAutosaveCoordinator(V3CoordinatorGate gate, V3SlotAutosaveService service)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public bool HasPendingAutosave => _service.HasPendingAutosave;

    public V3AutosaveDecision TryAutosave(
        string slotId,
        string root,
        RoadGraphV3Revision revision,
        bool hasNewerSuccess,
        out bool saved)
    {
        saved = false;
        if (!_gate.TryAcquire(out Guid operationId))
            return _service.TryAutosave(slotId, root, revision, true, hasNewerSuccess, out saved);

        try
        {
            return _service.TryAutosave(slotId, root, revision, false, hasNewerSuccess, out saved);
        }
        finally
        {
            _gate.Release(operationId);
        }
    }
}
