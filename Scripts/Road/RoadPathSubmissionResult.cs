using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
    MissingPath,
    NoSegments,
    NullGeometrySegment,
    UnknownGeometryType,
    DiscontinuousGeometry,
    UnsupportedEndpointSnap,
}

public sealed class RoadGraphChangeSummary
{
    private static readonly ReadOnlyCollection<int> EmptyIDs = Array.AsReadOnly(Array.Empty<int>());

    public static RoadGraphChangeSummary Empty { get; } = new();

    public IReadOnlyList<int> CreatedNodeIDs { get; }
    public IReadOnlyList<int> CreatedEdgeIDs { get; }
    public IReadOnlyList<int> CreatedGroupIDs { get; }
    public IReadOnlyList<int> RemovedNodeIDs { get; }
    public IReadOnlyList<int> RemovedEdgeIDs { get; }
    public IReadOnlyList<int> RemovedGroupIDs { get; }
    public bool HasChanges =>
        CreatedNodeIDs.Count > 0 || CreatedEdgeIDs.Count > 0 || CreatedGroupIDs.Count > 0 ||
        RemovedNodeIDs.Count > 0 || RemovedEdgeIDs.Count > 0 || RemovedGroupIDs.Count > 0;

    private RoadGraphChangeSummary()
    {
        CreatedNodeIDs = EmptyIDs;
        CreatedEdgeIDs = EmptyIDs;
        CreatedGroupIDs = EmptyIDs;
        RemovedNodeIDs = EmptyIDs;
        RemovedEdgeIDs = EmptyIDs;
        RemovedGroupIDs = EmptyIDs;
    }

    internal RoadGraphChangeSummary(
        IEnumerable<int> createdNodeIDs,
        IEnumerable<int> createdEdgeIDs,
        IEnumerable<int> createdGroupIDs,
        IEnumerable<int> removedNodeIDs,
        IEnumerable<int> removedEdgeIDs,
        IEnumerable<int> removedGroupIDs)
    {
        CreatedNodeIDs = ToSortedReadOnly(createdNodeIDs);
        CreatedEdgeIDs = ToSortedReadOnly(createdEdgeIDs);
        CreatedGroupIDs = ToSortedReadOnly(createdGroupIDs);
        RemovedNodeIDs = ToSortedReadOnly(removedNodeIDs);
        RemovedEdgeIDs = ToSortedReadOnly(removedEdgeIDs);
        RemovedGroupIDs = ToSortedReadOnly(removedGroupIDs);
    }

    private static IReadOnlyList<int> ToSortedReadOnly(IEnumerable<int> ids)
    {
        int[] values = [.. ids];
        Array.Sort(values);
        return Array.AsReadOnly(values);
    }
}

public sealed record RoadPathSubmissionResult
{
    public bool Success => Error == RoadPathSubmissionError.None;
    public int? GroupID { get; }
    public RoadPathSubmissionError Error { get; }
    public RoadGraphChangeSummary Changes { get; }

    private RoadPathSubmissionResult(
        int? groupID,
        RoadPathSubmissionError error,
        RoadGraphChangeSummary changes)
    {
        GroupID = groupID;
        Error = error;
        Changes = changes;
    }

    internal static RoadPathSubmissionResult Succeeded(int groupID, RoadGraphChangeSummary changes) =>
        new(groupID, RoadPathSubmissionError.None, changes);

    internal static RoadPathSubmissionResult Rejected(RoadPathSubmissionError error) =>
        new(null, error, RoadGraphChangeSummary.Empty);
}
