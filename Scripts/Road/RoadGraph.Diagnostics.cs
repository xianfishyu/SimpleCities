using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

internal readonly record struct RoadGraphOperationMetrics(
    int SpatialCandidateEdgeCount,
    int QueryFragmentCandidateCount,
    long ExactGeometryTestCount,
    int FullEdgeScanPassCount,
    long FullEdgeVisitCount,
    int MutationAdmissionPassCount);

public partial class RoadGraph
{
    private int _spatialCandidateEdgeCount;
    private int _queryFragmentCandidateCount;
    private long _exactGeometryTestCount;
    private int _fullEdgeScanPassCount;
    private long _fullEdgeVisitCount;
    private int _mutationAdmissionPassCount;

    internal RoadGraphOperationMetrics LastOperationMetrics => new(
        _spatialCandidateEdgeCount,
        _queryFragmentCandidateCount,
        _exactGeometryTestCount,
        _fullEdgeScanPassCount,
        _fullEdgeVisitCount,
        _mutationAdmissionPassCount);

    private void BeginMeasuredOperation()
    {
        _spatialCandidateEdgeCount = 0;
        _queryFragmentCandidateCount = 0;
        _exactGeometryTestCount = 0;
        _fullEdgeScanPassCount = 0;
        _fullEdgeVisitCount = 0;
        _mutationAdmissionPassCount = 0;
    }

    private void RecordSpatialCandidates(int count) =>
        _spatialCandidateEdgeCount += count;

    private void RecordQueryFragmentCandidates(int count) =>
        _queryFragmentCandidateCount += count;

    private void RecordExactGeometryTest() => _exactGeometryTestCount++;

    private void RecordMutationAdmissionPass() => _mutationAdmissionPassCount++;

    private IEnumerable<GraphEdge> EnumerateEdgesForGeometryScan()
    {
        _fullEdgeScanPassCount++;
        foreach (GraphEdge edge in _edges.Values)
        {
            _fullEdgeVisitCount++;
            yield return edge;
        }
    }

