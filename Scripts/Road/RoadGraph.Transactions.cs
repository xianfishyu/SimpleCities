using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;

public partial class RoadGraph
{
    private static long s_nextFacadeID;
    private static long s_nextLineageID;
    private readonly HashSet<int> _touchedNodeIDs = [];
    private readonly HashSet<int> _touchedEdgeIDs = [];
    private Func<RoadGraphDelta, bool>? _deltaAdmission;

    public RoadGraphRevision CaptureRevision() => _revision;

    public GraphStateToken CurrentStateToken => _revision.StateToken;

    private static long AllocateFacadeID() => AllocateProcessIdentity(
        ref s_nextFacadeID,
        "RoadGraph facade");

    private static GraphLineageID AllocateLineageID()
        => new(AllocateProcessIdentity(ref s_nextLineageID, "RoadGraph lineage"));

    private static long AllocateProcessIdentity(ref long allocator, string name)
    {
        while (true)
        {
            long current = Volatile.Read(ref allocator);
            if (current == long.MaxValue)
                throw new InvalidOperationException($"{name} space is exhausted.");
            long next = current + 1;
            if (Interlocked.CompareExchange(ref allocator, next, current) == current)
                return next;
        }
    }

    private bool TryBeginMutation(out RoadGraphRevision before)
    {
        before = _revision;
        if (_mutationInProgress || _publishingChanges || _loadAdmission is not null)
            return false;

        _mutationInProgress = true;
        _touchedNodeIDs.Clear();
        _touchedEdgeIDs.Clear();
        return true;
    }

    private RoadPathSubmissionResult ExecuteSubmission(
        Func<RoadPathSubmissionResult> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (!TryBeginMutation(out RoadGraphRevision before))
            return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.MutationReentrant);

