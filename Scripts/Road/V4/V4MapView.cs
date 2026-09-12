using Godot;
using System;
using System.Collections.Generic;
using SimpleCities.RoadCore;
using CoreRoadLocation = SimpleCities.RoadCore.RoadLocation;

/// <summary>V4 隔离地图的米制网格显示及联合加载参与者。</summary>
public partial class V4MapView : Node2D, IScenePresentationLoadParticipant
{
    private V4RoadDisplay? _display;
    private Vector2[]? _preview;
    private bool _previewValid;
    private bool _hovered;
    private long _sceneGeneration;
    private Admission? _admission;
    internal RoadSnapshot? Presented => _display?.Snapshot;
    internal int MeshSurfaceCount => _display?.Mesh?.GetSurfaceCount() ?? 0;

    internal void ShowNewMap(RoadSnapshot snapshot)
    {
        if (_admission is not null)
            throw new InvalidOperationException("Cannot replace the displayed map during load.");
        V4RoadDisplay replacement = V4RoadDisplay.Prepare(snapshot, RoadPresentation.Prepare(snapshot.Nodes, snapshot.Edges));
        V4RoadDisplay? previous = _display;
        _display = replacement;
        _preview = null;
        _hovered = false;
        previous?.Dispose();
        QueueRedraw();
    }

    internal V4RoadDisplay PrepareDisplay(RoadSnapshot snapshot, RoadSurfaceData? surface) => V4RoadDisplay.Prepare(snapshot, surface);
    internal V4RoadDisplay? CommitDisplay(V4RoadDisplay target)
    {
        V4RoadDisplay? previous = _display;
        _display = target;
        _hovered = false;
        return previous;
    }

    internal void SetHovered(bool hovered)
    {
        if (_hovered == hovered) return;
        _hovered = hovered;
        QueueRedraw();
    }

    internal void ShowPreview(RoadPoint? start, RoadPoint? end, bool valid)
    {
        _preview = start.HasValue && end.HasValue
            ? [new Vector2((float)start.Value.X, (float)start.Value.Y), new Vector2((float)end.Value.X, (float)end.Value.Y)] : null;
        _previewValid = valid;
        QueueRedraw();
    }

    internal Godot.Collections.Dictionary PickRoad(Vector2 world)
    {
        CoreRoadLocation? location = _display?.Hit(world);
        if (location is not CoreRoadLocation hit || _display is null) return new();
        return new()
        {
            ["edgeId"] = hit.Edge.Value,
            ["parameter"] = hit.Parameter,
            ["sourceToken"] = hit.Source.ToString(),
            ["profile"] = _display.Snapshot.Edges[0].Profile.Value,
            ["surfaceCenter"] = _display.SurfaceCenter(hit.Parameter),
        };
    }

    public override void _ExitTree()
    {
        _display?.Dispose();
        _display = null;
    }

    public override void _Draw()
    {
        if (Presented is null)
            return;
        var bounds = new Rect2(-4000, -4000, 8000, 8000);
        DrawRect(bounds, new Color(0.35f, 0.35f, 0.35f), filled: false, width: 3);
        if (_display?.Mesh is ArrayMesh mesh)
            DrawMesh(mesh, null, modulate: _hovered ? new Color(1.4f, 1.4f, 1.4f) : Colors.White);
        if (_preview is not null)
            DrawLine(_preview[0], _preview[1], _previewValid ? new Color("75dfcb") : new Color("ef6f76"), 10);
    }

    void IScenePresentationLoadParticipant.ConfigureSceneGeneration(long generation) => _sceneGeneration = generation;
    IScenePresentationLoadAdmission IScenePresentationLoadParticipant.BeginSceneLoadAdmission()
    {
        if (_admission is not null)
            throw new InvalidOperationException("V4 presentation is already loading.");
        return _admission = new Admission(this, _sceneGeneration);
    }

    private sealed record Presentation(MapDefinition Map, RoadSurfaceData? Surface) : IPreparedScenePresentation;
    private sealed class Preparer : IScenePresentationPreparer
    {
        public IPreparedScenePresentation Prepare(IPreparedSaveState state)
        {
            PreparedRoadState prepared = ((RoadSaveParticipant.Prepared)state).Value;
            return new Presentation(prepared.Map, RoadPresentation.Prepare(prepared.Nodes, prepared.Edges));
        }
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
            return new CommitPlan(owner, this, V4RoadDisplay.Prepare(target.Value, prepared.Surface));
        }

        public void Dispose()
        {
            if (ReferenceEquals(owner._admission, this))
                owner._admission = null;
        }
    }

    private sealed class CommitPlan(V4MapView owner, Admission admission, V4RoadDisplay target) : INonThrowingLoadCommitPlan
    {
        private V4RoadDisplay? _previous;
        private bool _committed;
        private bool _disposed;
        public string ParticipantID => "v4-presentation";
        public bool IsGenerationCurrent => admission.IsCurrent;
        public void CommitReferences()
        {
            _previous = owner.CommitDisplay(target);
            _committed = true;
        }
        public IReadOnlyList<string> PublishNotifications()
        {
            owner.QueueRedraw();
            return Array.Empty<string>();
        }
        public void CompleteCommit()
        {
            _previous?.Dispose();
            _previous = null;
            admission.Dispose();
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!_committed) target.Dispose();
            _previous?.Dispose();
            _previous = null;
            admission.Dispose();
        }
    }
}
