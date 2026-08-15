using System.IO;
using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotIntegrityTests
{
    [Fact]
    public void Verify_ValidSlot_Succeeds()
    {
        string root = GetTempRoot();
        try
        {
            Directory.CreateDirectory(root);
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest(data);
            IReadOnlyDictionary<string, byte[]> files = V3SlotWriter.BuildFiles(
                manifest,
                new Dictionary<string, byte[]> { [manifest.Files[0].Name] = data });
            foreach (KeyValuePair<string, byte[]> file in files)
                File.WriteAllBytes(Path.Combine(root, file.Key), file.Value);

            V3SlotIntegrityResult result = V3SlotIntegrity.Verify(root);

            Assert.True(result.Success, result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Verify_CorruptPayload_Fails()
    {
        string root = GetTempRoot();
        try
        {
            Directory.CreateDirectory(root);
            byte[] data = "road-network"u8.ToArray();
            V3Manifest manifest = CreateManifest(data);
            IReadOnlyDictionary<string, byte[]> files = V3SlotWriter.BuildFiles(
                manifest,
                new Dictionary<string, byte[]> { [manifest.Files[0].Name] = data });
            foreach (KeyValuePair<string, byte[]> file in files)
                File.WriteAllBytes(Path.Combine(root, file.Key), file.Value);
            File.WriteAllBytes(Path.Combine(root, manifest.Files[0].Name), "other"u8.ToArray());

            V3SlotIntegrityResult result = V3SlotIntegrity.Verify(root);

            Assert.False(result.Success);
            Assert.StartsWith("InvalidPayload:", result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Verify_MissingManifest_Fails()
    {
        string root = GetTempRoot();
        try
        {
            Directory.CreateDirectory(root);

            V3SlotIntegrityResult result = V3SlotIntegrity.Verify(root);

            Assert.False(result.Success);
            Assert.Equal("MissingManifest", result.Error);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string GetTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"v3-integrity-{Guid.NewGuid():N}");

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

    private static V3Manifest CreateManifest(byte[] data) =>
        new(
            V3SaveRoot.FormatFamily,
            V3SaveRoot.SchemaVersion,
            "city-001",
            "河湾城",
            "2026-08-12T08:00:00.0000000Z",
            "河湾城",
            1200,
            50000m,
            null,
            [new V3ManifestFile("road_network.json", data.LongLength, V3PayloadDigest.ComputeSha256(data))]);
}
