using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.Text;

namespace SimpleCities.Tests.V3;

public sealed class V3RoadSlotFactoryTests
{
    [Fact]
    public void Create_BuildsValidSlot()
    {
        RoadGraphV3Revision revision = CreateRevision();
        V3RoadSlotBundle bundle = V3RoadSlotFactory.Create(
            "city-001",
            "河湾城",
            "河湾城",
            "2026-08-12T08:00:00.0000000Z",
            1200,
            50000m,
            null,
            revision);

        Assert.Equal("city-001", bundle.Manifest.SlotId);
        Assert.True(V3ManifestValidator.Validate(bundle.Manifest).Success);
        Assert.Contains(V3RoadSlotFactory.RoadNetworkFileName, bundle.Payloads.Keys);
    }

    [Fact]
    public void Payload_DeserializesToGraph()
    {
        RoadGraphV3Revision revision = CreateRevision();
        V3RoadSlotBundle bundle = V3RoadSlotFactory.Create(
            "city-001",
            "n",
            "n",
            "2026-08-12T08:00:00.0000000Z",
            null,
            null,
            null,
            revision);
        string json = Encoding.UTF8.GetString(bundle.Payloads[V3RoadSlotFactory.RoadNetworkFileName]);

        RoadGraphV3CodecResult result = RoadGraphV3Codec.Deserialize(json);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Graph);
    }

    private static RoadGraphV3Revision CreateRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        return revision;
    }
}
