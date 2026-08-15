using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 工具命令执行器：把铺路/改造会话转换为控制器命令。
/// </summary>
public sealed class RoadToolCommandExecutor
{
    private readonly RoadGraphV3Controller _controller;

    public RoadToolCommandExecutor(RoadGraphV3Controller controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    public bool TryBuild(RoadPlacementSessionV3 session, out RoadGraphV3ChangeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(session);

        summary = null!;
        if (!session.TryCommit(out RoadBuildRequest? request))
            return false;
        return _controller.TryBuild(request, out summary);
    }

    public bool TryUpgrade(RoadUpgradeSessionV3 session, out IReadOnlyList<int> changedEdgeIDs)
    {
        ArgumentNullException.ThrowIfNull(session);

        changedEdgeIDs = [];
        if (!session.TryCommit(out IReadOnlyList<int> edgeIDs) || edgeIDs.Count == 0)
            return false;

        var changed = new List<int>();
        foreach (int edgeID in edgeIDs)
        {
            if (!_controller.TryChangeRoadType(edgeID, session.TargetType, out _))
                return false;
            changed.Add(edgeID);
        }

        changedEdgeIDs = changed;
        return true;
    }
}
