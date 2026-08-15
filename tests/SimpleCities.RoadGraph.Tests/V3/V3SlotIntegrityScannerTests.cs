using SimpleCities.Core.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotIntegrityScannerTests
{
    [Fact]
    public void Scan_ValidSlot_ReturnsCompleteV3()
    {
        string root = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(root);
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });

            IReadOnlyList<V3SlotSummary> list = V3SlotIntegrityScanner.Scan(root);

            V3SlotSummary summary = Assert.Single(list);
            Assert.Equal(V3SlotOccupant.CompleteV3, summary.Occupant);
            Assert.Equal("city-001", summary.DisplayName);
            Assert.Equal("2026-08-12T08:00:00.0000000Z", summary.Timestamp);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Scan_CorruptPayload_ReturnsCorruptV3()
    {
        string root = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(root);
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });
            File.WriteAllText(Path.Combine(root, "city-001", "road_network.json"), "corrupt");

            IReadOnlyList<V3SlotSummary> list = V3SlotIntegrityScanner.Scan(root);

            V3SlotSummary summary = Assert.Single(list);
            Assert.Equal(V3SlotOccupant.CorruptV3, summary.Occupant);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Scan_NoManifest_ReturnsForeign()
    {
        string root = GetTempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "city-001"));

            IReadOnlyList<V3SlotSummary> list = V3SlotIntegrityScanner.Scan(root);

            V3SlotSummary summary = Assert.Single(list);
            Assert.Equal(V3SlotOccupant.Foreign, summary.Occupant);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-scan-{Guid.NewGuid():N}");

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
