using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SimpleCities.RoadCore;
using CoreRoadLocation = SimpleCities.RoadCore.RoadLocation;

/// <summary>V4 隔离地图的米制网格显示及联合加载参与者。</summary>
public partial class V4MapView : Node2D, IScenePresentationLoadParticipant
{
    private V4RoadDisplay? _display;
    private sealed record PreviewSegment(Vector2 Start, Vector2 End, Color Color, bool Conflict);
    private PreviewSegment[] _preview = [];
    private readonly V4SelectionDisplay _selection = new();
    private long _sceneGeneration;
    private Admission? _admission;
    internal RoadSnapshot? Presented => _display?.Snapshot;
    internal int MeshSurfaceCount => _display?.Mesh?.GetSurfaceCount() ?? 0;
    internal string DrawSubmittedToken { get; private set; } = "";

    internal void ShowNewMap(RoadSnapshot snapshot)
    {
        if (_admission is not null)
            throw new InvalidOperationException("Cannot replace the displayed map during load.");
        V4RoadDisplay replacement = V4RoadDisplay.Prepare(snapshot, RoadPresentation.Prepare(snapshot.Nodes, snapshot.Edges));
        V4RoadDisplay? previous = _display;
        _display = replacement;
        _preview = [];
        _selection.Clear();
        previous?.Dispose();
        QueueRedraw();
    }

    internal V4RoadDisplay PrepareDisplay(RoadSnapshot snapshot, RoadSurfaceData? surface) => V4RoadDisplay.Prepare(snapshot, surface);
    internal V4RoadDisplay? CommitDisplay(V4RoadDisplay target)
    {
        V4RoadDisplay? previous = _display;
        _display = target;
        _selection.Clear();
        return previous;
    }

    internal void SetSelection(RoadGridSpan? hover, IReadOnlyList<RoadGridSpan> selected)
    {
        if (Presented is RoadSnapshot snapshot) _selection.Set(snapshot, hover, selected);
        else _selection.Clear();
        QueueRedraw();
    }

    internal void ClearSelection()
    {
        _selection.Clear();
        QueueRedraw();
    }

    internal Godot.Collections.Array<Godot.Collections.Dictionary> DescribeSelection() => _selection.Describe();

    internal void ShowPreview(RoadPoint? start, RoadPoint? end, SimpleCities.RoadCore.RoadBuildResult? result)
    {
        var segments = new List<PreviewSegment>();
        if (start is RoadPoint a && end is RoadPoint b)
        {
            var neutral = new Color("9aa5ad");
            if (result is null || result.Conflicts.Count == 0)
                Add(0, 1, result?.Status == RoadBuildStatus.Ready ? new Color("75dfcb") : neutral, false);
            else
            {
                double cursor = 0;
                foreach (RoadConflictSpan conflict in result.Conflicts)
                {
                    if (cursor < conflict.StartParameter) Add(cursor, conflict.StartParameter, neutral, false);
                    Add(conflict.StartParameter, conflict.EndParameter, new Color("ef6f76"), true);
                    cursor = conflict.EndParameter;
                }
                if (cursor < 1) Add(cursor, 1, neutral, false);
            }

            void Add(double from, double to, Color color, bool conflict)
            {
                Vector2 Point(double t) => new((float)(a.X + (b.X - a.X) * t), (float)(a.Y + (b.Y - a.Y) * t));
                segments.Add(new PreviewSegment(Point(from), Point(to), color, conflict));
            }
        }
        _preview = segments.ToArray();
        QueueRedraw();
    }

    internal Godot.Collections.Array<Godot.Collections.Dictionary> DescribePreview()
    {
        var segments = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (PreviewSegment segment in _preview)
            segments.Add(new() { ["start"] = segment.Start, ["end"] = segment.End, ["color"] = segment.Color, ["conflict"] = segment.Conflict });
        return segments;
    }

    internal Godot.Collections.Dictionary PickRoad(Vector2 world)
    {
        var query = QueryHit(world, null);
        if (query.Status != SpatialQueryStatus.Ready)
            return new() { ["queryStatus"] = query.Status.ToString(), ["reason"] = query.Reason };
        return query.Results.Count == 0 ? new() : DescribeHit(query.Results[0]);
    }

    private Godot.Collections.Dictionary DescribeHit(V4RoadDisplay.HitResult picked)
    {
        if (_display is null) return new();
        CoreRoadLocation hit = picked.Location;
        return new()
        {
            ["edgeId"] = hit.Edge.Value,
            ["junctionNodeId"] = picked.JunctionNode?.Value ?? 0,
            ["parameter"] = hit.Parameter,
            ["sourceToken"] = hit.Source.ToString(),
            ["profile"] = _display.Snapshot.FindEdge(hit.Edge)?.Profile.Value ?? "",
            ["surfaceCenter"] = _display.SurfaceCenter(hit),
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
            DrawMesh(mesh, null, modulate: Colors.White);
        DrawSubmittedToken = Presented.Token.ToString();
        _selection.Draw(this);
        foreach (PreviewSegment segment in _preview)
            DrawLine(segment.Start, segment.End, segment.Color, 10);
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
