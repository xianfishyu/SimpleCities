using System;
using System.Collections.Generic;
using System.IO;
using SimpleCities.RoadCore;

/// <summary>将 V4 核心内容接到通用槽位事务及联合加载；核心不引用应用契约。</summary>
internal sealed class RoadSaveParticipant(RoadNetwork network) : ISceneNetworkLoadParticipant
{
    internal static SceneStoragePolicy Storage { get; } = new("user://saves-v4", ["road_network_v4"]);
    private Admission? _admission;
    internal RoadNetwork Network { get; } = network;
    public string SaveFileName => "road_network_v4";
    public ISaveSnapshot CaptureSnapshot() => new Snapshot(Network.Snapshot);
    public void WriteSnapshot(Stream destination, ISaveSnapshot snapshot) =>
        RoadCodec.Write(destination, ((Snapshot)snapshot).Value);
    public IStreamingLoadReader CaptureLoadReader() => new Reader();

    public void CommitPreparedLoad(IPreparedSaveState preparedState)
    {
        using ISceneNetworkLoadAdmission admission = BeginSceneLoadAdmission();
        using var aggregate = new PreparedAggregateLoad([admission.PreflightPreparedLoad(preparedState, out _)]);
        aggregate.Commit(new UncoordinatedStorageOperationLease(SaveOperationKind.Load));
    }

    public ISceneNetworkLoadAdmission BeginSceneLoadAdmission()
    {
        if (_admission is not null)
            throw new InvalidOperationException("V4 network is already loading.");
        return _admission = new Admission(this);
    }

    internal sealed record Prepared(PreparedRoadState Value) : IPreparedSaveState;
    internal sealed record Target(RoadSnapshot Value) : IPreparedSaveState;
    private sealed record Snapshot(RoadSnapshot Value) : ISaveSnapshot;
    private sealed class Reader : IStreamingLoadReader
    {
        public IPreparedSaveState PrepareLoad(Stream source) => new Prepared(RoadCodec.Read(source));
    }

    private sealed class Admission(RoadSaveParticipant owner) : ISceneNetworkLoadAdmission
    {
        internal bool IsCurrent => ReferenceEquals(owner._admission, this);
        public INonThrowingLoadCommitPlan PreflightPreparedLoad(IPreparedSaveState state, out IPreparedSaveState targetState)
        {
            if (!IsCurrent || state is not Prepared prepared)
                throw new LoadPreflightInvalidException("Invalid V4 network load admission or payload.");
            RoadPlan plan = owner.Network.PlanLoad(prepared.Value);
            targetState = new Target(plan.Target);
            return new CommitPlan(owner, this, plan);
        }

        public void Dispose()
        {
            if (IsCurrent)
                owner._admission = null;
        }
    }

    private sealed class CommitPlan(RoadSaveParticipant owner, Admission admission, RoadPlan plan)
        : INonThrowingLoadCommitPlan
    {
        public string ParticipantID => "v4-network";
        public bool IsGenerationCurrent => admission.IsCurrent && owner.Network.CanCommit(plan);
        // Aggregate validates all plans on this same thread immediately before these reference swaps.
        public void CommitReferences() => owner.Network.TryCommit(plan);
        public IReadOnlyList<string> PublishNotifications() => Array.Empty<string>();
        public void CompleteCommit() => admission.Dispose();
        public void Dispose() => admission.Dispose();
    }
}
