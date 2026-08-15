using SimpleCities.Core.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotRecoveryServiceTests
{
    [Fact]
    public void Recover_RestoresMissingSlotFromBackup()
    {
        string root = GetTempRoot();
        string backupRoot = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(root);
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });
            V3SlotBackupService.Backup("city-001", root, backupRoot);
            store.Delete("city-001");

            Assert.True(V3SlotRecoveryService.Recover("city-001", root, backupRoot));
            Assert.True(store.Load("city-001").Success);
        }
        finally
        {
            Cleanup(root);
            Cleanup(backupRoot);
        }
    }

    [Fact]
    public void Recover_WhenSlotExists_ReturnsFalse()
    {
        string root = GetTempRoot();
        string backupRoot = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(root);
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });
            V3SlotBackupService.Backup("city-001", root, backupRoot);

            Assert.False(V3SlotRecoveryService.Recover("city-001", root, backupRoot));
        }
        finally
        {
            Cleanup(root);
            Cleanup(backupRoot);
        }
    }

    [Fact]
    public void Recover_MissingBackup_ReturnsFalse()
    {
        string root = GetTempRoot();
        string backupRoot = GetTempRoot();
        try
        {
            Assert.False(V3SlotRecoveryService.Recover("missing", root, backupRoot));
        }
        finally
        {
            Cleanup(root);
            Cleanup(backupRoot);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-recover-{Guid.NewGuid():N}");

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
