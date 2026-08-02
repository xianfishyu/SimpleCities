using Godot;
using System;
using System.Collections.Generic;

public partial class RoadGraph
{
    private const float GeometryParameterTolerance = 1e-5f;

    internal bool SplitEdgeAtGeometryParameters(
        int edgeID,
        IEnumerable<EdgeGeometrySplitPoint> splitPoints)
    {
        ArgumentNullException.ThrowIfNull(splitPoints);
        if (!_edges.TryGetValue(edgeID, out GraphEdge? edge))
            return false;

        List<NormalizedEdgeSplitPoint> normalized = NormalizeEdgeSplitPoints(edge, splitPoints);
        if (normalized.Count == 0)
            return false;

        var replacementGeometry = new List<IReadOnlyList<RoadGeometrySegment>>(normalized.Count + 1);
        var currentGeometry = new List<RoadGeometrySegment>();
        int nextSplitIndex = 0;

        for (int segmentIndex = 0; segmentIndex < edge.GeometrySegments.Count; segmentIndex++)
        {
            RoadGeometrySegment source = edge.GeometrySegments[segmentIndex];
            var localParameters = new List<float>();
            foreach (NormalizedEdgeSplitPoint splitPoint in normalized)
            {
                if (splitPoint.EdgeParameter <= segmentIndex + GeometryParameterTolerance ||
                    splitPoint.EdgeParameter >= segmentIndex + 1f - GeometryParameterTolerance)
                    continue;
                localParameters.Add(splitPoint.EdgeParameter - segmentIndex);
            }

            IReadOnlyList<RoadGeometrySubsegment> subsegments =
                RoadGeometrySubdivision.SplitAtParameters(
                    source,
                    localParameters,
                    GeometryParameterTolerance);
            foreach (RoadGeometrySubsegment subsegment in subsegments)
            {
                currentGeometry.Add(subsegment.Geometry);
                float edgeParameter = segmentIndex + subsegment.ParameterEnd;
                if (nextSplitIndex >= normalized.Count ||
                    Mathf.Abs(normalized[nextSplitIndex].EdgeParameter - edgeParameter) >
                    GeometryParameterTolerance)
                    continue;

                replacementGeometry.Add(currentGeometry.ToArray());
                currentGeometry = new List<RoadGeometrySegment>();
                nextSplitIndex++;
            }
        }
        replacementGeometry.Add(currentGeometry.ToArray());

        if (replacementGeometry.Count != normalized.Count + 1 || currentGeometry.Count == 0)
            throw new InvalidOperationException("Edge subdivision did not produce the expected topology boundaries.");

        var nodes = new List<GraphNode>(normalized.Count + 2)
        {
            GetNode(edge.NodeA) ?? throw new InvalidOperationException("Edge start node is missing."),
        };
        foreach (NormalizedEdgeSplitPoint splitPoint in normalized)
            nodes.Add(GetOrCreateExactNode(splitPoint.Position));
        nodes.Add(GetNode(edge.NodeB) ?? throw new InvalidOperationException("Edge end node is missing."));

        for (int index = 0; index < nodes.Count - 1; index++)
        {
            if (nodes[index].ID == nodes[index + 1].ID)
                throw new InvalidOperationException("Edge subdivision collapsed adjacent topology nodes.");
        }

        int groupID = edge.GroupID;
        RoadType type = edge.Type;
        DetachEdgeForReplacement(edge);
        EdgeRemoved?.Invoke(edge);

        for (int index = 0; index < replacementGeometry.Count; index++)
        {
            if (AddEdge(nodes[index], nodes[index + 1], replacementGeometry[index], groupID, type) is null)
                throw new InvalidOperationException("Edge subdivision failed to create a replacement edge.");
        }
        return true;
    }

    private List<NormalizedEdgeSplitPoint> NormalizeEdgeSplitPoints(
        GraphEdge edge,
        IEnumerable<EdgeGeometrySplitPoint> splitPoints)
    {
        var candidates = new List<NormalizedEdgeSplitPoint>();
        foreach (EdgeGeometrySplitPoint splitPoint in splitPoints)
        {
            if (splitPoint.GeometrySegmentIndex < 0 ||
                splitPoint.GeometrySegmentIndex >= edge.GeometrySegments.Count)
                throw new ArgumentOutOfRangeException(nameof(splitPoints));
            if (!float.IsFinite(splitPoint.SegmentParameter) ||
                splitPoint.SegmentParameter < 0f || splitPoint.SegmentParameter > 1f)
                throw new ArgumentOutOfRangeException(nameof(splitPoints));

            float edgeParameter = splitPoint.GeometrySegmentIndex + splitPoint.SegmentParameter;
            if (edgeParameter <= GeometryParameterTolerance ||
                edgeParameter >= edge.GeometrySegments.Count - GeometryParameterTolerance)
                continue;

            RoadGeometrySegment segment = edge.GeometrySegments[splitPoint.GeometrySegmentIndex];
            Vector2 position = segment.GetPosition(splitPoint.SegmentParameter);
            if (ArePositionsApproximatelyEqual(position, edge.GeometrySegments[0].Start) ||
                ArePositionsApproximatelyEqual(position, edge.GeometrySegments[^1].End))
                continue;
            candidates.Add(new NormalizedEdgeSplitPoint(
                edgeParameter,
                position));
        }

        candidates.Sort((left, right) => left.EdgeParameter.CompareTo(right.EdgeParameter));
        var normalized = new List<NormalizedEdgeSplitPoint>(candidates.Count);
        foreach (NormalizedEdgeSplitPoint candidate in candidates)
        {
            if (normalized.Count > 0 &&
                (candidate.EdgeParameter - normalized[^1].EdgeParameter <= GeometryParameterTolerance ||
                 ArePositionsApproximatelyEqual(candidate.Position, normalized[^1].Position)))
                continue;
            normalized.Add(candidate);
        }
        return normalized;
    }

    private GraphNode GetOrCreateExactNode(Vector2 position)
    {
        GraphNode? existing = FindClosestIndexedNode(position, Mathf.Sqrt(GeometryEpsilon));
        if (existing is not null)
            return existing;

        var node = new GraphNode(NextID(), position);
        _nodes.Add(node.ID, node);
        InsertNodeSpatialRef(node);
        return node;
    }

    private void DetachEdgeForReplacement(GraphEdge edge)
    {
        _edges.Remove(edge.ID);
        RemoveEdgeSpatialRefs(edge.ID);
        GetNode(edge.NodeA)?.RemoveEdge(edge.ID);
        GetNode(edge.NodeB)?.RemoveEdge(edge.ID);
        if (_groups.TryGetValue(edge.GroupID, out RoadGroup? group))
        {
            group.RemoveEdge(edge.ID);
            if (group.IsEmpty)
                _groups.Remove(group.ID);
        }
    }

    private readonly record struct NormalizedEdgeSplitPoint(
        float EdgeParameter,
        Vector2 Position);
}
