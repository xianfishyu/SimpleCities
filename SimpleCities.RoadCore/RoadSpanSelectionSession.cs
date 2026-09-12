namespace SimpleCities.RoadCore;

/// <summary>只读选择手势：预选独立于已选，抬起保留区间，版本变化使旧会话失效。</summary>
public sealed class RoadSpanSelectionSession
{
    private readonly List<RoadGridSpan> _selected = [];
    private readonly HashSet<RoadSpanKey> _keys = [];

    public RoadSpanSelectionSession() => Selected = _selected.AsReadOnly();

    public RoadStateToken? Source { get; private set; }
    public RoadGridSpan? Hovered { get; private set; }
    public IReadOnlyList<RoadGridSpan> Selected { get; }
    public bool IsSelecting { get; private set; }

    public bool Hover(RoadSnapshot snapshot, RoadGridSpan? span)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!Matches(snapshot) || (span is not null && span.Source != snapshot.Token))
        {
            Clear();
            return false;
        }
        Source = snapshot.Token;
        Hovered = span;
        return true;
    }

    public void Begin(RoadSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        RoadGridSpan? hover = Hovered?.Source == snapshot.Token ? Hovered : null;
        Clear();
        Source = snapshot.Token;
        Hovered = hover;
        IsSelecting = true;
    }

    public bool Accumulate(RoadSnapshot snapshot, IEnumerable<RoadGridSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(spans);
        if (!Matches(snapshot))
        {
            Clear();
            return false;
        }
        if (!IsSelecting) return false;
        RoadGridSpan[] additions = spans.ToArray();
        if (additions.Any(span => span.Source != snapshot.Token))
        {
            Clear();
            return false;
        }
        foreach (RoadGridSpan span in additions)
            if (_keys.Add(span.Key)) _selected.Add(span);
        return true;
    }

    public void End() => IsSelecting = false;

    public void Clear()
    {
        _selected.Clear();
        _keys.Clear();
        Source = null;
        Hovered = null;
        IsSelecting = false;
    }

    private bool Matches(RoadSnapshot snapshot) => Source is null || Source == snapshot.Token;
}
