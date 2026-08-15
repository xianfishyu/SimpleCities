using SimpleCities.Core.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotListServiceTests
{
    [Fact]
    public void List_ReturnsSavedSlots()
    {
        string root = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(root);
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });
            store.Save("city-002", CreateManifest("city-002", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });

            IReadOnlyList<V3SlotSummary> list = V3SlotListService.List(root);

            Assert.Equal(2, list.Count);
            Assert.All(list, summary => Assert.True(summary.IsUsable));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void List_EmptyRoot_ReturnsEmpty()
    {
        string root = GetTempRoot();
        try
        {
            Directory.CreateDirectory(root);

            IReadOnlyList<V3SlotSummary> list = V3SlotListService.List(root);

            Assert.Empty(list);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-listsvc-{Guid.NewGuid():N}");

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
