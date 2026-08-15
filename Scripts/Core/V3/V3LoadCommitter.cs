using System;

namespace SimpleCities.Core.V3;

/// <summary>
/// Load 提交门：只有协议处于 Preflight 且所有 participant 已准备时才允许进入 non-yield commit。
/// </summary>
public sealed class V3LoadCommitter
{
    private readonly V3LoadProtocol _protocol;
    private readonly V3PreparedAggregate _aggregate;

    public V3LoadCommitter(V3LoadProtocol protocol, V3PreparedAggregate aggregate)
    {
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
    }

    public bool TryCommit()
    {
        if (_protocol.Phase != V3LoadPhase.Preflight || !_aggregate.AllPrepared)
            return false;
        if (!_protocol.TryEnterCommit())
            return false;
        return _protocol.Complete();
    }

    public bool Fail() => _protocol.Fail();
}
