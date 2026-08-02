using Godot;
using System;
using System.Collections.Generic;

public enum RoadGeometryIntersectionKind
{
    Crossing,
    Tangent,
    EndpointTouch,
}

public readonly record struct RoadGeometryIntersection(
    float FirstParameter,
    float SecondParameter,
    Vector2 Position,
    RoadGeometryIntersectionKind Kind);

public sealed class RoadGeometryIntersectionResult
{
    public IReadOnlyList<RoadGeometryIntersection> Intersections { get; }
    public bool HasOverlap { get; }

    public RoadGeometryIntersectionResult(
        IReadOnlyList<RoadGeometryIntersection> intersections,
        bool hasOverlap)
    {
        Intersections = Array.AsReadOnly([.. intersections]);
        HasOverlap = hasOverlap;
    }
}

public static class RoadGeometryIntersectionQuery
{
    private const int MaxSubdivisionDepth = 24;
    private const int MaxPairSubdivisionDepth = 48;
    private const float TangentCrossTolerance = 1e-3f;

    public static RoadGeometryIntersectionResult FindIntersections(
        RoadGeometrySegment first,
        RoadGeometrySegment second,
        float spatialTolerance = 1e-3f,
        float endpointParameterTolerance = 1e-4f)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ValidateTolerances(spatialTolerance, endpointParameterTolerance);

        if (first is LineRoadGeometrySegment firstLine)
            return FindLineIntersections(
                firstLine, second, spatialTolerance, endpointParameterTolerance);
        if (second is LineRoadGeometrySegment secondLine)
            return SwapResult(FindLineIntersections(
                secondLine, first, spatialTolerance, endpointParameterTolerance));
        if (RoadGeometrySerializer.Serialize(first) == RoadGeometrySerializer.Serialize(second))
            return new RoadGeometryIntersectionResult([], hasOverlap: true);

        var candidates = new List<RoadGeometryIntersection>();
        var stack = new Stack<PairSearchInterval>();
        stack.Push(new PairSearchInterval(first, 0f, 1f, second, 0f, 1f, 0));

        while (stack.TryPop(out PairSearchInterval interval))
        {
            if (!BoundsOverlap(
                    interval.First.Bounds,
                    interval.Second.Bounds,
                    spatialTolerance))
                continue;

            float firstSize = MaxDimension(interval.First.Bounds);
            float secondSize = MaxDimension(interval.Second.Bounds);
            bool converged = (firstSize <= spatialTolerance && secondSize <= spatialTolerance) ||
                             (interval.FirstParameterEnd - interval.FirstParameterStart <= 1e-6f &&
                              interval.SecondParameterEnd - interval.SecondParameterStart <= 1e-6f) ||
                             interval.Depth >= MaxPairSubdivisionDepth;
            if (converged)
            {
                TryAddPairLeafHit(
                    first,
                    second,
                    interval,
                    spatialTolerance,
                    endpointParameterTolerance,
                    candidates);
                continue;
            }

            if (firstSize >= secondSize)
            {
                RoadGeometrySplit split = interval.First.Split(0.5f);
                float midpoint = (interval.FirstParameterStart + interval.FirstParameterEnd) * 0.5f;
                stack.Push(interval.WithFirst(
                    split.After, midpoint, interval.FirstParameterEnd));
                stack.Push(interval.WithFirst(
                    split.Before, interval.FirstParameterStart, midpoint));
            }
            else
            {
                RoadGeometrySplit split = interval.Second.Split(0.5f);
                float midpoint = (interval.SecondParameterStart + interval.SecondParameterEnd) * 0.5f;
                stack.Push(interval.WithSecond(
                    split.After, midpoint, interval.SecondParameterEnd));
                stack.Push(interval.WithSecond(
                    split.Before, interval.SecondParameterStart, midpoint));
            }
        }

