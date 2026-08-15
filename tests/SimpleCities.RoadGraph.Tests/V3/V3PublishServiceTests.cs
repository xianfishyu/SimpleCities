using SimpleCities.Core.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3PublishServiceTests
{
    [Fact]
    public void Publish_FirstSave_ReturnsDescriptorWithEmptyOldDigest()
    {
        string root = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("city-001", data);
            var payloads = new Dictionary<string, byte[]> { ["road_network.json"] = data };

            V3PublishResult result = V3PublishService.Publish("city-001", root, manifest, payloads);

            Assert.True(result.Success, result.Error);
            Assert.Equal(string.Empty, result.Descriptor!.OldDigest);
            Assert.Equal("city-001", result.Descriptor.SlotId);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Publish_Overwrite_ReturnsOldDigest()
    {
        string root = GetTempRoot();
        try
        {
            byte[] firstData = "road-network"u8.ToArray();
            V3Manifest firstManifest = CreateManifest("city-001", firstData);
            var firstPayloads = new Dictionary<string, byte[]> { ["road_network.json"] = firstData };
            V3PublishService.Publish("city-001", root, firstManifest, firstPayloads);

            byte[] secondData = "road-network-2"u8.ToArray();
            V3Manifest secondManifest = CreateManifest("city-001", secondData);
            var secondPayloads = new Dictionary<string, byte[]> { ["road_network.json"] = secondData };

            V3PublishResult result = V3PublishService.Publish("city-001", root, secondManifest, secondPayloads);

            Assert.True(result.Success, result.Error);
            Assert.NotEqual(string.Empty, result.Descriptor!.OldDigest);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Publish_InvalidSlot_Fails()
    {
        string root = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("bad/slot", data);
            var payloads = new Dictionary<string, byte[]> { ["road_network.json"] = data };

            V3PublishResult result = V3PublishService.Publish("bad/slot", root, manifest, payloads);

            Assert.False(result.Success);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-pub-{Guid.NewGuid():N}");

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
