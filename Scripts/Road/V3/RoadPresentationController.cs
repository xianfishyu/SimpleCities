using System;

namespace SimpleCities.Road.V3;

/// <summary>
/// 道路表现控制器：把权威图 revision 构建为表面快照，并只允许 desired token 匹配的后台结果发布。
/// </summary>
public sealed class RoadPresentationController
{
    private readonly RoadPresentationState _state;
    private readonly RoadStyleProvider _styles;
    private RoadSurfaceSnapshot? _pendingSnapshot;

    public RoadPresentationController(RoadPresentationState state, RoadStyleProvider styles)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
        HitProvider = new RoadSurfaceHitProvider(_state);
    }

    public bool IsStalled => _state.IsStalled;
    public RoadSurfaceSnapshot? PresentedSnapshot => _state.PresentedSnapshot;
    public RoadSurfaceSnapshot? PendingSnapshot => _pendingSnapshot;
    public RoadSurfaceHitProvider HitProvider { get; }
    public RoadPresentationState State => _state;

    public void Reset(RoadRenderToken token)
    {
        _pendingSnapshot = null;
        _state.SetDesired(token);
        _state.TryPublish(token, new RoadSurfaceSnapshot(new GraphStateToken(0, 0, 0), []));
    }

    public bool TryRequest(
        RoadGraphV3Revision revision,
        GraphStateToken graphToken,
        RoadRenderToken desiredToken)
    {
        ArgumentNullException.ThrowIfNull(revision);

        RoadSurfaceSnapshotBuildResult build = RoadSurfaceSnapshotBuilder.Build(revision, graphToken, _styles);
        if (!build.Success || build.Snapshot is null)
            return false;

        _pendingSnapshot = build.Snapshot;
        _state.SetDesired(desiredToken);
        return true;
    }

    public bool TryPublish(RoadRenderToken resultToken)
    {
        if (_pendingSnapshot is null)
            return false;

        if (!_state.TryPublish(resultToken, _pendingSnapshot))
            return false;

        _pendingSnapshot = null;
        return true;
    }

    public bool TryApplyFullReset(RoadPresentationFullReset plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.TryApplyTo(_state);
    }
}
