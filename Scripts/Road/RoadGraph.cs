using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

public class RoadGraph : ISaveable
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

    public int AddRoad(Vector2 start, Vector2 end, Vector2[] waypoints, RoadType type = RoadType.Street)
    {
        if (start == end) return -1;

        var path = new List<Vector2>(waypoints.Length + 2) { start };
        path.AddRange(waypoints);
        path.Add(end);

        // Coverage check must run BEFORE any mutating step (ResolveIntersections,
        // SplitEdgesAtPathAnchors) — otherwise a fully-covered AddRoad still splits
        // existing edges at the incoming path's anchors and then returns -1, leaving
        // the graph churned. Coverage of the polyline in R² does not depend on how
        // the path is later subdivided by anchors.
        if (IsPathFullyCovered(path)) return -1;

        path = ResolveIntersections(path);
        SplitEdgesAtPathAnchors(path);
        path = InsertExistingNodeAnchors(path);

        if (IsPathFullyCovered(path)) return -1;

        var group = new RoadGroup(NextID(), type);
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

            if (AddEdge(nodeA, nodeB, Array.Empty<Vector2>(), group.ID, type) != null)
            {
                anyAdded = true;
                touchedNodeIDs.Add(nodeA.ID);
                touchedNodeIDs.Add(nodeB.ID);
            }
        }

        if (!anyAdded)
        {
            _groups.Remove(group.ID);
            return -1;
        }

        foreach (int nodeID in touchedNodeIDs.ToList())
            TryMergeAtNode(nodeID, suppressMerge: true);

        if (_groups.TryGetValue(group.ID, out var maybeEmpty) && maybeEmpty.IsEmpty)
            _groups.Remove(group.ID);

        return group.ID;
    }

    public bool RemoveEdge(int edgeID) => RemoveEdge(edgeID, suppressMerge: true);

    public bool RemoveRoadGroup(int groupID)
    {
        if (!_groups.TryGetValue(groupID, out var group)) return false;

        foreach (int edgeID in group.EdgeIDs.ToList())
            RemoveEdge(edgeID, suppressMerge: true);

        _groups.Remove(groupID);

        return true;
    }

    public GraphEdge? GetEdge(int edgeID) => _edges.GetValueOrDefault(edgeID);
    public GraphNode? GetNode(int nodeID) => _nodes.GetValueOrDefault(nodeID);
    public RoadGroup? GetGroup(int groupID) => _groups.GetValueOrDefault(groupID);

    public GraphEdge? FindClosestEdge(Vector2 position, float maxRadius)
    {
        int bestEdgeID = -1;
        float bestDistSq = maxRadius * maxRadius;
        var candidateEdgeIDs = new HashSet<int>();

        foreach (var hit in _spatialIndex.QueryRadius(position, maxRadius))
        {
            if (TryGetEdgeID(hit, out int edgeID))
                candidateEdgeIDs.Add(edgeID);
        }

        foreach (int edgeID in candidateEdgeIDs)
        {
            var edge = GetEdge(edgeID);
            if (edge == null) continue;
            float d2 = DistanceSquaredToPath(edge.GetFullPath(GetNode), position);
            if (d2 <= bestDistSq)
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

    public IEnumerable<GraphEdge> GetAllEdges() => _edges.Values;
    public IEnumerable<GraphNode> GetAllNodes() => _nodes.Values;
    public IEnumerable<RoadGroup> GetAllGroups() => _groups.Values;

    public object CaptureState()
    {
        var data = new RoadGraphSaveData
        {
            Version = 2,
            NextID = _nextID,
        };

        foreach (var node in _nodes.Values)
        {
            data.Junctions.Add(new JunctionData
            {
                ID = node.ID,
                X = node.Position.X,
                Y = node.Position.Y,
            });
        }

        foreach (var edge in _edges.Values)
        {
            var segmentData = new SegmentData
            {
                ID = edge.ID,
                FromJunctionID = edge.NodeA,
                ToJunctionID = edge.NodeB,
                RoadID = edge.GroupID,
                TotalLength = edge.Length,
                Type = (int)edge.Type,
            };
            foreach (var point in edge.InternalPoints)
                segmentData.Waypoints.Add(new Vector2Data(point));
            data.Segments.Add(segmentData);
        }

        foreach (var group in _groups.Values)
        {
            data.Roads.Add(new RoadData
            {
                ID = group.ID,
                SegmentIDs = group.EdgeIDs.ToList(),
                Type = (int)group.Type,
            });
        }

        return data;
    }

    public void RestoreState(string json)
    {
        var data = SaveJson.Deserialize<RoadGraphSaveData>(json);

        ClearGraph();
        _nextID = data.NextID;

        RestoreFromSavedData(data);
        RebuildNodeEdges();
        RebuildSpatialIndex();
        EnsureNextIDBeyondLoadedEntities();

        GraphCleared?.Invoke();
    }

    private GraphEdge? AddEdge(GraphNode nodeA, GraphNode nodeB, Vector2[] points, int groupID, RoadType type)
    {
        if (nodeA.ID == nodeB.ID) return null;

        float length = ComputeLength(nodeA.Position, nodeB.Position, points);
        var edge = new GraphEdge(NextID(), nodeA.ID, nodeB.ID, points, groupID, type, length);
        _edges[edge.ID] = edge;

        if (!_groups.TryGetValue(groupID, out var group))
        {
            group = new RoadGroup(groupID, type);
            _groups[groupID] = group;
        }
        group.AddEdge(edge.ID);

        nodeA.AddEdge(edge.ID, nodeB.ID);
        nodeB.AddEdge(edge.ID, nodeA.ID);
        InsertEdgeSpatialRefs(edge);

        EdgeAdded?.Invoke(edge);
        return edge;
    }

    private bool RemoveEdge(int edgeID, bool suppressMerge)
    {
        if (!_edges.TryGetValue(edgeID, out var edge)) return false;

        _edges.Remove(edgeID);
        RemoveEdgeSpatialRefs(edgeID);


        var nodeA = GetNode(edge.NodeA);
        var nodeB = GetNode(edge.NodeB);

        nodeA?.RemoveEdge(edge.ID);
        nodeB?.RemoveEdge(edge.ID);

        RemoveNodeIfIsolated(nodeA);
        if (nodeB != nodeA) RemoveNodeIfIsolated(nodeB);

        if (_groups.TryGetValue(edge.GroupID, out var group))
        {
            group.RemoveEdge(edge.ID);
            if (group.IsEmpty) _groups.Remove(group.ID);
        }

        EdgeRemoved?.Invoke(edge);

        if (!suppressMerge)
        {
            if (nodeA != null && _nodes.ContainsKey(nodeA.ID))
                TryMergeAtNode(nodeA.ID, suppressMerge: true);
            if (nodeB != null && nodeB != nodeA && _nodes.ContainsKey(nodeB.ID))
                TryMergeAtNode(nodeB.ID, suppressMerge: true);
        }

        return true;
    }

    private void SplitEdgeAtPosition(int edgeID, Vector2 splitPos)
    {
        if (!_edges.TryGetValue(edgeID, out var edge)) return;

        var fullPath = edge.GetFullPath(GetNode);
        if (fullPath.Length < 2) return;

        int hitIndex = FindSubSegmentContaining(fullPath, splitPos);

        // If splitPos matches an interior waypoint exactly (sub-segment boundary),
        // FindSubSegmentContaining won't find it. Detect by direct point match.
        if (hitIndex < 0)
        {
            for (int i = 1; i < fullPath.Length - 1; i++)
            {
                if (fullPath[i].DistanceSquaredTo(splitPos) < GeometryEpsilon)
                {
                    // Split at waypoint index i: left = fullPath[0..i], right = fullPath[i..end]
                    var leftPts = new List<Vector2>();
                    var rightPts = new List<Vector2>();
                    for (int k = 1; k < i; k++)
                        leftPts.Add(fullPath[k]);
                    for (int k = i + 1; k < fullPath.Length - 1; k++)
                        rightPts.Add(fullPath[k]);

                    int groupID = edge.GroupID;
                    RoadType type = edge.Type;
                    var start = fullPath[0];
                    var end = fullPath[^1];

                    RemoveEdge(edge.ID, suppressMerge: true);

                    var splitNode = GetOrCreateNode(splitPos);
                    var nodeA = GetOrCreateNode(start);
                    var nodeB = GetOrCreateNode(end);

                    AddEdge(nodeA, splitNode, leftPts.ToArray(), groupID, type);
                    AddEdge(splitNode, nodeB, rightPts.ToArray(), groupID, type);
                    return;
                }
            }
            return; // splitPos not found anywhere on this edge
        }

        var leftPoints = new List<Vector2>();
        var rightPoints = new List<Vector2>();

        for (int i = 1; i <= hitIndex; i++)
            leftPoints.Add(fullPath[i]);
        for (int i = hitIndex + 1; i < fullPath.Length - 1; i++)
            rightPoints.Add(fullPath[i]);

        int grpID = edge.GroupID;
        RoadType edgeType = edge.Type;
        var pathStart = fullPath[0];
        var pathEnd = fullPath[^1];

        RemoveEdge(edge.ID, suppressMerge: true);

        var split = GetOrCreateNode(splitPos);
        var nA = GetOrCreateNode(pathStart);
        var nB = GetOrCreateNode(pathEnd);

        AddEdge(nA, split, leftPoints.ToArray(), grpID, edgeType);
        AddEdge(split, nB, rightPoints.ToArray(), grpID, edgeType);
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
            float radius = a.DistanceTo(b) * 0.5f + SnapRadius;
            var center = (a + b) * 0.5f;

            foreach (var hit in _spatialIndex.QueryRadius(center, radius))
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
        foreach (var edge in _edges.Values)
        {
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
        foreach (var (q1, q2) in CollectExistingSubSegments())
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

    private IEnumerable<(Vector2 a, Vector2 b)> CollectExistingSubSegments()
    {
        foreach (var edge in _edges.Values)
        {
            var path = edge.GetFullPath(GetNode);
            for (int i = 0; i < path.Length - 1; i++)
                yield return (path[i], path[i + 1]);
        }
    }

    private void QueryCandidateEdgeIDs(Vector2 a, Vector2 b, HashSet<int> result)
    {
        // Query from both endpoints to ensure we find edges whose spatial refs
        // might be near either end of the path segment, not just its midpoint.
        float segLen = a.DistanceTo(b);
        float radius = segLen + IndexBucketSize * 2f;

        foreach (var hit in _spatialIndex.QueryRadius(a, radius))
        {
            if (TryGetEdgeID(hit, out int edgeID)) result.Add(edgeID);
        }
        foreach (var hit in _spatialIndex.QueryRadius(b, radius))
        {
            if (TryGetEdgeID(hit, out int edgeID)) result.Add(edgeID);
        }
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
        foreach (var edge in _edges.Values)
        {
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

    private bool TryMergeAtNode(int nodeID, bool suppressMerge)
    {
        if (!_nodes.TryGetValue(nodeID, out var node)) return false;
        if (node.EdgeCount != 2) return false;

        var refs = node.Edges.ToArray();
        if (!_edges.TryGetValue(refs[0].EdgeID, out var edgeA)) return false;
        if (!_edges.TryGetValue(refs[1].EdgeID, out var edgeB)) return false;
        if (edgeA.ID == edgeB.ID) return false;
        if (edgeA.GroupID != edgeB.GroupID || edgeA.Type != edgeB.Type) return false;

        var (farAID, seqAToNode) = OrientTowardsNode(edgeA, nodeID);
        var (farBID, seqBToNode) = OrientTowardsNode(edgeB, nodeID);
        if (farAID == farBID) return false;
        if (seqAToNode.Count < 2 || seqBToNode.Count < 2) return false;

        if (!AreOppositeCollinear(node.Position, seqAToNode[^2], seqBToNode[^2])) return false;

        int keepGroupID = edgeA.GroupID;
        RoadType type = edgeA.Type;
        var mergedPoints = new List<Vector2>();
        for (int i = 1; i < seqAToNode.Count - 1; i++)
            mergedPoints.Add(seqAToNode[i]);
        mergedPoints.Add(node.Position);
        for (int i = seqBToNode.Count - 2; i >= 1; i--)
            mergedPoints.Add(seqBToNode[i]);

        var farA = GetNode(farAID);
        var farB = GetNode(farBID);
        if (farA == null || farB == null) return false;

        RemoveEdge(edgeA.ID, suppressMerge: true);
        RemoveEdge(edgeB.ID, suppressMerge: true);

        // Far nodes may have been removed as "isolated" during RemoveEdge.
        // Re-insert them so the merged edge and renderer can find them.
        if (!_nodes.ContainsKey(farA.ID))
        {
            _nodes[farA.ID] = farA;
            InsertNodeSpatialRef(farA);
        }
        if (!_nodes.ContainsKey(farB.ID))
        {
            _nodes[farB.ID] = farB;
            InsertNodeSpatialRef(farB);
        }

        AddEdge(farA, farB, mergedPoints.ToArray(), keepGroupID, type);
        _ = suppressMerge;
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
        var nodeA = GetNode(edge.NodeA);
        var nodeB = GetNode(edge.NodeB);

        if (nodeA != null) refs.Add(new EdgePointRef(edge.ID, nodeA.Position));
        foreach (var point in edge.InternalPoints)
            refs.Add(new EdgePointRef(edge.ID, point));
        if (nodeB != null) refs.Add(new EdgePointRef(edge.ID, nodeB.Position));

        var path = edge.GetFullPath(GetNode);
        for (int i = 0; i < path.Length - 1; i++)
            refs.Add(new EdgeSegmentRef(edge.ID, path[i], path[i + 1]));

        _edgeRefs[edge.ID] = refs;
        foreach (var edgeRef in refs)
        {
            if (edgeRef is EdgeSegmentRef segmentRef)
                _spatialIndex.InsertSegment(segmentRef);
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

    private void RestoreFromSavedData(RoadGraphSaveData data)
    {
        foreach (var nodeData in data.Junctions)
        {
            var node = new GraphNode(nodeData.ID, new Vector2(nodeData.X, nodeData.Y));
            _nodes[node.ID] = node;
        }

        foreach (var groupData in data.Roads)
        {
            var groupType = groupData.Type.HasValue ? (RoadType)groupData.Type.Value : RoadType.Street;
            var group = new RoadGroup(groupData.ID, groupType);
            foreach (int edgeID in groupData.SegmentIDs)
                group.AddEdge(edgeID);
            _groups[group.ID] = group;
        }

        foreach (var edgeData in data.Segments)
        {
            var points = edgeData.Waypoints.Select(p => p.ToVector2()).ToArray();

            // Edge type resolution order:
            //   1. Explicit type on the segment (v2+ saves).
            //   2. Owning group's type if the group carried one (v2+ saves).
            //   3. RoadType.Street (legacy v1 saves).
            RoadType edgeType;
            if (edgeData.Type.HasValue)
                edgeType = (RoadType)edgeData.Type.Value;
            else if (_groups.TryGetValue(edgeData.RoadID, out var existingGroup))
                edgeType = existingGroup.Type;
            else
                edgeType = RoadType.Street;

            var edge = new GraphEdge(
                edgeData.ID,
                edgeData.FromJunctionID,
                edgeData.ToJunctionID,
                points,
                edgeData.RoadID,
                edgeType,
                edgeData.TotalLength > 0f
                    ? edgeData.TotalLength
                    : ComputeLength(edgeData.FromJunctionID, edgeData.ToJunctionID, points)
            );
            _edges[edge.ID] = edge;

            if (!_groups.TryGetValue(edge.GroupID, out var group))
            {
                group = new RoadGroup(edge.GroupID, edgeType);
                _groups[group.ID] = group;
            }
            group.AddEdge(edge.ID);
        }
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

    private void EnsureNextIDBeyondLoadedEntities()
    {
        int maxID = -1;
        if (_nodes.Count > 0) maxID = Mathf.Max(maxID, _nodes.Keys.Max());
        if (_edges.Count > 0) maxID = Mathf.Max(maxID, _edges.Keys.Max());
        if (_groups.Count > 0) maxID = Mathf.Max(maxID, _groups.Keys.Max());
        if (_nextID <= maxID) _nextID = maxID + 1;
    }

    private float ComputeLength(int nodeAID, int nodeBID, Vector2[] points)
    {
        var nodeA = GetNode(nodeAID);
        var nodeB = GetNode(nodeBID);
        if (nodeA == null || nodeB == null) return 0f;
        return ComputeLength(nodeA.Position, nodeB.Position, points);
    }

    private static float ComputeLength(Vector2 start, Vector2 end, Vector2[] points)
    {
        float length = 0f;
        var previous = start;
        foreach (var point in points)
        {
            length += previous.DistanceTo(point);
            previous = point;
        }
        length += previous.DistanceTo(end);
        return length;
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

    private class RoadGraphSaveData
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("nextID")]
        public int NextID { get; set; }

        [JsonPropertyName("junctions")]
        public List<JunctionData> Junctions { get; set; } = new();

        [JsonPropertyName("segments")]
        public List<SegmentData> Segments { get; set; } = new();

        [JsonPropertyName("roads")]
        public List<RoadData> Roads { get; set; } = new();
    }
}
