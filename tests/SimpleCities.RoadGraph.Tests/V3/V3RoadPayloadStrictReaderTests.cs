using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3RoadPayloadStrictReaderTests
{
    [Fact]
    public void Read_ValidPayload_Succeeds()
    {
        string json = CreateJson();

        V3StrictRoadPayloadResult result = V3RoadPayloadStrictReader.Read(json, V3PayloadBudget.Default);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Graph);
    }

    [Fact]
    public void Read_DuplicateKey_Fails()
    {
        const string json = """{"formatFamily":"simple-cities-v3","payloadType":"road-network","schemaVersion":1,"nextID":1,"nextID":2,"nodes":[],"edges":[]}""";

        V3StrictRoadPayloadResult result = V3RoadPayloadStrictReader.Read(json, V3PayloadBudget.Default);

        Assert.False(result.Success);
        Assert.StartsWith("DuplicateKey:", result.Error);
    }

    [Fact]
    public void Read_OverBudget_Fails()
    {
        string json = CreateJson();
        var budget = V3PayloadBudget.Default with { MaxNodes = 1 };

        V3StrictRoadPayloadResult result = V3RoadPayloadStrictReader.Read(json, budget);

        Assert.False(result.Success);
        Assert.Equal("EntityCountsExceeded", result.Error);
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
