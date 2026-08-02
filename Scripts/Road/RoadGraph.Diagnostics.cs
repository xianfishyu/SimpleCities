using System.Collections.Generic;

internal readonly record struct RoadGraphOperationMetrics(
    int SpatialCandidateEdgeCount,
    int FullEdgeScanPassCount,
    long FullEdgeVisitCount);

public partial class RoadGraph
{
    private int _spatialCandidateEdgeCount;
    private int _fullEdgeScanPassCount;
    private long _fullEdgeVisitCount;

    internal RoadGraphOperationMetrics LastOperationMetrics => new(
        _spatialCandidateEdgeCount,
        _fullEdgeScanPassCount,
        _fullEdgeVisitCount);

    private void BeginMeasuredOperation()
    {
        _spatialCandidateEdgeCount = 0;
        _fullEdgeScanPassCount = 0;
        _fullEdgeVisitCount = 0;
    }

    private void RecordSpatialCandidates(int count) =>
        _spatialCandidateEdgeCount += count;

    private IEnumerable<GraphEdge> EnumerateEdgesForGeometryScan()
    {
        _fullEdgeScanPassCount++;
        foreach (GraphEdge edge in _edges.Values)
        {
            _fullEdgeVisitCount++;
            yield return edge;
        }
    }
}
