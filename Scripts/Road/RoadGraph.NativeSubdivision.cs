using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RoadGraph
{
    private const float GeometryParameterTolerance = 1e-5f;

    internal bool SplitEdgeAtGeometryParameters(
        int edgeID,
        IEnumerable<EdgeGeometrySplitPoint> splitPoints,
        bool canonicalize = true)
    {
        return ExecuteBooleanMutation(() => SplitEdgeAtGeometryParametersCore(
                edgeID,
                splitPoints,
                canonicalize,
                admissionAlreadyValidated: false));
    }

    private bool ApplyAdmittedEdgeSubdivision(
        int edgeID,
        IEnumerable<EdgeGeometrySplitPoint> splitPoints)
    {
        return SplitEdgeAtGeometryParametersCore(
            edgeID,
            splitPoints,
            canonicalize: false,
            admissionAlreadyValidated: true);
    }

    private bool SplitEdgeAtGeometryParametersCore(
        int edgeID,
        IEnumerable<EdgeGeometrySplitPoint> splitPoints,
        bool canonicalize,
        bool admissionAlreadyValidated)
    {
        ArgumentNullException.ThrowIfNull(splitPoints);
        if (!_edges.TryGetValue(edgeID, out GraphEdge? edge))
            return false;

        List<NormalizedEdgeSplitPoint> normalized = NormalizeEdgeSplitPoints(edge, splitPoints);
        if (normalized.Count == 0)
            return false;

        IReadOnlyList<IReadOnlyList<RoadGeometrySegment>> replacementGeometry =
            BuildSubdivisionGeometry(edge, normalized);

        if (!admissionAlreadyValidated &&
            !AdmitEdgeSubdivision(edge, replacementGeometry, normalized.Count))
            return false;

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

        DetachEdge(edge);
        var replacementEdges = new List<GraphEdge>(replacementGeometry.Count);

        for (int index = 0; index < replacementGeometry.Count; index++)
        {
            GraphEdge? replacement = AddEdge(
                nodes[index],
                nodes[index + 1],
                replacementGeometry[index],
                edge.RoadType,
                edgeID: index == 0 ? edge.ID : null,
                emitEvent: false);
            if (replacement is null)
                throw new InvalidOperationException("Edge subdivision failed to create a replacement edge.");
            replacementEdges.Add(replacement);
        }

        if (canonicalize)
            FinalizeMutation(nodes.Select(node => node.ID));
        return true;
    }

    private static IReadOnlyList<IReadOnlyList<RoadGeometrySegment>> BuildSubdivisionGeometry(
        GraphEdge edge,
        IReadOnlyList<NormalizedEdgeSplitPoint> normalized)
    {
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

        var anchoredGeometry = new IReadOnlyList<RoadGeometrySegment>[replacementGeometry.Count];
        for (int index = 0; index < replacementGeometry.Count; index++)
        {
            Vector2 start = index == 0
                ? edge.GeometrySegments[0].Start
                : normalized[index - 1].Position;
            Vector2 end = index == normalized.Count
                ? edge.GeometrySegments[^1].End
                : normalized[index].Position;
            anchoredGeometry[index] = RoadGeometryCanonicalizer.ReanchorChain(
                replacementGeometry[index],
                start,
                end);
        }
        return anchoredGeometry;
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
            Vector2 nativePosition = segment.GetPosition(splitPoint.SegmentParameter);
            Vector2 position = splitPoint.CanonicalPosition ?? nativePosition;
            if (!RoadNumericPolicy.IsWithinCoordinateRange(position) ||
                RoadNumericPolicy.DistanceSquared(position, nativePosition) >
                (double)RoadNumericPolicy.MaximumIntersectionClusterDiameter *
                RoadNumericPolicy.MaximumIntersectionClusterDiameter)
            {
                throw new InvalidOperationException(
                    "An edge split position must remain within the admitted intersection cluster.");
            }
            if (ArePositionsApproximatelyEqual(position, edge.GeometrySegments[0].Start) ||
                ArePositionsApproximatelyEqual(position, edge.GeometrySegments[^1].End))
                continue;
            candidates.Add(new NormalizedEdgeSplitPoint(
                edgeParameter,
                position,
                splitPoint.CanonicalPosition.HasValue));
        }

        candidates.Sort((left, right) =>
        {
            int parameterOrder = left.EdgeParameter.CompareTo(right.EdgeParameter);
            if (parameterOrder != 0)
                return parameterOrder;
            int canonicalOrder = right.HasCanonicalPosition.CompareTo(left.HasCanonicalPosition);
            if (canonicalOrder != 0)
                return canonicalOrder;
            int xOrder = left.Position.X.CompareTo(right.Position.X);
            return xOrder != 0 ? xOrder : left.Position.Y.CompareTo(right.Position.Y);
        });
        var normalized = new List<NormalizedEdgeSplitPoint>(candidates.Count);
        foreach (NormalizedEdgeSplitPoint candidate in candidates)
        {
            if (normalized.Count > 0)
            {
                NormalizedEdgeSplitPoint previous = normalized[^1];
                if (candidate.EdgeParameter - previous.EdgeParameter <= GeometryParameterTolerance)
                {
                    if (candidate.HasCanonicalPosition && previous.HasCanonicalPosition &&
                        !IsWithinIntersectionCluster(candidate.Position, previous.Position))
                    {
                        throw new InvalidOperationException(
                            "Equivalent edge split parameters have conflicting canonical positions.");
                    }
                    if (candidate.HasCanonicalPosition && !previous.HasCanonicalPosition)
                        normalized[^1] = candidate;
                    continue;
                }
                if (ArePositionsApproximatelyEqual(candidate.Position, previous.Position))
                    continue;
            }
            normalized.Add(candidate);
        }
        return normalized;
    }

    private GraphNode GetOrCreateExactNode(Vector2 position)
    {
        int existingNodeID = -1;
        foreach (ISpatialRef hit in _spatialIndex.QueryRadius(position, 0f))
        {
            if (hit is not NodeSpatialRef nodeRef ||
                !RoadExactPredicates.SameBits(nodeRef.Position, position))
            {
                continue;
            }
            if (existingNodeID < 0 || nodeRef.NodeID < existingNodeID)
                existingNodeID = nodeRef.NodeID;
        }
        if (existingNodeID >= 0)
            return GetNode(existingNodeID) ??
                throw new InvalidOperationException("An indexed exact node is missing from the graph.");

        var node = new GraphNode(NextID(), position);
        TrackNodeChange(node.ID);
        _nodes.Add(node.ID, node);
        InsertNodeSpatialRef(node);
        return node;
    }

    private void DetachEdge(GraphEdge edge)
    {
        if (!_edges.Remove(edge.ID))
            return;
        TrackEdgeChange(edge.ID);
        if (edge.NodeA == edge.NodeB)
            _selfLoopCount--;
        _geometrySegmentCount -= edge.GeometrySegments.Count;
        AdjustTotalGeometryLength(-SumGeometryLength(edge));
        RemoveEdgeSpatialRefs(edge.ID);
        RemoveNodeIncidence(edge.NodeA, edge.ID, EdgeEndpoint.A);
        RemoveNodeIncidence(edge.NodeB, edge.ID, EdgeEndpoint.B);
    }

    private void RemoveNodeIncidence(
        int nodeID,
        int edgeID,
        EdgeEndpoint endpoint)
    {
        if (!_nodes.TryGetValue(nodeID, out GraphNode? node))
            return;

        GraphNode updated = node.WithRemovedIncidence(edgeID, endpoint, out bool removed);
        if (removed)
        {
            TrackNodeChange(nodeID);
            _nodes[nodeID] = updated;
        }
    }

    private readonly record struct NormalizedEdgeSplitPoint(
        float EdgeParameter,
        Vector2 Position,
        bool HasCanonicalPosition);
}
