using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadQueryFragmentTests
{
    [Fact]
    public void QueryFragment_ExposesFields()
    {
        var fragment = new RoadQueryFragment(
            7,
            1,
            2,
            0.25f,
            0.5f,
            new Rect2(0f, 0f, 1f, 1f));

        Assert.Equal(7, fragment.EdgeID);
        Assert.Equal(1, fragment.GeometryIndex);
        Assert.Equal(2, fragment.FragmentIndex);
        Assert.Equal(0.25f, fragment.ParameterStart);
        Assert.Equal(0.5f, fragment.ParameterEnd);
        Assert.Equal(new Rect2(0f, 0f, 1f, 1f), fragment.ConservativeBounds);
    }

    [Fact]
    public void NormalizeBoundary_PrimitiveJoin_MapsToNextStart()
    {
        RoadLocation location = RoadQueryOwnership.NormalizeBoundary(7, 0, 1f, 2, isSelfLoop: false);

        Assert.Equal(new RoadLocation(7, 1, 0f), location);
    }

    [Fact]
    public void NormalizeBoundary_SelfLoopSeam_MapsToFirstStart()
    {
        RoadLocation location = RoadQueryOwnership.NormalizeBoundary(7, 1, 1f, 2, isSelfLoop: true);

        Assert.Equal(new RoadLocation(7, 0, 0f), location);
    }

    [Fact]
    public void NormalizeBoundary_NonLoopEnd_StaysOnLastFragment()
    {
        RoadLocation location = RoadQueryOwnership.NormalizeBoundary(7, 1, 1f, 2, isSelfLoop: false);

        Assert.Equal(new RoadLocation(7, 1, 1f), location);
    }

    [Fact]
    public void NormalizeBoundary_Interior_StaysUnchanged()
    {
        RoadLocation location = RoadQueryOwnership.NormalizeBoundary(7, 1, 0.5f, 2, isSelfLoop: false);

        Assert.Equal(new RoadLocation(7, 1, 0.5f), location);
    }

    [Fact]
    public void NormalizeBoundary_RejectsOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoadQueryOwnership.NormalizeBoundary(7, 2, 0.5f, 2, false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoadQueryOwnership.NormalizeBoundary(7, 0, 1.1f, 2, false));
    }
}
