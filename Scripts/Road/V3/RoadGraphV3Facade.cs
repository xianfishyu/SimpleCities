using Godot;
using System;
using System.Collections.Generic;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 稳定 facade：内部持有不可变 root，普通 mutation 原子替换 root 并递增
/// DomainRevisionID / ChangeSequence；full reset 创建新 lineage。
/// </summary>
public sealed class RoadGraphV3Facade
{
    private RoadGraphV3Revision _revision;
    private long _domainRevisionID;
    private long _changeSequence;

    public long LineageID { get; private set; }
    public RoadGraphV3Revision Revision => _revision;
    public GraphStateToken CurrentToken => new(LineageID, _domainRevisionID, _changeSequence);

    public RoadGraphV3Facade(RoadGraphV3Revision initialRevision, long lineageID = 1)
    {
        ArgumentNullException.ThrowIfNull(initialRevision);
        _revision = initialRevision;
        LineageID = lineageID;
        _domainRevisionID = 0;
        _changeSequence = 0;
    }

    public bool TryAddNode(Vector2 position, out RoadGraphV3ChangeSummary summary, out int nodeID)
    {
        if (!_revision.TryAddNode(position, out RoadGraphV3Revision next, out nodeID))
        {
            summary = null!;
            return false;
        }

        Commit(next, out summary, createdNodeIDs: [nodeID]);
        return true;
    }

    public bool TryAddEdge(
        int nodeAID,
        int nodeBID,
        IReadOnlyList<RoadGeometrySegment> geometry,
        RoadType roadType,
        out RoadGraphV3ChangeSummary summary,
        out int edgeID)
    {
        if (!_revision.TryAddEdge(nodeAID, nodeBID, geometry, roadType, out RoadGraphV3Revision next, out edgeID))
        {
            summary = null!;
            return false;
        }

        Commit(next, out summary, createdEdgeIDs: [edgeID]);
        return true;
    }

    public bool TryRemoveEdge(int edgeID, out RoadGraphV3ChangeSummary summary)
    {
        if (!_revision.TryRemoveEdge(edgeID, out RoadGraphV3Revision next))
        {
            summary = null!;
            return false;
        }

        Commit(next, out summary, removedEdgeIDs: [edgeID]);
        return true;
    }

    public bool TryChangeRoadType(int edgeID, RoadType roadType, out RoadGraphV3ChangeSummary summary)
    {
        if (!_revision.TryChangeRoadType(edgeID, roadType, out RoadGraphV3Revision next))
        {
            summary = null!;
            return false;
        }

        Commit(next, out summary, updatedEdgeIDs: [edgeID]);
        return true;
    }

    public void ReplaceWithFullReset(RoadGraphV3Revision newRevision, long newLineageID)
    {
        ArgumentNullException.ThrowIfNull(newRevision);
        _revision = newRevision;
        LineageID = newLineageID;
        _domainRevisionID = 0;
        _changeSequence++;
    }

    private void Commit(
        RoadGraphV3Revision next,
        out RoadGraphV3ChangeSummary summary,
        IEnumerable<int>? createdNodeIDs = null,
        IEnumerable<int>? removedNodeIDs = null,
        IEnumerable<int>? updatedNodeIDs = null,
        IEnumerable<int>? createdEdgeIDs = null,
        IEnumerable<int>? removedEdgeIDs = null,
        IEnumerable<int>? updatedEdgeIDs = null)
    {
        _revision = next;
        _domainRevisionID++;
        _changeSequence++;
        summary = RoadGraphV3ChangeSummaryFactory.Incremental(
            createdNodeIDs ?? [],
            removedNodeIDs ?? [],
            updatedNodeIDs ?? [],
            createdEdgeIDs ?? [],
            removedEdgeIDs ?? [],
            updatedEdgeIDs ?? [],
            _changeSequence);
    }
}
