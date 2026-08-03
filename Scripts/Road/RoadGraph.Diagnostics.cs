using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

        var registeredSpatialRefs = new HashSet<ISpatialRef>();
        foreach (GraphNode node in _nodes.Values)
        {
            Require(node.EdgeCount > 0, $"Node {node.ID} is isolated.");
            Require(_nodeRefs.TryGetValue(node.ID, out NodeSpatialRef? nodeRef),
                $"Node {node.ID} has no spatial reference.");
            Require(nodeRef!.NodeID == node.ID && nodeRef.Position == node.Position,
                $"Node {node.ID} has an inconsistent spatial reference.");
            Require(_spatialIndex.HasExactCoverage(nodeRef, new Rect2(node.Position, Vector2.Zero)),
                $"Node {node.ID} does not occupy exactly its expected spatial bucket.");
            registeredSpatialRefs.Add(nodeRef);

            foreach (EdgeRef edgeRef in node.Edges)
            {
                Require(_edges.TryGetValue(edgeRef.EdgeID, out GraphEdge? edge),
                    $"Node {node.ID} references missing edge {edgeRef.EdgeID}.");
                Require(_nodes.ContainsKey(edgeRef.NeighborNodeID),
                    $"Node {node.ID} references missing neighbor {edgeRef.NeighborNodeID}.");
                Require(
                    edge!.NodeA == node.ID && edge.NodeB == edgeRef.NeighborNodeID ||
                    edge.NodeB == node.ID && edge.NodeA == edgeRef.NeighborNodeID,
                    $"Node {node.ID} has an inconsistent reference to edge {edge.ID}.");
            }
        }

        foreach (GraphEdge edge in _edges.Values)
        {
            Require(_nodes.TryGetValue(edge.NodeA, out GraphNode? nodeA),
                $"Edge {edge.ID} has missing endpoint {edge.NodeA}.");
            Require(_nodes.TryGetValue(edge.NodeB, out GraphNode? nodeB),
                $"Edge {edge.ID} has missing endpoint {edge.NodeB}.");
            Require(nodeA!.Edges.Count(edgeRef =>
                edgeRef.EdgeID == edge.ID && edgeRef.NeighborNodeID == edge.NodeB) == 1,
                $"Edge {edge.ID} is not referenced exactly once by node {edge.NodeA}.");
            Require(nodeB!.Edges.Count(edgeRef =>
                edgeRef.EdgeID == edge.ID && edgeRef.NeighborNodeID == edge.NodeA) == 1,
                $"Edge {edge.ID} is not referenced exactly once by node {edge.NodeB}.");
            Require(_groups.TryGetValue(edge.GroupID, out RoadGroup? group),
                $"Edge {edge.ID} references missing group {edge.GroupID}.");
            Require(group!.EdgeIDs.Contains(edge.ID),
                $"Group {group.ID} does not reference edge {edge.ID}.");
            Require(ArePositionsApproximatelyEqual(edge.GeometrySegments[0].Start, nodeA.Position),
                $"Edge {edge.ID} geometry does not start at node {edge.NodeA}.");
            Require(ArePositionsApproximatelyEqual(edge.GeometrySegments[^1].End, nodeB.Position),
                $"Edge {edge.ID} geometry does not end at node {edge.NodeB}.");

            Require(_edgeRefs.TryGetValue(edge.ID, out List<ISpatialRef>? edgeRefs),
                $"Edge {edge.ID} has no spatial references.");
            Require(edgeRefs!.Count == edge.GeometrySegments.Count,
                $"Edge {edge.ID} spatial-reference count does not match its geometry.");
            for (int index = 0; index < edge.GeometrySegments.Count; index++)
            {
                EdgeGeometryRef? geometryRef = edgeRefs[index] as EdgeGeometryRef;
                Require(geometryRef is not null,
                    $"Edge {edge.ID} has a non-geometry spatial reference.");
                Require(geometryRef!.EdgeID == edge.ID &&
                        ReferenceEquals(geometryRef.Geometry, edge.GeometrySegments[index]),
                    $"Edge {edge.ID} has an inconsistent geometry spatial reference.");
                Require(_spatialIndex.HasExactCoverage(geometryRef, geometryRef.Bounds),
                    $"Edge {edge.ID} geometry {index} does not occupy exactly its expected buckets.");
                registeredSpatialRefs.Add(geometryRef);
            }
        }

        foreach (RoadGroup group in _groups.Values)
        {
            Require(!group.IsEmpty, $"Group {group.ID} is empty.");
            foreach (int edgeID in group.EdgeIDs)
            {
                Require(_edges.TryGetValue(edgeID, out GraphEdge? edge),
                    $"Group {group.ID} references missing edge {edgeID}.");
                Require(edge!.GroupID == group.ID,
                    $"Group {group.ID} contains edge {edgeID} owned by group {edge.GroupID}.");
            }
        }

        Require(registeredSpatialRefs.SetEquals(_spatialIndex.CaptureDistinctReferences()),
            "Spatial index contains missing or unregistered references.");
    }
}
