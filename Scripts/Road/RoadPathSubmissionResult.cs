public enum RoadPathSubmissionError
{
    None,
    TooFewPoints,
    NonFiniteCoordinate,
    DegenerateSegment,
    CollapsedByNodeIdentity,
    RepeatedPoint,
    SelfIntersection,
    FullyCovered,
    NoChanges,
}

public readonly record struct RoadPathSubmissionResult
{
    public bool Success => Error == RoadPathSubmissionError.None;
    public int? GroupID { get; }
    public RoadPathSubmissionError Error { get; }

    private RoadPathSubmissionResult(int? groupID, RoadPathSubmissionError error)
    {
        GroupID = groupID;
        Error = error;
    }

    internal static RoadPathSubmissionResult Succeeded(int groupID) =>
        new(groupID, RoadPathSubmissionError.None);

    internal static RoadPathSubmissionResult Rejected(RoadPathSubmissionError error) =>
        new(null, error);
}
