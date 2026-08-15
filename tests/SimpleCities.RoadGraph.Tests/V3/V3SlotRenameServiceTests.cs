using SimpleCities.Core.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotRenameServiceTests
{
    [Fact]
    public void Rename_MovesSlotToNewId()
    {
        string root = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(root);
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });

            Assert.True(V3SlotRenameService.Rename("city-001", "city-002", root));
            Assert.False(store.Load("city-001").Success);
            Assert.True(store.Load("city-002").Success);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Rename_MissingOrConflict_Fails()
    {
        string root = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(root);
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });
            store.Save("city-002", CreateManifest("city-002", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });

            Assert.False(V3SlotRenameService.Rename("missing", "city-002", root));
            Assert.False(V3SlotRenameService.Rename("city-001", "city-002", root));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-rename-{Guid.NewGuid():N}");

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
