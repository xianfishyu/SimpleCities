using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RoadGraph
{
    public RoadPathSubmissionResult SubmitPath(RoadBuildRequest? request)
    {
        BeginMeasuredOperation();
        return ExecuteSubmission(() =>
        {
            if (request is null)
                return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.MissingPath);
            if (!RoadTypeContract.IsDefined(request.RoadType))
                return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.InvalidRoadType);
            return SubmitPathCore(request.Path, request.RoadType);
        });
    }

    private RoadPathSubmissionResult SubmitPathCore(RoadPath? path, RoadType roadType)
    {
        RoadPathSubmissionError validationError = ValidateNativePath(path);
        if (validationError != RoadPathSubmissionError.None)
            return RoadPathSubmissionResult.Rejected(validationError);

        RoadPathSubmissionError resolutionError = ResolveNativeSegments(path!, out RoadGeometrySegment[] segments);
        if (resolutionError != RoadPathSubmissionError.None)
            return RoadPathSubmissionResult.Rejected(resolutionError);
        RoadPathSubmissionError planningError =
            PlanNativePathIntersections(segments, out NativePathIntersectionPlan intersectionPlan);
        if (planningError != RoadPathSubmissionError.None)
            return RoadPathSubmissionResult.Rejected(planningError);
        bool[] coveredSegments = segments.Select(IsGeometryCovered).ToArray();
        if (coveredSegments.All(covered => covered))
            return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.FullyCovered);
        RoadPathSubmissionError piecePlanningError = PlanIncomingPieces(
            segments,
            coveredSegments,
            intersectionPlan,
            out IReadOnlyList<NativePathPiece> incomingPieces);
        if (piecePlanningError != RoadPathSubmissionError.None)
            return RoadPathSubmissionResult.Rejected(piecePlanningError);
        if (incomingPieces.All(piece => piece.Covered))
            return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.FullyCovered);

        RoadPathSubmissionError admissionError =
            AdmitNativePathMutation(incomingPieces, intersectionPlan, out _);
        if (admissionError != RoadPathSubmissionError.None)
            return RoadPathSubmissionResult.Rejected(admissionError);

        ApplyExistingEdgeSplits(intersectionPlan);

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
                    roadType,
                    emitEvent: false) is not null)
                anyAdded = true;
        }

        if (!anyAdded)
            return RoadPathSubmissionResult.Rejected(RoadPathSubmissionError.NoChanges);

        FinalizeMutation(_touchedNodeIDs.ToArray());
        return RoadPathSubmissionResult.Succeeded(RoadGraphChangeSummary.Empty);
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
            if (RoadNumericPolicy.ValidateGeometry(segment) != RoadNumericError.None)
                return RoadPathSubmissionError.NumericOutOfRange;
            bool exactFullTurn = segment is CircularArcRoadGeometrySegment { IsFullTurn: true };
            if (segment.Length <= 0f ||
                (!exactFullTurn && segment.Start.DistanceSquaredTo(segment.End) < GeometryEpsilon))
                return RoadPathSubmissionError.DegenerateSegment;
            if (index > 0 && path.Segments[index - 1]!.End != segment.Start)
                return RoadPathSubmissionError.DiscontinuousGeometry;

            if (index == 0)
                anchors.Add(segment.Start);
            anchors.Add(segment.End);
        }

        for (int index = 0; index < anchors.Count - 1; index++)
        {
            bool exactFullTurn = path.Segments[index] is CircularArcRoadGeometrySegment
            {
                IsFullTurn: true,
            };
            if (exactFullTurn)
                continue;
            if (anchors[index].DistanceTo(anchors[index + 1]) <= SnapRadius)
                return RoadPathSubmissionError.CollapsedByNodeIdentity;

            GraphNode? existingA = FindClosestIndexedNode(anchors[index], SnapRadius);
            GraphNode? existingB = FindClosestIndexedNode(anchors[index + 1], SnapRadius);
            if (existingA is not null && existingA == existingB)
                return RoadPathSubmissionError.CollapsedByNodeIdentity;
        }

        return RoadPathSubmissionError.None;
    }

    private RoadPathSubmissionError ResolveNativeSegments(RoadPath path, out RoadGeometrySegment[] resolved)
    {
        resolved = new RoadGeometrySegment[path.Segments.Count];
        for (int index = 0; index < resolved.Length; index++)
        {
            RoadGeometrySegment source = path.Segments[index]!;
            Vector2 resolvedStart = FindClosestIndexedNode(source.Start, SnapRadius)?.Position ?? source.Start;
            Vector2 resolvedEnd = FindClosestIndexedNode(source.End, SnapRadius)?.Position ?? source.End;
            bool exactFullTurn = source is CircularArcRoadGeometrySegment { IsFullTurn: true };
            if (!exactFullTurn && resolvedStart.DistanceTo(resolvedEnd) <= SnapRadius)
                return RoadPathSubmissionError.CollapsedByNodeIdentity;
            if (!TrySnapGeometry(source, resolvedStart, resolvedEnd, out RoadGeometrySegment? snapped))
                return RoadPathSubmissionError.UnsupportedEndpointSnap;
            RoadGeometryCanonicalizationResult canonical =
                RoadGeometryCanonicalizer.Canonicalize([snapped]);
            snapped = canonical.GeometrySegments[0];
            if (RoadNumericPolicy.ValidateGeometry(snapped) != RoadNumericError.None)
                return RoadPathSubmissionError.NumericOutOfRange;
            if (index > 0 && resolved[index - 1].End != snapped.Start)
                return RoadPathSubmissionError.UnsupportedEndpointSnap;
            resolved[index] = snapped;
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
                geometry = CircularArcRoadGeometrySegment.CreateAnchored(
                    arc.Center + startDelta,
                    arc.Radius,
                    arc.StartAngle,
                    arc.SweepAngle,
                    start,
                    end,
                    arc.EndAngle);
                return true;
            case ClothoidRoadGeometrySegment clothoid when startDelta == endDelta:
                geometry = ClothoidRoadGeometrySegment.CreateAnchored(
                    start,
                    clothoid.StartHeading,
                    clothoid.StartCurvature,
                    clothoid.EndCurvature,
                    clothoid.ArcLength,
                    end,
                    clothoid.ReverseStartHeading);
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
        return FindCandidateGeometryRefs(geometry)
            .Select(fragment => (fragment.EdgeID, fragment.GeometryIndex))
            .Distinct()
            .Any(candidate =>
                _edges.TryGetValue(candidate.EdgeID, out GraphEdge? edge) &&
                edge.GeometrySegments.Count == 1 &&
                SaveJson.Serialize(RoadGeometrySerializer.ToData(
                    edge.GeometrySegments[candidate.GeometryIndex])) == serialized);
    }

    private bool IsNativeLineCovered(LineRoadGeometrySegment line)
    {
        Vector2 direction = line.End - line.Start;
        var intervals = new List<(float Start, float End)>();
        foreach (LineRoadGeometrySegment existing in FindCandidateGeometryRefs(line)
                     .Select(fragment => fragment.Geometry)
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
        RoadType roadType,
        int? edgeID = null,
        bool emitEvent = true)
    {
        IReadOnlyList<RoadGeometrySegment> anchoredGeometry =
            RoadGeometryCanonicalizer.ReanchorChain(
                geometrySegments,
                nodeA.Position,
                nodeB.Position);
        var edge = new GraphEdge(roadType, edgeID ?? NextID(), nodeA.ID, nodeB.ID, anchoredGeometry);
        AttachEdge(edge);

        return edge;
    }

    private void AttachEdge(GraphEdge edge)
    {
        _edges.Add(edge.ID, edge);
        TrackEdgeChange(edge.ID);
        if (edge.NodeA == edge.NodeB)
            _selfLoopCount++;
        _geometrySegmentCount += edge.GeometrySegments.Count;
        AdjustTotalGeometryLength(SumGeometryLength(edge));
        AttachEdgeIncidences(edge);
        InsertEdgeSpatialRefs(edge);
    }

    private static double SumGeometryLength(GraphEdge edge) =>
        edge.GeometrySegments.Sum(geometry => (double)geometry.Length);

    private void AdjustTotalGeometryLength(double delta)
    {
        double next = _totalGeometryLength + delta;
        if (!double.IsFinite(next) || next < -1e-6d)
            throw new InvalidOperationException("RoadGraph total geometry length became invalid.");
        _totalGeometryLength = next <= 0d ? 0d : next;
    }
}
