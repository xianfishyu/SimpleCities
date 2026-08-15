using Godot;
using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// 组合 facade + 有界 delta 历史的控制器：普通 mutation 记录 delta，undo/redo 通过
/// delta applier 应用逆/正 delta 并递增 ChangeSequence。
/// </summary>
public sealed class RoadGraphV3Controller
{
    private readonly RoadGraphV3Facade _facade;
    private readonly RoadEditHistoryV3 _history;

    public RoadGraphV3Facade Facade => _facade;
    public RoadEditHistoryV3 History => _history;

    public RoadGraphV3Controller(RoadGraphV3Facade facade, RoadEditHistoryV3 history)
    {
        _facade = facade ?? throw new ArgumentNullException(nameof(facade));
        _history = history ?? throw new ArgumentNullException(nameof(history));
    }

    public bool TryAddNode(Vector2 position, out RoadGraphV3ChangeSummary summary)
    {
        RoadGraphV3Snapshot before = _facade.CaptureSnapshot();
        long beforeRevision = before.Token.DomainRevisionID;
        if (!_facade.TryAddNode(position, out summary, out int nodeID))
            return false;

        RoadGraphV3Node after = _facade.Revision.Nodes[nodeID];
        var delta = new RoadGraphV3Delta(
            beforeRevision,
            _facade.CurrentToken.DomainRevisionID,
            [new RoadGraphV3EntityChange<RoadGraphV3Node>(null, after)],
            []);
        if (!_history.TryPush(delta))
        {
            _facade.Restore(before);
            return false;
        }

        return true;
    }

    public bool TryAddEdge(
        int nodeAID,
        int nodeBID,
        IReadOnlyList<RoadGeometrySegment> geometry,
        RoadType roadType,
        out RoadGraphV3ChangeSummary summary)
    {
        RoadGraphV3Snapshot before = _facade.CaptureSnapshot();
        long beforeRevision = before.Token.DomainRevisionID;
        if (!_facade.TryAddEdge(nodeAID, nodeBID, geometry, roadType, out summary, out int edgeID))
            return false;

        RoadGraphV3Edge after = _facade.Revision.Edges[edgeID];
        var delta = new RoadGraphV3Delta(
            beforeRevision,
            _facade.CurrentToken.DomainRevisionID,
            [],
            [new RoadGraphV3EntityChange<RoadGraphV3Edge>(null, after)]);
        if (!_history.TryPush(delta))
        {
            _facade.Restore(before);
            return false;
        }

        return true;
    }

    public bool TryRemoveEdge(int edgeID, out RoadGraphV3ChangeSummary summary)
    {
        if (!_facade.Revision.Edges.TryGetValue(edgeID, out RoadGraphV3Edge? before))
        {
            summary = null!;
            return false;
        }

        RoadGraphV3Snapshot snapshotBefore = _facade.CaptureSnapshot();
        long beforeRevision = snapshotBefore.Token.DomainRevisionID;
        if (!_facade.TryRemoveEdge(edgeID, out summary))
            return false;

        var delta = new RoadGraphV3Delta(
            beforeRevision,
            _facade.CurrentToken.DomainRevisionID,
            [],
            [new RoadGraphV3EntityChange<RoadGraphV3Edge>(before, null)]);
        if (!_history.TryPush(delta))
        {
            _facade.Restore(snapshotBefore);
            return false;
        }

        return true;
    }

    public bool TryChangeRoadType(int edgeID, RoadType roadType, out RoadGraphV3ChangeSummary summary)
    {
        if (!_facade.Revision.Edges.TryGetValue(edgeID, out RoadGraphV3Edge? before))
        {
            summary = null!;
            return false;
        }

        RoadGraphV3Snapshot snapshotBefore = _facade.CaptureSnapshot();
        long beforeRevision = snapshotBefore.Token.DomainRevisionID;
        if (!_facade.TryChangeRoadType(edgeID, roadType, out summary))
            return false;

        RoadGraphV3Edge after = _facade.Revision.Edges[edgeID];
        var delta = new RoadGraphV3Delta(
            beforeRevision,
            _facade.CurrentToken.DomainRevisionID,
            [],
            [new RoadGraphV3EntityChange<RoadGraphV3Edge>(before, after)]);
        if (!_history.TryPush(delta))
        {
            _facade.Restore(snapshotBefore);
            return false;
        }

        return true;
    }

    public bool TryUndo(out RoadGraphV3ChangeSummary summary)
    {
        if (!_history.TryUndo(out RoadGraphV3Delta delta))
        {
            summary = null!;
            return false;
        }

        return _facade.TryApplyDelta(delta.Invert(), out summary);
    }

    public bool TryRedo(out RoadGraphV3ChangeSummary summary)
    {
        if (!_history.TryRedo(out RoadGraphV3Delta delta))
        {
            summary = null!;
            return false;
        }

        return _facade.TryApplyDelta(delta, out summary);
    }
}
