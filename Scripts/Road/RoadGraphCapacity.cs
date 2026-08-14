using System;

internal enum RoadGraphCapacityError
{
    None,
    NodeLimitExceeded,
    EdgeLimitExceeded,
    GeometryLimitExceeded,
    QueryFragmentLimitExceeded,
    BucketLimitExceeded,
    SpatialReferenceLimitExceeded,
    MutationCandidateLimitExceeded,
    MutationSplitLimitExceeded,
    IntersectionWitnessLimitExceeded,
    EdgeGeometryLimitExceeded,
    PreparedAllocationLimitExceeded,
    IDSpaceExhausted,
}

internal readonly record struct RoadGraphResourceCounts(
    long Nodes,
    long Edges,
    long GeometrySegments,
    long QueryFragments,
    long Buckets,
    long SpatialReferences);

internal sealed record RoadGraphCapacity
{
    internal static RoadGraphCapacity Default { get; } = new();

    internal int MaximumNodes { get; init; } = 1_000_000;
    internal int MaximumEdges { get; init; } = 1_000_000;
    internal int MaximumGeometrySegments { get; init; } = 4_000_000;
    internal int MaximumGeometrySegmentsPerEdge { get; init; } = 4_000_000;
    internal int MaximumQueryFragments { get; init; } = 8_000_000;
    internal int MaximumBuckets { get; init; } = 2_000_000;
    internal int MaximumSpatialReferences { get; init; } = 16_000_000;
    internal int MaximumMutationCandidates { get; init; } = 65_536;
    internal int MaximumMutationSplits { get; init; } = 65_536;
    internal int MaximumIntersectionWitnesses { get; init; } = 16_384;
    internal int MaximumIntersectionClusterWitnesses { get; init; } = 1_024;
    internal long MaximumPreparedAllocationBytes { get; init; } = 8L * 1024 * 1024 * 1024;

    internal RoadGraphCapacityError Validate(RoadGraphResourceCounts counts)
    {
        if (counts.Nodes < 0 || counts.Nodes > MaximumNodes)
            return RoadGraphCapacityError.NodeLimitExceeded;
        if (counts.Edges < 0 || counts.Edges > MaximumEdges)
            return RoadGraphCapacityError.EdgeLimitExceeded;
        if (counts.GeometrySegments < 0 || counts.GeometrySegments > MaximumGeometrySegments)
            return RoadGraphCapacityError.GeometryLimitExceeded;
        if (counts.QueryFragments < 0 || counts.QueryFragments > MaximumQueryFragments)
            return RoadGraphCapacityError.QueryFragmentLimitExceeded;
        if (counts.Buckets < 0 || counts.Buckets > MaximumBuckets)
            return RoadGraphCapacityError.BucketLimitExceeded;
        if (counts.SpatialReferences < 0 || counts.SpatialReferences > MaximumSpatialReferences)
            return RoadGraphCapacityError.SpatialReferenceLimitExceeded;
        return RoadGraphCapacityError.None;
    }

    internal RoadGraphCapacityError ValidateMutationWork(int candidates, int splits, int witnesses)
    {
        if (candidates < 0 || candidates > MaximumMutationCandidates)
            return RoadGraphCapacityError.MutationCandidateLimitExceeded;
        if (splits < 0 || splits > MaximumMutationSplits)
            return RoadGraphCapacityError.MutationSplitLimitExceeded;
        if (witnesses < 0 || witnesses > MaximumIntersectionWitnesses)
            return RoadGraphCapacityError.IntersectionWitnessLimitExceeded;
        return RoadGraphCapacityError.None;
    }

    internal RoadGraphCapacityError ValidateEdgeGeometryCount(long geometrySegments) =>
        geometrySegments < 0 || geometrySegments > MaximumGeometrySegmentsPerEdge
            ? RoadGraphCapacityError.EdgeGeometryLimitExceeded
            : RoadGraphCapacityError.None;

    internal RoadGraphCapacityError ValidatePreparedAllocation(long nodes, long edges, long geometrySegments)
    {
        if (nodes < 0 || edges < 0 || geometrySegments < 0)
            return RoadGraphCapacityError.PreparedAllocationLimitExceeded;
        const long bytesPerNode = 256;
        const long bytesPerEdge = 512;
        const long bytesPerGeometry = 1024;
        if (!TryMultiplyAndAdd(nodes, bytesPerNode, 0, out long estimate) ||
            !TryMultiplyAndAdd(edges, bytesPerEdge, estimate, out estimate) ||
            !TryMultiplyAndAdd(geometrySegments, bytesPerGeometry, estimate, out estimate) ||
            estimate > MaximumPreparedAllocationBytes)
        {
            return RoadGraphCapacityError.PreparedAllocationLimitExceeded;
        }
        return RoadGraphCapacityError.None;
    }

    private static bool TryMultiplyAndAdd(long count, long unit, long current, out long result)
    {
        result = 0;
        if (count > long.MaxValue / unit)
            return false;
        long value = count * unit;
        if (current > long.MaxValue - value)
            return false;
        result = current + value;
        return true;
    }
}

internal readonly record struct RoadGraphIDReservation(int Start, int Count)
{
    internal int EndExclusive => checked(Start + Count);

    internal int GetID(int offset)
    {
        if ((uint)offset >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(offset));
        return checked(Start + offset);
    }

    internal static RoadGraphCapacityError TryCreate(
        int nextID,
        int count,
        out RoadGraphIDReservation reservation)
    {
        reservation = default;
        if (nextID < 0 || count < 0 || (long)nextID + count > int.MaxValue)
            return RoadGraphCapacityError.IDSpaceExhausted;

        reservation = new RoadGraphIDReservation(nextID, count);
        return RoadGraphCapacityError.None;
    }
}
