using SimpleCities.Core.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotStatusServiceTests
{
    [Fact]
    public void GetStatus_ValidSlot_ReturnsCompleteV3()
    {
        string root = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(root);
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });

            V3SlotSummary summary = V3SlotStatusService.GetStatus("city-001", root);

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
    public void GetStatus_CorruptSlot_ReturnsCorruptV3()
    {
        string root = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(root);
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });
            File.WriteAllText(Path.Combine(root, "city-001", "road_network.json"), "corrupt");

            V3SlotSummary summary = V3SlotStatusService.GetStatus("city-001", root);

            Assert.Equal(V3SlotOccupant.CorruptV3, summary.Occupant);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void GetStatus_MissingSlot_ReturnsAbsent()
    {
        string root = GetTempRoot();
        try
        {
            V3SlotSummary summary = V3SlotStatusService.GetStatus("missing", root);

            Assert.Equal(V3SlotOccupant.Absent, summary.Occupant);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void GetStatus_InvalidSlotId_ReturnsUnsafe()
    {
        string root = GetTempRoot();
        try
        {
            V3SlotSummary summary = V3SlotStatusService.GetStatus("bad.name", root);

            Assert.Equal(V3SlotOccupant.Unsafe, summary.Occupant);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-status-{Guid.NewGuid():N}");

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
