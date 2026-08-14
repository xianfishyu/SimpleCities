using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

internal enum RoadIntersectionSourceKind
{
    Existing = 0,
    Incoming = 1,
}

internal readonly record struct RoadIntersectionSource(
    RoadIntersectionSourceKind Kind,
    int OwnerID,
    int GeometryIndex,
    float Parameter);

internal readonly record struct RoadIntersectionWitness(
    RoadIntersectionSource First,
    RoadIntersectionSource Second,
    Vector2 FirstPosition,
    Vector2 SecondPosition,
    int? ExistingNodeID = null,
    Vector2 ExistingNodePosition = default);

internal readonly record struct RoadIntersectionCluster(
    Vector2 Position,
    int? ExistingNodeID,
    IReadOnlyList<RoadIntersectionWitness> Witnesses);

internal enum RoadIntersectionClusterError
{
    None,
    NumericOutOfRange,
    CapacityExceeded,
    AmbiguousIntersection,
}

internal readonly record struct RoadIntersectionClusterResult(
    RoadIntersectionClusterError Error,
    IReadOnlyList<RoadIntersectionCluster> Clusters)
{
    internal bool Success => Error == RoadIntersectionClusterError.None;
}

internal static class RoadIntersectionClusterer
{
    private static readonly ReadOnlyCollection<RoadIntersectionCluster> EmptyClusters =
        Array.AsReadOnly(Array.Empty<RoadIntersectionCluster>());

    internal static RoadIntersectionClusterResult Cluster(
        IEnumerable<RoadIntersectionWitness> witnesses,
        RoadGraphCapacity? capacity = null)
    {
        ArgumentNullException.ThrowIfNull(witnesses);
        capacity ??= RoadGraphCapacity.Default;

        RoadIntersectionWitness[] values = witnesses.ToArray();
        if (values.Length > capacity.MaximumIntersectionWitnesses)
            return Failure(RoadIntersectionClusterError.CapacityExceeded);

        var candidates = new Candidate[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            if (!TryCreateCandidate(values[index], out candidates[index]))
                return Failure(RoadIntersectionClusterError.NumericOutOfRange);
        }
        Array.Sort(candidates, CandidateComparer.Instance);

        var sets = new DisjointSet(candidates.Length);
        double epsilonSquared =
            (double)RoadNumericPolicy.IntersectionClusterEpsilon *
            RoadNumericPolicy.IntersectionClusterEpsilon;
        for (int first = 0; first < candidates.Length; first++)
        {
            for (int second = first + 1; second < candidates.Length; second++)
            {
                double dx = (double)candidates[second].Position.X - candidates[first].Position.X;
                if (dx > RoadNumericPolicy.IntersectionClusterEpsilon)
                    break;
                if (RoadNumericPolicy.DistanceSquared(
                        candidates[first].Position,
                        candidates[second].Position) <= epsilonSquared)
                {
                    sets.Union(first, second);
                }
            }
        }

        var components = new SortedDictionary<int, List<Candidate>>();
        for (int index = 0; index < candidates.Length; index++)
        {
            int root = sets.Find(index);
            if (!components.TryGetValue(root, out List<Candidate>? component))
                components[root] = component = new List<Candidate>();
            component.Add(candidates[index]);
        }

        var clusters = new List<RoadIntersectionCluster>(components.Count);
        double maximumDiameterSquared =
            (double)RoadNumericPolicy.MaximumIntersectionClusterDiameter *
            RoadNumericPolicy.MaximumIntersectionClusterDiameter;
        foreach (List<Candidate> component in components.Values)
        {
            if (component.Count > capacity.MaximumIntersectionClusterWitnesses)
                return Failure(RoadIntersectionClusterError.CapacityExceeded);
            if (HasDiameterAbove(component, maximumDiameterSquared))
                return Failure(RoadIntersectionClusterError.AmbiguousIntersection);

            int? existingNodeID = null;
            Vector2 existingPosition = default;
            foreach (Candidate candidate in component)
            {
                if (candidate.Witness.ExistingNodeID is not int nodeID)
                    continue;
                Vector2 nodePosition = RoadNumericPolicy.Canonicalize(
                    candidate.Witness.ExistingNodePosition);
                if (!RoadNumericPolicy.IsWithinCoordinateRange(nodePosition))
                    return Failure(RoadIntersectionClusterError.NumericOutOfRange);
                if (existingNodeID is int priorNodeID && priorNodeID != nodeID)
                    return Failure(RoadIntersectionClusterError.AmbiguousIntersection);
                if (existingNodeID == nodeID && !RoadExactPredicates.SameBits(existingPosition, nodePosition))
                    return Failure(RoadIntersectionClusterError.AmbiguousIntersection);
                existingNodeID = nodeID;
                existingPosition = nodePosition;
            }

            if (existingNodeID.HasValue && component.Any(candidate =>
                    RoadNumericPolicy.DistanceSquared(candidate.Position, existingPosition) >
                    maximumDiameterSquared))
            {
                return Failure(RoadIntersectionClusterError.AmbiguousIntersection);
            }

            Vector2 position = existingNodeID.HasValue
                ? existingPosition
                : component[0].Position;
            RoadIntersectionWitness[] orderedWitnesses = component
                .Select(candidate => candidate.Witness)
                .ToArray();
            clusters.Add(new RoadIntersectionCluster(
                position,
                existingNodeID,
                Array.AsReadOnly(orderedWitnesses)));
        }

        clusters.Sort((left, right) => ComparePositions(left.Position, right.Position));
        return new RoadIntersectionClusterResult(
            RoadIntersectionClusterError.None,
            clusters.AsReadOnly());
    }

