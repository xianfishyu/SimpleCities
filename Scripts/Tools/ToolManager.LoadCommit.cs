using System;
using System.Collections.Generic;

public partial class ToolManager
{
    internal ToolLoadAdmission BeginLoadAdmission()
    {
        if (_loadAdmission is not null || _roadBuilder is null)
            throw new InvalidOperationException("ToolManager cannot admit a load in its current state.");

        _loadAdmissionGeneration = NextLoadGeneration(_loadAdmissionGeneration);
        RoadBuilder.RoadBuilderLoadAdmission builderAdmission =
            _roadBuilder.BeginFullResetAdmission();
        var admission = new ToolLoadAdmission(
            this,
            _loadAdmissionGeneration,
            _currentTool,
            _roadBuilder,
            builderAdmission);
        _loadAdmission = admission;
        return admission;
    }

    internal INonThrowingLoadCommitPlan PreflightFullReset(ToolLoadAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        if (!IsLoadAdmissionCurrent(admission))
            throw new LoadPreflightInvalidException("ToolManager load admission is stale.");
        RoadBuilder.RoadBuilderLoadCommitPlan builderPlan = _roadBuilder!.PreflightFullReset(
            admission.BuilderAdmission,
            keepRemoveHoverActive: admission.CurrentTool == ToolType.RoadRemove,
            keepUpgradeHoverActive: admission.CurrentTool == ToolType.RoadUpgrade);
        return new ToolLoadCommitPlan(this, admission, builderPlan);
    }

    private bool IsLoadAdmissionCurrent(ToolLoadAdmission admission) =>
        ReferenceEquals(_loadAdmission, admission) &&
        admission.Generation == _loadAdmissionGeneration &&
        ReferenceEquals(_roadBuilder, admission.Builder);

    private void AbandonLoadAdmission(ToolLoadAdmission admission)
    {
        if (ReferenceEquals(_loadAdmission, admission))
            _loadAdmission = null;
    }

    private static long NextLoadGeneration(long generation) =>
        generation == long.MaxValue ? 1 : generation + 1;

    internal sealed class ToolLoadAdmission : IDisposable
    {
        private ToolManager? _owner;

        internal ToolLoadAdmission(
            ToolManager owner,
            long generation,
            ToolType currentTool,
            RoadBuilder builder,
            RoadBuilder.RoadBuilderLoadAdmission builderAdmission)
        {
            _owner = owner;
            Generation = generation;
            CurrentTool = currentTool;
            Builder = builder;
            BuilderAdmission = builderAdmission;
        }

        internal long Generation { get; }
        internal ToolType CurrentTool { get; }
        internal RoadBuilder Builder { get; }
        internal RoadBuilder.RoadBuilderLoadAdmission BuilderAdmission { get; }

        public void Dispose()
        {
            ToolManager? owner = _owner;
            _owner = null;
            BuilderAdmission.Dispose();
            owner?.AbandonLoadAdmission(this);
        }
    }

    private sealed class ToolLoadCommitPlan : INonThrowingLoadCommitPlan
    {
        private readonly ToolManager _owner;
        private readonly ToolLoadAdmission _admission;
        private readonly RoadBuilder.RoadBuilderLoadCommitPlan _builderPlan;
        private bool _completed;

        internal ToolLoadCommitPlan(
            ToolManager owner,
            ToolLoadAdmission admission,
            RoadBuilder.RoadBuilderLoadCommitPlan builderPlan)
        {
            _owner = owner;
            _admission = admission;
            _builderPlan = builderPlan;
        }

        public string ParticipantID => "road-tools";
        public bool IsGenerationCurrent =>
            !_completed &&
            _owner.IsLoadAdmissionCurrent(_admission) &&
            _builderPlan.IsGenerationCurrent;

        public void CommitReferences() => _builderPlan.CommitReferences();
        public IReadOnlyList<string> PublishNotifications() => [];

        public void CompleteCommit()
        {
            _builderPlan.CompleteCommit();
            _owner.AbandonLoadAdmission(_admission);
            _completed = true;
        }

        public void Dispose()
        {
            if (_completed)
                return;
            _builderPlan.Dispose();
            _admission.Dispose();
        }
    }
}