        List<RoadGeometryIntersection> intersections = CoalescePairHits(
            first,
            second,
            candidates,
            spatialTolerance,
            endpointParameterTolerance);
        intersections.Sort((left, right) =>
        {
            int firstOrder = left.FirstParameter.CompareTo(right.FirstParameter);
            return firstOrder != 0
                ? firstOrder
                : left.SecondParameter.CompareTo(right.SecondParameter);
        });
        return new RoadGeometryIntersectionResult(intersections, hasOverlap: false);
    }

    public static RoadGeometryIntersectionResult FindLineIntersections(
        LineRoadGeometrySegment line,
        RoadGeometrySegment geometry,
        float spatialTolerance = 1e-3f,
        float endpointParameterTolerance = 1e-4f)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(geometry);
        ValidateTolerances(spatialTolerance, endpointParameterTolerance);

        if (geometry is LineRoadGeometrySegment otherLine)
            return FindLineLine(line, otherLine, spatialTolerance, endpointParameterTolerance);

        Vector2 lineVector = line.End - line.Start;
        float lineLength = lineVector.Length();
        Vector2 lineDirection = lineVector / lineLength;
        Vector2 lineNormal = new(-lineDirection.Y, lineDirection.X);
        var hits = new List<RoadGeometryIntersection>();
        var stack = new Stack<SearchInterval>();
        stack.Push(new SearchInterval(geometry, 0f, 1f, 0));

        while (stack.TryPop(out SearchInterval interval))
        {
            if (!BoundsCanMeetLine(
                    interval.Geometry.Bounds,
                    line.Start,
                    lineDirection,
                    lineNormal,
                    lineLength,
                    spatialTolerance))
                continue;

            Vector2 boundsSize = interval.Geometry.Bounds.Size;
            bool converged = Mathf.Max(boundsSize.X, boundsSize.Y) <= spatialTolerance ||
                             interval.ParameterEnd - interval.ParameterStart <= 1e-6f ||
                             interval.Depth >= MaxSubdivisionDepth;
            if (converged)
            {
                TryAddLeafHit(
                    line,
                    geometry,
                    interval,
                    spatialTolerance,
                    endpointParameterTolerance,
                    hits);
                continue;
            }

            RoadGeometrySplit split = interval.Geometry.Split(0.5f);
            float midpoint = (interval.ParameterStart + interval.ParameterEnd) * 0.5f;
            stack.Push(new SearchInterval(
                split.After, midpoint, interval.ParameterEnd, interval.Depth + 1));
            stack.Push(new SearchInterval(
                split.Before, interval.ParameterStart, midpoint, interval.Depth + 1));
        }

        List<RoadGeometryIntersection> coalesced = CoalesceHits(
            line,
            geometry,
            hits,
            spatialTolerance,
            endpointParameterTolerance);
        coalesced.Sort((left, right) =>
        {
            int first = left.FirstParameter.CompareTo(right.FirstParameter);
            return first != 0 ? first : left.SecondParameter.CompareTo(right.SecondParameter);
        });
        return new RoadGeometryIntersectionResult(coalesced, hasOverlap: false);
    }

    private static RoadGeometryIntersectionResult FindLineLine(
        LineRoadGeometrySegment first,
        LineRoadGeometrySegment second,
        float spatialTolerance,
        float endpointParameterTolerance)
    {
        Vector2 r = first.End - first.Start;
        Vector2 s = second.End - second.Start;
        Vector2 offset = second.Start - first.Start;
        float cross = Cross(r, s);
        float crossTolerance = spatialTolerance * Mathf.Max(r.Length(), s.Length());

        if (Mathf.Abs(cross) > crossTolerance)
        {
            float firstParameter = Cross(offset, s) / cross;
            float secondParameter = Cross(offset, r) / cross;
            if (!ParameterWithinSegment(firstParameter, endpointParameterTolerance) ||
                !ParameterWithinSegment(secondParameter, endpointParameterTolerance))
                return new RoadGeometryIntersectionResult([], hasOverlap: false);

            firstParameter = Mathf.Clamp(firstParameter, 0f, 1f);
            secondParameter = Mathf.Clamp(secondParameter, 0f, 1f);
            var hit = CreateHit(
                first,
                second,
                firstParameter,
                secondParameter,
                endpointParameterTolerance);
            return new RoadGeometryIntersectionResult([hit], hasOverlap: false);
        }

        float distanceToFirstLine = Mathf.Abs(Cross(offset, r)) / r.Length();
        if (distanceToFirstLine > spatialTolerance)
            return new RoadGeometryIntersectionResult([], hasOverlap: false);

        float secondStartOnFirst = offset.Dot(r) / r.LengthSquared();
        float secondEndOnFirst = (second.End - first.Start).Dot(r) / r.LengthSquared();
        float overlapStart = Mathf.Max(0f, Mathf.Min(secondStartOnFirst, secondEndOnFirst));
        float overlapEnd = Mathf.Min(1f, Mathf.Max(secondStartOnFirst, secondEndOnFirst));
        float parameterSpatialTolerance = spatialTolerance / r.Length();
        if (overlapEnd < overlapStart - parameterSpatialTolerance)
            return new RoadGeometryIntersectionResult([], hasOverlap: false);

        if (overlapEnd - overlapStart > parameterSpatialTolerance)
            return new RoadGeometryIntersectionResult([], hasOverlap: true);

        float firstParameterAtTouch = Mathf.Clamp((overlapStart + overlapEnd) * 0.5f, 0f, 1f);
        Vector2 position = first.GetPosition(firstParameterAtTouch);
        float secondParameterAtTouch = Mathf.Clamp(
            (position - second.Start).Dot(s) / s.LengthSquared(), 0f, 1f);
        var endpointHit = new RoadGeometryIntersection(
            firstParameterAtTouch,
            secondParameterAtTouch,
            position,
            RoadGeometryIntersectionKind.EndpointTouch);
        return new RoadGeometryIntersectionResult([endpointHit], hasOverlap: false);
    }

    private static RoadGeometryIntersectionResult SwapResult(
        RoadGeometryIntersectionResult result)
    {
        var swapped = new List<RoadGeometryIntersection>(result.Intersections.Count);
        foreach (RoadGeometryIntersection intersection in result.Intersections)
        {
            swapped.Add(new RoadGeometryIntersection(
                intersection.SecondParameter,
                intersection.FirstParameter,
                intersection.Position,
                intersection.Kind));
        }
        return new RoadGeometryIntersectionResult(swapped, result.HasOverlap);
    }

    private static void TryAddPairLeafHit(
        RoadGeometrySegment first,
        RoadGeometrySegment second,
        PairSearchInterval interval,
        float spatialTolerance,
        float endpointParameterTolerance,
        List<RoadGeometryIntersection> candidates)
    {
        float firstParameter =
            (interval.FirstParameterStart + interval.FirstParameterEnd) * 0.5f;
        float secondParameter =
            (interval.SecondParameterStart + interval.SecondParameterEnd) * 0.5f;
        Vector2 firstPosition = first.GetPosition(firstParameter);
        Vector2 secondPosition = second.GetPosition(secondParameter);
        if (firstPosition.DistanceSquaredTo(secondPosition) > spatialTolerance * spatialTolerance)
            return;

        candidates.Add(CreateHit(
            first,
            second,
            firstParameter,
            secondParameter,
            endpointParameterTolerance));
    }

    private static List<RoadGeometryIntersection> CoalescePairHits(
        RoadGeometrySegment first,
        RoadGeometrySegment second,
        List<RoadGeometryIntersection> candidates,
        float spatialTolerance,
        float endpointParameterTolerance)
    {
        if (candidates.Count <= 1)
            return candidates;

        float firstTolerance = ParameterClusterTolerance(first, spatialTolerance);
        float secondTolerance = ParameterClusterTolerance(second, spatialTolerance);
        var visited = new bool[candidates.Count];
        var result = new List<RoadGeometryIntersection>();
        var queue = new Queue<int>();

        for (int start = 0; start < candidates.Count; start++)
        {
            if (visited[start]) continue;
            visited[start] = true;
            queue.Enqueue(start);
            RoadGeometryIntersection best = candidates[start];
            float bestResidual = IntersectionResidualSquared(first, second, best);

            while (queue.TryDequeue(out int current))
            {
                RoadGeometryIntersection currentHit = candidates[current];
                float residual = IntersectionResidualSquared(first, second, currentHit);
                if (residual < bestResidual)
                {
                    best = currentHit;
                    bestResidual = residual;
                }

                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    if (visited[candidateIndex]) continue;
                    RoadGeometryIntersection candidate = candidates[candidateIndex];
                    if (Mathf.Abs(candidate.FirstParameter - currentHit.FirstParameter) <= firstTolerance &&
                        Mathf.Abs(candidate.SecondParameter - currentHit.SecondParameter) <= secondTolerance)
                    {
                        visited[candidateIndex] = true;
                        queue.Enqueue(candidateIndex);
                    }
                }
            }

            result.Add(CreateHit(
                first,
                second,
                best.FirstParameter,
                best.SecondParameter,
                endpointParameterTolerance));
        }

        return result;
    }

    private static bool BoundsCanMeetLine(
        Rect2 bounds,
        Vector2 lineStart,
        Vector2 lineDirection,
        Vector2 lineNormal,
        float lineLength,
        float tolerance)
    {
        Vector2[] corners =
        [
            bounds.Position,
            new Vector2(bounds.End.X, bounds.Position.Y),
            bounds.End,
            new Vector2(bounds.Position.X, bounds.End.Y),
        ];
        float minNormal = float.PositiveInfinity;
        float maxNormal = float.NegativeInfinity;
        float minAlong = float.PositiveInfinity;
        float maxAlong = float.NegativeInfinity;
        foreach (Vector2 corner in corners)
        {
            Vector2 relative = corner - lineStart;
            float normal = relative.Dot(lineNormal);
            float along = relative.Dot(lineDirection);
            minNormal = Mathf.Min(minNormal, normal);
            maxNormal = Mathf.Max(maxNormal, normal);
            minAlong = Mathf.Min(minAlong, along);
            maxAlong = Mathf.Max(maxAlong, along);
        }

        return minNormal <= tolerance && maxNormal >= -tolerance &&
               minAlong <= lineLength + tolerance && maxAlong >= -tolerance;
    }

    private static void TryAddLeafHit(
        LineRoadGeometrySegment line,
        RoadGeometrySegment geometry,
        SearchInterval interval,
        float spatialTolerance,
        float endpointParameterTolerance,
        List<RoadGeometryIntersection> hits)
    {
        float secondParameter = (interval.ParameterStart + interval.ParameterEnd) * 0.5f;
        Vector2 curvePosition = geometry.GetPosition(secondParameter);
        Vector2 lineVector = line.End - line.Start;
        float firstParameter = (curvePosition - line.Start).Dot(lineVector) / lineVector.LengthSquared();
        if (!ParameterWithinSegment(
                firstParameter,
                spatialTolerance / lineVector.Length()))
            return;

        firstParameter = Mathf.Clamp(firstParameter, 0f, 1f);
        Vector2 linePosition = line.GetPosition(firstParameter);
        if (linePosition.DistanceSquaredTo(curvePosition) > spatialTolerance * spatialTolerance)
            return;

        var hit = CreateHit(
            line,
            geometry,
            firstParameter,
            secondParameter,
            endpointParameterTolerance);
        float duplicateDistanceSquared = spatialTolerance * spatialTolerance * 4f;
        if (hits.Exists(existing =>
                existing.Position.DistanceSquaredTo(hit.Position) <= duplicateDistanceSquared))
            return;
        hits.Add(hit);
    }

    private static List<RoadGeometryIntersection> CoalesceHits(
        LineRoadGeometrySegment line,
        RoadGeometrySegment geometry,
        List<RoadGeometryIntersection> hits,
        float spatialTolerance,
        float endpointParameterTolerance)
    {
        if (hits.Count <= 1)
            return hits;

        hits.Sort((left, right) => left.SecondParameter.CompareTo(right.SecondParameter));
        float parameterTolerance = Mathf.Max(
            2e-6f,
            spatialTolerance / Mathf.Max(geometry.Length, spatialTolerance) * 8f);
        var result = new List<RoadGeometryIntersection>();
        RoadGeometryIntersection best = hits[0];
        float bestResidual = IntersectionResidualSquared(line, geometry, best);
        float previousParameter = best.SecondParameter;

        for (int i = 1; i < hits.Count; i++)
        {
            RoadGeometryIntersection candidate = hits[i];
            if (candidate.SecondParameter - previousParameter > parameterTolerance)
            {
                result.Add(CreateHit(
                    line,
                    geometry,
                    best.FirstParameter,
                    best.SecondParameter,
                    endpointParameterTolerance));
                best = candidate;
                bestResidual = IntersectionResidualSquared(line, geometry, candidate);
            }
            else
            {
                float residual = IntersectionResidualSquared(line, geometry, candidate);
                if (residual < bestResidual)
                {
                    best = candidate;
                    bestResidual = residual;
                }
            }
            previousParameter = candidate.SecondParameter;
        }

        result.Add(CreateHit(
            line,
            geometry,
            best.FirstParameter,
            best.SecondParameter,
            endpointParameterTolerance));
        return result;
    }

    private static float IntersectionResidualSquared(
        RoadGeometrySegment first,
        RoadGeometrySegment second,
        RoadGeometryIntersection intersection) =>
        first.GetPosition(intersection.FirstParameter).DistanceSquaredTo(
            second.GetPosition(intersection.SecondParameter));

    private static bool BoundsOverlap(Rect2 first, Rect2 second, float tolerance) =>
        first.Position.X <= second.End.X + tolerance &&
        first.End.X + tolerance >= second.Position.X &&
        first.Position.Y <= second.End.Y + tolerance &&
        first.End.Y + tolerance >= second.Position.Y;

    private static float MaxDimension(Rect2 bounds) =>
        Mathf.Max(bounds.Size.X, bounds.Size.Y);

    private static float ParameterClusterTolerance(
        RoadGeometrySegment geometry,
        float spatialTolerance) =>
        Mathf.Max(2e-6f, spatialTolerance / Mathf.Max(geometry.Length, spatialTolerance) * 8f);

    private static RoadGeometryIntersection CreateHit(
        RoadGeometrySegment first,
        RoadGeometrySegment second,
        float firstParameter,
        float secondParameter,
        float endpointParameterTolerance)
    {
        Vector2 firstPosition = first.GetPosition(firstParameter);
        Vector2 secondPosition = second.GetPosition(secondParameter);
        bool endpoint = IsEndpoint(firstParameter, endpointParameterTolerance) ||
                        IsEndpoint(secondParameter, endpointParameterTolerance);
        float tangentCross = Mathf.Abs(Cross(
            first.GetUnitTangent(firstParameter),
            second.GetUnitTangent(secondParameter)));
        RoadGeometryIntersectionKind kind = endpoint
            ? RoadGeometryIntersectionKind.EndpointTouch
            : tangentCross <= TangentCrossTolerance
                ? RoadGeometryIntersectionKind.Tangent
                : RoadGeometryIntersectionKind.Crossing;
        return new RoadGeometryIntersection(
            firstParameter,
            secondParameter,
            (firstPosition + secondPosition) * 0.5f,
            kind);
    }

    private static void ValidateTolerances(float spatialTolerance, float endpointParameterTolerance)
    {
        if (!float.IsFinite(spatialTolerance) || spatialTolerance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(spatialTolerance));
        if (!float.IsFinite(endpointParameterTolerance) ||
            endpointParameterTolerance < 0f || endpointParameterTolerance >= 0.5f)
            throw new ArgumentOutOfRangeException(nameof(endpointParameterTolerance));
    }

    private static bool ParameterWithinSegment(float parameter, float tolerance) =>
        parameter >= -tolerance && parameter <= 1f + tolerance;

    private static bool IsEndpoint(float parameter, float tolerance) =>
        parameter <= tolerance || parameter >= 1f - tolerance;

    private static float Cross(Vector2 left, Vector2 right) =>
        left.X * right.Y - left.Y * right.X;

    private readonly record struct SearchInterval(
        RoadGeometrySegment Geometry,
        float ParameterStart,
        float ParameterEnd,
        int Depth);

    private readonly record struct PairSearchInterval(
        RoadGeometrySegment First,
        float FirstParameterStart,
        float FirstParameterEnd,
        RoadGeometrySegment Second,
        float SecondParameterStart,
        float SecondParameterEnd,
        int Depth)
    {
        public PairSearchInterval WithFirst(
            RoadGeometrySegment geometry,
            float parameterStart,
            float parameterEnd) =>
            new(
                geometry,
                parameterStart,
                parameterEnd,
                Second,
                SecondParameterStart,
                SecondParameterEnd,
                Depth + 1);

        public PairSearchInterval WithSecond(
            RoadGeometrySegment geometry,
            float parameterStart,
            float parameterEnd) =>
            new(
                First,
                FirstParameterStart,
                FirstParameterEnd,
                geometry,
                parameterStart,
                parameterEnd,
                Depth + 1);
    }
}