        try
        {
            RoadPathSubmissionResult result = mutation();
            if (!result.Success)
            {
                CancelMutation(before);
                return result;
            }

            MutationCommit? commit = CommitMutation(before);
            if (commit is null)
                return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.CapacityExceeded);
            return RoadPathSubmissionResult.Succeeded(commit.Summary);
        }
        catch
        {
            CancelMutation(before);
            throw;
        }
    }

    private bool ExecuteBooleanMutation(Func<bool> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (!TryBeginMutation(out RoadGraphRevision before))
            return false;

        try
        {
            if (!mutation())
            {
                CancelMutation(before);
                return false;
            }

            return CommitMutation(before) is not null;
        }
        catch
        {
            CancelMutation(before);
            throw;
        }
    }

    public RoadTypeChangeResult ChangeRoadType(
        IEnumerable<int>? edgeIDs,
        RoadType targetType)
    {
        BeginMeasuredOperation();
        if (!RoadTypeContract.IsDefined(targetType))
            return RoadTypeChangeResult.Rejected(RoadTypeChangeError.InvalidRoadType);
        if (edgeIDs is null)
            return RoadTypeChangeResult.Rejected(RoadTypeChangeError.EmptySelection);

        int[] targets = edgeIDs.Distinct().Order().ToArray();
        if (targets.Length == 0)
            return RoadTypeChangeResult.Rejected(RoadTypeChangeError.EmptySelection);
        if (targets.Any(edgeID => !_edges.ContainsKey(edgeID)))
            return RoadTypeChangeResult.Rejected(RoadTypeChangeError.MissingEdge);
        if (targets.All(edgeID => _edges[edgeID].RoadType == targetType))
            return RoadTypeChangeResult.Rejected(RoadTypeChangeError.NoChanges);
        if (!TryBeginMutation(out RoadGraphRevision before))
            return RoadTypeChangeResult.Rejected(RoadTypeChangeError.MutationReentrant);

        try
        {
            var affectedNodeIDs = new HashSet<int>();
            foreach (int edgeID in targets)
            {
                GraphEdge edge = _edges[edgeID];
                if (edge.RoadType == targetType)
                    continue;

                affectedNodeIDs.Add(edge.NodeA);
                affectedNodeIDs.Add(edge.NodeB);
                ReplaceEdgePreservingIncidences(edge, new GraphEdge(
                    targetType,
                    edge.ID,
                    edge.NodeA,
                    edge.NodeB,
                    edge.GeometrySegments));
            }

            FinalizeMutation(affectedNodeIDs);
            MutationCommit? commit = CommitMutation(before);
            if (commit is null)
                return RoadTypeChangeResult.Rejected(RoadTypeChangeError.CapacityExceeded);
            return RoadTypeChangeResult.Succeeded(
                commit.Summary,
                commit.Delta);
        }
        catch
        {
            CancelMutation(before);
            throw;
        }
    }

    private void CancelMutation(RoadGraphRevision before)
    {
        RestoreWorkingState(before);
        _mutationInProgress = false;
        _touchedNodeIDs.Clear();
        _touchedEdgeIDs.Clear();
    }

    internal bool ExecuteWithDeltaAdmission(
        Func<bool> edit,
        Func<RoadGraphDelta, bool> admission)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(admission);
        if (_deltaAdmission is not null)
            throw new InvalidOperationException("A RoadGraph delta-admission scope is already active.");

        _deltaAdmission = admission;
        try
        {
            return edit();
        }
        finally
        {
            _deltaAdmission = null;
        }
    }

    private MutationCommit? CommitMutation(
        RoadGraphRevision before,
        bool isFullReset = false)
    {
        AssertCommittedInvariants();

        GraphLineageID lineageID = isFullReset
            ? AllocateLineageID()
            : before.LineageID;
        long domainRevisionID = isFullReset
            ? 0
            : _nextDomainRevisionID;
        long nextSequence = checked(before.ChangeSequence + 1);
        long nextDomainRevisionID = isFullReset
            ? 1
            : checked(_nextDomainRevisionID + 1);
        RoadGraphRevision after = CaptureWorkingRevision(
            lineageID,
            domainRevisionID,
            nextSequence);
        if (!isFullReset && !HasRootChanges(before, after))
        {
            CancelMutation(before);
            throw new InvalidOperationException("A successful graph mutation produced no root changes.");
        }

        RoadGraphDelta delta = CreateDelta(before, after, isFullReset);
        if (_deltaAdmission is not null && !_deltaAdmission(delta))
        {
            CancelMutation(before);
            return null;
        }
        RoadGraphChangeSummary summary = CreateChangeSummary(
            delta,
            isFullReset,
            nextSequence);

        _revision = after;
        _nextDomainRevisionID = nextDomainRevisionID;
        PublishDiagnosticsSnapshot(after);

        var committed = new MutationCommit(delta, summary, CurrentStateToken);
        try
        {
            PublishGraphChanged(committed);
        }
        finally
        {
            EndMutation();
        }
        return committed;
    }

    public RoadGraphDeltaApplyResult ApplyDelta(
        RoadGraphDelta delta,
        RoadGraphDeltaDirection direction,
        GraphStateToken expectedToken)
    {
        ArgumentNullException.ThrowIfNull(delta);
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));

        GraphStateToken current = CurrentStateToken;
        long requiredRevisionID = direction == RoadGraphDeltaDirection.Reverse
            ? delta.AfterRevisionID
            : delta.BeforeRevisionID;
        if (delta.IsFullReset ||
            expectedToken != current ||
            delta.LineageID != current.LineageID ||
            requiredRevisionID != current.DomainRevisionID ||
            !DeltaMatchesCurrentRoot(delta, direction))
        {
            return StaleDeltaResult(current);
        }
        if (!TryBeginMutation(out RoadGraphRevision before))
            return StaleDeltaResult(current);

        try
        {
            long nextSequence = checked(before.ChangeSequence + 1);
            ApplyDeltaEntities(delta, direction);
            AssertCommittedInvariants();

            long targetRevisionID = direction == RoadGraphDeltaDirection.Reverse
                ? delta.BeforeRevisionID
                : delta.AfterRevisionID;
            RoadGraphRevision after = CaptureWorkingRevision(
                before.LineageID,
                targetRevisionID,
                nextSequence);
            RoadGraphDelta appliedDelta = CreateDelta(before, after);
            RoadGraphChangeSummary summary = CreateChangeSummary(
                appliedDelta,
                isFullReset: false,
                nextSequence);

            _revision = after;
            PublishDiagnosticsSnapshot(after);
            var committed = new MutationCommit(appliedDelta, summary, CurrentStateToken);
            try
            {
                PublishGraphChanged(committed);
            }
            finally
            {
                EndMutation();
            }
            return new RoadGraphDeltaApplyResult(
                RoadGraphDeltaApplyError.None,
                committed.StateToken,
                summary);
        }
        catch
        {
            CancelMutation(before);
            throw;
        }
    }

    private static RoadGraphDeltaApplyResult StaleDeltaResult(GraphStateToken current) =>
        new(
            RoadGraphDeltaApplyError.StaleGraphState,
            current,
            RoadGraphChangeSummary.Empty);

    private bool DeltaMatchesCurrentRoot(
        RoadGraphDelta delta,
        RoadGraphDeltaDirection direction)
    {
        foreach (RoadGraphEntityDelta<GraphNode> change in delta.Nodes)
        {
            GraphNode? expected = direction == RoadGraphDeltaDirection.Reverse
                ? change.After
                : change.Before;
            _revision.NodeMap.TryGetValue(change.ID, out GraphNode? current);
            if (!ReferenceEquals(expected, current))
                return false;
        }
        foreach (RoadGraphEntityDelta<GraphEdge> change in delta.Edges)
        {
            GraphEdge? expected = direction == RoadGraphDeltaDirection.Reverse
                ? change.After
                : change.Before;
            _revision.EdgeMap.TryGetValue(change.ID, out GraphEdge? current);
            if (!ReferenceEquals(expected, current))
                return false;
        }
        return true;
    }

    private void ApplyDeltaEntities(
        RoadGraphDelta delta,
        RoadGraphDeltaDirection direction)
    {
        foreach (RoadGraphEntityDelta<GraphEdge> change in delta.Edges)
        {
            if (_edges.TryGetValue(change.ID, out GraphEdge? current))
            {
                RemoveEdgeSpatialRefs(change.ID);
                if (current.NodeA == current.NodeB)
                    _selfLoopCount--;
                _geometrySegmentCount -= current.GeometrySegments.Count;
                AdjustTotalGeometryLength(-SumGeometryLength(current));
            }
            _edges.Remove(change.ID);
            TrackEdgeChange(change.ID);
        }
        foreach (RoadGraphEntityDelta<GraphNode> change in delta.Nodes)
        {
            RemoveNodeSpatialRef(change.ID);
            _nodes.Remove(change.ID);
            TrackNodeChange(change.ID);
        }

        foreach (RoadGraphEntityDelta<GraphNode> change in delta.Nodes)
        {
            GraphNode? target = direction == RoadGraphDeltaDirection.Reverse
                ? change.Before
                : change.After;
            if (target is null)
                continue;
            _nodes.Add(target.ID, target);
            InsertNodeSpatialRef(target);
        }
        foreach (RoadGraphEntityDelta<GraphEdge> change in delta.Edges)
        {
            GraphEdge? target = direction == RoadGraphDeltaDirection.Reverse
                ? change.Before
                : change.After;
            if (target is null)
                continue;
            _edges.Add(target.ID, target);
            if (target.NodeA == target.NodeB)
                _selfLoopCount++;
            _geometrySegmentCount += target.GeometrySegments.Count;
            AdjustTotalGeometryLength(SumGeometryLength(target));
            InsertEdgeSpatialRefs(target);
        }
    }

    private void ReplaceEdgePreservingIncidences(
        GraphEdge existing,
        GraphEdge replacement)
    {
        if (existing.ID != replacement.ID ||
            existing.NodeA != replacement.NodeA ||
            existing.NodeB != replacement.NodeB)
        {
            throw new ArgumentException(
                "An incidence-preserving replacement must retain its ID and endpoints.",
                nameof(replacement));
        }

        RemoveEdgeSpatialRefs(existing.ID);
        _geometrySegmentCount -= existing.GeometrySegments.Count;
        double lengthDelta = SumGeometryLength(replacement) - SumGeometryLength(existing);
        AdjustTotalGeometryLength(lengthDelta);
        _edges[existing.ID] = replacement;
        TrackEdgeChange(existing.ID);
        _geometrySegmentCount += replacement.GeometrySegments.Count;
        InsertEdgeSpatialRefs(replacement);
    }

    private RoadGraphRevision CaptureWorkingRevision(
        GraphLineageID lineageID,
        long domainRevisionID,
        long changeSequence)
    {
        ImmutableDictionary<int, GraphNode> nodes = _nodes.ToImmutable();
        ImmutableDictionary<int, GraphEdge> edges = _edges.ToImmutable();
        ImmutableDictionary<int, NodeSpatialRef> nodeReferences = _nodeRefs.ToImmutable();
        ImmutableDictionary<int, ImmutableArray<ISpatialRef>> edgeReferences =
            _edgeRefs.ToImmutable();
        return new RoadGraphRevision(
            lineageID,
            domainRevisionID,
            changeSequence,
            _nextID,
            _totalGeometryLength,
            nodes,
            edges,
            nodeReferences,
            edgeReferences,
            _spatialIndex.CaptureSnapshot(),
            CaptureResourceCounts());
    }

    private void RestoreWorkingState(RoadGraphRevision revision)
    {
        _nodes = revision.NodeMap.ToBuilder();
        _edges = revision.EdgeMap.ToBuilder();
        _nodeRefs = revision.NodeReferenceMap.ToBuilder();
        _edgeRefs = revision.EdgeReferenceMap.ToBuilder();
        _spatialIndex = new UniformGrid(revision.SpatialIndex);
        _nextID = revision.NextIDWatermark;
        _geometrySegmentCount = revision.ResourceCounts.GeometrySegments;
        _queryFragmentCount = revision.ResourceCounts.QueryFragments;
        _selfLoopCount = revision.Edges.Values.Count(edge => edge.NodeA == edge.NodeB);
        _totalGeometryLength = revision.TotalGeometryLength;
    }

    private static bool HasRootChanges(
        RoadGraphRevision before,
        RoadGraphRevision after) =>
        !ReferenceEquals(before.NodeMap, after.NodeMap) ||
        !ReferenceEquals(before.EdgeMap, after.EdgeMap) ||
        before.NextIDWatermark != after.NextIDWatermark;

    private RoadGraphDelta CreateDelta(
        RoadGraphRevision before,
        RoadGraphRevision after,
        bool isFullReset = false)
    {
        int[] nodeIDs = isFullReset
            ? before.NodeMap.Keys.Concat(after.NodeMap.Keys).Distinct().Order().ToArray()
            : _touchedNodeIDs.Order().ToArray();
        var nodes = new List<RoadGraphEntityDelta<GraphNode>>();
        foreach (int nodeID in nodeIDs)
        {
            before.NodeMap.TryGetValue(nodeID, out GraphNode? previous);
            after.NodeMap.TryGetValue(nodeID, out GraphNode? current);
            if (!ReferenceEquals(previous, current))
                nodes.Add(new RoadGraphEntityDelta<GraphNode>(nodeID, previous, current));
        }

        int[] edgeIDs = isFullReset
            ? before.EdgeMap.Keys.Concat(after.EdgeMap.Keys).Distinct().Order().ToArray()
            : _touchedEdgeIDs.Order().ToArray();
        var edges = new List<RoadGraphEntityDelta<GraphEdge>>();
        foreach (int edgeID in edgeIDs)
        {
            before.EdgeMap.TryGetValue(edgeID, out GraphEdge? previous);
            after.EdgeMap.TryGetValue(edgeID, out GraphEdge? current);
            if (!ReferenceEquals(previous, current))
                edges.Add(new RoadGraphEntityDelta<GraphEdge>(edgeID, previous, current));
        }

        return new RoadGraphDelta(before, after, nodes, edges, isFullReset);
    }

    private void TrackNodeChange(int nodeID)
    {
        if (_mutationInProgress)
            _touchedNodeIDs.Add(nodeID);
    }

    private void TrackEdgeChange(int edgeID)
    {
        if (_mutationInProgress)
            _touchedEdgeIDs.Add(edgeID);
    }

    private void SealPreparedInitialRevision()
    {
        AssertCommittedInvariants();
        _revision = CaptureWorkingRevision(_revision.LineageID, 0, 0);
        _nextDomainRevisionID = 1;
        PublishDiagnosticsSnapshot(_revision);
    }

    private void EndMutation()
    {
        _mutationInProgress = false;
        _touchedNodeIDs.Clear();
        _touchedEdgeIDs.Clear();
    }

    private static RoadGraphChangeSummary CreateChangeSummary(
        RoadGraphDelta delta,
        bool isFullReset,
        long changeSequence) => new(
        delta.Nodes.Where(change => change.Before is null).Select(change => change.ID),
        delta.Edges.Where(change => change.Before is null).Select(change => change.ID),
        delta.Nodes.Where(change => change.After is null).Select(change => change.ID),
        delta.Edges.Where(change => change.After is null).Select(change => change.ID),
        delta.Nodes.Where(change => change.Before is not null && change.After is not null)
            .Select(change => change.ID),
        delta.Edges.Where(change => change.Before is not null && change.After is not null)
            .Select(change => change.ID),
        isFullReset,
        changeSequence);

    private void PublishGraphChanged(MutationCommit commit)
    {
        _ = PublishGraphChangedWithWarnings(commit);
    }

    private IReadOnlyList<string> PublishGraphChangedWithWarnings(MutationCommit commit)
    {
        var warnings = new List<string>();
        try
        {
            Action<RoadGraphChangedEvent>? handlers = GraphChanged;
            if (handlers is null)
                return warnings;

            _publishingChanges = true;
            var notification = new RoadGraphChangedEvent(
                commit.Delta,
                commit.Summary,
                commit.StateToken);
            foreach (Action<RoadGraphChangedEvent> handler in handlers
                         .GetInvocationList()
                         .Cast<Action<RoadGraphChangedEvent>>())
            {
                try
                {
                    handler(notification);
                }
                catch (Exception exception)
                {
                    warnings.Add($"RoadGraph observer failed: {exception.Message}");
                    SafeTraceObserverFailure(commit.StateToken.ChangeSequence, exception);
                }
            }
        }
        catch (Exception exception)
        {
            warnings.Add($"RoadGraph notification failed: {exception.Message}");
            SafeTraceObserverFailure(commit.StateToken.ChangeSequence, exception);
        }
        finally
        {
            _publishingChanges = false;
        }
        return warnings;
    }

    private static void SafeTraceObserverFailure(long sequence, Exception exception)
    {
        try
        {
            Trace.TraceWarning(
                "RoadGraph GraphChanged observer failed for sequence {0}: {1}",
                sequence,
                exception);
        }
        catch
        {
            // Diagnostics cannot be allowed to escape an already committed root.
        }
    }

    private sealed record MutationCommit(
        RoadGraphDelta Delta,
        RoadGraphChangeSummary Summary,
        GraphStateToken StateToken);
}
