using Godot;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class RoadBuildRequestTests
{
    [Fact]
    public void Validate_AcceptsValidRequest()
    {
        var request = new RoadBuildRequest(
            new RoadPath([new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))]),
            RoadType.Street);

        request.Validate();
    }

    [Fact]
    public void Validate_RejectsUnknownRoadType()
    {
        var request = new RoadBuildRequest(
            new RoadPath([new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))]),
            (RoadType)999);

        Assert.Throws<ArgumentOutOfRangeException>(() => request.Validate());
    }

    [Fact]
    public void Validate_RejectsEmptyGeometry()
    {
        var request = new RoadBuildRequest(new RoadPath([]), RoadType.Street);

        Assert.Throws<ArgumentException>(() => request.Validate());
    }

    [Fact]
    public void Validate_RejectsNullSegment()
    {
        var request = new RoadBuildRequest(
            new RoadPath(new RoadGeometrySegment?[] { null }),
            RoadType.Street);

        Assert.Throws<ArgumentException>(() => request.Validate());
    }
}
