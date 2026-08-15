using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadIntersectionClusterTests
{
    [Fact]
    public void Cluster_GroupsCloseWitnessesTogether()
    {
        var witnesses = new IntersectionWitness[]
        {
            new("a", new Vector2(0f, 0f), null),
            new("b", new Vector2(0.001f, 0f), null),
            new("c", new Vector2(10f, 0f), null),
        };

        IntersectionClusterResult result = RoadIntersectionCluster.Cluster(witnesses, 0.01f, 1f);

        Assert.True(result.Success);
        Assert.Equal(2, result.Clusters.Count);
        Assert.Equal(2, result.Clusters[0].Witnesses.Count);
        Assert.Single(result.Clusters[1].Witnesses);
    }

    [Fact]
    public void Cluster_UniqueExistingNode_ReusesItsExactPosition()
    {
        var witnesses = new IntersectionWitness[]
        {
            new("a", new Vector2(2f, 3f), 7),
            new("b", new Vector2(2.001f, 3f), null),
        };

        IntersectionClusterResult result = RoadIntersectionCluster.Cluster(witnesses, 0.01f, 1f);

        Assert.True(result.Success);
        IntersectionCluster cluster = Assert.Single(result.Clusters);
        Assert.Equal(7, cluster.ExistingNodeID);
        Assert.Equal(new Vector2(2f, 3f), cluster.Position);
    }

    [Fact]
    public void Cluster_MultipleExistingNodes_ReturnsAmbiguous()
    {
        var witnesses = new IntersectionWitness[]
        {
            new("a", new Vector2(0f, 0f), 1),
            new("b", new Vector2(0.001f, 0f), 2),
        };

        IntersectionClusterResult result = RoadIntersectionCluster.Cluster(witnesses, 0.01f, 1f);

        Assert.False(result.Success);
        Assert.Equal("MultipleExistingNodes", result.Error);
    }

    [Fact]
    public void Cluster_NoExistingNode_ChoosesSmallestStableKey()
    {
        var witnesses = new IntersectionWitness[]
        {
            new("z", new Vector2(1.001f, 0f), null),
            new("a", new Vector2(1f, 0f), null),
        };

        IntersectionClusterResult result = RoadIntersectionCluster.Cluster(witnesses, 0.01f, 1f);

        Assert.True(result.Success);
        IntersectionCluster cluster = Assert.Single(result.Clusters);
        Assert.Null(cluster.ExistingNodeID);
        Assert.Equal(new Vector2(1f, 0f), cluster.Position);
    }

    [Fact]
    public void Cluster_ComponentDiameterTooLarge_ReturnsAmbiguous()
    {
        var witnesses = new IntersectionWitness[]
        {
            new("a", new Vector2(0f, 0f), null),
            new("b", new Vector2(0.005f, 0f), null),
            new("c", new Vector2(0.01f, 0f), null),
        };

        IntersectionClusterResult result = RoadIntersectionCluster.Cluster(witnesses, 0.006f, 0.008f);

        Assert.False(result.Success);
        Assert.Equal("ClusterDiameterExceeded", result.Error);
    }

    [Fact]
    public void Cluster_DeterministicRegardlessOfInputOrder()
    {
        var forward = new IntersectionWitness[]
        {
            new("a", new Vector2(0f, 0f), null),
            new("b", new Vector2(0.001f, 0f), null),
            new("c", new Vector2(10f, 0f), null),
        };
        var reversed = forward.Reverse().ToArray();

        IntersectionClusterResult first = RoadIntersectionCluster.Cluster(forward, 0.01f, 1f);
        IntersectionClusterResult second = RoadIntersectionCluster.Cluster(reversed, 0.01f, 1f);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.Clusters.Count, second.Clusters.Count);
        Assert.Equal(first.Clusters[0].Position, second.Clusters[0].Position);
        Assert.Equal(first.Clusters[0].Witnesses.Count, second.Clusters[0].Witnesses.Count);
    }

    [Fact]
    public void Cluster_EmptyInput_SucceedsWithNoClusters()
    {
        IntersectionClusterResult result = RoadIntersectionCluster.Cluster([], 0.01f, 1f);

        Assert.True(result.Success);
        Assert.Empty(result.Clusters);
    }
}
