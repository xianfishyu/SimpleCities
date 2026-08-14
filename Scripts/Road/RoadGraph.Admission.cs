using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

public partial class RoadGraph
{
    private bool AdmitEdgeSubdivision(
        GraphEdge edge,
        IReadOnlyList<IReadOnlyList<RoadGeometrySegment>> replacementGeometry,
        int splitCount)
    {
        RecordMutationAdmissionPass();
        RoadGraphResourceCounts current = CaptureResourceCounts();
        long replacementGeometryCount = replacementGeometry.Sum(chain => (long)chain.Count);
        long replacementFragmentCount = 0;
        long replacementSpatialEntries = 0;
        double currentGraphLength = _totalGeometryLength;
        double projectedGraphLength = currentGraphLength - edge.GeometrySegments.Sum(
            geometry => (double)geometry.Length);

        foreach (IReadOnlyList<RoadGeometrySegment> chain in replacementGeometry)
        {
            RoadNumericError numericError = RoadNumericPolicy.ValidateGeometryChain(
                chain,
                projectedGraphLength,
                out _,
                out projectedGraphLength);
            if (numericError != RoadNumericError.None)
                return false;

            if (!TryMeasureQueryFragments(
                    chain,
                    ownsEdgeEnd: true,
                    ref replacementFragmentCount,
                    ref replacementSpatialEntries))
                return false;
        }

        if (!_edgeRefs.TryGetValue(edge.ID, out ImmutableArray<ISpatialRef> oldReferences))
            return false;
        long oldFragmentCount = oldReferences.Length;
        long oldSpatialEntries = 0;
        foreach (EdgeGeometryRef fragment in oldReferences.Cast<EdgeGeometryRef>())
        {
            if (!_spatialIndex.TryCountCoveredBuckets(fragment.Bounds, out long bucketCount) ||
                !TryAdd(ref oldSpatialEntries, bucketCount))
            {
                return false;
            }
        }

        long projectedSpatialEntries = current.SpatialReferences - oldSpatialEntries;
        if (projectedSpatialEntries < 0L ||
            !TryAdd(ref projectedSpatialEntries, replacementSpatialEntries) ||
            !TryAdd(ref projectedSpatialEntries, splitCount))
        {
            return false;
        }

        var projected = new RoadGraphResourceCounts(
            current.Nodes + splitCount,
            current.Edges + splitCount,
            current.GeometrySegments - edge.GeometrySegments.Count + replacementGeometryCount,
            current.QueryFragments - oldFragmentCount + replacementFragmentCount,
            current.Buckets + replacementSpatialEntries + splitCount,
            projectedSpatialEntries);
        if (_capacity.Validate(projected) != RoadGraphCapacityError.None ||
            _capacity.ValidateMutationWork(1, splitCount, 0) != RoadGraphCapacityError.None)
        {
            return false;
        }

        long requiredIDs = splitCount * 2L;
        return requiredIDs <= int.MaxValue &&
               RoadGraphIDReservation.TryCreate(
                   _nextID,
                   (int)requiredIDs,
                   out _) == RoadGraphCapacityError.None;
    }

    private bool CanAllocateIDs(int count) =>
        RoadGraphIDReservation.TryCreate(
            _nextID,
            count,
            out _) == RoadGraphCapacityError.None;

