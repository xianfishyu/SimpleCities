using SimpleCities.Core.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotTransactionServiceTests
{
    [Fact]
    public void Publish_Delete_Recover_RoundTrips()
    {
        string root = GetTempRoot();
        string backupRoot = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest("city-001", data);
            var payloads = new Dictionary<string, byte[]> { ["road_network.json"] = data };

            Assert.True(V3SlotTransactionService.Publish("city-001", root, manifest, payloads).Success);
            V3SlotBackupService.Backup("city-001", root, backupRoot);
            Assert.True(V3SlotTransactionService.Delete("city-001", root).Success);
            Assert.False(new V3FileSlotStore(root).Load("city-001").Success);

            Assert.True(V3SlotTransactionService.Recover("city-001", root, backupRoot));
            Assert.True(new V3FileSlotStore(root).Load("city-001").Success);
        }
        finally
        {
            Cleanup(root);
            Cleanup(backupRoot);
        }
    }

    [Fact]
    public void Delete_MissingSlot_Fails()
    {
        string root = GetTempRoot();
        try
        {
            V3DeleteResult result = V3SlotTransactionService.Delete("missing", root);

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-tx-{Guid.NewGuid():N}");

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
