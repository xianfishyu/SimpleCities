using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCities.Road.V3;

/// <summary>
/// V3 稳定 facade：内部持有不可变 root，普通 mutation 原子替换 root 并递增
/// DomainRevisionID / ChangeSequence；full reset 创建新 lineage。
/// </summary>
public sealed class RoadGraphV3Facade
{
    private const float QueryBucketSize = 64f;

    private RoadGraphV3Revision _revision;
    private long _domainRevisionID;
    private long _changeSequence;

    public long LineageID { get; private set; }
    public RoadGraphV3Revision Revision => _revision;
    public GraphStateToken CurrentToken => new(LineageID, _domainRevisionID, _changeSequence);
    public RoadGraphV3Diagnostics Diagnostics { get; private set; } = null!;

    public RoadGraphV3Snapshot CaptureSnapshot() => new(_revision, CurrentToken);

    public void Restore(RoadGraphV3Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _revision = snapshot.Revision;
        LineageID = snapshot.Token.LineageID;
        _domainRevisionID = snapshot.Token.DomainRevisionID;
        _changeSequence = snapshot.Token.ChangeSequence;
        UpdateDiagnostics();
    }

    public RoadGraphV3Facade(RoadGraphV3Revision initialRevision, long lineageID = 1)
    {
        ArgumentNullException.ThrowIfNull(initialRevision);
        _revision = initialRevision;
        LineageID = lineageID;
        _domainRevisionID = 0;
        _changeSequence = 0;
        UpdateDiagnostics();
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

    public bool TryRemoveEdges(
        IReadOnlyList<int> edgeIDs,
        out RoadGraphV3ChangeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(edgeIDs);

        RoadGraphV3Revision current = _revision;
        foreach (int edgeID in edgeIDs.Distinct().Order())
        {
            if (!current.TryRemoveEdge(edgeID, out RoadGraphV3Revision next))
            {
                summary = null!;
                return false;
            }
            current = next;
        }

        RoadGraphV3Delta delta = RoadGraphV3DeltaBuilder.BuildDelta(
            _revision,
            current,
            _domainRevisionID,
            _domainRevisionID + 1);
        if (delta.IsEmpty)
        {
            summary = null!;
            return false;
        }

        _revision = current;
        _domainRevisionID++;
        _changeSequence++;
        UpdateDiagnostics();
        summary = RoadGraphV3ChangeSummaryFactory.FromDelta(delta, _changeSequence, false);
        return true;
    }

    public bool TryChangeRoadTypes(
        IReadOnlyList<int> edgeIDs,
        RoadType roadType,
        out RoadGraphV3ChangeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(edgeIDs);
        if (!RoadTypeChangeValidator.IsValidRoadType(roadType))
        {
            summary = null!;
            return false;
        }

        RoadGraphV3Revision current = _revision;
        foreach (int edgeID in edgeIDs.Distinct().Order())
        {
            if (!current.TryChangeRoadType(edgeID, roadType, out RoadGraphV3Revision next))
            {
                summary = null!;
                return false;
            }
            current = next;
        }

        RoadGraphV3Delta delta = RoadGraphV3DeltaBuilder.BuildDelta(
            _revision,
            current,
            _domainRevisionID,
            _domainRevisionID + 1);
        if (delta.IsEmpty)
        {
            summary = null!;
            return false;
        }

        _revision = current;
        _domainRevisionID++;
        _changeSequence++;
        UpdateDiagnostics();
        summary = RoadGraphV3ChangeSummaryFactory.FromDelta(delta, _changeSequence, false);
        return true;
    }

    public bool TryNormalize(out RoadGraphV3ChangeSummary summary)
    {
        RoadGraphV3Revision next = RoadGraphV3Canonicalizer.Canonicalize(_revision);
        RoadGraphV3Delta delta = RoadGraphV3DeltaBuilder.BuildDelta(
            _revision,
            next,
            _domainRevisionID,
            _domainRevisionID + 1);
        if (delta.IsEmpty)
        {
            summary = null!;
            return false;
        }

        _revision = next;
        _domainRevisionID++;
        _changeSequence++;
        UpdateDiagnostics();
        summary = RoadGraphV3ChangeSummaryFactory.FromDelta(delta, _changeSequence, false);
        return true;
    }

    public bool TryApplyDelta(RoadGraphV3Delta delta, out RoadGraphV3ChangeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(delta);
        if (!RoadGraphV3DeltaApplier.TryApply(_revision, delta, out RoadGraphV3Revision next))
        {
            summary = null!;
            return false;
        }

        _revision = next;
        _domainRevisionID = delta.AfterRevisionID;
        _changeSequence++;
        UpdateDiagnostics();
        summary = RoadGraphV3ChangeSummaryFactory.FromDelta(delta, _changeSequence, isFullReset: false);
        return true;
    }

    public void ReplaceWithFullReset(RoadGraphV3Revision newRevision, long newLineageID)
    {
        ArgumentNullException.ThrowIfNull(newRevision);
        _revision = newRevision;
        LineageID = newLineageID;
        _domainRevisionID = 0;
        _changeSequence++;
        UpdateDiagnostics();
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
        UpdateDiagnostics();
        summary = RoadGraphV3ChangeSummaryFactory.Incremental(
            createdNodeIDs ?? [],
            removedNodeIDs ?? [],
            updatedNodeIDs ?? [],
            createdEdgeIDs ?? [],
            removedEdgeIDs ?? [],
            updatedEdgeIDs ?? [],
            _changeSequence);
    }

    private void UpdateDiagnostics()
    {
        int parallelEdgeCount = _revision.Edges.Values
            .GroupBy(edge => edge.IsSelfLoop
                ? (int.MinValue, int.MinValue)
                : (Math.Min(edge.NodeAID, edge.NodeBID), Math.Max(edge.NodeAID, edge.NodeBID)))
            .Count(group => group.Count() > 1);

        Diagnostics = new RoadGraphV3Diagnostics(
            _revision.Nodes.Count,
            _revision.Edges.Count,
            _revision.Edges.Values.Sum(edge => edge.Geometry.Count),
            _revision.Edges.Values.Count(edge => edge.IsSelfLoop),
            parallelEdgeCount,
            _changeSequence,
            CountQueryFragments(_revision));
    }

    private static int CountQueryFragments(RoadGraphV3Revision revision)
    {
        int count = 0;
        foreach (RoadGraphV3Edge edge in revision.Edges.Values)
        {
            for (int index = 0; index < edge.Geometry.Count; index++)
                count += RoadQueryFragmentBuilder.BuildSegmentFragments(edge.ID, index, edge.Geometry[index], QueryBucketSize).Count;
        }

        return count;
    }
}
