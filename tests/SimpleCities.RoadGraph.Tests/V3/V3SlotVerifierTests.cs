using SimpleCities.Core.V3;

namespace SimpleCities.Tests.V3;

public sealed class V3SlotVerifierTests
{
    [Fact]
    public void Verify_ValidSlot_Succeeds()
    {
        byte[] data = "road-network"u8.ToArray();
        V3Manifest manifest = ValidManifest(data);
        var files = new Dictionary<string, byte[]> { [manifest.Files[0].Name] = data };

        V3SlotVerificationResult result = V3SlotVerifier.Verify(manifest, files);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public void Verify_MissingPayload_Fails()
    {
        byte[] data = "road-network"u8.ToArray();
        V3Manifest manifest = ValidManifest(data);

        V3SlotVerificationResult result = V3SlotVerifier.Verify(manifest, new Dictionary<string, byte[]>());

        Assert.False(result.Success);
        Assert.StartsWith("MissingDeclaredFile:", result.Error);
    }

    [Fact]
    public void Verify_DigestMismatch_Fails()
    {
        byte[] data = "road-network"u8.ToArray();
        V3Manifest manifest = ValidManifest(data);
        var files = new Dictionary<string, byte[]> { [manifest.Files[0].Name] = "other"u8.ToArray() };

        V3SlotVerificationResult result = V3SlotVerifier.Verify(manifest, files);

        Assert.False(result.Success);
        Assert.StartsWith("DigestMismatch:", result.Error);
    }

    [Fact]
    public void Verify_UndeclaredFile_Fails()
    {
        byte[] data = "road-network"u8.ToArray();
        V3Manifest manifest = ValidManifest(data);
        var files = new Dictionary<string, byte[]>
        {
            [manifest.Files[0].Name] = data,
            ["extra.json"] = data,
        };

        V3SlotVerificationResult result = V3SlotVerifier.Verify(manifest, files);

        Assert.False(result.Success);
        Assert.StartsWith("UndeclaredFile:", result.Error);
    }

    private static V3Manifest ValidManifest(byte[] data) =>
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
