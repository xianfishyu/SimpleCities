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

    public bool TryRemove(RoadRemovalSessionV3 session, out IReadOnlyList<int> removedEdgeIDs)
    {
        ArgumentNullException.ThrowIfNull(session);

        removedEdgeIDs = [];
        if (!session.TryCommit(out IReadOnlyList<int> edgeIDs) || edgeIDs.Count == 0)
            return false;

        foreach (int edgeID in edgeIDs)
        {
            if (!_controller.Facade.Revision.Edges.ContainsKey(edgeID))
                return false;
        }

        var removed = new List<int>();
        foreach (int edgeID in edgeIDs)
        {
            if (!_controller.TryRemoveEdge(edgeID, out _))
                return false;
            removed.Add(edgeID);
        }

        removedEdgeIDs = removed;
        return true;
    }

    public bool TryUpgrade(RoadUpgradeSessionV3 session, out IReadOnlyList<int> changedEdgeIDs)
    {
        ArgumentNullException.ThrowIfNull(session);

        changedEdgeIDs = [];
        if (!session.TryCommit(out IReadOnlyList<int> edgeIDs) || edgeIDs.Count == 0)
            return false;

        foreach (int edgeID in edgeIDs)
        {
            if (!_controller.Facade.Revision.Edges.ContainsKey(edgeID))
                return false;
        }

        if (!_controller.TryUpgradeSelection(edgeIDs, session.TargetType, out _))
            return false;

        changedEdgeIDs = edgeIDs;
        return true;
    }
}
