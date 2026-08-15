using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadNodeSnapTests
{
    [Fact]
    public void SnapAll_ClosestNodeWithinRadius_SnapsToIt()
    {
        var nodes = new Dictionary<int, Vector2>
        {
            [1] = new(10f, 0f),
            [2] = new(1f, 0f),
        };

        IReadOnlyList<RoadSnappedAnchor> result = RoadNodeSnap.SnapAll(
            [new Vector2(1.2f, 0f)],
            nodes,
            0.5f);

        RoadSnappedAnchor anchor = Assert.Single(result);
        Assert.True(anchor.IsSnapped);
        Assert.Equal(2, anchor.NodeID);
        Assert.Equal(nodes[2], anchor.Position);
    }

    [Fact]
    public void SnapAll_EqualDistance_ChoosesSmallestNodeId()
    {
        var nodes = new Dictionary<int, Vector2>
        {
            [10] = new(1f, 0f),
            [5] = new(0f, 1f),
        };

        IReadOnlyList<RoadSnappedAnchor> result = RoadNodeSnap.SnapAll(
            [Vector2.Zero],
            nodes,
            2f);

        RoadSnappedAnchor anchor = Assert.Single(result);
        Assert.True(anchor.IsSnapped);
        Assert.Equal(5, anchor.NodeID);
    }

    [Fact]
    public void SnapAll_OutsideRadius_DoesNotSnap()
    {
        var nodes = new Dictionary<int, Vector2>
        {
            [1] = Vector2.Zero,
        };

        IReadOnlyList<RoadSnappedAnchor> result = RoadNodeSnap.SnapAll(
            [new Vector2(10f, 10f)],
            nodes,
            1f);

        RoadSnappedAnchor anchor = Assert.Single(result);
        Assert.False(anchor.IsSnapped);
        Assert.Null(anchor.NodeID);
        Assert.Equal(new Vector2(10f, 10f), anchor.Position);
    }

    [Fact]
    public void SnapAll_ResolvesEveryAnchorInOnePass()
    {
        var nodes = new Dictionary<int, Vector2>
        {
            [3] = new(0f, 0f),
            [7] = new(5f, 5f),
        };

        IReadOnlyList<RoadSnappedAnchor> result = RoadNodeSnap.SnapAll(
            [
                new Vector2(0.1f, 0f),
                new Vector2(100f, 100f),
                new Vector2(5f, 5f),
            ],
            nodes,
            1f);

        Assert.Equal(3, result.Count);
        Assert.Equal(3, result[0].NodeID);
        Assert.False(result[1].IsSnapped);
        Assert.Equal(7, result[2].NodeID);
    }

    [Fact]
    public void SnapAll_CheckedDoubleDistance_HandlesLargeFiniteCoordinates()
    {
        var nodes = new Dictionary<int, Vector2>
        {
            [1] = new(RoadNumericPolicy.MaxCoordinateMagnitude, 0f),
        };

        IReadOnlyList<RoadSnappedAnchor> result = RoadNodeSnap.SnapAll(
            [new Vector2(-RoadNumericPolicy.MaxCoordinateMagnitude, 0f)],
            nodes,
            RoadNumericPolicy.MaxCoordinateMagnitude * 3f);

        RoadSnappedAnchor anchor = Assert.Single(result);
        Assert.True(anchor.IsSnapped);
        Assert.Equal(1, anchor.NodeID);
    }

    [Fact]
    public void SnapAll_InvalidRadius_Throws()
    {
        var nodes = new Dictionary<int, Vector2>();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoadNodeSnap.SnapAll([Vector2.Zero], nodes, -1f));
    }
}