    private RoadPathSubmissionError AdmitNativePathMutation(
        IReadOnlyList<NativePathPiece> incomingPieces,
        NativePathIntersectionPlan intersectionPlan,
        out int reservedIDCount)
    {
        RecordMutationAdmissionPass();
        reservedIDCount = 0;
        RoadGraphResourceCounts current = CaptureResourceCounts();

        long existingSplitCount = 0;
        long removedGeometryCount = 0;
        long removedFragmentCount = 0;
        long removedSpatialEntries = 0;
        long replacementGeometryCount = 0;
        long replacementFragmentCount = 0;
        long replacementSpatialEntries = 0;
        foreach ((int edgeID, List<EdgeGeometrySplitPoint> splitPoints) in
                 intersectionPlan.ExistingEdgeSplits)
        {
            if (!_edges.TryGetValue(edgeID, out GraphEdge? edge))
                continue;

            List<NormalizedEdgeSplitPoint> normalized = NormalizeEdgeSplitPoints(edge, splitPoints);
            int normalizedSplitCount = normalized.Count;
            existingSplitCount += normalizedSplitCount;
            removedGeometryCount += edge.GeometrySegments.Count;
            if (!_edgeRefs.TryGetValue(edge.ID, out ImmutableArray<ISpatialRef> oldReferences))
                return RoadPathSubmissionError.CapacityExceeded;
            removedFragmentCount += oldReferences.Length;
            foreach (EdgeGeometryRef fragment in oldReferences.Cast<EdgeGeometryRef>())
            {
                if (!_spatialIndex.TryCountCoveredBuckets(fragment.Bounds, out long bucketCount))
                    return RoadPathSubmissionError.NumericOutOfRange;
                if (!TryAdd(ref removedSpatialEntries, bucketCount))
                    return RoadPathSubmissionError.CapacityExceeded;
            }

            IReadOnlyList<IReadOnlyList<RoadGeometrySegment>> replacementChains =
                BuildSubdivisionGeometry(edge, normalized);
            foreach (IReadOnlyList<RoadGeometrySegment> chain in replacementChains)
            {
                replacementGeometryCount += chain.Count;
                if (!TryMeasureQueryFragments(
                        chain,
                        ownsEdgeEnd: true,
                        ref replacementFragmentCount,
                        ref replacementSpatialEntries))
                    return RoadPathSubmissionError.CapacityExceeded;
            }
        }

        NativePathPiece[] addedPieces = incomingPieces.Where(piece => !piece.Covered).ToArray();
        long incomingEdgeCount = addedPieces.LongLength;
        long worstNewNodes = existingSplitCount + incomingEdgeCount * 2L;
        long prospectiveEdges = current.Edges + existingSplitCount + incomingEdgeCount;
        long incomingFragmentCount = 0;
        long incomingSpatialEntries = 0;

        double projectedGraphLength = _totalGeometryLength;
        foreach (NativePathPiece piece in addedPieces)
        {
            RoadNumericError numericError = RoadNumericPolicy.ValidateGeometryChain(
                [piece.Geometry],
                projectedGraphLength,
                out _,
                out projectedGraphLength);
            if (numericError != RoadNumericError.None)
                return RoadPathSubmissionError.NumericOutOfRange;

            if (!TryMeasureQueryFragments(
                    [piece.Geometry],
                    ownsEdgeEnd: true,
                    ref incomingFragmentCount,
                    ref incomingSpatialEntries))
                return RoadPathSubmissionError.CapacityExceeded;
        }

        long prospectiveGeometry = current.GeometrySegments - removedGeometryCount;
        long prospectiveFragments = current.QueryFragments - removedFragmentCount;
        long prospectiveSpatialEntries = current.SpatialReferences - removedSpatialEntries;
        if (prospectiveGeometry < 0L || prospectiveFragments < 0L || prospectiveSpatialEntries < 0L ||
            !TryAdd(ref prospectiveGeometry, replacementGeometryCount) ||
            !TryAdd(ref prospectiveGeometry, incomingEdgeCount) ||
            !TryAdd(ref prospectiveFragments, replacementFragmentCount) ||
            !TryAdd(ref prospectiveFragments, incomingFragmentCount) ||
            !TryAdd(ref prospectiveSpatialEntries, replacementSpatialEntries) ||
            !TryAdd(ref prospectiveSpatialEntries, incomingSpatialEntries) ||
            !TryAdd(ref prospectiveSpatialEntries, worstNewNodes))
        {
            return RoadPathSubmissionError.CapacityExceeded;
        }
        long prospectiveBuckets = current.Buckets;
        if (!TryAdd(ref prospectiveBuckets, replacementSpatialEntries) ||
            !TryAdd(ref prospectiveBuckets, incomingSpatialEntries) ||
            !TryAdd(ref prospectiveBuckets, worstNewNodes))
        {
            return RoadPathSubmissionError.CapacityExceeded;
        }
        var projected = new RoadGraphResourceCounts(
            current.Nodes + worstNewNodes,
            prospectiveEdges,
            prospectiveGeometry,
            prospectiveFragments,
            prospectiveBuckets,
            prospectiveSpatialEntries);
        if (_capacity.Validate(projected) != RoadGraphCapacityError.None)
            return RoadPathSubmissionError.CapacityExceeded;

        long splitWork = existingSplitCount +
                         Math.Max(0L, incomingPieces.Count - intersectionPlan.IncomingSplitPoints.Count);
        if (splitWork > int.MaxValue ||
            _capacity.ValidateMutationWork(
                intersectionPlan.CandidateEdgeCount,
                (int)splitWork,
                intersectionPlan.IntersectionWitnessCount) != RoadGraphCapacityError.None)
        {
            return RoadPathSubmissionError.CapacityExceeded;
        }

        long requiredIDs = existingSplitCount * 2L + incomingEdgeCount * 3L;
        if (requiredIDs > int.MaxValue ||
            RoadGraphIDReservation.TryCreate(
                _nextID,
                (int)requiredIDs,
                out _) != RoadGraphCapacityError.None)
        {
            return RoadPathSubmissionError.CapacityExceeded;
        }

        reservedIDCount = (int)requiredIDs;
        return RoadPathSubmissionError.None;
    }

    private bool TryMeasureQueryFragments(
        IReadOnlyList<RoadGeometrySegment> geometrySegments,
        bool ownsEdgeEnd,
        ref long fragmentCount,
        ref long spatialEntries)
    {
        long remaining = _capacity.MaximumQueryFragments - fragmentCount;
        if (remaining < 0L ||
            !RoadQueryFragmentFactory.TryCreateChain(
                -1,
                geometrySegments,
                _spatialIndex.BucketSize,
                ownsEdgeEnd,
                (int)Math.Min(remaining, int.MaxValue),
                out IReadOnlyList<EdgeGeometryRef> fragments))
        {
            return false;
        }

        if (!TryAdd(ref fragmentCount, fragments.Count))
            return false;
        foreach (EdgeGeometryRef fragment in fragments)
        {
            if (!_spatialIndex.TryCountCoveredBuckets(fragment.Bounds, out long bucketCount) ||
                !TryAdd(ref spatialEntries, bucketCount))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryAdd(ref long total, long value)
    {
        if (value < 0L || total > long.MaxValue - value)
            return false;
        total += value;
        return true;
    }
}
