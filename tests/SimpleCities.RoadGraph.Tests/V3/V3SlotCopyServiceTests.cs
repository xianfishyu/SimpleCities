using SimpleCities.Core.V3;
using System.IO;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotCopyServiceTests
{
    [Fact]
    public void Copy_CopiesSlotToDestination()
    {
        string sourceRoot = GetTempRoot();
        string destinationRoot = GetTempRoot();
        try
        {
            byte[] data = "road-network"u8.ToArray();
            var store = new V3FileSlotStore(sourceRoot);
            store.Save("city-001", CreateManifest("city-001", data), new Dictionary<string, byte[]> { ["road_network.json"] = data });

            Assert.True(V3SlotCopyService.Copy("city-001", sourceRoot, destinationRoot));
            Assert.True(new V3FileSlotStore(destinationRoot).Load("city-001").Success);
        }
        finally
        {
            Cleanup(sourceRoot);
            Cleanup(destinationRoot);
        }
    }

    [Fact]
    public void Copy_MissingSlot_Fails()
    {
        string sourceRoot = GetTempRoot();
        string destinationRoot = GetTempRoot();
        try
        {
            Assert.False(V3SlotCopyService.Copy("missing", sourceRoot, destinationRoot));
        }
        finally
        {
            Cleanup(sourceRoot);
            Cleanup(destinationRoot);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-copy-{Guid.NewGuid():N}");

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
