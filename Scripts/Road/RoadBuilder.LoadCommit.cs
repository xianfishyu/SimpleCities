using System;

public partial class RoadBuilder
{
    internal RoadBuilderLoadAdmission BeginFullResetAdmission()
    {
        if (_loadAdmission is not null || _graph is null)
            throw new InvalidOperationException("RoadBuilder cannot admit a full reset in its current state.");

        _loadAdmissionGeneration = NextLoadGeneration(_loadAdmissionGeneration);
        var admission = new RoadBuilderLoadAdmission(
            this,
            _loadAdmissionGeneration,
            _graph);
        _loadAdmission = admission;
        return admission;
    }

    internal RoadBuilderLoadCommitPlan PreflightFullReset(
        RoadBuilderLoadAdmission admission,
        bool keepRemoveHoverActive)
    {
        ArgumentNullException.ThrowIfNull(admission);
        if (!IsLoadAdmissionCurrent(admission))
            throw new LoadPreflightInvalidException("RoadBuilder load admission is stale.");
        var replacementHistory = new RoadEditHistory(admission.Graph);
        return new RoadBuilderLoadCommitPlan(
            this,
            admission,
            replacementHistory,
            keepRemoveHoverActive,
            _editHistory);
    }

    private bool IsLoadAdmissionCurrent(RoadBuilderLoadAdmission admission) =>
        ReferenceEquals(_loadAdmission, admission) &&
        admission.Generation == _loadAdmissionGeneration &&
        ReferenceEquals(_graph, admission.Graph);

    private void AbandonLoadAdmission(RoadBuilderLoadAdmission admission)
    {
        if (ReferenceEquals(_loadAdmission, admission))
            _loadAdmission = null;
    }

    private static long NextLoadGeneration(long generation) =>
        generation == long.MaxValue ? 1 : generation + 1;

    internal sealed class RoadBuilderLoadAdmission : IDisposable
    {
        private RoadBuilder? _owner;

        internal RoadBuilderLoadAdmission(RoadBuilder owner, long generation, RoadGraph graph)
        {
            _owner = owner;
            Generation = generation;
            Graph = graph;
        }

        internal long Generation { get; }
        internal RoadGraph Graph { get; }

        public void Dispose()
        {
            RoadBuilder? owner = _owner;
            _owner = null;
            owner?.AbandonLoadAdmission(this);
        }
    }

    internal sealed class RoadBuilderLoadCommitPlan : IDisposable
    {
        private readonly RoadBuilder _owner;
        private readonly RoadBuilderLoadAdmission _admission;
        private readonly RoadEditHistory _replacementHistory;
        private readonly RoadEditHistory? _oldHistory;
        private readonly bool _keepRemoveHoverActive;
        private bool _committed;
        private bool _completed;

        internal RoadBuilderLoadCommitPlan(
            RoadBuilder owner,
            RoadBuilderLoadAdmission admission,
            RoadEditHistory replacementHistory,
            bool keepRemoveHoverActive,
            RoadEditHistory? oldHistory)
        {
            _owner = owner;
            _admission = admission;
            _replacementHistory = replacementHistory;
            _keepRemoveHoverActive = keepRemoveHoverActive;
            _oldHistory = oldHistory;
        }

        internal bool IsGenerationCurrent =>
            !_committed && _owner.IsLoadAdmissionCurrent(_admission);

        internal void CommitReferences()
        {
            _oldHistory?.Dispose();
            _owner._placementSession = null;
            _owner._removalSession = null;
            _owner._editHistory = _replacementHistory;
            _owner._leftPressStartedSession = false;
            _owner._ignoreNextLeftRelease = false;
            _owner._isRemoveHoverActive = _keepRemoveHoverActive;
            _owner._lastHoveredEdgeID = -1;
            _committed = true;
        }

        internal void CompleteCommit()
        {
            _owner.AbandonLoadAdmission(_admission);
            _completed = true;
        }

        public void Dispose()
        {
            if (_completed)
                return;
            if (!_committed)
            {
                _replacementHistory.Dispose();
                _admission.Dispose();
            }
        }
    }
}
