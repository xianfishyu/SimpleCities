using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

public partial class RoadGraph : ISceneNetworkLoadParticipant
{
    partial void ProbeLoadCompleteCommitFailure();

    ISceneNetworkLoadAdmission ISceneNetworkLoadParticipant.BeginSceneLoadAdmission() =>
        BeginLoadAdmission();

    internal RoadGraphLoadAdmission BeginLoadAdmission()
    {
        if (_mutationInProgress || _publishingChanges || _loadAdmission is not null)
            throw new InvalidOperationException("RoadGraph cannot admit a load during another graph operation.");

        _loadAdmissionGeneration = NextLoadAdmissionGeneration(_loadAdmissionGeneration);
        var admission = new RoadGraphLoadAdmission(
            this,
            _loadAdmissionGeneration,
            _revision);
        _loadAdmission = admission;
        return admission;
    }

    internal INonThrowingLoadCommitPlan PreflightPreparedLoad(
        RoadGraphLoadAdmission admission,
        IPreparedSaveState preparedState,
        out RoadGraphRevision targetRevision)
    {
        ArgumentNullException.ThrowIfNull(admission);
        if (preparedState is not RoadGraphRevision preparedRevision)
            throw new ArgumentException("Prepared state is not a RoadGraph revision.", nameof(preparedState));
        if (!IsLoadAdmissionCurrent(admission))
            throw new LoadPreflightInvalidException("RoadGraph load admission is stale.");

        RoadGraphRevision before = admission.BeforeRevision;
        long nextSequence = checked(before.ChangeSequence + 1);
        RoadGraphRevision after = preparedRevision.WithRuntimeMetadata(
            AllocateLineageID(),
            domainRevisionID: 0,
            changeSequence: nextSequence,
            preparedRevision.NextIDWatermark);
        targetRevision = after;

        ImmutableDictionary<int, GraphNode>.Builder nodes = after.NodeMap.ToBuilder();
        ImmutableDictionary<int, GraphEdge>.Builder edges = after.EdgeMap.ToBuilder();
        ImmutableDictionary<int, NodeSpatialRef>.Builder nodeReferences =
            after.NodeReferenceMap.ToBuilder();
        ImmutableDictionary<int, ImmutableArray<ISpatialRef>>.Builder edgeReferences =
            after.EdgeReferenceMap.ToBuilder();
        var spatialIndex = new UniformGrid(after.SpatialIndex);
        RoadGraphDelta delta = CreateDelta(before, after, isFullReset: true);
        RoadGraphChangeSummary summary = CreateChangeSummary(
            delta,
            isFullReset: true,
            nextSequence);
        var commit = new MutationCommit(delta, summary, after.StateToken);

        return new RoadGraphLoadCommitPlan(
            this,
            admission,
            nodes,
            edges,
            nodeReferences,
            edgeReferences,
            spatialIndex,
            after,
            commit);
    }

    private bool IsLoadAdmissionCurrent(RoadGraphLoadAdmission admission) =>
        ReferenceEquals(_loadAdmission, admission) &&
        admission.Generation == _loadAdmissionGeneration &&
        ReferenceEquals(_revision, admission.BeforeRevision);

    private void AbandonLoadAdmission(RoadGraphLoadAdmission admission)
    {
        if (ReferenceEquals(_loadAdmission, admission))
            _loadAdmission = null;
    }

    private static long NextLoadAdmissionGeneration(long generation) =>
        generation == long.MaxValue ? 1 : generation + 1;

    internal sealed class RoadGraphLoadAdmission : ISceneNetworkLoadAdmission
    {
        private RoadGraph? _owner;

        internal RoadGraphLoadAdmission(
            RoadGraph owner,
            long generation,
            RoadGraphRevision beforeRevision)
        {
            _owner = owner;
            Generation = generation;
            BeforeRevision = beforeRevision;
        }

        internal long Generation { get; }
        internal RoadGraphRevision BeforeRevision { get; }

