using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SimpleCities.RoadCore;

/// <summary>按实际道路格段缓存显示坐标；选择改变时转换，绘制时复用。</summary>
internal sealed class V4SelectionDisplay
{
    private sealed record Stroke(RoadGridSpan Span, Vector2[] Points, Color Color, float Width, bool Selected);
    private Stroke[] _strokes = [];
    private RoadStateToken? _source;
    private RoadGridSpan? _hover;
    private RoadGridSpan[] _selected = [];

    internal void Set(RoadSnapshot snapshot, RoadGridSpan? hover, IReadOnlyList<RoadGridSpan> selected)
    {
        if (_source == snapshot.Token && SameOptional(_hover, hover) && _selected.Length == selected.Count &&
            _selected.Where((span, index) => !V4RoadDisplay.SameSpan(span, selected[index])).Any() == false)
            return;
        var strokes = new List<Stroke>();
        foreach (RoadGridSpan span in selected) Add(span, true);
        if (hover is not null && !selected.Any(span => V4RoadDisplay.SameSpan(span, hover))) Add(hover, false);
        _strokes = strokes.ToArray();
        _source = snapshot.Token;
        _hover = hover;
        _selected = selected.ToArray();

        void Add(RoadGridSpan span, bool isSelected)
        {
            if (span.Source != snapshot.Token) return;
            RoadEdge? edge = snapshot.Edges.FirstOrDefault(candidate => candidate.Id == span.Edge);
            if (edge is null) return;
            Vector2[] points = span.Points.Select(point => new Vector2((float)point.X, (float)point.Y)).ToArray();
            if (points.Length < 2 || points.Any(point => !point.IsFinite()))
                throw new InvalidOperationException("V4 selection display coordinates are invalid.");
            float width = (float)RoadProfiles.Get(edge.Profile).WidthMetres;
            strokes.Add(new Stroke(span, points,
                isSelected ? new Color(1f, 0.78f, 0.25f, 0.68f) : new Color(0.35f, 0.93f, 1f, 0.58f),
                width * (isSelected ? 0.8f : 0.45f), isSelected));
        }
    }

    private static bool SameOptional(RoadGridSpan? left, RoadGridSpan? right) =>
        left is null ? right is null : right is not null && V4RoadDisplay.SameSpan(left, right);

    internal void Clear()
    {
        _strokes = [];
        _source = null;
        _hover = null;
        _selected = [];
    }

    internal void Draw(Node2D canvas)
    {
        foreach (Stroke stroke in _strokes)
            canvas.DrawPolyline(stroke.Points, stroke.Color, stroke.Width, antialiased: true);
    }

    internal Godot.Collections.Array<Godot.Collections.Dictionary> Describe()
    {
        var strokes = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (Stroke stroke in _strokes)
        {
            var ranges = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (var range in stroke.Span.Ranges)
                ranges.Add(new() { ["startParameter"] = range.StartParameter, ["endParameter"] = range.EndParameter });
            strokes.Add(new()
            {
                ["sourceToken"] = stroke.Span.Source.ToString(),
                ["edgeId"] = stroke.Span.Edge.Value,
                ["ranges"] = ranges,
                ["points"] = stroke.Points,
                ["color"] = stroke.Color,
                ["width"] = stroke.Width,
                ["selected"] = stroke.Selected,
            });
        }
        return strokes;
    }
}
