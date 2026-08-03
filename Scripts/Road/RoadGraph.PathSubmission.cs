using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RoadGraph
{
    public RoadPathSubmissionResult SubmitPath(RoadPath? path)
    {
        BeginMeasuredOperation();
        RoadPathSubmissionError validationError = ValidateNativePath(path);
        if (validationError != RoadPathSubmissionError.None)
            return RoadPathSubmissionResult.Rejected(validationError);

        RoadPathSubmissionError resolutionError = ResolveNativeSegments(path!, out RoadGeometrySegment[] segments);
        if (resolutionError != RoadPathSubmissionError.None)
            return RoadPathSubmissionResult.Rejected(resolutionError);
        bool[] coveredSegments = segments.Select(IsGeometryCovered).ToArray();
        if (coveredSegments.All(covered => covered))
            return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.FullyCovered);

        NativePathIntersectionPlan intersectionPlan = PlanNativePathIntersections(segments);
        IReadOnlyList<NativePathPiece> incomingPieces =
            PlanIncomingPieces(segments, coveredSegments, intersectionPlan);
        if (incomingPieces.All(piece => piece.Covered))
            return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.FullyCovered);

        EntitySnapshot entitiesBefore = CaptureEntitySnapshot();
        ApplyExistingEdgeSplits(intersectionPlan);
        var group = new RoadGroup(NextID());
        _groups.Add(group.ID, group);

        bool anyAdded = false;
        foreach (NativePathPiece piece in incomingPieces)
        {
            if (piece.Covered)
                continue;

            GraphNode nodeA = GetOrCreateExactNode(piece.Geometry.Start);
            GraphNode nodeB = GetOrCreateExactNode(piece.Geometry.End);
            if (AddEdge(
                    nodeA,
                    nodeB,
                    new[] { piece.Geometry },
                    group.ID) is not null)
                anyAdded = true;
        }

        if (!anyAdded)
        {
            _groups.Remove(group.ID);
            return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.NoChanges);
        }

        return RoadPathSubmissionResult.Succeeded(group.ID, DescribeChanges(entitiesBefore));
    }

    private RoadPathSubmissionError ValidateNativePath(RoadPath? path)
    {
        if (path is null)
            return RoadPathSubmissionError.MissingPath;
        if (path.Segments.Count == 0)
            return RoadPathSubmissionError.NoSegments;

        var anchors = new List<Vector2>(path.Segments.Count + 1);
        for (int index = 0; index < path.Segments.Count; index++)
        {
            RoadGeometrySegment? segment = path.Segments[index];
            if (segment is null)
                return RoadPathSubmissionError.NullGeometrySegment;
            if (!IsSupportedGeometryType(segment))
                return RoadPathSubmissionError.UnknownGeometryType;
            if (!IsFiniteGeometry(segment))
                return RoadPathSubmissionError.NonFiniteCoordinate;
            if (segment.Length <= 0f || segment.Start.DistanceSquaredTo(segment.End) < GeometryEpsilon)
                return RoadPathSubmissionError.DegenerateSegment;
            if (index > 0 && path.Segments[index - 1]!.End != segment.Start)
                return RoadPathSubmissionError.DiscontinuousGeometry;

            if (index == 0)
                anchors.Add(segment.Start);
            anchors.Add(segment.End);
        }

        for (int index = 0; index < anchors.Count - 1; index++)
        {
            if (anchors[index].DistanceTo(anchors[index + 1]) <= SnapRadius)
                return RoadPathSubmissionError.CollapsedByNodeIdentity;

            GraphNode? existingA = FindClosestIndexedNode(anchors[index], SnapRadius);
            GraphNode? existingB = FindClosestIndexedNode(anchors[index + 1], SnapRadius);
            if (existingA is not null && existingA == existingB)
                return RoadPathSubmissionError.CollapsedByNodeIdentity;
        }

        for (int i = 0; i < anchors.Count; i++)
        {
            for (int j = i + 1; j < anchors.Count; j++)
            {
                if (anchors[i].DistanceSquaredTo(anchors[j]) < GeometryEpsilon)
                    return RoadPathSubmissionError.RepeatedPoint;
            }
        }

        return RoadPathSubmissionError.None;
    }

    private RoadPathSubmissionError ResolveNativeSegments(RoadPath path, out RoadGeometrySegment[] resolved)
    {
        resolved = new RoadGeometrySegment[path.Segments.Count];
        var resolvedAnchors = new List<Vector2>(resolved.Length + 1);
        for (int index = 0; index < resolved.Length; index++)
        {
            RoadGeometrySegment source = path.Segments[index]!;
            Vector2 resolvedStart = FindClosestIndexedNode(source.Start, SnapRadius)?.Position ?? source.Start;
            Vector2 resolvedEnd = FindClosestIndexedNode(source.End, SnapRadius)?.Position ?? source.End;
            if (resolvedStart.DistanceTo(resolvedEnd) <= SnapRadius)
                return RoadPathSubmissionError.CollapsedByNodeIdentity;
            if (!TrySnapGeometry(source, resolvedStart, resolvedEnd, out RoadGeometrySegment? snapped))
                return RoadPathSubmissionError.UnsupportedEndpointSnap;
            if (index > 0 && resolved[index - 1].End != snapped.Start)
                return RoadPathSubmissionError.UnsupportedEndpointSnap;
            if (index == 0)
                resolvedAnchors.Add(snapped.Start);
            resolvedAnchors.Add(snapped.End);
            resolved[index] = snapped;
        }

        for (int i = 0; i < resolvedAnchors.Count; i++)
        {
            for (int j = i + 1; j < resolvedAnchors.Count; j++)
            {
                if (resolvedAnchors[i].DistanceSquaredTo(resolvedAnchors[j]) < GeometryEpsilon)
                    return RoadPathSubmissionError.RepeatedPoint;
            }
        }
        return RoadPathSubmissionError.None;
    }

    private static bool TrySnapGeometry(
        RoadGeometrySegment source,
        Vector2 start,
        Vector2 end,
        out RoadGeometrySegment geometry)
    {
        geometry = source;
        if (source.Start == start && source.End == end)
            return true;

        Vector2 startDelta = start - source.Start;
        Vector2 endDelta = end - source.End;
        switch (source)
        {
            case LineRoadGeometrySegment:
                geometry = new LineRoadGeometrySegment(start, end);
                return true;
            case CubicBezierRoadGeometrySegment cubic:
                geometry = new CubicBezierRoadGeometrySegment(
                    start,
                    cubic.Control1 + startDelta,
                    cubic.Control2 + endDelta,
                    end);
                return true;
            case CubicHermiteRoadGeometrySegment hermite:
                geometry = new CubicHermiteRoadGeometrySegment(
                    start, hermite.StartTangent, end, hermite.EndTangent);
                return true;
            case RationalQuadraticRoadGeometrySegment rational:
                geometry = new RationalQuadraticRoadGeometrySegment(
                    start,
                    rational.StartWeight,
                    rational.Control + (startDelta + endDelta) * 0.5f,
                    rational.ControlWeight,
                    end,
                    rational.EndWeight);
                return true;
            case CircularArcRoadGeometrySegment arc when startDelta == endDelta:
                geometry = new CircularArcRoadGeometrySegment(
                    arc.Center + startDelta, arc.Radius, arc.StartAngle, arc.SweepAngle);
                return true;
            case ClothoidRoadGeometrySegment clothoid when startDelta == endDelta:
                geometry = new ClothoidRoadGeometrySegment(
                    clothoid.Start + startDelta,
                    clothoid.StartHeading,
                    clothoid.StartCurvature,
                    clothoid.EndCurvature,
                    clothoid.ArcLength);
                return true;
            default:
                return false;
        }
    }

    private static bool IsSupportedGeometryType(RoadGeometrySegment geometry) => geometry switch
    {
        LineRoadGeometrySegment => true,
        CubicBezierRoadGeometrySegment => true,
        CubicHermiteRoadGeometrySegment => true,
        CircularArcRoadGeometrySegment => true,
        ClothoidRoadGeometrySegment => true,
        RationalQuadraticRoadGeometrySegment => true,
        _ => false,
    };

    private static bool IsFiniteGeometry(RoadGeometrySegment geometry)
    {
        if (!float.IsFinite(geometry.Start.X) || !float.IsFinite(geometry.Start.Y) ||
            !float.IsFinite(geometry.End.X) || !float.IsFinite(geometry.End.Y) ||
            !float.IsFinite(geometry.Length))
            return false;

        Rect2 bounds = geometry.Bounds;
        return float.IsFinite(bounds.Position.X) && float.IsFinite(bounds.Position.Y) &&
            float.IsFinite(bounds.Size.X) && float.IsFinite(bounds.Size.Y);
    }

    private bool IsGeometryCovered(RoadGeometrySegment geometry)
    {
        if (geometry is LineRoadGeometrySegment line)
            return IsNativeLineCovered(line);

        string serialized = SaveJson.Serialize(RoadGeometrySerializer.ToData(geometry));
        return FindCandidateEdgeIDs(geometry).Any(edgeID =>
            _edges.TryGetValue(edgeID, out GraphEdge? edge) &&
            edge.GeometrySegments.Count == 1 &&
            SaveJson.Serialize(RoadGeometrySerializer.ToData(edge.GeometrySegments[0])) == serialized);
    }

    private bool IsNativeLineCovered(LineRoadGeometrySegment line)
    {
        Vector2 direction = line.End - line.Start;
        var intervals = new List<(float Start, float End)>();
        foreach (LineRoadGeometrySegment existing in FindCandidateEdgeIDs(line)
            .Select(edgeID => _edges.GetValueOrDefault(edgeID))
            .Where(edge => edge is not null)
            .SelectMany(edge => edge!.GeometrySegments)
            .OfType<LineRoadGeometrySegment>())
        {
            if (!IsPointOnInfiniteLine(line.Start, line.End, existing.Start) ||
                !IsPointOnInfiniteLine(line.Start, line.End, existing.End))
                continue;

            float first = (existing.Start - line.Start).Dot(direction) / direction.LengthSquared();
            float second = (existing.End - line.Start).Dot(direction) / direction.LengthSquared();
            float start = Mathf.Max(Mathf.Min(first, second), 0f);
            float end = Mathf.Min(Mathf.Max(first, second), 1f);
            if (end - start > GeometryEpsilon)
                intervals.Add((start, end));
        }

        intervals.Sort((left, right) => left.Start.CompareTo(right.Start));
        float coveredUntil = 0f;
        foreach ((float start, float end) in intervals)
        {
            if (start > coveredUntil + GeometryEpsilon)
                return false;
            coveredUntil = Mathf.Max(coveredUntil, end);
        }
        return intervals.Count > 0 && coveredUntil >= 1f - GeometryEpsilon;
    }

    private GraphEdge? AddEdge(
        GraphNode nodeA,
        GraphNode nodeB,
        IReadOnlyList<RoadGeometrySegment> geometrySegments,
        int groupID,
        bool emitEvent = true)
    {
        if (nodeA.ID == nodeB.ID)
            return null;

        var edge = new GraphEdge(NextID(), nodeA.ID, nodeB.ID, geometrySegments, groupID);
        _edges.Add(edge.ID, edge);

        if (!_groups.TryGetValue(groupID, out RoadGroup? group))
        {
            group = new RoadGroup(groupID);
            _groups.Add(groupID, group);
        }
        group.AddEdge(edge.ID);

        nodeA.AddEdge(edge.ID, nodeB.ID);
        nodeB.AddEdge(edge.ID, nodeA.ID);
        InsertEdgeSpatialRefs(edge);

        if (emitEvent)
            EdgeAdded?.Invoke(edge);
        return edge;
    }
}
