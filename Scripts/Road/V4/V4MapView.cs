using Godot;
using System;
using System.Collections.Generic;
using SimpleCities.RoadCore;

/// <summary>V4 隔离地图的米制网格显示及联合加载参与者。</summary>
public partial class V4MapView : Node2D, IScenePresentationLoadParticipant
{
    private RoadSnapshot? _presented;
    private long _sceneGeneration;
    private Admission? _admission;
    internal RoadSnapshot? Presented => _presented;

    internal void ShowNewMap(RoadSnapshot snapshot)
    {
        if (_admission is not null)
            throw new InvalidOperationException("Cannot replace the displayed map during load.");
        _presented = snapshot;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_presented is null)
            return;
        int cell = _presented.Map.CellSizeMetres;
        var bounds = new Rect2(-4000, -4000, 8000, 8000);
        DrawRect(bounds, new Color("172e36"));
        var grid = new Color(0.38f, 0.65f, 0.68f, 0.30f);
        var diagonal = new Color(0.38f, 0.65f, 0.68f, 0.12f);
        for (int i = -4000; i <= 4000; i += cell)
        {
            DrawLine(new Vector2(i, -4000), new Vector2(i, 4000), grid);
            DrawLine(new Vector2(-4000, i), new Vector2(4000, i), grid);
        }
        for (int i = -8000; i <= 8000; i += cell)
        {
            float x0 = Math.Max(-4000, -4000 - i);
            float x1 = Math.Min(4000, 4000 - i);
            DrawLine(new Vector2(x0, x0 + i), new Vector2(x1, x1 + i), diagonal);
            DrawLine(new Vector2(x0, -x0 - i), new Vector2(x1, -x1 - i), diagonal);
        }
        DrawRect(bounds, new Color("6ebcc3"), filled: false, width: 12);
        DrawLine(new Vector2(-4000, 0), new Vector2(4000, 0), new Color("75969d"), 6);
        DrawLine(new Vector2(0, -4000), new Vector2(0, 4000), new Color("75969d"), 6);
        DrawCircle(Vector2.Zero, 35, new Color("e9ca8c"));
    }

    void IScenePresentationLoadParticipant.ConfigureSceneGeneration(long generation) => _sceneGeneration = generation;
    IScenePresentationLoadAdmission IScenePresentationLoadParticipant.BeginSceneLoadAdmission()
    {
        if (_admission is not null)
            throw new InvalidOperationException("V4 presentation is already loading.");
        return _admission = new Admission(this, _sceneGeneration);
    }

    private sealed record Presentation(MapDefinition Map) : IPreparedScenePresentation;
    private sealed class Preparer : IScenePresentationPreparer
    {
        public IPreparedScenePresentation Prepare(IPreparedSaveState state) =>
            new Presentation(((RoadSaveParticipant.Prepared)state).Value.Map);
    }

    private sealed class Admission(V4MapView owner, long generation) : IScenePresentationLoadAdmission
    {
        internal bool IsCurrent => ReferenceEquals(owner._admission, this) && owner._sceneGeneration == generation;
        public IScenePresentationPreparer Preparer { get; } = new Preparer();
        public INonThrowingLoadCommitPlan PreflightPreparedLoad(IPreparedScenePresentation presentation, IPreparedSaveState targetState)
        {
            if (!IsCurrent || presentation is not Presentation prepared ||
                targetState is not RoadSaveParticipant.Target target || prepared.Map != target.Value.Map)
                throw new LoadPreflightInvalidException("V4 presentation does not match the target map.");
            return new CommitPlan(owner, this, target.Value);
        }

        public void Dispose()
        {
            if (ReferenceEquals(owner._admission, this))
                owner._admission = null;
        }
    }

    private sealed class CommitPlan(V4MapView owner, Admission admission, RoadSnapshot target) : INonThrowingLoadCommitPlan
    {
        public string ParticipantID => "v4-presentation";
        public bool IsGenerationCurrent => admission.IsCurrent;
        public void CommitReferences() => owner._presented = target;
        public IReadOnlyList<string> PublishNotifications()
        {
            owner.QueueRedraw();
            return Array.Empty<string>();
        }
        public void CompleteCommit() => admission.Dispose();
        public void Dispose() => admission.Dispose();
    }
}
