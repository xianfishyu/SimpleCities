using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3StrictRoadPayloadReaderTests
{
    [Fact]
    public void Read_ValidJson_ReturnsGraph()
    {
        string json = CreateJson();

        V3StrictRoadPayloadResult result = V3StrictRoadPayloadReader.Read(json, V3PayloadBudget.Default);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Graph);
        Assert.NotNull(result.NextID);
    }

    [Fact]
    public void Read_RejectsOverBudgetCounts()
    {
        string json = CreateJson();
        var budget = V3PayloadBudget.Default with { MaxNodes = 1 };

        V3StrictRoadPayloadResult result = V3StrictRoadPayloadReader.Read(json, budget);

        Assert.False(result.Success);
        Assert.Equal("EntityCountsExceeded", result.Error);
    }

    [Fact]
    public void Read_RejectsInvalidJson()
    {
        V3StrictRoadPayloadResult result = V3StrictRoadPayloadReader.Read("not json", V3PayloadBudget.Default);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    private static string CreateJson()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        return RoadGraphV3Persistence.Serialize(revision);
    }
}
