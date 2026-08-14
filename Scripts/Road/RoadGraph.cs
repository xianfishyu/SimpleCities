using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

public partial class RoadGraph : IStreamingSaveable
{
    private const float SnapRadius = RoadNumericPolicy.NodeSnapRadius;
    private const float GeometryEpsilon = 1e-4f;
    private const float IndexBucketSize = 64f;

    private ImmutableDictionary<int, GraphNode>.Builder _nodes;
    private ImmutableDictionary<int, GraphEdge>.Builder _edges;
    private UniformGrid _spatialIndex;
    private ImmutableDictionary<int, NodeSpatialRef>.Builder _nodeRefs;
    private ImmutableDictionary<int, ImmutableArray<ISpatialRef>>.Builder _edgeRefs;
    private readonly RoadGraphCapacity _capacity;

    private int _nextID;
    private RoadGraphRevision _revision;
    private long _nextDomainRevisionID;
    private bool _mutationInProgress;
    private bool _publishingChanges;
    private RoadGraphLoadAdmission? _loadAdmission;
    private long _loadAdmissionGeneration;
    private long _geometrySegmentCount;
    private long _queryFragmentCount;
    private double _totalGeometryLength;

    public string SaveFileName => "road_network";

    public IStreamingLoadReader CaptureLoadReader() => new RoadGraphLoadReader(
        _capacity,
        _spatialIndex.BucketSize);

    public event Action<RoadGraphChangedEvent>? GraphChanged;

    public RoadGraph() : this(IndexBucketSize, RoadGraphCapacity.Default, 0) { }

    public RoadGraph(float bucketSize) : this(bucketSize, RoadGraphCapacity.Default, 0) { }

    internal RoadGraph(RoadGraphCapacity capacity, int initialNextID = 0)
        : this(IndexBucketSize, capacity, initialNextID) { }

    internal RoadGraph(float bucketSize, RoadGraphCapacity capacity, int initialNextID = 0)
    {
        ArgumentNullException.ThrowIfNull(capacity);
        if (initialNextID < 0)
            throw new ArgumentOutOfRangeException(nameof(initialNextID));

        _capacity = capacity;
        _nextID = initialNextID;
        _nodes = ImmutableDictionary.CreateBuilder<int, GraphNode>();
        _edges = ImmutableDictionary.CreateBuilder<int, GraphEdge>();
        _nodeRefs = ImmutableDictionary.CreateBuilder<int, NodeSpatialRef>();
        _edgeRefs = ImmutableDictionary.CreateBuilder<int, ImmutableArray<ISpatialRef>>();
        _spatialIndex = new UniformGrid(bucketSize);
        GraphLineageID lineageID = AllocateLineageID();
        _revision = CaptureWorkingRevision(lineageID, 0, 0);
        _nextDomainRevisionID = 1;
    }

    private int NextID()
    {
        if (_nextID == int.MaxValue)
            throw new InvalidOperationException("RoadGraph ID space is exhausted.");
        return _nextID++;
    }

    internal int NextIDWatermark => _nextID;

    internal IReadOnlyList<EdgeGeometryRef> CaptureQueryFragments(int edgeID) =>
        _edgeRefs.TryGetValue(edgeID, out ImmutableArray<ISpatialRef> references)
            ? references.Cast<EdgeGeometryRef>().ToArray()
            : Array.Empty<EdgeGeometryRef>();

    internal RoadGraphResourceCounts CaptureResourceCounts()
    {
        return new RoadGraphResourceCounts(
            _nodes.Count,
            _edges.Count,
            _geometrySegmentCount,
            _queryFragmentCount,
            _spatialIndex.BucketCount,
            _spatialIndex.ReferenceEntryCount);
    }

