using Godot;
using System;
using System.Diagnostics;

public partial class RoadFullResetPerformanceProbe : RefCounted
{
    public Godot.Collections.Dictionary CommitCurrentSnapshot(RoadSystem roadSystem)
    {
        ArgumentNullException.ThrowIfNull(roadSystem);
        RoadGraph graph = roadSystem.Graph;
        ArgumentNullException.ThrowIfNull(graph);
        if (graph.CaptureSnapshot() is not IPreparedSaveState preparedState)
        {
            throw new InvalidOperationException(
                "The current RoadGraph snapshot cannot be committed as prepared state.");
        }

        GraphStateToken before = graph.CurrentStateToken;
        long barrierStarted = Stopwatch.GetTimestamp();
        graph.CommitPreparedLoad(preparedState);
        TimeSpan barrierDuration = Stopwatch.GetElapsedTime(barrierStarted);
        GraphStateToken after = graph.CurrentStateToken;

        return new Godot.Collections.Dictionary
        {
            ["barrierMs"] = barrierDuration.TotalMilliseconds,
            ["graphFacadeID"] = graph.FacadeID,
            ["beforeLineageID"] = before.LineageID.Value,
            ["beforeDomainRevisionID"] = before.DomainRevisionID,
            ["beforeChangeSequence"] = before.ChangeSequence,
            ["afterLineageID"] = after.LineageID.Value,
            ["afterDomainRevisionID"] = after.DomainRevisionID,
            ["afterChangeSequence"] = after.ChangeSequence,
        };
    }
}
