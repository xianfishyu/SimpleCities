using System;

public enum RoadTypeChangeError
{
    None,
    EmptySelection,
    InvalidRoadType,
    MissingEdge,
    NoChanges,
    MutationReentrant,
    CapacityExceeded,
}

public sealed record RoadTypeChangeResult
{
    public bool Success => Error == RoadTypeChangeError.None;
    public RoadTypeChangeError Error { get; }
    public RoadGraphChangeSummary Changes { get; }
    public RoadGraphDelta? Delta { get; }

    private RoadTypeChangeResult(
        RoadTypeChangeError error,
        RoadGraphChangeSummary changes,
        RoadGraphDelta? delta)
    {
        Error = error;
        Changes = changes;
        Delta = delta;
    }

    internal static RoadTypeChangeResult Succeeded(
        RoadGraphChangeSummary changes,
        RoadGraphDelta delta) =>
        new(RoadTypeChangeError.None, changes, delta);

    internal static RoadTypeChangeResult Rejected(RoadTypeChangeError error) =>
        new(error, RoadGraphChangeSummary.Empty, null);
}
