using SimpleCities.Core.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotHealthCheckTests
{
    [Fact]
    public void Check_CountsClassifications()
    {
        string root = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(root);
            store.Save("valid", CreateManifest("valid", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });

            var corruptStore = new V3FileSlotStore(root);
            corruptStore.Save("corrupt", CreateManifest("corrupt", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });
            File.WriteAllText(Path.Combine(root, "corrupt", "road_network.json"), "corrupt");

            Directory.CreateDirectory(Path.Combine(root, "foreign"));
            Directory.CreateDirectory(Path.Combine(root, "bad.name"));

            V3SlotHealthCheckResult result = V3SlotHealthCheck.Check(root);

            Assert.Equal(4, result.Total);
            Assert.Equal(1, result.Complete);
            Assert.Equal(1, result.Corrupt);
            Assert.Equal(1, result.Foreign);
            Assert.Equal(1, result.Unsafe);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-health-{Guid.NewGuid():N}");

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
