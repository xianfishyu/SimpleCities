using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadSpatialIndexV3Tests
{
    [Fact]
    public void TryInsert_AddsFragmentAndQueryRectFindsIt()
    {
        var index = new RoadSpatialIndexV3(1f, RoadGraphCapacity.Default);
        var fragment = new RoadQueryFragment(7, 0, 0, 0f, 1f, new Rect2(0f, 0f, 1f, 1f));

        Assert.True(index.TryInsert(fragment));

        IReadOnlyList<RoadQueryFragment> result = index.QueryRect(new Rect2(0.2f, 0.2f, 0.5f, 0.5f));
        Assert.Single(result);
        Assert.Equal(fragment, result[0]);
    }

    [Fact]
    public void QueryRadius_ReturnsCandidatesInRange()
    {
        var index = new RoadSpatialIndexV3(1f, RoadGraphCapacity.Default);
        index.TryInsert(new RoadQueryFragment(1, 0, 0, 0f, 1f, new Rect2(0f, 0f, 1f, 1f)));
        index.TryInsert(new RoadQueryFragment(2, 0, 0, 0f, 1f, new Rect2(10f, 10f, 1f, 1f)));

        IReadOnlyList<RoadQueryFragment> result = index.QueryRadius(new Vector2(0.5f, 0.5f), 2f);

        Assert.Single(result);
        Assert.Equal(1, result[0].EdgeID);
    }

    [Fact]
    public void TryInsert_RejectsWhenFragmentCapacityExceeded()
    {
        var capacity = RoadGraphCapacity.Default with { MaxQueryFragments = 1 };
        var index = new RoadSpatialIndexV3(1f, capacity);
        var first = new RoadQueryFragment(1, 0, 0, 0f, 1f, new Rect2(0f, 0f, 1f, 1f));
        var second = new RoadQueryFragment(2, 0, 0, 0f, 1f, new Rect2(5f, 5f, 1f, 1f));

        Assert.True(index.TryInsert(first));
        Assert.False(index.TryInsert(second));
    }

    [Fact]
    public void TryInsert_RejectsWhenBucketReferencesExceeded()
    {
        var capacity = RoadGraphCapacity.Default with { MaxBucketReferences = 1 };
        var index = new RoadSpatialIndexV3(1f, capacity);
        var fragment = new RoadQueryFragment(1, 0, 0, 0f, 1f, new Rect2(0f, 0f, 2f, 2f));

        Assert.False(index.TryInsert(fragment));
    }

    [Fact]
    public void QueryRect_DoesNotReturnNonIntersectingFragment()
    {
        var index = new RoadSpatialIndexV3(1f, RoadGraphCapacity.Default);
        index.TryInsert(new RoadQueryFragment(1, 0, 0, 0f, 1f, new Rect2(0f, 0f, 1f, 1f)));

        IReadOnlyList<RoadQueryFragment> result = index.QueryRect(new Rect2(10f, 10f, 1f, 1f));

        Assert.Empty(result);
    }

    [Fact]
    public void Clear_ResetsMetrics()
    {
        var index = new RoadSpatialIndexV3(1f, RoadGraphCapacity.Default);
        index.TryInsert(new RoadQueryFragment(1, 0, 0, 0f, 1f, new Rect2(0f, 0f, 1f, 1f)));
        index.QueryRect(new Rect2(0f, 0f, 1f, 1f));

        index.Clear();

        Assert.Equal(0, index.FragmentCount);
        Assert.Equal(0, index.BucketCount);
        Assert.Equal(0, index.BucketReferenceCount);
        Assert.Equal(0, index.LastQueryCandidateCount);
    }
}
