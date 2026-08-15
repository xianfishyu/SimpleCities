using SimpleCities.Core.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotPayloadServiceTests
{
    [Fact]
    public void GetPayload_ReturnsSavedPayload()
    {
        string root = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(root);
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });

            byte[]? payload = V3SlotPayloadService.GetPayload("city-001", root, "road_network.json");

            Assert.Equal(data, payload);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void GetPayload_MissingSlotOrFile_ReturnsNull()
    {
        string root = GetTempRoot();
        try
        {
            Assert.Null(V3SlotPayloadService.GetPayload("missing", root, "road_network.json"));
            Assert.Null(V3SlotPayloadService.GetPayload("missing", root, "unknown.json"));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-payload-{Guid.NewGuid():N}");

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