    internal void AssertInvariants()
    {
        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        Require(_nodeRefs.Keys.ToHashSet().SetEquals(_nodes.Keys),
            "Node spatial-reference IDs do not match graph node IDs.");
        Require(_edgeRefs.Keys.ToHashSet().SetEquals(_edges.Keys),
            "Edge spatial-reference IDs do not match graph edge IDs.");

        var expectedSpatialCoverage = new Dictionary<ISpatialRef, Rect2>(
            ReferenceEqualityComparer.Instance);
        foreach (GraphNode node in _nodes.Values)
        {
            Require(node.IncidenceCount > 0, $"Node {node.ID} is isolated.");
            Require(_nodeRefs.TryGetValue(node.ID, out NodeSpatialRef? nodeRef),
                $"Node {node.ID} has no spatial reference.");
            Require(nodeRef!.NodeID == node.ID && nodeRef.Position == node.Position,
                $"Node {node.ID} has an inconsistent spatial reference.");
            Require(expectedSpatialCoverage.TryAdd(
                    nodeRef,
                    new Rect2(node.Position, Vector2.Zero)),
                $"Node {node.ID} reuses a spatial reference.");

            foreach (EdgeIncidence incidence in node.Incidences)
            {
                Require(_edges.TryGetValue(incidence.EdgeID, out GraphEdge? edge),
                    $"Node {node.ID} references missing edge {incidence.EdgeID}.");
                Require(_nodes.ContainsKey(incidence.NeighborNodeID),
                    $"Node {node.ID} references missing neighbor {incidence.NeighborNodeID}.");
                bool matchesEndpoint = incidence.Endpoint switch
                {
                    EdgeEndpoint.A =>
                        edge!.NodeA == node.ID && edge.NodeB == incidence.NeighborNodeID,
                    EdgeEndpoint.B =>
                        edge!.NodeB == node.ID && edge.NodeA == incidence.NeighborNodeID,
                    _ => false,
                };
                Require(matchesEndpoint,
                    $"Node {node.ID} has an inconsistent {incidence.Endpoint} incidence for edge {edge!.ID}.");
            }
        }

        foreach (GraphEdge edge in _edges.Values)
        {
            Require(RoadTypeContract.IsDefined(edge.RoadType),
                $"Edge {edge.ID} has an invalid road type.");
            Require(_nodes.TryGetValue(edge.NodeA, out GraphNode? nodeA),
                $"Edge {edge.ID} has missing endpoint {edge.NodeA}.");
            Require(_nodes.TryGetValue(edge.NodeB, out GraphNode? nodeB),
                $"Edge {edge.ID} has missing endpoint {edge.NodeB}.");
            Require(edge.NodeA == edge.NodeB || edge.NodeA < edge.NodeB,
                $"Non-loop edge {edge.ID} is not oriented by ascending endpoint ID.");
            Require(nodeA!.Incidences.Count(incidence =>
                incidence.EdgeID == edge.ID &&
                incidence.Endpoint == EdgeEndpoint.A &&
                incidence.NeighborNodeID == edge.NodeB) == 1,
                $"Edge {edge.ID} is not referenced exactly once as A by node {edge.NodeA}.");
            Require(nodeB!.Incidences.Count(incidence =>
                incidence.EdgeID == edge.ID &&
                incidence.Endpoint == EdgeEndpoint.B &&
                incidence.NeighborNodeID == edge.NodeA) == 1,
                $"Edge {edge.ID} is not referenced exactly once as B by node {edge.NodeB}.");
            if (edge.NodeA == edge.NodeB)
            {
                Require(nodeA.Incidences.Count(incidence => incidence.EdgeID == edge.ID) == 2,
                    $"Self-loop edge {edge.ID} must contribute exactly two incidences.");
                Require(RoadExactPredicates.SameBits(
                        edge.GeometrySegments[0].Start,
                        edge.GeometrySegments[^1].End),
                    $"Self-loop edge {edge.ID} must close exactly at its seam.");
                IReadOnlyList<RoadGeometrySegment> reversed =
                    RoadGeometryDirection.ReverseChain(edge.GeometrySegments);
                Require(RoadGeometryDirection.CompareCanonicalKeys(
                        edge.GeometrySegments,
                        reversed) <= 0,
                    $"Self-loop edge {edge.ID} does not use its canonical direction.");
            }
            Require(ArePositionsApproximatelyEqual(edge.GeometrySegments[0].Start, nodeA.Position),
                $"Edge {edge.ID} geometry does not start at node {edge.NodeA}.");
            Require(ArePositionsApproximatelyEqual(edge.GeometrySegments[^1].End, nodeB.Position),
                $"Edge {edge.ID} geometry does not end at node {edge.NodeB}.");

            Require(_edgeRefs.TryGetValue(edge.ID, out ImmutableArray<ISpatialRef> edgeRefs),
                $"Edge {edge.ID} has no spatial references.");
            Require(edgeRefs.Length >= edge.GeometrySegments.Count,
                $"Edge {edge.ID} has fewer query fragments than geometry segments.");
            foreach (IGrouping<int, EdgeGeometryRef> geometryRefs in edgeRefs
                         .Cast<EdgeGeometryRef>()
                         .GroupBy(reference => reference.GeometryIndex))
            {
                int geometryIndex = geometryRefs.Key;
                Require(geometryIndex >= 0 && geometryIndex < edge.GeometrySegments.Count,
                    $"Edge {edge.ID} has a query fragment with an invalid geometry index.");
                EdgeGeometryRef[] ordered = geometryRefs.OrderBy(reference => reference.FragmentIndex).ToArray();
                Require(ordered[0].ParameterStart == RoadGeometrySegment.ParameterStart &&
                        ordered[^1].ParameterEnd == RoadGeometrySegment.ParameterEnd,
                    $"Edge {edge.ID} geometry {geometryIndex} query fragments do not cover the parameter endpoints.");
                for (int fragmentIndex = 0; fragmentIndex < ordered.Length; fragmentIndex++)
                {
                    EdgeGeometryRef geometryRef = ordered[fragmentIndex];
                    Require(geometryRef.EdgeID == edge.ID &&
                            geometryRef.GeometryIndex == geometryIndex &&
                            geometryRef.FragmentIndex == fragmentIndex &&
                            ReferenceEquals(geometryRef.SourceGeometry, edge.GeometrySegments[geometryIndex]),
                        $"Edge {edge.ID} has an inconsistent query fragment.");
                    if (fragmentIndex > 0)
                    {
                        Require(ordered[fragmentIndex - 1].ParameterEnd == geometryRef.ParameterStart,
                            $"Edge {edge.ID} geometry {geometryIndex} query fragments have a parameter gap.");
                    }
                    Require(expectedSpatialCoverage.TryAdd(geometryRef, geometryRef.Bounds),
                        $"Edge {edge.ID} geometry {geometryIndex} fragment {fragmentIndex} reuses a spatial reference.");
                }
            }
            Require(edgeRefs.Cast<EdgeGeometryRef>().Select(reference => reference.GeometryIndex)
                    .Distinct().Count() == edge.GeometrySegments.Count,
                $"Edge {edge.ID} does not have query fragments for every geometry segment.");
        }

        foreach (GraphNode node in _nodes.Values)
        {
            if (node.IncidenceCount != 2 || node.IncidentEdgeCount == 1)
                continue;

            GraphEdge[] incidentEdges = node.Incidences
                .Select(incidence => _edges[incidence.EdgeID])
                .DistinctBy(edge => edge.ID)
                .ToArray();
            Require(incidentEdges.Length == 2 &&
                    incidentEdges[0].RoadType != incidentEdges[1].RoadType,
                $"Node {node.ID} is a non-canonical same-type degree-2 boundary.");
        }

        Require(_spatialIndex.HasExactCoverage(expectedSpatialCoverage),
            "Spatial index coverage does not exactly match registered references.");
        Require(_capacity.Validate(CaptureResourceCounts()) == RoadGraphCapacityError.None,
            "RoadGraph committed resources exceed the configured capacity.");
        double measuredLength = _edges.Values.Sum(edge => SumGeometryLength(edge));
        double lengthTolerance = Math.Max(1e-6d, measuredLength * 1e-12d);
        Require(Math.Abs(measuredLength - _totalGeometryLength) <= lengthTolerance,
            "RoadGraph total geometry length does not match its committed edges.");
    }
}
