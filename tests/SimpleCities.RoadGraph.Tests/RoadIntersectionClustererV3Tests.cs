using Godot;

public sealed class RoadIntersectionClustererV3Tests
{
    [Fact]
    public void Cluster_IsStableAcrossEnumerationAndPairOrder()
    {
        RoadIntersectionWitness[] witnesses =
        [
            Witness(12, 0, 0.25f, 3, 1, 0.75f, new Vector2(2f, 4f), new Vector2(2.00002f, 4f)),
            Witness(14, 0, 0.5f, 5, 0, 0.5f, new Vector2(2.00001f, 4f), new Vector2(2f, 4f)),
        ];
        RoadIntersectionWitness swapped = new(
            witnesses[0].Second,
            witnesses[0].First,
            witnesses[0].SecondPosition,
            witnesses[0].FirstPosition);

        RoadIntersectionClusterResult first = RoadIntersectionClusterer.Cluster(witnesses);
        RoadIntersectionClusterResult second = RoadIntersectionClusterer.Cluster(
            [witnesses[1], swapped]);

        RoadIntersectionCluster firstCluster = Assert.Single(first.Clusters);
        RoadIntersectionCluster secondCluster = Assert.Single(second.Clusters);
        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(RoadExactPredicates.SameBits(firstCluster.Position, secondCluster.Position));
    }

    [Fact]
    public void Cluster_RejectsChainWhoseTransitiveDiameterExceedsLimit()
    {
        float step = RoadNumericPolicy.IntersectionClusterEpsilon * 0.75f;
        RoadIntersectionClusterResult result = RoadIntersectionClusterer.Cluster(
        [
            WitnessAt(0, new Vector2(0f, 0f)),
            WitnessAt(1, new Vector2(step, 0f)),
            WitnessAt(2, new Vector2(step * 2f, 0f)),
        ]);

        Assert.Equal(RoadIntersectionClusterError.AmbiguousIntersection, result.Error);
        Assert.Empty(result.Clusters);
    }

    [Fact]
    public void Cluster_UsesUniqueExistingNodeCoordinateExactly()
    {
        Vector2 existingPosition = new(8f, 9f);
        RoadIntersectionWitness witness = WitnessAt(0, new Vector2(8.00002f, 9f)) with
        {
            ExistingNodeID = 42,
            ExistingNodePosition = existingPosition,
        };

        RoadIntersectionCluster cluster = Assert.Single(
            RoadIntersectionClusterer.Cluster([witness]).Clusters);

        Assert.Equal(42, cluster.ExistingNodeID);
        Assert.True(RoadExactPredicates.SameBits(existingPosition, cluster.Position));
    }

    [Fact]
    public void Cluster_DoesNotSnapToNearbyNodeWithoutExplicitProvenance()
    {
        Vector2 candidate = new(8.00002f, 9f);

        RoadIntersectionCluster cluster = Assert.Single(
            RoadIntersectionClusterer.Cluster([WitnessAt(0, candidate)]).Clusters);

        Assert.Null(cluster.ExistingNodeID);
        Assert.True(RoadExactPredicates.SameBits(candidate, cluster.Position));
        Assert.False(RoadExactPredicates.SameBits(new Vector2(8f, 9f), cluster.Position));
    }

    [Fact]
    public void Cluster_RejectsTwoDifferentExistingNodeIdentities()
    {
        RoadIntersectionWitness first = WitnessAt(0, Vector2.Zero) with
        {
            ExistingNodeID = 1,
            ExistingNodePosition = Vector2.Zero,
        };
        RoadIntersectionWitness second = WitnessAt(1, new Vector2(0.00001f, 0f)) with
        {
            ExistingNodeID = 2,
            ExistingNodePosition = new Vector2(0.00001f, 0f),
        };

        RoadIntersectionClusterResult result = RoadIntersectionClusterer.Cluster([first, second]);

        Assert.Equal(RoadIntersectionClusterError.AmbiguousIntersection, result.Error);
    }

    [Fact]
    public void Cluster_RejectsExistingNodeBeyondRepresentativeOffset()
    {
        RoadIntersectionWitness witness = WitnessAt(0, Vector2.Zero) with
        {
            ExistingNodeID = 1,
            ExistingNodePosition = new Vector2(
                MathF.BitIncrement(RoadNumericPolicy.MaximumIntersectionClusterDiameter),
                0f),
        };

        RoadIntersectionClusterResult result = RoadIntersectionClusterer.Cluster([witness]);

        Assert.Equal(RoadIntersectionClusterError.AmbiguousIntersection, result.Error);
    }

    [Fact]
    public void Cluster_RejectsWitnessCountAboveCapacity()
    {
        var capacity = new RoadGraphCapacity
        {
            MaximumIntersectionWitnesses = 1,
        };

        RoadIntersectionClusterResult result = RoadIntersectionClusterer.Cluster(
            [WitnessAt(0, Vector2.Zero), WitnessAt(1, new Vector2(2f, 0f))],
            capacity);

        Assert.Equal(RoadIntersectionClusterError.CapacityExceeded, result.Error);
    }

    private static RoadIntersectionWitness WitnessAt(int ownerID, Vector2 position) =>
        Witness(ownerID, 0, 0.5f, ownerID + 100, 0, 0.5f, position, position);

    private static RoadIntersectionWitness Witness(
        int firstOwner,
        int firstGeometry,
        float firstParameter,
        int secondOwner,
        int secondGeometry,
        float secondParameter,
        Vector2 firstPosition,
        Vector2 secondPosition) =>
        new(
            new RoadIntersectionSource(
                RoadIntersectionSourceKind.Existing,
                firstOwner,
                firstGeometry,
                firstParameter),
            new RoadIntersectionSource(
                RoadIntersectionSourceKind.Incoming,
                secondOwner,
                secondGeometry,
                secondParameter),
            firstPosition,
            secondPosition);
}
