using System.Collections.Generic;
using System.Linq;

public partial class RoadGraph
{
    private NativePathIntersectionPlan PlanNativePathIntersections(
        IReadOnlyList<RoadGeometrySegment> incomingSegments)
    {
        var incomingSplitParameters = new List<float>[incomingSegments.Count];
        for (int index = 0; index < incomingSplitParameters.Length; index++)
            incomingSplitParameters[index] = new List<float>();

        var existingEdgeSplits = new Dictionary<int, List<EdgeGeometrySplitPoint>>();
        foreach (GraphEdge edge in _edges.Values.OrderBy(edge => edge.ID))
        {
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
                }
            }
        }

        return new NativePathIntersectionPlan(existingEdgeSplits, incomingSplitParameters);
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
        IEnumerable<float> interiorParameters = splitParameters.Where(parameter =>
            !ArePositionsApproximatelyEqual(geometry.GetPosition(parameter), geometry.Start) &&
            !ArePositionsApproximatelyEqual(geometry.GetPosition(parameter), geometry.End));
        return RoadGeometrySubdivision.SplitAtParameters(
            geometry,
            interiorParameters,
            GeometryParameterTolerance);
    }

    private sealed record NativePathIntersectionPlan(
        Dictionary<int, List<EdgeGeometrySplitPoint>> ExistingEdgeSplits,
        IReadOnlyList<List<float>> IncomingSplitParameters);
}
