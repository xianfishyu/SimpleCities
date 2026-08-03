using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

/// <summary>输入策略生成的不可变预览与提交快照。</summary>
public sealed class RoadPathDraft
{
    private readonly ReadOnlyCollection<Vector2> _previewPoints;

    public IReadOnlyList<Vector2> PreviewPoints => _previewPoints;
    public RoadPath? Path { get; }
    public bool CanCommit => Path != null;
    public Vector2 PreviewFrom => _previewPoints[0];
    public Vector2 PreviewTo => _previewPoints[^1];

    public RoadPathDraft(IReadOnlyList<Vector2> previewPoints, RoadPath? path)
    {
        ArgumentNullException.ThrowIfNull(previewPoints);
        if (previewPoints.Count == 0)
            throw new ArgumentException("A road path draft needs at least one preview point.", nameof(previewPoints));
        if (previewPoints.Any(point => !point.IsFinite()))
            throw new ArgumentException("Road path draft preview points must be finite.", nameof(previewPoints));
        if (path != null && previewPoints.Count < 2)
            throw new ArgumentException("A committable road path draft needs at least two preview points.", nameof(previewPoints));

        _previewPoints = Array.AsReadOnly(previewPoints.ToArray());
        Path = path;
    }

    public static RoadPathDraft Empty(Vector2 startPosition) => new([startPosition], null);
}