    private static bool TryCreateCandidate(
        RoadIntersectionWitness witness,
        out Candidate candidate)
    {
        candidate = default;
        if (!RoadNumericPolicy.IsWithinCoordinateRange(witness.FirstPosition) ||
            !RoadNumericPolicy.IsWithinCoordinateRange(witness.SecondPosition) ||
            !IsValidSource(witness.First) || !IsValidSource(witness.Second))
        {
            return false;
        }

        RoadIntersectionSource first = witness.First;
        RoadIntersectionSource second = witness.Second;
        if (CompareSources(first, second) > 0)
            (first, second) = (second, first);

        float x = RoadNumericPolicy.Canonicalize(
            (float)(((double)witness.FirstPosition.X + witness.SecondPosition.X) * 0.5d));
        float y = RoadNumericPolicy.Canonicalize(
            (float)(((double)witness.FirstPosition.Y + witness.SecondPosition.Y) * 0.5d));
        Vector2 position = new(x, y);
        if (!RoadNumericPolicy.IsWithinCoordinateRange(position))
            return false;

        candidate = new Candidate(witness, first, second, position);
        return true;
    }

    private static bool IsValidSource(RoadIntersectionSource source) =>
        source.OwnerID >= 0 && source.GeometryIndex >= 0 &&
        float.IsFinite(source.Parameter) && source.Parameter >= 0f && source.Parameter <= 1f;

    private static bool HasDiameterAbove(List<Candidate> component, double maximumSquared)
    {
        for (int first = 0; first < component.Count; first++)
        for (int second = first + 1; second < component.Count; second++)
        {
            if (RoadNumericPolicy.DistanceSquared(
                    component[first].Position,
                    component[second].Position) > maximumSquared)
            {
                return true;
            }
        }
        return false;
    }

    private static int CompareSources(RoadIntersectionSource left, RoadIntersectionSource right)
    {
        int comparison = left.Kind.CompareTo(right.Kind);
        if (comparison != 0) return comparison;
        comparison = left.OwnerID.CompareTo(right.OwnerID);
        if (comparison != 0) return comparison;
        comparison = left.GeometryIndex.CompareTo(right.GeometryIndex);
        if (comparison != 0) return comparison;
        return CompareFloats(left.Parameter, right.Parameter);
    }

    private static int ComparePositions(Vector2 left, Vector2 right)
    {
        int comparison = CompareFloats(left.X, right.X);
        return comparison != 0 ? comparison : CompareFloats(left.Y, right.Y);
    }

    private static int CompareFloats(float left, float right)
    {
        int leftBits = BitConverter.SingleToInt32Bits(RoadNumericPolicy.Canonicalize(left));
        int rightBits = BitConverter.SingleToInt32Bits(RoadNumericPolicy.Canonicalize(right));
        uint leftKey = leftBits < 0 ? ~(uint)leftBits : (uint)leftBits | 0x80000000u;
        uint rightKey = rightBits < 0 ? ~(uint)rightBits : (uint)rightBits | 0x80000000u;
        return leftKey.CompareTo(rightKey);
    }

    private static RoadIntersectionClusterResult Failure(RoadIntersectionClusterError error) =>
        new(error, EmptyClusters);

    private readonly record struct Candidate(
        RoadIntersectionWitness Witness,
        RoadIntersectionSource First,
        RoadIntersectionSource Second,
        Vector2 Position);

    private sealed class CandidateComparer : IComparer<Candidate>
    {
        internal static CandidateComparer Instance { get; } = new();

        public int Compare(Candidate left, Candidate right)
        {
            int comparison = CompareSources(left.First, right.First);
            if (comparison != 0) return comparison;
            comparison = CompareSources(left.Second, right.Second);
            if (comparison != 0) return comparison;
            return ComparePositions(left.Position, right.Position);
        }
    }

    private sealed class DisjointSet
    {
        private readonly int[] _parents;
        private readonly byte[] _ranks;

        internal DisjointSet(int count)
        {
            _parents = new int[count];
            _ranks = new byte[count];
            for (int index = 0; index < count; index++)
                _parents[index] = index;
        }

        internal int Find(int value)
        {
            while (_parents[value] != value)
            {
                _parents[value] = _parents[_parents[value]];
                value = _parents[value];
            }
            return value;
        }

        internal void Union(int first, int second)
        {
            int firstRoot = Find(first);
            int secondRoot = Find(second);
            if (firstRoot == secondRoot) return;

            if (_ranks[firstRoot] < _ranks[secondRoot])
                _parents[firstRoot] = secondRoot;
            else if (_ranks[firstRoot] > _ranks[secondRoot])
                _parents[secondRoot] = firstRoot;
            else
            {
                _parents[secondRoot] = firstRoot;
                _ranks[firstRoot]++;
            }
        }
    }
}
