using System;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// 道路表面命中 provider：只接受与已呈现 snapshot 同 token 且 owner 仍存在的命中。
/// </summary>
public sealed class RoadSurfaceHitProvider
{
    private readonly RoadPresentationState _state;

    public RoadSurfaceHitProvider(RoadPresentationState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public bool TryResolve(RoadSurfaceHit hit, out RoadSurfaceHit resolved)
    {
        ArgumentNullException.ThrowIfNull(hit);

        resolved = null!;
        if (_state.IsStalled)
            return false;

        RoadSurfaceSnapshot? snapshot = _state.PresentedSnapshot;
        if (snapshot is null || !snapshot.IsValid)
            return false;

        if (hit.Token != snapshot.Token)
            return false;

        bool ownerExists = snapshot.Owners.Any(owner =>
            owner.Kind == hit.OwnerKind &&
            owner.NodeID == hit.NodeID &&
            owner.EdgeID == hit.EdgeID &&
            owner.Endpoint == hit.Endpoint);
        if (!ownerExists)
            return false;

        resolved = hit;
        return true;
    }

    public bool TryResolveEdge(RoadSurfaceHit hit, out int edgeID)
    {
        if (!TryResolve(hit, out RoadSurfaceHit resolved) || resolved.EdgeID is not int id)
        {
            edgeID = 0;
            return false;
        }

        edgeID = id;
        return true;
    }
}
