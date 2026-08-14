using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RoadGraph
{
    private const float IntersectionEndpointParameterTolerance = 1e-4f;

    private RoadPathSubmissionError PlanNativePathIntersections(
        IReadOnlyList<RoadGeometrySegment> incomingSegments,
        out NativePathIntersectionPlan plan)
    {
        plan = NativePathIntersectionPlan.Empty;
        var incomingSplitPoints = new List<IncomingGeometrySplitPoint>[incomingSegments.Count];
        var incomingOverlapIntervals = new List<ParameterInterval>[incomingSegments.Count];
        for (int index = 0; index < incomingSplitPoints.Length; index++)
        {
            incomingSplitPoints[index] = new List<IncomingGeometrySplitPoint>();
            incomingOverlapIntervals[index] = new List<ParameterInterval>();
        }

        var existingEdgeSplits = new Dictionary<int, List<EdgeGeometrySplitPoint>>();
        var candidatePairs = new Dictionary<
            (int IncomingIndex, int EdgeID, int GeometryIndex, int FragmentIndex),
            EdgeGeometryRef>();
        for (int incomingIndex = 0; incomingIndex < incomingSegments.Count; incomingIndex++)
        {
            foreach (EdgeGeometryRef fragment in FindCandidateGeometryRefs(incomingSegments[incomingIndex]))
            {
                candidatePairs.TryAdd(
                    (incomingIndex, fragment.EdgeID, fragment.GeometryIndex, fragment.FragmentIndex),
                    fragment);
            }
            if (candidatePairs.Count > _capacity.MaximumMutationCandidates)
                return RoadPathSubmissionError.CapacityExceeded;
        }

        var witnesses = new List<RoadIntersectionWitness>();
        var fragmentOverlaps = new List<FragmentOverlap>();

        foreach (((int incomingSegmentIndex, int edgeID, int geometryIndex, int fragmentIndex),
                 EdgeGeometryRef fragment) in candidatePairs
                     .OrderBy(candidate => candidate.Key.IncomingIndex)
                     .ThenBy(candidate => candidate.Key.EdgeID)
                     .ThenBy(candidate => candidate.Key.GeometryIndex)
                     .ThenBy(candidate => candidate.Key.FragmentIndex))
        {
            if (!_edges.TryGetValue(edgeID, out GraphEdge? edge))
                continue;
            int existingSegmentIndex = geometryIndex;
            RoadGeometrySegment existingGeometry = edge.GeometrySegments[existingSegmentIndex];
            RecordExactGeometryTest();
            RoadGeometryIntersectionResult result =
                RoadGeometryIntersectionQuery.FindIntersections(
                    incomingSegments[incomingSegmentIndex],
                    fragment.Geometry);
            foreach (RoadGeometryIntersection localIntersection in result.Intersections)
            {
                float localExistingParameter = CanonicalizeFragmentParameter(
                    localIntersection.SecondParameter);
                if (!fragment.OwnsLocalParameter(localExistingParameter))
                    continue;
                float existingParameter = fragment.ToSourceParameter(localExistingParameter);
                var intersection = new RoadGeometryIntersection(
                    localIntersection.FirstParameter,
                    existingParameter,
                    localIntersection.Position,
                    localIntersection.Kind);
                if (witnesses.Count == _capacity.MaximumIntersectionWitnesses)
                    return RoadPathSubmissionError.CapacityExceeded;

                RoadIntersectionWitness witness = CreateIntersectionWitness(
                    edge,
                    existingSegmentIndex,
                    existingGeometry,
                    incomingSegmentIndex,
                    incomingSegments[incomingSegmentIndex],
                    intersection);
                witnesses.Add(witness);
            }
            foreach (RoadGeometryOverlap localOverlap in result.Overlaps)
            {
                float existingAtStart = fragment.ToSourceParameter(
                    CanonicalizeFragmentParameter(localOverlap.SecondParameterAtFirstStart));
                float existingAtEnd = fragment.ToSourceParameter(
                    CanonicalizeFragmentParameter(localOverlap.SecondParameterAtFirstEnd));
                fragmentOverlaps.Add(new FragmentOverlap(
                    incomingSegmentIndex,
                    edge.ID,
                    localOverlap.FirstParameterStart,
                    localOverlap.FirstParameterEnd,
                    existingSegmentIndex + existingAtStart,
                    existingSegmentIndex + existingAtEnd));
            }
        }

        int incomingPairCount = 0;
        bool isClosed = incomingSegments.Count > 0 && RoadExactPredicates.SameBits(
            incomingSegments[0].Start,
            incomingSegments[^1].End);
        for (int firstIndex = 0; firstIndex < incomingSegments.Count; firstIndex++)
        for (int secondIndex = firstIndex + 1; secondIndex < incomingSegments.Count; secondIndex++)
        {
            if (incomingPairCount == int.MaxValue ||
                candidatePairs.Count + (long)incomingPairCount + 1L >
                _capacity.MaximumMutationCandidates)
            {
                return RoadPathSubmissionError.CapacityExceeded;
            }
            incomingPairCount++;

            RoadGeometrySegment first = incomingSegments[firstIndex];
            RoadGeometrySegment second = incomingSegments[secondIndex];
            RecordExactGeometryTest();
            RoadGeometryIntersectionResult result =
                RoadGeometryIntersectionQuery.FindIntersections(first, second);
            if (result.HasOverlap)
                return RoadPathSubmissionError.SelfOverlap;

            foreach (RoadGeometryIntersection intersection in result.Intersections)
            {
                if (IsOrdinaryPathJoin(
                        firstIndex,
                        secondIndex,
                        incomingSegments.Count,
                        isClosed,
                        first,
                        second,
                        intersection))
                {
                    continue;
                }
                if (witnesses.Count == _capacity.MaximumIntersectionWitnesses)
                    return RoadPathSubmissionError.CapacityExceeded;

                witnesses.Add(CreateIncomingIntersectionWitness(
                    firstIndex,
                    secondIndex,
                    first,
                    second,
                    intersection));
            }
        }

        foreach (MergedOverlap overlap in MergeFragmentOverlaps(fragmentOverlaps))
        {
            incomingOverlapIntervals[overlap.IncomingSegmentIndex].Add(new ParameterInterval(
                overlap.IncomingStart,
                overlap.IncomingEnd));
            if (!_edges.TryGetValue(overlap.EdgeID, out GraphEdge? edge))
                continue;
            if (!TryAddOverlapBoundaryWitness(
                witnesses,
                incomingSegments[overlap.IncomingSegmentIndex],
                overlap.IncomingSegmentIndex,
                edge,
                overlap.IncomingStart,
                overlap.ExistingAtStart) ||
                !TryAddOverlapBoundaryWitness(
                witnesses,
                incomingSegments[overlap.IncomingSegmentIndex],
                overlap.IncomingSegmentIndex,
                edge,
                overlap.IncomingEnd,
                overlap.ExistingAtEnd))
            {
                return RoadPathSubmissionError.CapacityExceeded;
            }
        }

        RoadIntersectionClusterResult clusterResult =
            RoadIntersectionClusterer.Cluster(witnesses, _capacity);
        if (!clusterResult.Success)
        {
            return clusterResult.Error switch
            {
                RoadIntersectionClusterError.CapacityExceeded =>
                    RoadPathSubmissionError.CapacityExceeded,
                RoadIntersectionClusterError.AmbiguousIntersection =>
                    RoadPathSubmissionError.AmbiguousIntersection,
                _ => RoadPathSubmissionError.NumericOutOfRange,
            };
        }

        foreach (RoadIntersectionCluster cluster in clusterResult.Clusters)
        {
            foreach (RoadIntersectionWitness witness in cluster.Witnesses)
            {
                AddClusterSplit(
                    witness.First,
                    cluster.Position,
                    incomingSplitPoints,
                    existingEdgeSplits,
                    incomingSegments.Count,
                    isClosed);
                AddClusterSplit(
                    witness.Second,
                    cluster.Position,
                    incomingSplitPoints,
                    existingEdgeSplits,
                    incomingSegments.Count,
                    isClosed);
            }
        }

        if (!CanBuildPlannedExistingSubdivisions(existingEdgeSplits))
            return RoadPathSubmissionError.AmbiguousIntersection;

        plan = new NativePathIntersectionPlan(
            existingEdgeSplits,
            incomingSplitPoints,
            incomingOverlapIntervals,
            candidatePairs.Count + incomingPairCount,
            witnesses.Count,
            clusterResult.Clusters);
        return RoadPathSubmissionError.None;
    }

    private static bool IsOrdinaryPathJoin(
        int firstIndex,
        int secondIndex,
        int segmentCount,
        bool isClosed,
        RoadGeometrySegment first,
        RoadGeometrySegment second,
        RoadGeometryIntersection intersection)
    {
        if (intersection.Kind != RoadGeometryIntersectionKind.EndpointTouch)
            return false;
        if (secondIndex == firstIndex + 1)
        {
            return ArePositionsApproximatelyEqual(intersection.Position, first.End) &&
                   ArePositionsApproximatelyEqual(intersection.Position, second.Start);
        }
        return isClosed && firstIndex == 0 && secondIndex == segmentCount - 1 &&
               ArePositionsApproximatelyEqual(intersection.Position, first.Start) &&
               ArePositionsApproximatelyEqual(intersection.Position, second.End);
    }

    private static RoadIntersectionWitness CreateIncomingIntersectionWitness(
        int firstIndex,
        int secondIndex,
        RoadGeometrySegment first,
        RoadGeometrySegment second,
        RoadGeometryIntersection intersection) =>
        new(
            new RoadIntersectionSource(
                RoadIntersectionSourceKind.Incoming,
                0,
                firstIndex,
                intersection.FirstParameter),
            new RoadIntersectionSource(
                RoadIntersectionSourceKind.Incoming,
                0,
                secondIndex,
                intersection.SecondParameter),
            first.GetPosition(intersection.FirstParameter),
            second.GetPosition(intersection.SecondParameter));

    private bool TryAddOverlapBoundaryWitness(
        List<RoadIntersectionWitness> witnesses,
        RoadGeometrySegment incomingGeometry,
        int incomingSegmentIndex,
        GraphEdge existingEdge,
        float incomingParameter,
        float existingEdgeParameter)
    {
        float canonical = Mathf.Clamp(
            existingEdgeParameter,
            0f,
            existingEdge.GeometrySegments.Count);
        int existingSegmentIndex = Math.Min(
            Mathf.FloorToInt(canonical),
            existingEdge.GeometrySegments.Count - 1);
        float existingSegmentParameter = canonical - existingSegmentIndex;
        RoadGeometrySegment existingGeometry = existingEdge.GeometrySegments[existingSegmentIndex];
        var intersection = new RoadGeometryIntersection(
            incomingParameter,
            existingSegmentParameter,
            incomingGeometry.GetPosition(incomingParameter),
            RoadGeometryIntersectionKind.EndpointTouch);
        if (witnesses.Count == _capacity.MaximumIntersectionWitnesses)
            return false;
        witnesses.Add(CreateIntersectionWitness(
            existingEdge,
            existingSegmentIndex,
            existingGeometry,
            incomingSegmentIndex,
            incomingGeometry,
            intersection));
        return true;
    }

    private static void AddClusterSplit(
        RoadIntersectionSource source,
        Vector2 position,
        IReadOnlyList<List<IncomingGeometrySplitPoint>> incomingSplitPoints,
        Dictionary<int, List<EdgeGeometrySplitPoint>> existingEdgeSplits,
        int incomingSegmentCount,
        bool isClosed)
    {
        if (source.Kind == RoadIntersectionSourceKind.Incoming)
        {
            float parameter = CanonicalizeIntersectionEndpointParameter(source.Parameter);
            incomingSplitPoints[source.GeometryIndex].Add(
                new IncomingGeometrySplitPoint(parameter, position));
            if (parameter == RoadGeometrySegment.ParameterStart)
            {
                int previousIndex = source.GeometryIndex - 1;
                if (previousIndex >= 0)
                {
                    incomingSplitPoints[previousIndex].Add(
                        new IncomingGeometrySplitPoint(
                            RoadGeometrySegment.ParameterEnd,
                            position));
                }
                else if (isClosed && incomingSegmentCount > 0)
                {
                    incomingSplitPoints[incomingSegmentCount - 1].Add(
                        new IncomingGeometrySplitPoint(
                            RoadGeometrySegment.ParameterEnd,
                            position));
                }
            }
            else if (parameter == RoadGeometrySegment.ParameterEnd)
            {
                int nextIndex = source.GeometryIndex + 1;
                if (nextIndex < incomingSegmentCount)
                {
                    incomingSplitPoints[nextIndex].Add(
                        new IncomingGeometrySplitPoint(
                            RoadGeometrySegment.ParameterStart,
                            position));
                }
                else if (isClosed && incomingSegmentCount > 0)
                {
                    incomingSplitPoints[0].Add(
                        new IncomingGeometrySplitPoint(
                            RoadGeometrySegment.ParameterStart,
                            position));
                }
            }
            return;
        }

        GetOrCreateEdgeSplitList(existingEdgeSplits, source.OwnerID).Add(
            new EdgeGeometrySplitPoint(
                source.GeometryIndex,
                CanonicalizeIntersectionEndpointParameter(source.Parameter),
                position));
    }

    private static float CanonicalizeIntersectionEndpointParameter(float parameter)
    {
        if (parameter <= IntersectionEndpointParameterTolerance)
            return RoadGeometrySegment.ParameterStart;
        if (parameter >= RoadGeometrySegment.ParameterEnd -
            IntersectionEndpointParameterTolerance)
        {
            return RoadGeometrySegment.ParameterEnd;
        }
        return parameter;
    }

    private bool CanBuildPlannedExistingSubdivisions(
        IReadOnlyDictionary<int, List<EdgeGeometrySplitPoint>> existingEdgeSplits)
    {
        try
        {
            foreach ((int edgeID, List<EdgeGeometrySplitPoint> splitPoints) in existingEdgeSplits)
            {
                if (!_edges.TryGetValue(edgeID, out GraphEdge? edge))
                    continue;
                List<NormalizedEdgeSplitPoint> normalized = NormalizeEdgeSplitPoints(edge, splitPoints);
                if (normalized.Count > 0)
                    BuildSubdivisionGeometry(edge, normalized);
            }
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static IReadOnlyList<MergedOverlap> MergeFragmentOverlaps(
        IReadOnlyList<FragmentOverlap> fragments)
    {
        var merged = new List<MergedOverlap>();
        foreach (IGrouping<(int IncomingSegmentIndex, int EdgeID), FragmentOverlap> group in fragments
                     .GroupBy(fragment => (fragment.IncomingSegmentIndex, fragment.EdgeID))
                     .OrderBy(group => group.Key.IncomingSegmentIndex)
                     .ThenBy(group => group.Key.EdgeID))
        {
            foreach (FragmentOverlap fragment in group
                         .OrderBy(fragment => fragment.IncomingStart)
                         .ThenBy(fragment => fragment.IncomingEnd)
                         .ThenBy(fragment => fragment.ExistingAtStart))
            {
                var candidate = new MergedOverlap(
                    fragment.IncomingSegmentIndex,
                    fragment.EdgeID,
                    fragment.IncomingStart,
                    fragment.IncomingEnd,
                    fragment.ExistingAtStart,
                    fragment.ExistingAtEnd);
                if (merged.Count == 0 ||
                    merged[^1].IncomingSegmentIndex != candidate.IncomingSegmentIndex ||
                    merged[^1].EdgeID != candidate.EdgeID ||
                    !CanMergeOverlaps(merged[^1], candidate))
                {
                    merged.Add(candidate);
                    continue;
                }

                MergedOverlap previous = merged[^1];
                if (candidate.IncomingEnd > previous.IncomingEnd)
                {
                    merged[^1] = previous with
                    {
                        IncomingEnd = candidate.IncomingEnd,
                        ExistingAtEnd = candidate.ExistingAtEnd,
                    };
                }
            }
        }
        return merged;
    }

    private static bool CanMergeOverlaps(MergedOverlap first, MergedOverlap second)
    {
        if (second.IncomingStart > first.IncomingEnd + GeometryParameterTolerance)
            return false;
        bool firstForward = first.ExistingAtEnd >= first.ExistingAtStart;
        bool secondForward = second.ExistingAtEnd >= second.ExistingAtStart;
        return firstForward == secondForward &&
               Mathf.Abs(first.ExistingAtEnd - second.ExistingAtStart) <= GeometryParameterTolerance;
    }

    private static float CanonicalizeFragmentParameter(float parameter)
    {
        if (parameter <= GeometryParameterTolerance)
            return RoadGeometrySegment.ParameterStart;
        if (parameter >= RoadGeometrySegment.ParameterEnd - GeometryParameterTolerance)
            return RoadGeometrySegment.ParameterEnd;
        return parameter;
    }

    private static List<EdgeGeometrySplitPoint> GetOrCreateEdgeSplitList(
        Dictionary<int, List<EdgeGeometrySplitPoint>> edgeSplits,
        int edgeID)
    {
        if (!edgeSplits.TryGetValue(edgeID, out List<EdgeGeometrySplitPoint>? splitPoints))
        {
            splitPoints = new List<EdgeGeometrySplitPoint>();
            edgeSplits.Add(edgeID, splitPoints);
        }
        return splitPoints;
    }

    private RoadIntersectionWitness CreateIntersectionWitness(
        GraphEdge existingEdge,
        int existingSegmentIndex,
        RoadGeometrySegment existingGeometry,
        int incomingSegmentIndex,
        RoadGeometrySegment incomingGeometry,
        RoadGeometryIntersection intersection)
    {
        int? existingNodeID = null;
        Vector2 existingNodePosition = default;
        float existingParameter = CanonicalizeIntersectionEndpointParameter(
            intersection.SecondParameter);
        Vector2 existingIntersectionPosition = existingGeometry.GetPosition(
            intersection.SecondParameter);
        Vector2 edgeStart = GetNode(existingEdge.NodeA)?.Position ??
            existingEdge.GeometrySegments[0].Start;
        Vector2 edgeEnd = GetNode(existingEdge.NodeB)?.Position ??
            existingEdge.GeometrySegments[^1].End;
        if (existingSegmentIndex == 0 &&
            (existingParameter == RoadGeometrySegment.ParameterStart ||
             IsWithinIntersectionCluster(existingIntersectionPosition, edgeStart)))
        {
            existingNodeID = existingEdge.NodeA;
            existingNodePosition = edgeStart;
        }
        else if (existingSegmentIndex == existingEdge.GeometrySegments.Count - 1 &&
                 (existingParameter == RoadGeometrySegment.ParameterEnd ||
                  IsWithinIntersectionCluster(existingIntersectionPosition, edgeEnd)))
        {
            existingNodeID = existingEdge.NodeB;
            existingNodePosition = edgeEnd;
        }

        return new RoadIntersectionWitness(
            new RoadIntersectionSource(
                RoadIntersectionSourceKind.Incoming,
                0,
                incomingSegmentIndex,
                intersection.FirstParameter),
            new RoadIntersectionSource(
                RoadIntersectionSourceKind.Existing,
                existingEdge.ID,
                existingSegmentIndex,
                intersection.SecondParameter),
            incomingGeometry.GetPosition(intersection.FirstParameter),
            existingIntersectionPosition,
            existingNodeID,
            existingNodePosition);
    }

    private static bool IsWithinIntersectionCluster(Vector2 first, Vector2 second) =>
        RoadNumericPolicy.DistanceSquared(first, second) <=
        (double)RoadNumericPolicy.MaximumIntersectionClusterDiameter *
        RoadNumericPolicy.MaximumIntersectionClusterDiameter;

    private void ApplyExistingEdgeSplits(NativePathIntersectionPlan plan)
    {
        foreach ((int edgeID, List<EdgeGeometrySplitPoint> splitPoints) in
                 plan.ExistingEdgeSplits.OrderBy(pair => pair.Key))
            ApplyAdmittedEdgeSubdivision(edgeID, splitPoints);
    }

    private static IReadOnlyList<IncomingPathSubsegment> SubdivideIncomingSegment(
        RoadGeometrySegment geometry,
        IEnumerable<IncomingGeometrySplitPoint> splitPoints)
    {
        List<IncomingGeometrySplitPoint> candidates = splitPoints
            .Select(splitPoint => splitPoint with
            {
                Parameter = CanonicalizeIntersectionEndpointParameter(splitPoint.Parameter),
            })
            .OrderBy(splitPoint => splitPoint.Parameter)
            .ThenBy(splitPoint => splitPoint.CanonicalPosition.HasValue ? 0 : 1)
            .ThenBy(splitPoint => splitPoint.CanonicalPosition?.X ?? float.PositiveInfinity)
            .ThenBy(splitPoint => splitPoint.CanonicalPosition?.Y ?? float.PositiveInfinity)
            .ToList();
        Vector2 start = geometry.Start;
        Vector2 end = geometry.End;
        var interiorSplitPoints = new List<IncomingGeometrySplitPoint>(candidates.Count);
        IncomingGeometrySplitPoint? previousCandidate = null;
        foreach (IncomingGeometrySplitPoint candidate in candidates)
        {
            if (previousCandidate is IncomingGeometrySplitPoint previous &&
                candidate.Parameter - previous.Parameter <= GeometryParameterTolerance)
            {
                if (HaveConflictingCanonicalPositions(previous, candidate))
                {
                    throw new InvalidOperationException(
                        "Equivalent incoming split parameters have conflicting canonical positions.");
                }
                if (previous.CanonicalPosition.HasValue || !candidate.CanonicalPosition.HasValue)
                    continue;
            }
            previousCandidate = candidate;

            if (candidate.Parameter <= IntersectionEndpointParameterTolerance)
            {
                start = candidate.CanonicalPosition ?? start;
                continue;
            }
            if (candidate.Parameter >= RoadGeometrySegment.ParameterEnd -
                IntersectionEndpointParameterTolerance)
            {
                end = candidate.CanonicalPosition ?? end;
                continue;
            }
            if (interiorSplitPoints.Count > 0 &&
                (candidate.Parameter - interiorSplitPoints[^1].Parameter <=
                     GeometryParameterTolerance ||
                 ArePositionsApproximatelyEqual(
                     geometry.GetPosition(candidate.Parameter),
                     geometry.GetPosition(interiorSplitPoints[^1].Parameter))))
            {
                if (HaveConflictingCanonicalPositions(interiorSplitPoints[^1], candidate))
                {
                    throw new InvalidOperationException(
                        "Equivalent incoming split positions have conflicting canonical positions.");
                }
                if (candidate.CanonicalPosition.HasValue &&
                    !interiorSplitPoints[^1].CanonicalPosition.HasValue)
                {
                    interiorSplitPoints[^1] = candidate;
                }
                continue;
            }
            interiorSplitPoints.Add(candidate);
        }
        IReadOnlyList<RoadGeometrySubsegment> nativeSubsegments =
            RoadGeometrySubdivision.SplitAtParameters(
            geometry,
            interiorSplitPoints.Select(splitPoint => splitPoint.Parameter),
            GeometryParameterTolerance);
        var planned = new List<IncomingPathSubsegment>(nativeSubsegments.Count);
        for (int index = 0; index < nativeSubsegments.Count; index++)
        {
            RoadGeometrySubsegment subsegment = nativeSubsegments[index];
            Vector2 anchoredStart = index == 0
                ? start
                : interiorSplitPoints[index - 1].CanonicalPosition ?? subsegment.Geometry.Start;
            Vector2 anchoredEnd = index == nativeSubsegments.Count - 1
                ? end
                : interiorSplitPoints[index].CanonicalPosition ?? subsegment.Geometry.End;
            IReadOnlyList<RoadGeometrySegment> anchored =
                RoadGeometryCanonicalizer.ReanchorChain(
                    [subsegment.Geometry],
                    anchoredStart,
                    anchoredEnd);
            planned.Add(new IncomingPathSubsegment(
                subsegment.ParameterStart,
                subsegment.ParameterEnd,
                anchored[0]));
        }
        return planned;
    }

    private static RoadPathSubmissionError PlanIncomingPieces(
        IReadOnlyList<RoadGeometrySegment> incomingSegments,
        IReadOnlyList<bool> coveredSegments,
        NativePathIntersectionPlan plan,
        out IReadOnlyList<NativePathPiece> plannedPieces)
    {
        plannedPieces = Array.Empty<NativePathPiece>();
        var pieces = new List<NativePathPiece>();
        try
        {
            for (int segmentIndex = 0; segmentIndex < incomingSegments.Count; segmentIndex++)
            {
                foreach (IncomingPathSubsegment subsegment in SubdivideIncomingSegment(
                             incomingSegments[segmentIndex],
                             plan.IncomingSplitPoints[segmentIndex]))
                {
                    float midpoint = (subsegment.ParameterStart + subsegment.ParameterEnd) * 0.5f;
                    bool covered = coveredSegments[segmentIndex] ||
                        plan.IncomingOverlapIntervals[segmentIndex].Any(interval =>
                            midpoint >= interval.Start - GeometryParameterTolerance &&
                            midpoint <= interval.End + GeometryParameterTolerance);
                    pieces.Add(new NativePathPiece(subsegment.Geometry, covered));
                }
            }
        }
        catch (ArgumentException)
        {
            return RoadPathSubmissionError.AmbiguousIntersection;
        }
        catch (InvalidOperationException)
        {
            return RoadPathSubmissionError.AmbiguousIntersection;
        }
        catch (NotSupportedException)
        {
            return RoadPathSubmissionError.UnsupportedEndpointSnap;
        }

        plannedPieces = pieces;
        return RoadPathSubmissionError.None;
    }

    private static bool HaveConflictingCanonicalPositions(
        IncomingGeometrySplitPoint first,
        IncomingGeometrySplitPoint second) =>
        first.CanonicalPosition is Vector2 firstPosition &&
        second.CanonicalPosition is Vector2 secondPosition &&
        !IsWithinIntersectionCluster(firstPosition, secondPosition);

    private sealed record NativePathIntersectionPlan(
        Dictionary<int, List<EdgeGeometrySplitPoint>> ExistingEdgeSplits,
        IReadOnlyList<List<IncomingGeometrySplitPoint>> IncomingSplitPoints,
        IReadOnlyList<List<ParameterInterval>> IncomingOverlapIntervals,
        int CandidateEdgeCount,
        int IntersectionWitnessCount,
        IReadOnlyList<RoadIntersectionCluster> IntersectionClusters)
    {
        internal static NativePathIntersectionPlan Empty { get; } = new(
            new Dictionary<int, List<EdgeGeometrySplitPoint>>(),
            System.Array.Empty<List<IncomingGeometrySplitPoint>>(),
            System.Array.Empty<List<ParameterInterval>>(),
            0,
            0,
            System.Array.Empty<RoadIntersectionCluster>());
    }

    private readonly record struct ParameterInterval(float Start, float End);

    private readonly record struct IncomingGeometrySplitPoint(
        float Parameter,
        Vector2? CanonicalPosition = null);

    private readonly record struct IncomingPathSubsegment(
        float ParameterStart,
        float ParameterEnd,
        RoadGeometrySegment Geometry);

    private readonly record struct FragmentOverlap(
        int IncomingSegmentIndex,
        int EdgeID,
        float IncomingStart,
        float IncomingEnd,
        float ExistingAtStart,
        float ExistingAtEnd);

    private readonly record struct MergedOverlap(
        int IncomingSegmentIndex,
        int EdgeID,
        float IncomingStart,
        float IncomingEnd,
        float ExistingAtStart,
        float ExistingAtEnd);

    private readonly record struct NativePathPiece(
        RoadGeometrySegment Geometry,
        bool Covered);
}
