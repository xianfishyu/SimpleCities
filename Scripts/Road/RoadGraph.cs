using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RoadGraph : IPreparedSaveable
{
    private const float SnapRadius = 0.5f;
    private const float GeometryEpsilon = 1e-4f;
    private const float IndexBucketSize = 64f;

    private readonly Dictionary<int, GraphNode> _nodes = new();
    private readonly Dictionary<int, GraphEdge> _edges = new();
    private readonly Dictionary<int, RoadGroup> _groups = new();
    private readonly UniformGrid _spatialIndex;
    private readonly Dictionary<int, NodeSpatialRef> _nodeRefs = new();
    private readonly Dictionary<int, List<ISpatialRef>> _edgeRefs = new();

    private int _nextID;

    public string SaveFileName => "road_network";

    public event Action<GraphEdge>? EdgeAdded;
    public event Action<GraphEdge>? EdgeRemoved;
    public event Action? GraphCleared;

    public RoadGraph() : this(IndexBucketSize) { }

    public RoadGraph(float bucketSize)
    {
        _spatialIndex = new UniformGrid(bucketSize);
    }

    private int NextID() => _nextID++;

    public int AddRoad(Vector2 start, Vector2 end, Vector2[] waypoints)
    {
        var path = new List<Vector2>(waypoints.Length + 2) { start };
        path.AddRange(waypoints);
        path.Add(end);

        return SubmitPolyline(path).GroupID ?? -1;
    }

    public RoadPathSubmissionResult SubmitPolyline(IReadOnlyList<Vector2>? points)
    {
        BeginMeasuredOperation();
        var validationError = ValidatePolyline(points);
        if (validationError != RoadPathSubmissionError.None)
            return RoadPathSubmissionResult.Rejected(validationError);

        var path = points!.ToList();

        // Coverage check must run BEFORE any mutating step (ResolveIntersections,
        // SplitEdgesAtPathAnchors) — otherwise a fully-covered AddRoad still splits
        // existing edges at the incoming path's anchors and then returns -1, leaving
        // the graph churned. Coverage of the polyline in R² does not depend on how
        // the path is later subdivided by anchors.
        if (IsPathFullyCovered(path))
            return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.FullyCovered);

        EntitySnapshot entitiesBefore = CaptureEntitySnapshot();
        path = ResolveIntersections(path);
        SplitEdgesAtPathAnchors(path);
        path = InsertExistingNodeAnchors(path);

        if (IsPathFullyCovered(path))
            return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.FullyCovered);

        var group = new RoadGroup(NextID());
        _groups[group.ID] = group;

        bool anyAdded = false;
        var touchedNodeIDs = new HashSet<int>();

        for (int i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            if (a.DistanceSquaredTo(b) < GeometryEpsilon) continue;
            if (IsPathCovered(a, b)) continue;

            var nodeA = GetOrCreateNode(a);
            var nodeB = GetOrCreateNode(b);
            if (nodeA.ID == nodeB.ID) continue;

            if (AddEdge(nodeA, nodeB, Array.Empty<Vector2>(), group.ID) != null)
            {
                anyAdded = true;
                touchedNodeIDs.Add(nodeA.ID);
                touchedNodeIDs.Add(nodeB.ID);
            }
        }

        if (!anyAdded)
        {
            _groups.Remove(group.ID);
            return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.NoChanges);
        }

        foreach (int nodeID in touchedNodeIDs.ToList())
            TryMergeAtNode(nodeID);

        if (_groups.TryGetValue(group.ID, out var maybeEmpty) && maybeEmpty.IsEmpty)
            _groups.Remove(group.ID);

        return RoadPathSubmissionResult.Succeeded(group.ID, DescribeChanges(entitiesBefore));
    }

    private EntitySnapshot CaptureEntitySnapshot() => new(
        [.. _nodes.Keys],
        [.. _edges.Keys],
        [.. _groups.Keys]);

    private RoadGraphChangeSummary DescribeChanges(EntitySnapshot before) => new(
        _nodes.Keys.Except(before.NodeIDs),
        _edges.Keys.Except(before.EdgeIDs),
        _groups.Keys.Except(before.GroupIDs),
        before.NodeIDs.Except(_nodes.Keys),
        before.EdgeIDs.Except(_edges.Keys),
        before.GroupIDs.Except(_groups.Keys));

    private readonly record struct EntitySnapshot(
        HashSet<int> NodeIDs,
        HashSet<int> EdgeIDs,
        HashSet<int> GroupIDs);

    public bool RemoveEdge(int edgeID)
    {
        BeginMeasuredOperation();
        if (!_edges.TryGetValue(edgeID, out GraphEdge? edge)) return false;

        DetachEdge(edge);
        CommitEdgeMutation([edge.NodeA, edge.NodeB], [edge.GroupID]);
        EdgeRemoved?.Invoke(edge);
        return true;
    }

    public bool RemoveRoadGroup(int groupID)
    {
        BeginMeasuredOperation();
        if (!_groups.TryGetValue(groupID, out var group)) return false;

        var removedEdges = new List<GraphEdge>();
        var affectedNodeIDs = new HashSet<int>();
        foreach (int edgeID in group.EdgeIDs.Order())
        {
            if (!_edges.TryGetValue(edgeID, out GraphEdge? edge)) continue;
            removedEdges.Add(edge);
            affectedNodeIDs.Add(edge.NodeA);
            affectedNodeIDs.Add(edge.NodeB);
            DetachEdge(edge);
        }

        CommitEdgeMutation(affectedNodeIDs, [groupID]);
        foreach (GraphEdge edge in removedEdges)
            EdgeRemoved?.Invoke(edge);

        return true;
    }

    public GraphEdge? GetEdge(int edgeID) => _edges.GetValueOrDefault(edgeID);
    public GraphNode? GetNode(int nodeID) => _nodes.GetValueOrDefault(nodeID);
    public RoadGroup? GetGroup(int groupID) => _groups.GetValueOrDefault(groupID);

    public GraphEdge? FindClosestEdge(Vector2 position, float maxRadius)
    {
        BeginMeasuredOperation();
        int bestEdgeID = -1;
        float bestDistSq = maxRadius * maxRadius;
        var candidateEdgeIDs = new HashSet<int>();

        foreach (var hit in _spatialIndex.QueryRadius(position, maxRadius))
        {
            if (TryGetEdgeID(hit, out int edgeID))
                candidateEdgeIDs.Add(edgeID);
        }
        RecordSpatialCandidates(candidateEdgeIDs.Count);

        foreach (int edgeID in candidateEdgeIDs)
        {
            var edge = GetEdge(edgeID);
            if (edge == null) continue;
            float d2 = edge.GeometrySegments
                .Min(segment => segment.FindClosestPoint(position).DistanceSquared);
            bool sameDistance = Mathf.IsEqualApprox(d2, bestDistSq);
            if (d2 < bestDistSq ||
                (sameDistance && (bestEdgeID < 0 || edgeID < bestEdgeID)))
            {
                bestDistSq = d2;
                bestEdgeID = edgeID;
            }
        }

        return bestEdgeID >= 0 ? GetEdge(bestEdgeID) : null;
    }

    public GraphNode? FindClosestNode(Vector2 position, float maxRadius)
    {
        return FindClosestIndexedNode(position, maxRadius);
    }

    private GraphNode? FindClosestIndexedNode(Vector2 position, float maxRadius)
    {
        int bestNodeID = -1;
        float bestDistSq = maxRadius * maxRadius;

        foreach (var hit in _spatialIndex.QueryRadius(position, maxRadius))
        {
            if (hit.Kind != SpatialRefKind.Node) continue;
            int nodeID = ((NodeSpatialRef)hit).NodeID;
            var node = GetNode(nodeID);
            if (node == null) continue;

            float d2 = node.Position.DistanceSquaredTo(position);
            bool sameDistance = Mathf.IsEqualApprox(d2, bestDistSq);
            if (bestNodeID >= 0 && !sameDistance && d2 > bestDistSq) continue;
            if (bestNodeID >= 0 && sameDistance && nodeID > bestNodeID) continue;

            bestDistSq = d2;
            bestNodeID = nodeID;
        }

        return bestNodeID >= 0 ? GetNode(bestNodeID) : null;
    }

    public IEnumerable<GraphEdge> GetAllEdges() => _edges.Values.ToArray();
    public IEnumerable<GraphNode> GetAllNodes() => _nodes.Values.ToArray();
    public IEnumerable<RoadGroup> GetAllGroups() => _groups.Values.ToArray();

    private GraphEdge? AddEdge(
        GraphNode nodeA,
        GraphNode nodeB,
        Vector2[] points,
        int groupID,
        bool emitEvent = true)
    {
        var geometrySegments = CreatePolylineGeometry(nodeA.Position, nodeB.Position, points);
        return AddEdge(nodeA, nodeB, geometrySegments, groupID, emitEvent);
    }

    private void SplitEdgeAtPosition(int edgeID, Vector2 splitPos)
    {
        if (!_edges.TryGetValue(edgeID, out var edge)) return;
        for (int segmentIndex = 0; segmentIndex < edge.GeometrySegments.Count; segmentIndex++)
        {
            RoadGeometrySegment geometry = edge.GeometrySegments[segmentIndex];
            if (!geometry.TryFindPointOnGeometry(
                    splitPos,
                    out RoadGeometryPointHit hit,
                    Mathf.Sqrt(GeometryEpsilon),
                    GeometryParameterTolerance))
                continue;

            SplitEdgeAtGeometryParameters(
                edgeID,
                [new EdgeGeometrySplitPoint(segmentIndex, hit.Parameter)]);
            return;
        }
    }

    private GraphNode GetOrCreateNode(Vector2 pos)
    {
        var existing = FindClosestIndexedNode(pos, SnapRadius);
        if (existing != null) return existing;

        var node = new GraphNode(NextID(), pos);
        _nodes[node.ID] = node;
        InsertNodeSpatialRef(node);
        return node;
    }

    private List<Vector2> ResolveIntersections(List<Vector2> path)
    {
        var collected = new List<(int pathSegIndex, float t, Vector2 pos)>();
        var candidateEdges = new HashSet<int>();

        for (int i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            QueryCandidateEdgeIDs(a, b, candidateEdges);

            foreach (int edgeID in candidateEdges.ToList())
            {
                if (!_edges.TryGetValue(edgeID, out var edge)) continue;
                var existing = edge.GetFullPath(GetNode);
                for (int j = 0; j < existing.Length - 1; j++)
                {
                    if (TryComputeInteriorCross(a, b, existing[j], existing[j + 1], out var cross, out float t))
                        collected.Add((i, t, cross));
                }

                // Also detect existing edge waypoints that lie on the new path segment.
                // These sit at sub-segment boundaries, so TryComputeInteriorCross misses them.
                for (int j = 1; j < existing.Length - 1; j++)
                {
                    var wp = existing[j];
                    if (!PointOnSegmentInterior(a, b, wp)) continue;
                    float tWp = ProjectParam(a, b, wp);
                    collected.Add((i, tWp, wp));
                }
            }

            candidateEdges.Clear();
        }

        if (collected.Count == 0) return path;

        var uniqueCrossings = DeduplicatePoints(collected.Select(c => c.pos));
        foreach (var cross in uniqueCrossings)
        {
            GetOrCreateNode(cross);
            foreach (int edgeID in FindEdgesContainingInteriorPoint(cross).ToList())
                SplitEdgeAtPosition(edgeID, cross);
        }

        return InsertCollectedPoints(path, collected);
    }

    private List<Vector2> InsertExistingNodeAnchors(List<Vector2> path)
    {
        var insertsBySegment = new Dictionary<int, List<(float t, Vector2 pos)>>();

        for (int i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            foreach (var hit in _spatialIndex.QueryBounds(CreateQueryBounds(a, b, SnapRadius)))
            {
                if (hit.Kind != SpatialRefKind.Node) continue;
                if (!PointOnSegmentInteriorOrEndpoint(a, b, hit.Position)) continue;
                float t = ProjectParam(a, b, hit.Position);
                if (t <= GeometryEpsilon || t >= 1f - GeometryEpsilon) continue;
                AddInsert(insertsBySegment, i, t, hit.Position);
            }
        }

        if (insertsBySegment.Count == 0) return path;

        var rebuilt = new List<Vector2>();
        for (int i = 0; i < path.Count - 1; i++)
        {
            rebuilt.Add(path[i]);
            if (!insertsBySegment.TryGetValue(i, out var inserts)) continue;
            inserts.Sort((a, b) => a.t.CompareTo(b.t));
            foreach (var insert in inserts)
                rebuilt.Add(insert.pos);
        }
        rebuilt.Add(path[^1]);
        return rebuilt;
    }

    private void SplitEdgesAtPathAnchors(IEnumerable<Vector2> path)
    {
        foreach (var point in path)
        {
            var edgeIDs = FindEdgesContainingInteriorPoint(point).ToList();

            // Also find edges whose interior waypoints coincide with this point.
            // FindEdgesContainingInteriorPoint uses PointOnSegmentInterior which
            // excludes sub-segment endpoints, but a point matching an edge waypoint
            // still needs a split.
            if (edgeIDs.Count == 0)
                edgeIDs = FindEdgesWithWaypointAt(point).ToList();

            if (edgeIDs.Count == 0) continue;

            GetOrCreateNode(point);
            foreach (int edgeID in edgeIDs)
                SplitEdgeAtPosition(edgeID, point);
        }
    }

    private IEnumerable<int> FindEdgesWithWaypointAt(Vector2 pos)
    {
        foreach (int edgeID in FindCandidateEdgeIDs(CreateQueryBounds(pos, pos, Mathf.Sqrt(GeometryEpsilon))))
        {
            if (!_edges.TryGetValue(edgeID, out GraphEdge? edge)) continue;
            foreach (var wp in edge.InternalPoints)
            {
                if (wp.DistanceSquaredTo(pos) < GeometryEpsilon)
                {
                    yield return edge.ID;
                    break;
                }
            }
        }
    }

    private RoadPathSubmissionError ValidatePolyline(IReadOnlyList<Vector2>? path)
    {
        if (path == null || path.Count < 2)
            return RoadPathSubmissionError.TooFewPoints;

        foreach (var point in path)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                return RoadPathSubmissionError.NonFiniteCoordinate;
        }

        for (int i = 0; i < path.Count - 1; i++)
        {
            if (ArePositionsApproximatelyEqual(path[i], path[i + 1]))
                return RoadPathSubmissionError.DegenerateSegment;

            if (path[i].DistanceSquaredTo(path[i + 1]) <= SnapRadius * SnapRadius)
                return RoadPathSubmissionError.CollapsedByNodeIdentity;

            var nodeA = FindClosestIndexedNode(path[i], SnapRadius);
            var nodeB = FindClosestIndexedNode(path[i + 1], SnapRadius);
            if (nodeA != null && nodeA.ID == nodeB?.ID)
                return RoadPathSubmissionError.CollapsedByNodeIdentity;
        }

        for (int i = 0; i < path.Count; i++)
        for (int j = i + 2; j < path.Count; j++)
        {
            if (ArePositionsApproximatelyEqual(path[i], path[j]))
                return RoadPathSubmissionError.RepeatedPoint;
        }

        for (int i = 0; i < path.Count - 2; i++)
        {
            if (PointOnSegmentInterior(path[i], path[i + 1], path[i + 2]) ||
                PointOnSegmentInterior(path[i + 1], path[i + 2], path[i]))
                return RoadPathSubmissionError.SelfIntersection;
        }

        for (int i = 0; i < path.Count - 1; i++)
        for (int j = i + 2; j < path.Count - 1; j++)
        {
            var a = path[i];
            var b = path[i + 1];
            var c = path[j];
            var d = path[j + 1];
            if (TryComputeInteriorCross(a, b, c, d, out _, out _) ||
                PointOnSegmentInteriorOrEndpoint(a, b, c) ||
                PointOnSegmentInteriorOrEndpoint(a, b, d) ||
                PointOnSegmentInteriorOrEndpoint(c, d, a) ||
                PointOnSegmentInteriorOrEndpoint(c, d, b))
                return RoadPathSubmissionError.SelfIntersection;
        }

        return RoadPathSubmissionError.None;
    }

    private bool IsPathFullyCovered(IReadOnlyList<Vector2> path)
    {
        if (path.Count < 2) return false;
        for (int i = 0; i < path.Count - 1; i++)
            if (!IsPathCovered(path[i], path[i + 1]))
                return false;
        return true;
    }

    private bool IsPathCovered(Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        if (d.LengthSquared() < GeometryEpsilon) return true;

        var intervals = new List<(float lo, float hi)>();
        HashSet<int> candidateEdgeIDs = FindCandidateEdgeIDs(
            CreateQueryBounds(a, b, Mathf.Sqrt(GeometryEpsilon)));
        foreach (var (q1, q2) in CollectExistingSubSegments(candidateEdgeIDs))
        {
            if (!IsPointOnInfiniteLine(a, b, q1)) continue;
            if (!IsPointOnInfiniteLine(a, b, q2)) continue;

            float t1 = ProjectParam(a, b, q1);
            float t2 = ProjectParam(a, b, q2);
            float lo = Mathf.Max(Mathf.Min(t1, t2), 0f);
            float hi = Mathf.Min(Mathf.Max(t1, t2), 1f);
            if (hi - lo > GeometryEpsilon) intervals.Add((lo, hi));
        }

        if (intervals.Count == 0) return false;

        intervals.Sort((x, y) => x.lo.CompareTo(y.lo));
        float coveredUntil = 0f;
        foreach (var (lo, hi) in intervals)
        {
            if (lo > coveredUntil + GeometryEpsilon) return false;
            if (hi > coveredUntil) coveredUntil = hi;
        }
        return coveredUntil >= 1f - GeometryEpsilon;
    }

    private IEnumerable<(Vector2 a, Vector2 b)> CollectExistingSubSegments(IEnumerable<int> edgeIDs)
    {
        foreach (int edgeID in edgeIDs)
        {
            if (!_edges.TryGetValue(edgeID, out GraphEdge? edge)) continue;
            var path = edge.GetFullPath(GetNode);
            for (int i = 0; i < path.Length - 1; i++)
                yield return (path[i], path[i + 1]);
        }
    }

    private void QueryCandidateEdgeIDs(Vector2 a, Vector2 b, HashSet<int> result)
    {
        foreach (int edgeID in FindCandidateEdgeIDs(
                     CreateQueryBounds(a, b, Mathf.Sqrt(GeometryEpsilon))))
            result.Add(edgeID);
    }

    private HashSet<int> FindCandidateEdgeIDs(Rect2 bounds)
    {
        var result = new HashSet<int>();
        foreach (ISpatialRef hit in _spatialIndex.QueryBounds(bounds))
            if (TryGetEdgeID(hit, out int edgeID))
                result.Add(edgeID);
        RecordSpatialCandidates(result.Count);
        return result;
    }

    private HashSet<int> FindCandidateEdgeIDs(RoadGeometrySegment geometry) =>
        FindCandidateEdgeIDs(CreateQueryBounds(
            geometry.Bounds.Position,
            geometry.Bounds.End,
            Mathf.Sqrt(GeometryEpsilon)));

    private static Rect2 CreateQueryBounds(Vector2 first, Vector2 second, float padding)
    {
        var minimum = new Vector2(
            Mathf.Min(first.X, second.X) - padding,
            Mathf.Min(first.Y, second.Y) - padding);
        var maximum = new Vector2(
            Mathf.Max(first.X, second.X) + padding,
            Mathf.Max(first.Y, second.Y) + padding);
        return new Rect2(minimum, maximum - minimum);
    }

    private static bool TryGetEdgeID(ISpatialRef spatialRef, out int edgeID)
    {
        switch (spatialRef)
        {
            case EdgePointRef pointRef:
                edgeID = pointRef.EdgeID;
                return true;
            case EdgeSegmentRef segmentRef:
                edgeID = segmentRef.EdgeID;
                return true;
            case EdgeGeometryRef geometryRef:
                edgeID = geometryRef.EdgeID;
                return true;
            default:
                edgeID = -1;
                return false;
        }
    }

    private static float DistanceSquaredToPath(IReadOnlyList<Vector2> path, Vector2 position)
    {
        float bestDistanceSquared = float.MaxValue;
        for (int i = 0; i < path.Count - 1; i++)
            bestDistanceSquared = Mathf.Min(bestDistanceSquared, DistanceSquaredToSegment(path[i], path[i + 1], position));
        return bestDistanceSquared;
    }

    private static float DistanceSquaredToSegment(Vector2 start, Vector2 end, Vector2 point)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared < GeometryEpsilon)
            return start.DistanceSquaredTo(point);

        float t = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        return (start + segment * t).DistanceSquaredTo(point);
    }

    private static bool AreOppositeCollinear(Vector2 node, Vector2 pointA, Vector2 pointB)
    {
        Vector2 toA = pointA - node;
        Vector2 toB = pointB - node;
        float lengthProduct = Mathf.Sqrt(toA.LengthSquared() * toB.LengthSquared());
        if (lengthProduct < GeometryEpsilon) return false;

        float cross = toA.X * toB.Y - toA.Y * toB.X;
        return Mathf.Abs(cross) <= GeometryEpsilon * lengthProduct && toA.Dot(toB) < 0f;
    }

    private IEnumerable<int> FindEdgesContainingInteriorPoint(Vector2 pos)
    {
        foreach (int edgeID in FindCandidateEdgeIDs(CreateQueryBounds(
                     pos,
                     pos,
                     Mathf.Sqrt(GeometryEpsilon))))
        {
            if (!_edges.TryGetValue(edgeID, out GraphEdge? edge)) continue;
            var path = edge.GetFullPath(GetNode);
            if (FindSubSegmentContaining(path, pos) >= 0)
                yield return edge.ID;
        }
    }

    private int FindSubSegmentContaining(IReadOnlyList<Vector2> path, Vector2 pos)
    {
        for (int i = 0; i < path.Count - 1; i++)
            if (PointOnSegmentInterior(path[i], path[i + 1], pos))
                return i;
        return -1;
    }

    private bool TryMergeAtNode(int nodeID)
    {
        if (!_nodes.TryGetValue(nodeID, out var node)) return false;
        if (node.EdgeCount != 2) return false;

        var refs = node.Edges.ToArray();
        if (!_edges.TryGetValue(refs[0].EdgeID, out var edgeA)) return false;
        if (!_edges.TryGetValue(refs[1].EdgeID, out var edgeB)) return false;
        if (edgeA.ID == edgeB.ID) return false;
        if (edgeA.GroupID != edgeB.GroupID) return false;

        var (farAID, seqAToNode) = OrientTowardsNode(edgeA, nodeID);
        var (farBID, seqBToNode) = OrientTowardsNode(edgeB, nodeID);
        if (farAID == farBID) return false;
        if (seqAToNode.Count < 2 || seqBToNode.Count < 2) return false;

        if (!AreOppositeCollinear(node.Position, seqAToNode[^2], seqBToNode[^2])) return false;

        int keepGroupID = edgeA.GroupID;
        var mergedPoints = new List<Vector2>();
        for (int i = 1; i < seqAToNode.Count - 1; i++)
            mergedPoints.Add(seqAToNode[i]);
        mergedPoints.Add(node.Position);
        for (int i = seqBToNode.Count - 2; i >= 1; i--)
            mergedPoints.Add(seqBToNode[i]);

        var farA = GetNode(farAID);
        var farB = GetNode(farBID);
        if (farA == null || farB == null) return false;

        DetachEdge(edgeA);
        DetachEdge(edgeB);
        GraphEdge? mergedEdge = AddEdge(
            farA,
            farB,
            mergedPoints.ToArray(),
            keepGroupID,
            emitEvent: false);
        if (mergedEdge is null)
            throw new InvalidOperationException("Collinear edge merge failed to create a replacement edge.");

        CommitEdgeMutation([nodeID], [keepGroupID]);
        EdgeRemoved?.Invoke(edgeA);
        EdgeRemoved?.Invoke(edgeB);
        EdgeAdded?.Invoke(mergedEdge);
        return true;
    }

    private (int farNodeID, List<Vector2> seq) OrientTowardsNode(GraphEdge edge, int nodeID)
    {
        var fullPath = edge.GetFullPath(GetNode);
        if (edge.NodeB == nodeID)
            return (edge.NodeA, fullPath.ToList());

        Array.Reverse(fullPath);
        return (edge.NodeB, fullPath.ToList());
    }

    private void InsertNodeSpatialRef(GraphNode node)
    {
        if (_nodeRefs.ContainsKey(node.ID)) return;
        var nodeRef = new NodeSpatialRef(node.ID, node.Position);
        _nodeRefs[node.ID] = nodeRef;
        _spatialIndex.Insert(nodeRef);
    }

    private void RemoveNodeSpatialRef(int nodeID)
    {
        if (!_nodeRefs.TryGetValue(nodeID, out var nodeRef)) return;
        _spatialIndex.Remove(nodeRef);
        _nodeRefs.Remove(nodeID);
    }

    private void InsertEdgeSpatialRefs(GraphEdge edge)
    {
        var refs = new List<ISpatialRef>();
        foreach (RoadGeometrySegment geometry in edge.GeometrySegments)
            refs.Add(new EdgeGeometryRef(edge.ID, geometry));

        _edgeRefs[edge.ID] = refs;
        foreach (var edgeRef in refs)
        {
            if (edgeRef is EdgeSegmentRef segmentRef)
                _spatialIndex.InsertSegment(segmentRef);
            else if (edgeRef is EdgeGeometryRef geometryRef)
                _spatialIndex.InsertGeometry(geometryRef);
            else
                _spatialIndex.Insert(edgeRef);
        }
    }

    private void RemoveEdgeSpatialRefs(int edgeID)
    {
        if (!_edgeRefs.TryGetValue(edgeID, out var refs)) return;
        foreach (var edgeRef in refs)
        {
            if (edgeRef is EdgeSegmentRef segmentRef)
                _spatialIndex.RemoveSegment(segmentRef);
            else if (edgeRef is EdgeGeometryRef geometryRef)
                _spatialIndex.RemoveGeometry(geometryRef);
            else
                _spatialIndex.Remove(edgeRef);
        }
        _edgeRefs.Remove(edgeID);
    }

    private void RemoveNodeIfIsolated(GraphNode? node)
    {
        if (node == null || node.EdgeCount > 0) return;
        _nodes.Remove(node.ID);
        RemoveNodeSpatialRef(node.ID);
    }

    private void CommitEdgeMutation(IEnumerable<int> affectedNodeIDs, IEnumerable<int> affectedGroupIDs)
    {
        foreach (int nodeID in affectedNodeIDs.Distinct())
            RemoveNodeIfIsolated(GetNode(nodeID));

        foreach (int groupID in affectedGroupIDs.Distinct())
        {
            if (_groups.TryGetValue(groupID, out RoadGroup? group) && group.IsEmpty)
                _groups.Remove(groupID);
        }

        AssertCommittedInvariants();
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void AssertCommittedInvariants() => AssertInvariants();

    private void RebuildNodeEdges()
    {
        foreach (var edge in _edges.Values)
        {
            var nodeA = GetNode(edge.NodeA);
            var nodeB = GetNode(edge.NodeB);
            nodeA?.AddEdge(edge.ID, edge.NodeB);
            nodeB?.AddEdge(edge.ID, edge.NodeA);
        }
    }

    private void RebuildSpatialIndex()
    {
        _spatialIndex.Clear();
        _nodeRefs.Clear();
        _edgeRefs.Clear();

        foreach (var node in _nodes.Values)
            InsertNodeSpatialRef(node);
        foreach (var edge in _edges.Values)
            InsertEdgeSpatialRefs(edge);
    }

    private void ClearGraph()
    {
        _nodes.Clear();
        _edges.Clear();
        _groups.Clear();
        _nodeRefs.Clear();
        _edgeRefs.Clear();
        _spatialIndex.Clear();
    }

    private static RoadGeometrySegment[] CreatePolylineGeometry(
        Vector2 start,
        Vector2 end,
        IReadOnlyList<Vector2> points)
    {
        var geometrySegments = new RoadGeometrySegment[points.Count + 1];
        Vector2 previous = start;
        for (int i = 0; i < points.Count; i++)
        {
            geometrySegments[i] = new LineRoadGeometrySegment(previous, points[i]);
            previous = points[i];
        }
        geometrySegments[^1] = new LineRoadGeometrySegment(previous, end);
        return geometrySegments;
    }

    private static List<Vector2> InsertCollectedPoints(List<Vector2> path, List<(int pathSegIndex, float t, Vector2 pos)> collected)
    {
        var bySegment = new Dictionary<int, List<(float t, Vector2 pos)>>();
        foreach (var item in collected)
            AddInsert(bySegment, item.pathSegIndex, item.t, item.pos);

        var rebuilt = new List<Vector2>();
        for (int i = 0; i < path.Count - 1; i++)
        {
            rebuilt.Add(path[i]);
            if (!bySegment.TryGetValue(i, out var inserts)) continue;
            inserts.Sort((a, b) => a.t.CompareTo(b.t));
            foreach (var insert in inserts)
                rebuilt.Add(insert.pos);
        }
        rebuilt.Add(path[^1]);
        return rebuilt;
    }

    private static void AddInsert(Dictionary<int, List<(float t, Vector2 pos)>> insertsBySegment, int segmentIndex, float t, Vector2 pos)
    {
        if (!insertsBySegment.TryGetValue(segmentIndex, out var inserts))
            insertsBySegment[segmentIndex] = inserts = new List<(float, Vector2)>();
        if (inserts.Any(existing => existing.pos.DistanceSquaredTo(pos) < GeometryEpsilon)) return;
        inserts.Add((t, pos));
    }

    private static List<Vector2> DeduplicatePoints(IEnumerable<Vector2> points)
    {
        var result = new List<Vector2>();
        foreach (var point in points)
        {
            if (result.Any(existing => existing.DistanceSquaredTo(point) < GeometryEpsilon)) continue;
            result.Add(point);
        }
        return result;
    }

    private static bool IsPointOnInfiniteLine(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        float cross = ab.X * ap.Y - ab.Y * ap.X;
        float scale = Mathf.Max(ab.LengthSquared(), 1f);
        return cross * cross < GeometryEpsilon * scale;
    }

    private static float ProjectParam(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        return (ap.X * ab.X + ap.Y * ab.Y) / ab.LengthSquared();
    }

    private static bool PointOnSegmentInterior(Vector2 a, Vector2 b, Vector2 q)
    {
        Vector2 ab = b - a;
        float lenSq = ab.LengthSquared();
        if (lenSq < GeometryEpsilon) return false;
        float t = ProjectParam(a, b, q);
        if (t <= GeometryEpsilon || t >= 1f - GeometryEpsilon) return false;
        Vector2 projection = a + ab * t;
        return projection.DistanceSquaredTo(q) < GeometryEpsilon;
    }

    private static bool PointOnSegmentInteriorOrEndpoint(Vector2 a, Vector2 b, Vector2 q)
    {
        Vector2 ab = b - a;
        float lenSq = ab.LengthSquared();
        if (lenSq < GeometryEpsilon) return false;
        float t = ProjectParam(a, b, q);
        if (t < -GeometryEpsilon || t > 1f + GeometryEpsilon) return false;
        Vector2 projection = a + ab * t;
        return projection.DistanceSquaredTo(q) < GeometryEpsilon;
    }

    private static bool ArePositionsApproximatelyEqual(Vector2 a, Vector2 b)
    {
        return a.DistanceSquaredTo(b) < GeometryEpsilon;
    }

    private static bool TryComputeInteriorCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, out Vector2 cross, out float t)
    {
        cross = default;
        t = 0f;
        if (ArePositionsApproximatelyEqual(p1, q1) ||
            ArePositionsApproximatelyEqual(p1, q2) ||
            ArePositionsApproximatelyEqual(p2, q1) ||
            ArePositionsApproximatelyEqual(p2, q2))
            return false;

        Vector2 r = p2 - p1;
        Vector2 s = q2 - q1;
        float rxs = r.X * s.Y - r.Y * s.X;
        if (Mathf.Abs(rxs) < 1e-6f) return false;

        Vector2 qp = q1 - p1;
        float tt = (qp.X * s.Y - qp.Y * s.X) / rxs;
        float uu = (qp.X * r.Y - qp.Y * r.X) / rxs;
        if (tt <= GeometryEpsilon || tt >= 1f - GeometryEpsilon) return false;
        if (uu <= GeometryEpsilon || uu >= 1f - GeometryEpsilon) return false;

        cross = p1 + r * tt;
        t = tt;
        return true;
    }

}