    public RoadPathSubmissionResult SubmitPolyline(
        RoadType roadType,
        IReadOnlyList<Vector2>? points)
    {
        BeginMeasuredOperation();
        return ExecuteSubmission(() =>
        {
            if (!RoadTypeContract.IsDefined(roadType))
                return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.InvalidRoadType);
            var validationError = ValidatePolyline(points);
            if (validationError != RoadPathSubmissionError.None)
                return RoadPathSubmissionResult.Rejected(validationError);

            Vector2[] path = points!
                .Select(RoadNumericPolicy.Canonicalize)
                .ToArray();
            var segments = new RoadGeometrySegment[path.Length - 1];
            for (int index = 0; index < segments.Length; index++)
                segments[index] = new LineRoadGeometrySegment(path[index], path[index + 1]);
            return SubmitPathCore(new RoadPath(segments), roadType);
        });
    }

    public bool RemoveEdge(int edgeID)
    {
        BeginMeasuredOperation();
        return ExecuteBooleanMutation(() => RemoveEdgesCore([edgeID]));
    }

    public bool RemoveEdges(IEnumerable<int>? edgeIDs)
    {
        BeginMeasuredOperation();
        ArgumentNullException.ThrowIfNull(edgeIDs);
        return ExecuteBooleanMutation(() => RemoveEdgesCore(edgeIDs));
    }

    private bool RemoveEdgesCore(IEnumerable<int> edgeIDs)
    {
        var removedEdges = new List<GraphEdge>();
        var affectedNodeIDs = new HashSet<int>();
        foreach (int edgeID in edgeIDs.Distinct().Order())
        {
            if (!_edges.TryGetValue(edgeID, out GraphEdge? edge)) continue;
            removedEdges.Add(edge);
            affectedNodeIDs.Add(edge.NodeA);
            affectedNodeIDs.Add(edge.NodeB);
            DetachEdge(edge);
        }

        if (removedEdges.Count == 0)
            return false;

        FinalizeMutation(affectedNodeIDs);

        return true;
    }

    public GraphEdge? GetEdge(int edgeID) => _edges.GetValueOrDefault(edgeID);
    public GraphNode? GetNode(int nodeID) => _nodes.GetValueOrDefault(nodeID);

    public GraphEdge? FindClosestEdge(Vector2 position, float maxRadius)
    {
        BeginMeasuredOperation();
        ValidateSpatialQuery(position, maxRadius);
        int bestEdgeID = -1;
        float bestDistSq = maxRadius * maxRadius;
        var candidateEdgeIDs = new HashSet<int>();
        var candidateFragments = new List<EdgeGeometryRef>();

        foreach (ISpatialRef hit in _spatialIndex.QueryRadius(position, maxRadius))
        {
            if (hit is not EdgeGeometryRef fragment)
                continue;
            candidateFragments.Add(fragment);
            candidateEdgeIDs.Add(fragment.EdgeID);
        }
        RecordSpatialCandidates(candidateEdgeIDs.Count);
        RecordQueryFragmentCandidates(candidateFragments.Count);

        foreach (EdgeGeometryRef fragment in candidateFragments)
        {
            if (!_edges.ContainsKey(fragment.EdgeID))
                continue;
            RecordExactGeometryTest();
            RoadGeometryClosestPoint closest = fragment.Geometry.FindClosestPoint(position);
            float d2 = closest.DistanceSquared;
            if (d2 > maxRadius * maxRadius)
                continue;
            bool sameDistance = Mathf.IsEqualApprox(d2, bestDistSq);
            if (d2 < bestDistSq ||
                (sameDistance && (bestEdgeID < 0 || fragment.EdgeID < bestEdgeID)))
            {
                bestDistSq = d2;
                bestEdgeID = fragment.EdgeID;
            }
        }

        return bestEdgeID >= 0 ? GetEdge(bestEdgeID) : null;
    }

    public IReadOnlyList<int> FindEdgeIDsNear(Vector2 position, float radius)
    {
        BeginMeasuredOperation();
        ValidateSpatialQuery(position, radius);

        var edgeIDs = new HashSet<int>();
        int fragmentCandidates = 0;
        foreach (ISpatialRef hit in _spatialIndex.QueryRadius(position, radius))
        {
            if (hit is not EdgeGeometryRef fragment || !_edges.ContainsKey(fragment.EdgeID))
                continue;
            fragmentCandidates++;
            RecordExactGeometryTest();
            RoadGeometryClosestPoint closest = fragment.Geometry.FindClosestPoint(position, GeometryEpsilon);
            if (closest.DistanceSquared <= radius * radius)
                edgeIDs.Add(fragment.EdgeID);
        }

        RecordQueryFragmentCandidates(fragmentCandidates);
        RecordSpatialCandidates(edgeIDs.Count);
        return edgeIDs.Order().ToArray();
    }

    public IReadOnlyList<int> FindEdgeIDsIntersecting(Rect2 bounds)
    {
        BeginMeasuredOperation();
        Rect2 normalizedBounds = NormalizeBounds(bounds);
        var candidateEdgeIDs = new HashSet<int>();
        int fragmentCandidates = 0;
        foreach (ISpatialRef hit in _spatialIndex.QueryBounds(normalizedBounds))
        {
            if (hit is not EdgeGeometryRef fragment || !_edges.ContainsKey(fragment.EdgeID))
                continue;
            fragmentCandidates++;
            RecordExactGeometryTest();
            if (GeometryIntersectsBounds(fragment.Geometry, normalizedBounds))
                candidateEdgeIDs.Add(fragment.EdgeID);
        }

        RecordQueryFragmentCandidates(fragmentCandidates);
        RecordSpatialCandidates(candidateEdgeIDs.Count);
        return candidateEdgeIDs.Order().ToArray();
    }

    public GraphNode? FindClosestNode(Vector2 position, float maxRadius)
    {
        BeginMeasuredOperation();
        return FindClosestIndexedNode(position, maxRadius);
    }

    private GraphNode? FindClosestIndexedNode(Vector2 position, float maxRadius)
    {
        ValidateSpatialQuery(position, maxRadius);
        int bestNodeID = -1;
        double bestDistSq = (double)maxRadius * maxRadius;

        foreach (var hit in _spatialIndex.QueryRadius(position, maxRadius))
        {
            if (hit.Kind != SpatialRefKind.Node) continue;
            int nodeID = ((NodeSpatialRef)hit).NodeID;
            var node = GetNode(nodeID);
            if (node == null) continue;

            double d2 = RoadNumericPolicy.DistanceSquared(node.Position, position);
            bool sameDistance = d2 == bestDistSq;
            if (bestNodeID >= 0 && !sameDistance && d2 > bestDistSq) continue;
            if (bestNodeID >= 0 && sameDistance && nodeID > bestNodeID) continue;

            bestDistSq = d2;
            bestNodeID = nodeID;
        }

        return bestNodeID >= 0 ? GetNode(bestNodeID) : null;
    }

    private static void ValidateSpatialQuery(Vector2 position, float radius)
    {
        if (!RoadNumericPolicy.IsWithinCoordinateRange(position))
            throw new ArgumentException("Position must be finite and within the RoadGraph coordinate range.", nameof(position));
        if (!float.IsFinite(radius) || radius < 0f || radius > RoadNumericPolicy.MaximumCoordinateMagnitude)
            throw new ArgumentOutOfRangeException(
                nameof(radius),
                radius,
                "Radius must be finite and within the RoadGraph query range.");
    }

    public IEnumerable<GraphEdge> GetAllEdges() => _edges.Values.ToArray();
    public IEnumerable<GraphNode> GetAllNodes() => _nodes.Values.ToArray();

    private static Rect2 NormalizeBounds(Rect2 bounds)
    {
        if (!bounds.Position.IsFinite() || !bounds.Size.IsFinite())
            throw new ArgumentException("Bounds must contain finite coordinates.", nameof(bounds));

        Vector2 end = bounds.End;
        Vector2 minimum = new(Mathf.Min(bounds.Position.X, end.X), Mathf.Min(bounds.Position.Y, end.Y));
        Vector2 maximum = new(Mathf.Max(bounds.Position.X, end.X), Mathf.Max(bounds.Position.Y, end.Y));
        return new Rect2(minimum, maximum - minimum);
    }

    private static bool GeometryIntersectsBounds(RoadGeometrySegment geometry, Rect2 bounds)
    {
        if (!BoundsOverlap(geometry.Bounds, bounds))
            return false;
        if (ContainsInclusive(bounds, geometry.Start) || ContainsInclusive(bounds, geometry.End))
            return true;

        if (bounds.Size == Vector2.Zero)
        {
            RoadGeometryClosestPoint closest = geometry.FindClosestPoint(bounds.Position, GeometryEpsilon);
            return closest.DistanceSquared <= GeometryEpsilon * GeometryEpsilon;
        }

        var boundaries = new List<LineRoadGeometrySegment>(4);
        Vector2 topLeft = bounds.Position;
        Vector2 bottomRight = bounds.End;
        Vector2 topRight = new(bottomRight.X, topLeft.Y);
        Vector2 bottomLeft = new(topLeft.X, bottomRight.Y);
        if (bounds.Size.X > 0f)
        {
            boundaries.Add(new LineRoadGeometrySegment(topLeft, topRight));
            if (bounds.Size.Y > 0f)
                boundaries.Add(new LineRoadGeometrySegment(bottomLeft, bottomRight));
        }
        if (bounds.Size.Y > 0f)
        {
            boundaries.Add(new LineRoadGeometrySegment(topLeft, bottomLeft));
            if (bounds.Size.X > 0f)
                boundaries.Add(new LineRoadGeometrySegment(topRight, bottomRight));
        }

        return boundaries.Any(boundary =>
        {
            RoadGeometryIntersectionResult result = RoadGeometryIntersectionQuery.FindIntersections(
                geometry,
                boundary,
                GeometryEpsilon,
                GeometryEpsilon);
            return result.Intersections.Count > 0 || result.HasOverlap;
        });
    }

    private static bool BoundsOverlap(Rect2 first, Rect2 second) =>
        first.Position.X <= second.End.X + GeometryEpsilon &&
        first.End.X + GeometryEpsilon >= second.Position.X &&
        first.Position.Y <= second.End.Y + GeometryEpsilon &&
        first.End.Y + GeometryEpsilon >= second.Position.Y;

    private static bool ContainsInclusive(Rect2 bounds, Vector2 point) =>
        point.X >= bounds.Position.X - GeometryEpsilon &&
        point.X <= bounds.End.X + GeometryEpsilon &&
        point.Y >= bounds.Position.Y - GeometryEpsilon &&
        point.Y <= bounds.End.Y + GeometryEpsilon;

    private GraphEdge? AddEdge(
        GraphNode nodeA,
        GraphNode nodeB,
        Vector2[] points,
        RoadType roadType,
        bool emitEvent = true)
    {
        var geometrySegments = CreatePolylineGeometry(nodeA.Position, nodeB.Position, points);
        return AddEdge(nodeA, nodeB, geometrySegments, roadType, emitEvent: emitEvent);
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
        TrackNodeChange(node.ID);
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
            if (!RoadNumericPolicy.IsWithinCoordinateRange(point))
                return RoadPathSubmissionError.NumericOutOfRange;
        }

        for (int i = 0; i < path.Count - 1; i++)
        {
            if (ArePositionsApproximatelyEqual(path[i], path[i + 1]))
                return RoadPathSubmissionError.DegenerateSegment;

            if (RoadNumericPolicy.DistanceSquared(path[i], path[i + 1]) <=
                (double)SnapRadius * SnapRadius)
                return RoadPathSubmissionError.CollapsedByNodeIdentity;

            var nodeA = FindClosestIndexedNode(path[i], SnapRadius);
            var nodeB = FindClosestIndexedNode(path[i + 1], SnapRadius);
            if (nodeA != null && nodeA.ID == nodeB?.ID)
                return RoadPathSubmissionError.CollapsedByNodeIdentity;
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

    private IReadOnlyList<EdgeGeometryRef> FindCandidateGeometryRefs(Rect2 bounds)
    {
        EdgeGeometryRef[] fragments = _spatialIndex.QueryBounds(bounds)
            .OfType<EdgeGeometryRef>()
            .OrderBy(fragment => fragment.EdgeID)
            .ThenBy(fragment => fragment.GeometryIndex)
            .ThenBy(fragment => fragment.FragmentIndex)
            .ToArray();
        RecordQueryFragmentCandidates(fragments.Length);
        RecordSpatialCandidates(fragments.Select(fragment => fragment.EdgeID).Distinct().Count());
        return fragments;
    }

    private IReadOnlyList<EdgeGeometryRef> FindCandidateGeometryRefs(
        RoadGeometrySegment geometry) =>
        FindCandidateGeometryRefs(CreateQueryBounds(
            geometry.Bounds.Position,
            geometry.Bounds.End,
            Mathf.Sqrt(GeometryEpsilon)));

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
        return RoadExactPredicates.Orient2DSign(pointA, node, pointB) == 0 &&
               RoadExactPredicates.DotSign(toA, toB) < 0;
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
        for (int geometryIndex = 0; geometryIndex < edge.GeometrySegments.Count; geometryIndex++)
        {
            refs.AddRange(RoadQueryFragmentFactory.Create(
                edge.ID,
                geometryIndex,
                edge.GeometrySegments[geometryIndex],
                _spatialIndex.BucketSize,
                edge.NodeA != edge.NodeB && geometryIndex == edge.GeometrySegments.Count - 1));
        }

        _edgeRefs[edge.ID] = [.. refs];
        _queryFragmentCount += refs.Count;
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
        _queryFragmentCount -= refs.Length;
        _edgeRefs.Remove(edgeID);
    }

    private void RemoveNodeIfIsolated(GraphNode? node)
    {
        if (node == null || node.IncidenceCount > 0) return;
        TrackNodeChange(node.ID);
        _nodes.Remove(node.ID);
        RemoveNodeSpatialRef(node.ID);
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void AssertCommittedInvariants() => AssertInvariants();

    private void RebuildSpatialIndex()
    {
        _spatialIndex.Clear();
        _nodeRefs.Clear();
        _edgeRefs.Clear();
        _geometrySegmentCount = _edges.Values.Sum(edge => (long)edge.GeometrySegments.Count);
        _totalGeometryLength = _edges.Values
            .SelectMany(edge => edge.GeometrySegments)
            .Sum(geometry => (double)geometry.Length);
        _queryFragmentCount = 0;

        foreach (var node in _nodes.Values)
            InsertNodeSpatialRef(node);
        foreach (var edge in _edges.Values)
            InsertEdgeSpatialRefs(edge);
    }

    private void ClearGraph()
    {
        _nodes.Clear();
        _edges.Clear();
        _nodeRefs.Clear();
        _edgeRefs.Clear();
        _spatialIndex.Clear();
        _geometrySegmentCount = 0;
        _queryFragmentCount = 0;
        _totalGeometryLength = 0d;
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
