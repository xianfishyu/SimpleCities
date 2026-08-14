using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public enum RoadPathSubmissionError
{
    None,
    TooFewPoints,
    NonFiniteCoordinate,
    DegenerateSegment,
    CollapsedByNodeIdentity,
    RepeatedPoint,
    SelfIntersection,
    SelfOverlap,
    FullyCovered,
    NoChanges,
    MissingPath,
    NoSegments,
    NullGeometrySegment,
    UnknownGeometryType,
    DiscontinuousGeometry,
    UnsupportedEndpointSnap,
    NumericOutOfRange,
    CapacityExceeded,
    AmbiguousIntersection,
    InvalidRoadType,
    MutationReentrant,
}

public sealed class RoadGraphChangeSummary
{
    private static readonly ReadOnlyCollection<int> EmptyIDs = Array.AsReadOnly(Array.Empty<int>());

    public static RoadGraphChangeSummary Empty { get; } = new();

    public IReadOnlyList<int> CreatedNodeIDs { get; }
    public IReadOnlyList<int> CreatedEdgeIDs { get; }
    public IReadOnlyList<int> RemovedNodeIDs { get; }
    public IReadOnlyList<int> RemovedEdgeIDs { get; }
    public IReadOnlyList<int> UpdatedNodeIDs { get; }
    public IReadOnlyList<int> UpdatedEdgeIDs { get; }
    public bool IsFullReset { get; }
    public long ChangeSequence { get; }
    public bool HasChanges =>
        CreatedNodeIDs.Count > 0 || CreatedEdgeIDs.Count > 0 ||
        RemovedNodeIDs.Count > 0 || RemovedEdgeIDs.Count > 0 ||
        UpdatedNodeIDs.Count > 0 || UpdatedEdgeIDs.Count > 0 ||
        IsFullReset;

    private RoadGraphChangeSummary()
    {
        CreatedNodeIDs = EmptyIDs;
        CreatedEdgeIDs = EmptyIDs;
        RemovedNodeIDs = EmptyIDs;
        RemovedEdgeIDs = EmptyIDs;
        UpdatedNodeIDs = EmptyIDs;
        UpdatedEdgeIDs = EmptyIDs;
    }

    internal RoadGraphChangeSummary(
        IEnumerable<int> createdNodeIDs,
        IEnumerable<int> createdEdgeIDs,
        IEnumerable<int> removedNodeIDs,
        IEnumerable<int> removedEdgeIDs,
        IEnumerable<int>? updatedNodeIDs = null,
        IEnumerable<int>? updatedEdgeIDs = null,
        bool isFullReset = false,
        long changeSequence = 0)
    {
        CreatedNodeIDs = ToSortedReadOnly(createdNodeIDs);
        CreatedEdgeIDs = ToSortedReadOnly(createdEdgeIDs);
        RemovedNodeIDs = ToSortedReadOnly(removedNodeIDs);
        RemovedEdgeIDs = ToSortedReadOnly(removedEdgeIDs);
        UpdatedNodeIDs = ToSortedReadOnly(updatedNodeIDs ?? []);
        UpdatedEdgeIDs = ToSortedReadOnly(updatedEdgeIDs ?? []);
        IsFullReset = isFullReset;
        ChangeSequence = changeSequence;
    }

    private static IReadOnlyList<int> ToSortedReadOnly(IEnumerable<int> ids)
    {
        int[] values = [.. ids.Distinct().Order()];
        return Array.AsReadOnly(values);
    }
}

public sealed record RoadPathSubmissionResult
{
    public bool Success => Error == RoadPathSubmissionError.None;
    public RoadPathSubmissionError Error { get; }
    public RoadGraphChangeSummary Changes { get; }

    private RoadPathSubmissionResult(
        RoadPathSubmissionError error,
        RoadGraphChangeSummary changes)
    {
        Error = error;
        Changes = changes;
    }

    internal static RoadPathSubmissionResult Succeeded(RoadGraphChangeSummary changes) =>
        new(RoadPathSubmissionError.None, changes);

    internal static RoadPathSubmissionResult Rejected(RoadPathSubmissionError error) =>
        new(error, RoadGraphChangeSummary.Empty);
}
