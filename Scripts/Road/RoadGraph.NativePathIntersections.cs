using System.Collections.Generic;
using System.Linq;

public partial class RoadGraph
{
    private NativePathIntersectionPlan PlanNativePathIntersections(
        IReadOnlyList<RoadGeometrySegment> incomingSegments)
    {
        var incomingSplitParameters = new List<float>[incomingSegments.Count];
        var incomingOverlapIntervals = new List<ParameterInterval>[incomingSegments.Count];
        for (int index = 0; index < incomingSplitParameters.Length; index++)
        {
            incomingSplitParameters[index] = new List<float>();
            incomingOverlapIntervals[index] = new List<ParameterInterval>();
        }

        var existingEdgeSplits = new Dictionary<int, List<EdgeGeometrySplitPoint>>();
        var candidateEdgeIDs = new HashSet<int>();
        foreach (RoadGeometrySegment incomingSegment in incomingSegments)
            candidateEdgeIDs.UnionWith(FindCandidateEdgeIDs(incomingSegment));

        foreach (int edgeID in candidateEdgeIDs.Order())
        {
            if (!_edges.TryGetValue(edgeID, out GraphEdge? edge)) continue;
            for (int existingSegmentIndex = 0;
                 existingSegmentIndex < edge.GeometrySegments.Count;
                 existingSegmentIndex++)
            {
                RoadGeometrySegment existingGeometry = edge.GeometrySegments[existingSegmentIndex];
                for (int incomingSegmentIndex = 0;
                     incomingSegmentIndex < incomingSegments.Count;
                     incomingSegmentIndex++)
                {
                    RoadGeometryIntersectionResult result =
                        RoadGeometryIntersectionQuery.FindIntersections(
                            incomingSegments[incomingSegmentIndex],
                            existingGeometry);
                    foreach (RoadGeometryIntersection intersection in result.Intersections)
                    {
                        incomingSplitParameters[incomingSegmentIndex].Add(
                            intersection.FirstParameter);
                        if (!existingEdgeSplits.TryGetValue(
                                edge.ID,
                                out List<EdgeGeometrySplitPoint>? edgeSplitPoints))
                        {
                            edgeSplitPoints = new List<EdgeGeometrySplitPoint>();
                            existingEdgeSplits.Add(edge.ID, edgeSplitPoints);
                        }
                        edgeSplitPoints.Add(new EdgeGeometrySplitPoint(
                            existingSegmentIndex,
                            intersection.SecondParameter));
                    }
                    foreach (RoadGeometryOverlap overlap in result.Overlaps)
                    {
                        incomingSplitParameters[incomingSegmentIndex].Add(
                            overlap.FirstParameterStart);
                        incomingSplitParameters[incomingSegmentIndex].Add(
                            overlap.FirstParameterEnd);
                        incomingOverlapIntervals[incomingSegmentIndex].Add(new ParameterInterval(
                            overlap.FirstParameterStart,
                            overlap.FirstParameterEnd));

                        if (!existingEdgeSplits.TryGetValue(
                                edge.ID,
                                out List<EdgeGeometrySplitPoint>? edgeSplitPoints))
                        {
                            edgeSplitPoints = new List<EdgeGeometrySplitPoint>();
                            existingEdgeSplits.Add(edge.ID, edgeSplitPoints);
                        }
                        edgeSplitPoints.Add(new EdgeGeometrySplitPoint(
                            existingSegmentIndex,
                            overlap.SecondParameterAtFirstStart));
                        edgeSplitPoints.Add(new EdgeGeometrySplitPoint(
                            existingSegmentIndex,
                            overlap.SecondParameterAtFirstEnd));
                    }
                }
            }
        }

        return new NativePathIntersectionPlan(
            existingEdgeSplits,
            incomingSplitParameters,
            incomingOverlapIntervals);
    }

    private void ApplyExistingEdgeSplits(NativePathIntersectionPlan plan)
    {
        foreach ((int edgeID, List<EdgeGeometrySplitPoint> splitPoints) in
                 plan.ExistingEdgeSplits.OrderBy(pair => pair.Key))
            SplitEdgeAtGeometryParameters(edgeID, splitPoints);
    }

    private static IReadOnlyList<RoadGeometrySubsegment> SubdivideIncomingSegment(
        RoadGeometrySegment geometry,
        IEnumerable<float> splitParameters)
    {
        List<float> candidates = splitParameters
            .Where(parameter =>
                !ArePositionsApproximatelyEqual(geometry.GetPosition(parameter), geometry.Start) &&
                !ArePositionsApproximatelyEqual(geometry.GetPosition(parameter), geometry.End))
            .Order()
            .ToList();
        var interiorParameters = new List<float>(candidates.Count);
        foreach (float candidate in candidates)
        {
            if (interiorParameters.Count > 0 &&
                (candidate - interiorParameters[^1] <= GeometryParameterTolerance ||
                 ArePositionsApproximatelyEqual(
                     geometry.GetPosition(candidate),
                     geometry.GetPosition(interiorParameters[^1]))))
                continue;
            interiorParameters.Add(candidate);
        }
        return RoadGeometrySubdivision.SplitAtParameters(
            geometry,
            interiorParameters,
            GeometryParameterTolerance);
    }

    private static IReadOnlyList<NativePathPiece> PlanIncomingPieces(
        IReadOnlyList<RoadGeometrySegment> incomingSegments,
        IReadOnlyList<bool> coveredSegments,
        NativePathIntersectionPlan plan)
    {
        var pieces = new List<NativePathPiece>();
        for (int segmentIndex = 0; segmentIndex < incomingSegments.Count; segmentIndex++)
        {
            foreach (RoadGeometrySubsegment subsegment in SubdivideIncomingSegment(
                         incomingSegments[segmentIndex],
                         plan.IncomingSplitParameters[segmentIndex]))
            {
                float midpoint = (subsegment.ParameterStart + subsegment.ParameterEnd) * 0.5f;
                bool covered = coveredSegments[segmentIndex] ||
                    plan.IncomingOverlapIntervals[segmentIndex].Any(interval =>
                        midpoint >= interval.Start - GeometryParameterTolerance &&
                        midpoint <= interval.End + GeometryParameterTolerance);
                pieces.Add(new NativePathPiece(subsegment.Geometry, covered));
            }
        }
        return pieces;
    }

    private sealed record NativePathIntersectionPlan(
        Dictionary<int, List<EdgeGeometrySplitPoint>> ExistingEdgeSplits,
        IReadOnlyList<List<float>> IncomingSplitParameters,
        IReadOnlyList<List<ParameterInterval>> IncomingOverlapIntervals);

    private readonly record struct ParameterInterval(float Start, float End);

    private readonly record struct NativePathPiece(
        RoadGeometrySegment Geometry,
        bool Covered);
}
