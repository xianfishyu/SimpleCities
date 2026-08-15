using Godot;
using SimpleCities.Core.V3;
using SimpleCities.Road.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotLoadServiceTests
{
    [Fact]
    public void Load_RoundTripsRevision()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            V3RoadSlotBundle bundle = V3RoadSlotFactory.Create("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null, revision);
            var store = new V3FileSlotStore(root);
            Assert.True(store.Save("city-001", bundle.Manifest, bundle.Payloads));

            V3SlotLoadServiceResult result = V3SlotLoadService.Load("city-001", root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.True(result.Success, result.Error);
            Assert.Equal(revision.Nodes.Count, result.Revision!.Nodes.Count);
            Assert.Equal(revision.Edges.Count, result.Revision.Edges.Count);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_MissingSlot_Fails()
    {
        string root = GetTempRoot();
        try
        {
            V3SlotLoadServiceResult result = V3SlotLoadService.Load("missing", root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_CorruptPayload_Fails()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            V3RoadSlotBundle bundle = V3RoadSlotFactory.Create("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null, revision);
            var store = new V3FileSlotStore(root);
            store.Save("city-001", bundle.Manifest, bundle.Payloads);
            File.WriteAllText(Path.Combine(root, "city-001", V3RoadSlotFactory.RoadNetworkFileName), "corrupt");

            V3SlotLoadServiceResult result = V3SlotLoadService.Load("city-001", root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Load_PayloadDigestMismatch_Fails()
    {
        string root = GetTempRoot();
        try
        {
            RoadGraphV3Revision revision = CreateRevision();
            V3RoadSlotBundle bundle = V3RoadSlotFactory.Create("city-001", "n", "n", "2026-08-12T08:00:00.0000000Z", null, null, null, revision);
            var store = new V3FileSlotStore(root);
            store.Save("city-001", bundle.Manifest, bundle.Payloads);

            RoadGraphV3Revision modified = CreateRevisionWithExtraNode();
            File.WriteAllText(
                Path.Combine(root, "city-001", V3RoadSlotFactory.RoadNetworkFileName),
                RoadGraphV3Persistence.Serialize(modified));

            V3SlotLoadServiceResult result = V3SlotLoadService.Load("city-001", root, RoadGraphCapacity.Default, V3PayloadBudget.Default);

            Assert.False(result.Success);
            Assert.Equal("PayloadDigestMismatch:road_network.json", result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-loadsvc-{Guid.NewGuid():N}");

    private static void Cleanup(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }

    private static RoadGraphV3Revision CreateRevision()
    {
        RoadGraphV3Revision revision = RoadGraphV3Revision.Empty(RoadGraphCapacity.Default);
        revision.TryAddNode(Vector2.Zero, out revision, out int a);
        revision.TryAddNode(new Vector2(1f, 0f), out revision, out int b);
        revision.TryAddEdge(a, b, [new LineRoadGeometrySegment(Vector2.Zero, new Vector2(1f, 0f))], RoadType.Street, out revision, out _);
        return revision;
    }

    private static RoadGraphV3Revision CreateRevisionWithExtraNode()
    {
        RoadGraphV3Revision revision = CreateRevision();
        revision.TryAddNode(new Vector2(2f, 0f), out revision, out _);
        return revision;
    }
}