        public INonThrowingLoadCommitPlan PreflightPreparedLoad(
            IPreparedSaveState preparedState,
            out IPreparedSaveState targetState)
        {
            RoadGraph owner = _owner ?? throw new ObjectDisposedException(nameof(RoadGraphLoadAdmission));
            INonThrowingLoadCommitPlan plan = owner.PreflightPreparedLoad(
                this, preparedState, out RoadGraphRevision targetRevision);
            targetState = targetRevision;
            return plan;
        }

        public void Dispose()
        {
            RoadGraph? owner = _owner;
            _owner = null;
            owner?.AbandonLoadAdmission(this);
        }
    }

    private sealed class RoadGraphLoadCommitPlan : INonThrowingLoadCommitPlan
    {
        private readonly RoadGraph _owner;
        private readonly RoadGraphLoadAdmission _admission;
        private readonly ImmutableDictionary<int, GraphNode>.Builder _nodes;
        private readonly ImmutableDictionary<int, GraphEdge>.Builder _edges;
        private readonly ImmutableDictionary<int, NodeSpatialRef>.Builder _nodeReferences;
        private readonly ImmutableDictionary<int, ImmutableArray<ISpatialRef>>.Builder _edgeReferences;
        private readonly UniformGrid _spatialIndex;
        private readonly RoadGraphRevision _after;
        private readonly MutationCommit _commit;
        private bool _referencesCommitted;
        private bool _completed;

        internal RoadGraphLoadCommitPlan(
            RoadGraph owner,
            RoadGraphLoadAdmission admission,
            ImmutableDictionary<int, GraphNode>.Builder nodes,
            ImmutableDictionary<int, GraphEdge>.Builder edges,
            ImmutableDictionary<int, NodeSpatialRef>.Builder nodeReferences,
            ImmutableDictionary<int, ImmutableArray<ISpatialRef>>.Builder edgeReferences,
            UniformGrid spatialIndex,
            RoadGraphRevision after,
            MutationCommit commit)
        {
            _owner = owner;
            _admission = admission;
            _nodes = nodes;
            _edges = edges;
            _nodeReferences = nodeReferences;
            _edgeReferences = edgeReferences;
            _spatialIndex = spatialIndex;
            _after = after;
            _commit = commit;
        }

        public string ParticipantID => "road-graph";
        public bool IsGenerationCurrent =>
            !_referencesCommitted && _owner.IsLoadAdmissionCurrent(_admission);

        public void CommitReferences()
        {
            _owner._nodes = _nodes;
            _owner._edges = _edges;
            _owner._nodeRefs = _nodeReferences;
            _owner._edgeRefs = _edgeReferences;
            _owner._spatialIndex = _spatialIndex;
            _owner._nextID = _after.NextIDWatermark;
            _owner._geometrySegmentCount = _after.ResourceCounts.GeometrySegments;
            _owner._queryFragmentCount = _after.ResourceCounts.QueryFragments;
            _owner._selfLoopCount = _after.Edges.Values.Count(edge => edge.NodeA == edge.NodeB);
            _owner._totalGeometryLength = _after.TotalGeometryLength;
            _owner._revision = _after;
            _owner._nextDomainRevisionID = 1;
            _owner.PublishDiagnosticsSnapshot(_after);
            _referencesCommitted = true;
        }

        public IReadOnlyList<string> PublishNotifications()
        {
            if (!_referencesCommitted)
                throw new InvalidOperationException("RoadGraph references were not committed.");
            return _owner.PublishGraphChangedWithWarnings(_commit);
        }

        public void CompleteCommit()
        {
            if (!_referencesCommitted)
                throw new InvalidOperationException("RoadGraph references were not committed.");
            _owner.AbandonLoadAdmission(_admission);
            _completed = true;
            _owner.ProbeLoadCompleteCommitFailure();
        }

        public void Dispose()
        {
            if (!_completed && !_referencesCommitted)
                _admission.Dispose();
        }
    }
}
