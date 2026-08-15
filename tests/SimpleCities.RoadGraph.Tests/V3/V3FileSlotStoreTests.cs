using System.IO;
using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3FileSlotStoreTests
{
    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        string root = GetTempRoot();
        try
        {
            var store = new V3FileSlotStore(root);
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("city-001", data);
            var payloads = new Dictionary<string, byte[]> { ["road_network.json"] = data };

            Assert.True(store.Save("city-001", manifest, payloads));
            V3SlotReadResult result = store.Load("city-001");

            Assert.True(result.Success, result.Error);
            Assert.Equal("city-001", result.Manifest!.SlotId);
            Assert.Equal(data, result.Payloads!["road_network.json"]);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Delete_RemovesSlot()
    {
        string root = GetTempRoot();
        try
        {
            var store = new V3FileSlotStore(root);
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("city-001", data);
            store.Save("city-001", manifest, new Dictionary<string, byte[]> { ["road_network.json"] = data });

            Assert.True(store.Delete("city-001"));
            Assert.False(store.Load("city-001").Success);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void List_ReturnsSavedSlots()
    {
        string root = GetTempRoot();
        try
        {
            var store = new V3FileSlotStore(root);
            byte[] data = "road-network"u8.ToArray();
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });
            store.Save("city-002", CreateManifest("city-002", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });

            IReadOnlyList<V3SlotSummary> list = store.List();

            Assert.Equal(2, list.Count);
            Assert.All(list, summary => Assert.True(summary.IsUsable));
            Assert.All(list, summary => Assert.Equal(summary.SlotId, summary.DisplayName));
            Assert.All(list, summary => Assert.Equal("2026-08-12T08:00:00.0000000Z", summary.Timestamp));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void List_CorruptPayload_ReturnsCorruptV3()
    {
        string root = GetTempRoot();
        try
        {
            var store = new V3FileSlotStore(root);
            byte[] data = "road-network"u8.ToArray();
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });
            File.WriteAllBytes(Path.Combine(root, "city-001", "road_network.json"), "other"u8.ToArray());

            IReadOnlyList<V3SlotSummary> list = store.List();

            V3SlotSummary summary = Assert.Single(list);
            Assert.Equal(V3SlotOccupant.CorruptV3, summary.Occupant);
            Assert.False(summary.IsUsable);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-store-{Guid.NewGuid():N}");

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

    private static V3Manifest CreateManifest(string slotId, byte[] data) =>
        new(
            V3SaveRoot.FormatFamily,
            V3SaveRoot.SchemaVersion,
            slotId,
            slotId,
            "2026-08-12T08:00:00.0000000Z",
            slotId,
            0,
            0m,
            null,
            [new V3ManifestFile("road_network.json", data.LongLength, V3PayloadDigest.ComputeSha256(data))]);
}
