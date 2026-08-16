using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Core.V3;

/// <summary>
/// Load aggregate 协调器：组合 V3LoadProtocol 与 V3PreparedAggregate，
/// 在 Preflight 全参与者就绪后执行一次 non-yield commit；commit 抛错会使协议进入 Failed。
/// </summary>
public sealed class V3LoadAggregateCoordinator
{
    private readonly V3LoadProtocol _protocol = new();
    private readonly HashSet<string> _required;
    private readonly HashSet<string> _prepared = new(StringComparer.Ordinal);
    private readonly List<string> _warnings = [];

    public V3LoadPhase Phase => _protocol.Phase;

    public V3PreparedAggregate Aggregate =>
        new(_required, _prepared, _warnings);

    public V3LoadAggregateCoordinator(IEnumerable<string> requiredParticipants)
    {
        ArgumentNullException.ThrowIfNull(requiredParticipants);
        _required = new HashSet<string>(requiredParticipants, StringComparer.Ordinal);
        if (_required.Count == 0)
            throw new ArgumentException("At least one required participant is needed.", nameof(requiredParticipants));
    }

    public bool TryBegin() => _protocol.TryEnterAdmission();

    public bool TryEnterPrepare() => _protocol.TryEnterPrepare();

    public bool TryPrepare(string participant)
    {
        if (!_required.Contains(participant))
            return false;
        return _prepared.Add(participant);
    }

    public void AddWarning(string warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        _warnings.Add(warning);
    }

    public bool TryEnterPreflight()
    {
        if (_protocol.Phase != V3LoadPhase.Prepare || !Aggregate.AllPrepared)
            return false;
        return _protocol.TryEnterPreflight();
    }

    public bool TryCommit(Action commitAction)
    {
        ArgumentNullException.ThrowIfNull(commitAction);
        if (_protocol.Phase != V3LoadPhase.Preflight || !Aggregate.AllPrepared)
            return false;
        if (!_protocol.TryEnterCommit())
            return false;

        try
        {
            commitAction();
        }
        catch
        {
            _protocol.Fail();
            return false;
        }

        return _protocol.Complete();
    }

    public bool Fail() => _protocol.Fail();
}
